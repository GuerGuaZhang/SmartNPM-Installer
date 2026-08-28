using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Utils;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// 安装执行器
    /// </summary>
    public class InstallExecutor
    {
        private readonly string _command;
        private readonly string _workingDirectory;
        private readonly Logger _logger;
        private Process? _process;
        private readonly StringBuilder _stdoutBuilder = new StringBuilder();
        private readonly StringBuilder _stderrBuilder = new StringBuilder();
        private readonly object _outputLock = new object();

        /// <summary>
        /// 输出数据事件
        /// </summary>
        public event Action<string>? OnOutput;

        /// <summary>
        /// 错误数据事件
        /// </summary>
        public event Action<string>? OnError;

        /// <summary>
        /// 进度更新事件
        /// </summary>
        public event Action<string, int>? OnProgressUpdate;

        /// <summary>
        /// 初始化安装执行器
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <param name="logger">日志服务</param>
        public InstallExecutor(string command, string workingDirectory, Logger logger)
        {
            _command = command;
            _workingDirectory = workingDirectory;
            _logger = logger;
        }

        /// <summary>
        /// 执行命令并捕获输出
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>安装结果</returns>
        public async Task<InstallResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            _stdoutBuilder.Clear();
            _stderrBuilder.Clear();

            // 构建完整的 PATH，包含常见的 Node.js 和 npm 安装路径
            var nodePaths = new[] {
                @"C:\Program Files\nodejs",
                @"C:\Program Files (x86)\nodejs",
                @"C:\Users\" + Environment.UserName + @"\AppData\Roaming\npm",
                @"C:\Users\" + Environment.UserName + @"\AppData\Local\Programs\nodejs"
            };
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            var additionalPaths = string.Join(";", nodePaths.Where(p => !currentPath.Contains(p)));
            var fullPath = $"{currentPath};{additionalPaths}";

            // 设置 npm 缓存目录到用户目录
            var npmCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sni-cache");
            Directory.CreateDirectory(npmCacheDir);

            // 使用 cmd.exe /c 来执行 npm 命令
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {_command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                EnvironmentVariables = {
                    ["PATH"] = fullPath,
                    ["npm_config_cache"] = npmCacheDir,
                    ["npm_config_registry"] = "https://registry.npmmirror.com"
                }
            };

            try
            {
                _process = new Process { StartInfo = psi };

                // 设置输出事件处理器
                _process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (_outputLock)
                        {
                            _stdoutBuilder.AppendLine(e.Data);
                        }
                        OnOutput?.Invoke(e.Data);
                        UpdateProgress(e.Data);
                    }
                };

                _process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (_outputLock)
                        {
                            _stderrBuilder.AppendLine(e.Data);
                        }
                        OnError?.Invoke(e.Data);
                        UpdateProgress(e.Data);
                    }
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                // 等待进程完成或取消
                await Task.Run(() => _process.WaitForExit(), cancellationToken);

                var duration = DateTime.Now - startTime;
                var stdout = _stdoutBuilder.ToString();
                var stderr = _stderrBuilder.ToString();

                // 尝试解析包数量
                int? packagesAdded = ParsePackagesAdded(stdout);

                return InstallResult.SuccessResult(stdout, stderr, _process.ExitCode, duration, packagesAdded);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("安装被用户取消");
                return InstallResult.FailureResult("安装被用户取消");
            }
            catch (Exception ex)
            {
                _logger.LogError($"执行安装命令失败: {ex.Message}");
                return InstallResult.FailureResult($"执行安装命令失败: {ex.Message}");
            }
            finally
            {
                _process?.Dispose();
            }
        }

        /// <summary>
        /// 终止进程
        /// </summary>
        public void Kill()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _logger.LogInfo("已终止 npm 进程");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"终止进程失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新进度显示
        /// </summary>
        /// <param name="output">输出文本</param>
        private void UpdateProgress(string output)
        {
            if (string.IsNullOrEmpty(output))
                return;

            // 根据输出特征推断进度阶段
            if (output.Contains("idealTree") || output.Contains("sill idealTree"))
            {
                OnProgressUpdate?.Invoke("解析依赖...", 20);
            }
            else if (output.Contains("reify:") || output.Contains("timing reifyNode"))
            {
                OnProgressUpdate?.Invoke("下载中...", 50);
            }
            else if (output.Contains("run-script") || output.Contains("postinstall"))
            {
                OnProgressUpdate?.Invoke("编译原生模块...", 80);
            }
            else if (output.Contains("added") && output.Contains("packages"))
            {
                OnProgressUpdate?.Invoke("完成", 100);
            }
        }

        /// <summary>
        /// 从输出中解析包数量
        /// </summary>
        /// <param name="stdout">标准输出</param>
        /// <returns>包数量</returns>
        private int? ParsePackagesAdded(string stdout)
        {
            // 匹配 "added 452 packages in 1m" 格式
            var match = Regex.Match(stdout, @"added (\d+) packages");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            {
                return count;
            }

            return null;
        }

        /// <summary>
        /// 同步执行命令（用于简单命令）
        /// </summary>
        /// <param name="fileName">命令文件名</param>
        /// <param name="arguments">命令参数</param>
        /// <returns>执行结果</returns>
        public static (string Output, string Error, int ExitCode) RunCommand(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return (string.Empty, "无法启动进程", -1);

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return (output, error, process.ExitCode);
            }
            catch (Exception ex)
            {
                return (string.Empty, ex.Message, -1);
            }
        }

        /// <summary>
        /// 执行子命令
        /// </summary>
        /// <param name="command">命令</param>
        /// <param name="workingDirectory">工作目录</param>
        /// <returns>执行结果</returns>
        public static (string Output, string Error, int ExitCode) RunSubCommand(string command, string workingDirectory)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false, // 允许交互
                    WorkingDirectory = workingDirectory
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return (string.Empty, "无法启动进程", -1);

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return (output, error, process.ExitCode);
            }
            catch (Exception ex)
            {
                return (string.Empty, ex.Message, -1);
            }
        }
    }
}
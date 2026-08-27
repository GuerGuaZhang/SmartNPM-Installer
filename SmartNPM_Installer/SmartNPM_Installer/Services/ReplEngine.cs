using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Utils;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// REPL 交互引擎
    /// </summary>
    public class ReplEngine
    {
        private readonly Logger _logger;
        private readonly ConfigManager _configManager;
        private readonly ErrorHealer _errorHealer;
        private EnvStatus _envStatus;
        private readonly List<InstallHistory> _installHistory = new List<InstallHistory>();
        private readonly string _stateFilePath;
        private bool _isInstalling = false;
        private CancellationTokenSource? _currentCts;

        /// <summary>
        /// 初始化 REPL 引擎
        /// </summary>
        public ReplEngine()
        {
            _logger = new Logger();
            _configManager = new ConfigManager(_logger);
            _errorHealer = new ErrorHealer(_configManager, _logger);
            _envStatus = new EnvStatus();
            _stateFilePath = Path.Combine(AppContext.BaseDirectory, "sni-state.json");

            // 加载会话状态
            LoadState();
        }

        /// <summary>
        /// 启动 REPL 循环
        /// </summary>
        public async Task StartAsync()
        {
            PrintBanner();

            // 执行环境扫描
            _envStatus = EnvScanner.Scan();
            PrintEnvTable();

            Console.WriteLine("\n提示: 输入 /help 查看所有命令，输入 exit 退出\n");

            // 主循环
            while (true)
            {
                Console.Write("smart-install> ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                // 处理退出命令
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    SaveState();
                    Console.WriteLine("再见！");
                    break;
                }

                // 处理内部命令
                if (input.StartsWith("/"))
                {
                    await HandleInternalCommand(input);
                    continue;
                }

                // 处理安装命令
                await HandleInstallCommand(input);
            }
        }

        /// <summary>
        /// 打印横幅
        /// </summary>
        private void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║  Smart NPM Installer (SNI) v1.0                 ║");
            Console.WriteLine("║  粘贴 npx/npm 命令，自动完成环境修复与全局安装   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
        }

        /// <summary>
        /// 打印环境状态表格
        /// </summary>
        private void PrintEnvTable()
        {
            Console.WriteLine("\n[系统扫描结果]");
            Console.WriteLine(EnvScanner.FormatEnvTable(_envStatus));

            Console.WriteLine("\n[配置状态]");
            var config = _configManager.CurrentConfig;
            Console.WriteLine($"Registry: {_envStatus.CurrentRegistry} {( _envStatus.IsRegistryMirror ? "✓" : "⚠")}");
            Console.WriteLine($"Allow-scripts: {(_envStatus.CurrentAllowScripts == "true" ? "完全信任" : _envStatus.CurrentAllowScripts != null ? $"已配置 {_envStatus.CurrentAllowScripts.Split(',').Length} 项白名单" : "未配置")}");
            Console.WriteLine($"Build Tools: {(_envStatus.HasBuildTools ? "已安装" : "未安装 ⚠")}");
        }

        /// <summary>
        /// 处理内部命令
        /// </summary>
        private async Task HandleInternalCommand(string command)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "/help":
                case "/?":
                    ShowHelp();
                    break;

                case "/scan":
                    _envStatus = EnvScanner.Scan();
                    PrintEnvTable();
                    break;

                case "/config":
                    if (parts.Length > 1 && parts[1] == "set" && parts.Length >= 4)
                    {
                        var key = parts[2];
                        var value = string.Join(" ", parts.Skip(3));
                        if (_configManager.SetConfigValue(key, value))
                        {
                            _logger.LogInfo($"配置已更新: {key} = {value}");
                        }
                        else
                        {
                            _logger.LogError($"配置更新失败: {key}");
                        }
                    }
                    else
                    {
                        ShowConfig();
                    }
                    break;

                case "/fix":
                    if (parts.Length > 1)
                    {
                        switch (parts[1].ToLower())
                        {
                            case "env":
                                FixEnvironment();
                                break;
                            case "buildtools":
                                await InstallBuildToolsAsync();
                                break;
                            default:
                                _logger.LogWarning($"未知的修复命令: {parts[1]}");
                                break;
                        }
                    }
                    break;

                case "/history":
                    ShowHistory();
                    break;

                case "/clear":
                case "cls":
                    Console.Clear();
                    break;

                case "/backup":
                    _configManager.RestoreNpmrc();
                    break;

                case "/restore":
                    _configManager.RestoreNpmrc();
                    break;

                default:
                    _logger.LogWarning($"未知的内部命令: {command}");
                    break;
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            Console.WriteLine("\n可用命令：");
            Console.WriteLine("  /help 或 /?          显示帮助信息");
            Console.WriteLine("  /scan                重新执行环境扫描");
            Console.WriteLine("  /config              显示当前配置");
            Console.WriteLine("  /config set <key> <value>  修改配置项");
            Console.WriteLine("  /fix env             手动触发环境修复");
            Console.WriteLine("  /fix buildtools      手动触发 Build Tools 安装");
            Console.WriteLine("  /history             显示本次会话的安装历史");
            Console.WriteLine("  /clear 或 cls        清屏");
            Console.WriteLine("  /backup              备份当前 .npmrc");
            Console.WriteLine("  /restore             从最近的备份恢复 .npmrc");
            Console.WriteLine("  exit 或 quit         保存状态并退出");
            Console.WriteLine("\n安装命令：");
            Console.WriteLine("  npx <package> [args]       执行包并全局安装");
            Console.WriteLine("  npm install -g <package>   全局安装包");
            Console.WriteLine("  <package>                  直接输入包名");
        }

        /// <summary>
        /// 显示配置
        /// </summary>
        private void ShowConfig()
        {
            var config = _configManager.CurrentConfig;
            Console.WriteLine("\n当前配置：");
            Console.WriteLine($"  registry: {config.Registry}");
            Console.WriteLine($"  allow-scripts 白名单: {string.Join(", ", config.AllowScriptsWhitelist)}");
            Console.WriteLine($"  autoInstallBuildTools: {config.AutoInstallBuildTools}");
            Console.WriteLine($"  npmCacheDir: {config.NpmCacheDir}");
            Console.WriteLine($"  maxRetryCount: {config.MaxRetryCount}");
            Console.WriteLine($"  preferGlobalInstall: {config.PreferGlobalInstall}");
            Console.WriteLine($"  subCommandAutoRun: {config.SubCommandAutoRun}");
        }

        /// <summary>
        /// 显示安装历史
        /// </summary>
        private void ShowHistory()
        {
            if (_installHistory.Count == 0)
            {
                Console.WriteLine("\n暂无安装历史");
                return;
            }

            Console.WriteLine("\n安装历史：");
            foreach (var entry in _installHistory)
            {
                var status = entry.Success ? "✓" : "✗";
                Console.WriteLine($"  [{entry.Timestamp:HH:mm:ss}] {status} {entry.PackageName} ({entry.Duration.TotalSeconds:F1}s)");
            }
        }

        /// <summary>
        /// 处理安装命令
        /// </summary>
        private async Task HandleInstallCommand(string input)
        {
            if (_isInstalling)
            {
                _logger.LogWarning("当前有安装任务正在进行，请等待完成或按 Ctrl+C 取消");
                return;
            }

            var command = CommandParser.Parse(input);
            if (command == null)
            {
                _logger.LogError("❌ 无法识别命令格式，请使用 npx <pkg> 或 npm install -g <pkg>");
                return;
            }

            // 校验包名
            if (!CommandParser.IsValidPackageName(command.PackageName))
            {
                _logger.LogError("❌ 包名包含非法字符");
                return;
            }

            _isInstalling = true;
            _currentCts = new CancellationTokenSource();

            try
            {
                Console.WriteLine($"\n▶ 开始安装: {command.PackageName}");

                // 预判原生模块
                var nativeModules = PredictNativeModules(command.PackageName);
                if (nativeModules.Count > 0)
                {
                    Console.WriteLine("▶ 依赖分析...");
                    Console.WriteLine($"   ⚠ 预判到原生模块依赖: {string.Join(", ", nativeModules)}");
                    Console.WriteLine("   将自动检查编译环境并追加 allow-scripts 白名单");

                    // 追加 allow-scripts 白名单
                    _configManager.AppendAllowScripts(nativeModules);
                }

                // 确保 registry
                _configManager.SetRegistry(_configManager.CurrentConfig.Registry);

                // 构造安装命令
                var installCmd = CommandParser.BuildInstallCommand(command);
                _logger.LogDebug($"执行命令: {installCmd}");

                // 执行安装
                var retryCount = 0;
                var maxRetry = _configManager.CurrentConfig.MaxRetryCount;

                while (retryCount < maxRetry)
                {
                    var executor = new InstallExecutor(installCmd, AppContext.BaseDirectory, _logger);

                    // 设置进度回调
                    executor.OnProgressUpdate += (stage, progress) =>
                    {
                        DrawProgressBar(progress, stage);
                    };

                    var result = await executor.ExecuteAsync(_currentCts.Token);

                    if (result.ExitCode == 0)
                    {
                        // 安装成功
                        Console.WriteLine($"\n▶ 安装完成 ✓  耗时 {result.Duration.TotalSeconds:F0}s" +
                                          (result.PackagesAdded.HasValue ? $"  |  新增 {result.PackagesAdded} 个包" : ""));

                        // 记录历史
                        _installHistory.Add(new InstallHistory
                        {
                            Timestamp = DateTime.Now,
                            PackageName = command.PackageName,
                            Success = true,
                            Duration = result.Duration
                        });

                        // 询问是否执行子命令
                        if (!string.IsNullOrEmpty(command.SubCommand))
                        {
                            await HandleSubCommandAsync(command);
                        }

                        break;
                    }
                    else
                    {
                        // 安装失败，分析错误
                        var healing = _errorHealer.Analyze(result.StandardError ?? "");

                        if (healing == null || !healing.Matched)
                        {
                            // 未知错误
                            _logger.LogError($"安装失败: {result.ErrorMessage}");
                            Console.WriteLine(result.StandardError);

                            _installHistory.Add(new InstallHistory
                            {
                                Timestamp = DateTime.Now,
                                PackageName = command.PackageName,
                                Success = false,
                                Duration = result.Duration
                            });

                            break;
                        }

                        if (healing.NeedsInteraction)
                        {
                            // 需要用户交互
                            Console.WriteLine(healing.Message);
                            var choice = Console.ReadLine()?.Trim() ?? "1";

                            if (choice == "3" || choice.ToLower() == "skip")
                            {
                                break;
                            }

                            _errorHealer.ApplyFix(healing, choice);
                        }
                        else
                        {
                            // 自动修复
                            Console.WriteLine($"检测到 {healing.Description}，正在自动修复...");
                            _errorHealer.ApplyFix(healing);
                        }

                        retryCount++;
                        Console.WriteLine($"第 {retryCount} 次重试...");
                    }
                }

                if (retryCount >= maxRetry)
                {
                    _logger.LogError($"超过最大重试次数 ({maxRetry})");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("安装被用户取消");
            }
            catch (Exception ex)
            {
                _logger.LogError($"安装过程中发生错误: {ex.Message}");
            }
            finally
            {
                _isInstalling = false;
                _currentCts?.Dispose();
                _currentCts = null;
            }
        }

        /// <summary>
        /// 处理子命令
        /// </summary>
        private async Task HandleSubCommandAsync(ParsedCommand command)
        {
            if (string.IsNullOrEmpty(command.SubCommand))
                return;

            var config = _configManager.CurrentConfig;

            // 检查是否自动执行
            if (config.SubCommandAutoRun)
            {
                Console.WriteLine($"▶ 自动执行子命令: {command.BinaryName} {command.SubCommand}");
                ExecuteSubCommand(command);
                return;
            }

            // 询问用户
            Console.Write($"\n检测到子命令 \"{command.SubCommand}\"，是否立即执行？ [Y/n/always/never] ");
            var input = Console.ReadLine()?.Trim().ToLower() ?? "";

            switch (input)
            {
                case "":
                case "y":
                    ExecuteSubCommand(command);
                    break;

                case "n":
                    break;

                case "always":
                    config.SubCommandAutoRun = true;
                    _configManager.SaveConfig();
                    ExecuteSubCommand(command);
                    break;

                case "never":
                    // 记住选择，不再询问（需要更复杂的实现）
                    break;
            }
        }

        /// <summary>
        /// 执行子命令
        /// </summary>
        private void ExecuteSubCommand(ParsedCommand command)
        {
            try
            {
                var fullCommand = $"{command.BinaryName} {command.SubCommand}";
                Console.WriteLine($"▶ 执行: {fullCommand}\n");

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {fullCommand}",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = AppContext.BaseDirectory
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"执行子命令失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 修复环境
        /// </summary>
        private void FixEnvironment()
        {
            Console.WriteLine("▶ 修复环境...");

            // 设置 registry
            _configManager.SetRegistry(_configManager.CurrentConfig.Registry);

            // 追加默认 allow-scripts 白名单
            _configManager.AppendAllowScripts(_configManager.CurrentConfig.AllowScriptsWhitelist);

            Console.WriteLine("✓ 环境修复完成");
        }

        /// <summary>
        /// 安装 Build Tools
        /// </summary>
        private async Task InstallBuildToolsAsync()
        {
            Console.WriteLine("▶ 正在安装 Build Tools...");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "install Microsoft.VisualStudio.2022.BuildTools --silent --override \"--wait --quiet --add ProductLang En-us --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);

                Console.WriteLine("✓ Build Tools 安装已启动");
                Console.WriteLine("⚠ 安装完成后请关闭并重新打开 SmartInstall.exe");
            }
            catch (Exception ex)
            {
                _logger.LogError($"启动 Build Tools 安装失败: {ex.Message}");
                Console.WriteLine("请手动安装 Build Tools: https://visualstudio.microsoft.com/visual-cpp-build-tools/");
            }
        }

        /// <summary>
        /// 预判原生模块
        /// </summary>
        private List<string> PredictNativeModules(string packageName)
        {
            var nativePackages = new HashSet<string>
            {
                "node-pty", "koffi", "sharp", "bcrypt", "better-sqlite3",
                "sqlite3", "canvas", "node-sass", "sass", "esbuild",
                "electron", "zeromq", "usb", "serialport", "ffi-napi",
                "ref-napi", "cpu-features", "tree-sitter"
            };

            var result = new List<string>();

            try
            {
                // 执行 npm view 获取依赖
                var psi = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = $"view {packageName} dependencies --json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        var deps = JsonSerializer.Deserialize<Dictionary<string, string>>(output);
                        if (deps != null)
                        {
                            foreach (var dep in deps.Keys)
                            {
                                if (nativePackages.Contains(dep) && !result.Contains(dep))
                                {
                                    result.Add(dep);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return result;
        }

        /// <summary>
        /// 绘制进度条
        /// </summary>
        private void DrawProgressBar(int progress, string stage)
        {
            if (progress < 0) progress = 0;
            if (progress > 100) progress = 100;

            var totalWidth = 20;
            var completedWidth = (int)(progress / 100.0 * totalWidth);
            var remainingWidth = totalWidth - completedWidth;

            var bar = new string('█', completedWidth) + new string('░', remainingWidth);
            Console.Write($"\r[{bar}] {stage}   ");
        }

        /// <summary>
        /// 加载会话状态
        /// </summary>
        private void LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    var state = JsonSerializer.Deserialize<SessionState>(json);
                    if (state?.InstallHistory != null)
                    {
                        _installHistory.AddRange(state.InstallHistory);
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        /// <summary>
        /// 保存会话状态
        /// </summary>
        private void SaveState()
        {
            try
            {
                var state = new SessionState
                {
                    InstallHistory = _installHistory,
                    LastSaveTime = DateTime.Now
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(state, options);
                File.WriteAllText(_stateFilePath, json);
            }
            catch
            {
                // 忽略错误
            }
        }
    }

    /// <summary>
    /// 安装历史记录
    /// </summary>
    public class InstallHistory
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 包名
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 耗时
        /// </summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// 会话状态
    /// </summary>
    public class SessionState
    {
        /// <summary>
        /// 安装历史
        /// </summary>
        public List<InstallHistory>? InstallHistory { get; set; }

        /// <summary>
        /// 最后保存时间
        /// </summary>
        public DateTime LastSaveTime { get; set; }
    }
}
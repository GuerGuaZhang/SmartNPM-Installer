using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SmartNPM_Installer.Models;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// 环境扫描服务
    /// </summary>
    public class EnvScanner
    {
        /// <summary>
        /// 执行完整的环境扫描
        /// </summary>
        /// <returns>环境状态</returns>
        public static EnvStatus Scan()
        {
            var status = new EnvStatus();

            // 检测 Node.js
            var nodeResult = RunCommand("node", "--version");
            if (nodeResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(nodeResult.Output))
            {
                status.NodeInstalled = true;
                status.NodeVersion = nodeResult.Output.Trim();
            }

            // 检测 npm
            var npmResult = RunCommand("npm", "--version");
            if (npmResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(npmResult.Output))
            {
                status.NpmVersion = npmResult.Output.Trim();
            }

            // 检测当前 registry
            var registryResult = RunCommand("npm", "config get registry");
            if (registryResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(registryResult.Output))
            {
                status.CurrentRegistry = registryResult.Output.Trim();
                status.IsRegistryMirror = IsMirrorRegistry(status.CurrentRegistry);
            }

            // 检测 allow-scripts
            var allowScriptsResult = RunCommand("npm", "config get allow-scripts");
            if (allowScriptsResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(allowScriptsResult.Output))
            {
                status.CurrentAllowScripts = allowScriptsResult.Output.Trim();
            }

            // 检测 Python
            var pythonResult = RunCommand("where", "python");
            if (pythonResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(pythonResult.Output))
            {
                status.HasPython = true;
                status.PythonPath = pythonResult.Output.Trim().Split('\n')[0].Trim();
            }
            else
            {
                // 尝试 python3
                pythonResult = RunCommand("where", "python3");
                if (pythonResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(pythonResult.Output))
                {
                    status.HasPython = true;
                    status.PythonPath = pythonResult.Output.Trim().Split('\n')[0].Trim();
                }
            }

            // 检测 VC++ Build Tools
            status.HasBuildTools = HasBuildTools();

            // 检测 .npmrc 路径
            var npmrcResult = RunCommand("npm", "config get userconfig");
            if (npmrcResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(npmrcResult.Output))
            {
                status.NpmrcPath = npmrcResult.Output.Trim();
            }
            else
            {
                // 默认路径
                status.NpmrcPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".npmrc");
            }

            // 检测 HTTP 代理
            status.HttpProxy = Environment.GetEnvironmentVariable("HTTP_PROXY");

            return status;
        }

        /// <summary>
        /// 检查是否为国内镜像源
        /// </summary>
        /// <param name="registry">registry 地址</param>
        /// <returns>是否为镜像源</returns>
        private static bool IsMirrorRegistry(string registry)
        {
            var mirrorDomains = new[]
            {
                "registry.npmmirror.com",
                "registry.npm.taobao.org",
                "npm.taobao.org",
                "cnpmjs.org",
                "registry.npm.cn",
                "npm.mirrors.cloud.tencent.com"
            };

            foreach (var domain in mirrorDomains)
            {
                if (registry.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否安装了 VC++ Build Tools
        /// </summary>
        /// <returns>是否安装</returns>
        private static bool HasBuildTools()
        {
            try
            {
                // 检查 VS 2022 Build Tools
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                if (key != null)
                {
                    var installed = key.GetValue("Installed");
                    if (installed != null && (int)installed == 1)
                        return true;
                }

                // 检查 VS 2019 (15.0)
                using var key2019 = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\VisualStudio\15.0\VC\Runtimes\x64");
                if (key2019 != null)
                {
                    var installed = key2019.GetValue("Installed");
                    if (installed != null && (int)installed == 1)
                        return true;
                }

                return false;
            }
            catch
            {
                // 无法访问注册表，假定未安装
                return false;
            }
        }

        /// <summary>
        /// 运行命令并捕获输出
        /// </summary>
        /// <param name="fileName">命令文件名</param>
        /// <param name="arguments">命令参数</param>
        /// <returns>命令执行结果</returns>
        private static (string Output, string Error, int ExitCode) RunCommand(string fileName, string arguments)
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
        /// 格式化环境状态为表格字符串
        /// </summary>
        /// <param name="status">环境状态</param>
        /// <returns>格式化的表格字符串</returns>
        public static string FormatEnvTable(EnvStatus status)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "┌─────────────────────┬──────────────────────────────┬────────┐",
                "│ 项目                │ 状态                         │ 结果   │",
                "├─────────────────────┼──────────────────────────────┼────────┤"
            };

            // Node.js
            var nodeStatus = status.NodeInstalled ? status.NodeVersion ?? "已安装" : "未安装";
            var nodeResult = status.NodeInstalled ? "✓" : "✗";
            lines.Add($"│ Node.js             │ {nodeStatus,-28} │ {nodeResult,-6} │");

            // npm
            var npmStatus = status.NpmVersion ?? "未安装";
            var npmResult = status.NpmVersion != null ? "✓" : "✗";
            lines.Add($"│ npm                 │ {npmStatus,-28} │ {npmResult,-6} │");

            // Registry
            var registryStatus = status.CurrentRegistry;
            if (registryStatus.Length > 28)
                registryStatus = "..." + registryStatus.Substring(registryStatus.Length - 25);
            var registryResult = status.IsRegistryMirror ? "✓" : "⚠";
            lines.Add($"│ Registry            │ {registryStatus,-28} │ {registryResult,-6} │");

            // Allow-scripts
            var allowScriptsStatus = "未配置";
            if (status.CurrentAllowScripts == "true")
                allowScriptsStatus = "完全信任";
            else if (!string.IsNullOrEmpty(status.CurrentAllowScripts))
                allowScriptsStatus = $"已配置 {status.CurrentAllowScripts.Split(',').Length} 项白名单";
            var allowScriptsResult = status.CurrentAllowScripts != null ? "✓" : "⚠";
            lines.Add($"│ Allow-scripts       │ {allowScriptsStatus,-28} │ {allowScriptsResult,-6} │");

            // Python
            var pythonStatus = status.HasPython ? "已安装" : "未安装";
            var pythonResult = status.HasPython ? "✓" : "⚠";
            if (status.HasPython && !string.IsNullOrEmpty(status.PythonPath))
            {
                pythonStatus = status.PythonPath;
                if (pythonStatus.Length > 28)
                    pythonStatus = "..." + pythonStatus.Substring(pythonStatus.Length - 25);
            }
            lines.Add($"│ Python              │ {pythonStatus,-28} │ {pythonResult,-6} │");

            // Build Tools
            var buildToolsStatus = status.HasBuildTools ? "已安装" : "未检测到";
            var buildToolsResult = status.HasBuildTools ? "✓" : "⚠";
            lines.Add($"│ VC++ Build Tools    │ {buildToolsStatus,-28} │ {buildToolsResult,-6} │");

            lines.Add("└─────────────────────┴──────────────────────────────┴────────┘");

            // HTTP 代理
            if (!string.IsNullOrEmpty(status.HttpProxy))
            {
                lines.Add($"HTTP 代理: {status.HttpProxy}");
            }

            return string.Join("\n", lines);
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Spectre.Console;
using SmartNPM_Installer.Models;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// 环境扫描服务
    /// </summary>
    public class EnvScanner
    {
        /// <summary>
        /// 国内镜像源列表
        /// </summary>
        private static readonly string[] MirrorDomains = new[]
        {
            "registry.npmmirror.com",
            "registry.npm.taobao.org",
            "npm.taobao.org",
            "cnpmjs.org",
            "registry.npm.cn",
            "npm.mirrors.cloud.tencent.com"
        };

        /// <summary>
        /// 默认国内镜像源
        /// </summary>
        public const string DefaultMirrorRegistry = "https://registry.npmmirror.com";

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

            // 检测 npm（尝试多种方式）
            var npmResult = RunCommand("npm", "--version");
            if (npmResult.ExitCode != 0 || string.IsNullOrWhiteSpace(npmResult.Output))
            {
                // 尝试通过 node 调用 npm
                npmResult = RunCommand("node", "-e \"console.log(require('child_process').execSync('npm --version').toString().trim())\"");
            }
            if (npmResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(npmResult.Output))
            {
                status.NpmVersion = npmResult.Output.Trim().Split('\n')[0].Trim();
            }

            // 检测当前 registry
            var registryResult = RunCommand("npm", "config get registry");
            if (registryResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(registryResult.Output))
            {
                status.CurrentRegistry = registryResult.Output.Trim().Split('\n')[0].Trim();
                status.IsRegistryMirror = IsMirrorRegistry(status.CurrentRegistry);
            }

            // 检测 allow-scripts
            var allowScriptsResult = RunCommand("npm", "config get allow-scripts");
            if (allowScriptsResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(allowScriptsResult.Output))
            {
                status.CurrentAllowScripts = allowScriptsResult.Output.Trim().Split('\n')[0].Trim();
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
        /// 自动切换到国内镜像源
        /// </summary>
        /// <param name="currentRegistry">当前 registry</param>
        /// <returns>是否切换成功</returns>
        public static bool AutoSwitchToMirror(string currentRegistry)
        {
            // 如果已经是国内镜像，直接返回
            if (IsMirrorRegistry(currentRegistry))
                return true;

            // 切换到国内镜像
            AnsiConsole.MarkupLine("[yellow]Detected non-China registry, switching to npmmirror...[/]");
            
            var result = RunCommand("npm", $"config set registry {DefaultMirrorRegistry} --location=user");
            if (result.ExitCode == 0)
            {
                AnsiConsole.MarkupLine($"[green]OK[/] Registry switched to: {DefaultMirrorRegistry}");
                return true;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]XX[/] Switch failed: {result.Error}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否为国内镜像源
        /// </summary>
        /// <param name="registry">registry 地址</param>
        /// <returns>是否为镜像源</returns>
        public static bool IsMirrorRegistry(string registry)
        {
            foreach (var domain in MirrorDomains)
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
        public static (string Output, string Error, int ExitCode) RunCommand(string fileName, string arguments)
        {
            try
            {
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

                // 对于 npm/node，使用 cmd /c 来执行，因为 npm 可能是 .ps1 或 .cmd 文件
                var actualFileName = fileName;
                var actualArguments = arguments;
                if (fileName.Equals("npm", StringComparison.OrdinalIgnoreCase) || 
                    fileName.Equals("npx", StringComparison.OrdinalIgnoreCase))
                {
                    actualFileName = "cmd.exe";
                    actualArguments = $"/c {fileName} {arguments}";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = actualFileName,
                    Arguments = actualArguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    EnvironmentVariables = {
                        ["PATH"] = fullPath
                    }
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return (string.Empty, "无法启动进程", -1);

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(10000); // 10秒超时

                return (output.Trim(), error.Trim(), process.ExitCode);
            }
            catch (Exception ex)
            {
                return (string.Empty, ex.Message, -1);
            }
        }

        /// <summary>
        /// 使用 Spectre.Console 打印环境状态表格
        /// </summary>
        /// <param name="status">环境状态</param>
        public static void PrintEnvTable(EnvStatus status)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Item[/]").Centered())
                .AddColumn(new TableColumn("[bold]Status[/]"))
                .AddColumn(new TableColumn("[bold]Result[/]").Centered());

            // Node.js
            var nodeStatus = status.NodeInstalled ? (status.NodeVersion ?? "Installed") : "Not installed";
            var nodeResult = status.NodeInstalled ? "[green]OK[/]" : "[red]XX[/]";
            table.AddRow("Node.js", Markup.Escape(nodeStatus), nodeResult);

            // npm
            var npmStatus = status.NpmVersion ?? "Not installed";
            var npmResult = status.NpmVersion != null ? "[green]OK[/]" : "[red]XX[/]";
            table.AddRow("npm", Markup.Escape(npmStatus), npmResult);

            // Registry
            var registryStatus = status.CurrentRegistry;
            if (registryStatus.Length > 35)
                registryStatus = "..." + registryStatus.Substring(registryStatus.Length - 32);
            var registryResult = status.IsRegistryMirror ? "[green]OK[/]" : "[yellow]!![/]";
            table.AddRow("Registry", Markup.Escape(registryStatus), registryResult);

            // Allow-scripts
            string allowScriptsStatus;
            if (status.CurrentAllowScripts == "true")
                allowScriptsStatus = "Fully trusted";
            else if (!string.IsNullOrEmpty(status.CurrentAllowScripts))
                allowScriptsStatus = $"Configured ({status.CurrentAllowScripts.Split(',').Length} items)";
            else
                allowScriptsStatus = "Not configured";
            
            var allowScriptsResult = status.CurrentAllowScripts != null ? "[green]OK[/]" : "[yellow]!![/]";
            table.AddRow("Allow-scripts", allowScriptsStatus, allowScriptsResult);

            // Python
            string pythonStatus;
            if (status.HasPython && !string.IsNullOrEmpty(status.PythonPath))
            {
                pythonStatus = status.PythonPath;
                if (pythonStatus.Length > 35)
                    pythonStatus = "..." + pythonStatus.Substring(pythonStatus.Length - 32);
            }
            else
            {
                pythonStatus = status.HasPython ? "Installed" : "Not installed";
            }
            var pythonResult = status.HasPython ? "[green]OK[/]" : "[yellow]!![/]";
            table.AddRow("Python", Markup.Escape(pythonStatus), pythonResult);

            // Build Tools
            var buildToolsStatus = status.HasBuildTools ? "Installed" : "Not detected";
            var buildToolsResult = status.HasBuildTools ? "[green]OK[/]" : "[yellow]!![/]";
            table.AddRow("VC++ Build Tools", buildToolsStatus, buildToolsResult);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue]System Scan Results[/]");
            AnsiConsole.Write(table);

            // HTTP 代理
            if (!string.IsNullOrEmpty(status.HttpProxy))
            {
                AnsiConsole.MarkupLine($"[grey]HTTP Proxy: {status.HttpProxy}[/]");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
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
            
            // 自动切换到国内镜像源
            if (!_envStatus.IsRegistryMirror)
            {
                EnvScanner.AutoSwitchToMirror(_configManager);
                // 重新扫描以更新状态
                _envStatus = EnvScanner.Scan();
            }
            
            PrintEnvTable();

            AnsiConsole.MarkupLine("\n[grey]Type /help for commands, exit to quit[/]\n");

            // 主循环
            while (true)
            {
                AnsiConsole.Write("[green]smart-install>[/] ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                // 处理退出命令
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    SaveState();
                    AnsiConsole.MarkupLine("[green]Goodbye![/]");
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
            AnsiConsole.Clear();
            var rule = new Rule("[bold cyan]Smart NPM Installer (SNI) v1.0[/]")
                .RuleStyle("cyan");
            AnsiConsole.Write(rule);
            
            var panel = new Panel("[grey]Paste npx/npm command, auto-fix & install[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan)
                .Padding(2, 0);
            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        /// <summary>
        /// 打印环境状态表格
        /// </summary>
        private void PrintEnvTable()
        {
            // 使用 Spectre.Console 打印表格
            EnvScanner.PrintEnvTable(_envStatus);

            // 配置状态
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue][Config Status][/]");
            
            var registryColor = _envStatus.IsRegistryMirror ? "green" : "yellow";
            var allowScriptsColor = _envStatus.CurrentAllowScripts != null ? "green" : "yellow";
            var buildToolsColor = _envStatus.HasBuildTools ? "green" : "yellow";
            
            var allowScriptsStatus = _envStatus.CurrentAllowScripts == "true" ? "[green]Fully trusted[/]" : 
                _envStatus.CurrentAllowScripts != null ? $"[green]Configured[/] ({_envStatus.CurrentAllowScripts.Split(',').Length} items)" : "[yellow]Not configured[/]";
            var buildToolsStatus = _envStatus.HasBuildTools ? "[green]Installed[/]" : "[yellow]Not installed[/]";
            
            AnsiConsole.MarkupLine($"Registry: [{registryColor}]{_envStatus.CurrentRegistry}[/]");
            AnsiConsole.MarkupLine($"Allow-scripts: {allowScriptsStatus}");
            AnsiConsole.MarkupLine($"Build Tools: {buildToolsStatus}");
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
                            AnsiConsole.MarkupLine($"[green]✓[/] Config updated: {key} = {value}");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]✗[/] Config update failed: {key}");
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
                                AnsiConsole.MarkupLine($"[yellow]Unknown fix command: {parts[1]}[/]");
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
                    _configManager.BackupNpmrc();
                    AnsiConsole.MarkupLine("[green]✓[/] .npmrc backed up");
                    break;

                case "/restore":
                    _configManager.RestoreNpmrc();
                    AnsiConsole.MarkupLine("[green]✓[/] .npmrc restored");
                    break;

                default:
                    AnsiConsole.MarkupLine($"[yellow]Unknown command: {command}[/]");
                    break;
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Command[/]").Centered())
                .AddColumn(new TableColumn("[bold]Description[/]"));

            table.AddRow("/help, /?", "Show this help");
            table.AddRow("/scan", "Re-scan environment");
            table.AddRow("/config", "Show current config");
            table.AddRow("/config set <key> <value>", "Update config");
            table.AddRow("/fix env", "Fix environment");
            table.AddRow("/fix buildtools", "Install Build Tools");
            table.AddRow("/history", "Show install history");
            table.AddRow("/backup", "Backup .npmrc");
            table.AddRow("/restore", "Restore .npmrc");
            table.AddRow("/clear, cls", "Clear screen");
            table.AddRow("exit, quit", "Exit program");

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue][Commands][/]");
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        /// <summary>
        /// 显示当前配置
        /// </summary>
        private void ShowConfig()
        {
            var config = _configManager.CurrentConfig;
            
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Key[/]").Centered())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            table.AddRow("registry", config.Registry ?? "Not set");
            table.AddRow("allowScriptsWhitelist", config.AllowScriptsWhitelist != null ? string.Join(", ", config.AllowScriptsWhitelist) : "Empty");
            table.AddRow("autoInstallBuildTools", config.AutoInstallBuildTools.ToString());
            table.AddRow("maxRetryCount", config.MaxRetryCount.ToString());
            table.AddRow("preferGlobalInstall", config.PreferGlobalInstall.ToString());
            table.AddRow("subCommandAutoRun", config.SubCommandAutoRun.ToString());

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue][Current Config][/]");
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        /// <summary>
        /// 显示安装历史
        /// </summary>
        private void ShowHistory()
        {
            if (_installHistory.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No install history[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Package[/]").Centered())
                .AddColumn(new TableColumn("[bold]Status[/]").Centered())
                .AddColumn(new TableColumn("[bold]Time[/]"));

            foreach (var item in _installHistory.TakeLast(10))
            {
                var status = item.Success ? "[green]✓[/]" : "[red]✗[/]";
                table.AddRow(item.PackageName, status, item.Timestamp.ToString("HH:mm:ss"));
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue][Install History (last 10)][/]");
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        /// <summary>
        /// 修复环境
        /// </summary>
        private void FixEnvironment()
        {
            AnsiConsole.MarkupLine("[yellow]Fixing environment...[/]");
            
            // 切换到国内镜像源
            if (!_envStatus.IsRegistryMirror)
            {
                EnvScanner.AutoSwitchToMirror(_configManager);
            }
            
            // 重新扫描
            _envStatus = EnvScanner.Scan();
            PrintEnvTable();
            
            AnsiConsole.MarkupLine("[green]✓[/] Environment fixed");
        }

        /// <summary>
        /// 安装 Build Tools
        /// </summary>
        private async Task InstallBuildToolsAsync()
        {
            AnsiConsole.MarkupLine("[yellow]Installing Build Tools via winget...[/]");
            
            var result = EnvScanner.RunCommand("winget", "install Microsoft.VisualStudio.2022.BuildTools --silent --accept-source-agreements --accept-package-agreements");
            
            if (result.ExitCode == 0)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Build Tools installed successfully");
                AnsiConsole.MarkupLine("[yellow]Please restart the application for changes to take effect[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Installation failed: {result.Error}");
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// 处理安装命令
        /// </summary>
        private async Task HandleInstallCommand(string input)
        {
            if (_isInstalling)
            {
                AnsiConsole.MarkupLine("[yellow]Installation in progress, please wait...[/]");
                return;
            }

            _isInstalling = true;
            _currentCts = new CancellationTokenSource();

            try
            {
                // 解析命令
                var parsed = CommandParser.Parse(input);
                if (parsed == null)
                {
                    AnsiConsole.MarkupLine("[red]Invalid command format[/]");
                    return;
                }

                // 构建安装命令
                var installCmd = CommandParser.BuildInstallCommand(parsed);
                AnsiConsole.MarkupLine($"[grey]Executing: {installCmd}[/]");

                // 创建安装执行器
                var executor = new InstallExecutor(installCmd, AppContext.BaseDirectory, _logger);
                
                // 订阅输出事件
                executor.OnOutput += (output) =>
                {
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        // 检测进度
                        if (output.Contains("idealTree") || output.Contains("reify"))
                        {
                            AnsiConsole.MarkupLine($"[grey]  {output}[/]");
                        }
                        else if (output.Contains("added") && output.Contains("packages"))
                        {
                            AnsiConsole.MarkupLine($"[green]  {output}[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"  {output}");
                        }
                    }
                };

                executor.OnError += (error) =>
                {
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        // 尝试错误自愈
                        var healing = _errorHealer.Analyze(error);
                        if (healing != null && healing.Matched)
                        {
                            AnsiConsole.MarkupLine($"[yellow]  Detected: {healing.Description}[/]");
                            if (!healing.NeedsInteraction)
                            {
                                AnsiConsole.MarkupLine($"[yellow]  Auto-fixing...[/]");
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]  {error}[/]");
                        }
                    }
                };

                // 执行安装
                var result = await executor.ExecuteAsync(_currentCts.Token);

                if (result.Success)
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Installation successful!");
                    
                    // 记录历史
                    _installHistory.Add(new InstallHistory
                    {
                        PackageName = parsed.PackageName,
                        Success = true,
                        Timestamp = DateTime.Now
                    });

                    // 询问是否运行子命令
                    if (!string.IsNullOrEmpty(parsed.SubCommand))
                    {
                        if (AnsiConsole.Confirm($"Run '{parsed.SubCommand}' now?", false))
                        {
                            var runCmd = $"{parsed.BinaryName} {parsed.SubCommand}";
                            AnsiConsole.MarkupLine($"[grey]Running: {runCmd}[/]");
                            EnvScanner.RunCommand(parsed.BinaryName, parsed.SubCommand);
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Installation failed: {result.ErrorMessage}");
                    
                    _installHistory.Add(new InstallHistory
                    {
                        PackageName = parsed.PackageName,
                        Success = false,
                        Timestamp = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
            finally
            {
                _isInstalling = false;
                _currentCts?.Dispose();
                _currentCts = null;
            }
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
                // 忽略加载错误
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
                    InstallHistory = _installHistory.TakeLast(100).ToList()
                };
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }
    }

    /// <summary>
    /// 会话状态
    /// </summary>
    public class SessionState
    {
        public List<InstallHistory> InstallHistory { get; set; } = new List<InstallHistory>();
    }

    /// <summary>
    /// 安装历史记录
    /// </summary>
    public class InstallHistory
    {
        public string PackageName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

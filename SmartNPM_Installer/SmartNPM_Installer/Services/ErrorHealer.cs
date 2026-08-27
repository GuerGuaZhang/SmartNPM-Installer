using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Utils;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// 错误自愈引擎
    /// </summary>
    public class ErrorHealer
    {
        private readonly ConfigManager _configManager;
        private readonly Logger _logger;

        // 错误模式匹配正则
        private static readonly Regex AllowScriptsRegex = new Regex(
            @"npm warn allowScripts\s+([@\w-]+)@",
            RegexOptions.Compiled);

        private static readonly Regex AllowScriptsFullRegex = new Regex(
            @"npm warn allowScripts\s+(\d+) packages have install scripts not yet covered by allowScripts:",
            RegexOptions.Compiled);

        private static readonly Regex AllowScriptsSuggestionRegex = new Regex(
            @"npm warn allowScripts Run `npm install -g --allow-scripts=([^`]+)`",
            RegexOptions.Compiled);

        /// <summary>
        /// 初始化错误自愈引擎
        /// </summary>
        /// <param name="configManager">配置管理器</param>
        /// <param name="logger">日志服务</param>
        public ErrorHealer(ConfigManager configManager, Logger logger)
        {
            _configManager = configManager;
            _logger = logger;
        }

        /// <summary>
        /// 分析错误输出并返回修复建议
        /// </summary>
        /// <param name="stderr">标准错误输出</param>
        /// <returns>修复结果，如果无法识别则返回null</returns>
        public HealingResult? Analyze(string stderr)
        {
            if (string.IsNullOrEmpty(stderr))
                return null;

            // P0: allow-scripts 阻止
            var allowScriptsResult = AnalyzeAllowScripts(stderr);
            if (allowScriptsResult != null)
                return allowScriptsResult;

            // P0: 网络错误
            var networkResult = AnalyzeNetworkError(stderr);
            if (networkResult != null)
                return networkResult;

            // P1: 缺少 Build Tools
            var buildToolsResult = AnalyzeBuildTools(stderr);
            if (buildToolsResult != null)
                return buildToolsResult;

            // P1: 缺少 Python
            var pythonResult = AnalyzePython(stderr);
            if (pythonResult != null)
                return pythonResult;

            // P2: 权限不足
            var permissionResult = AnalyzePermission(stderr);
            if (permissionResult != null)
                return permissionResult;

            // P2: 包不存在
            var notFoundResult = AnalyzeNotFound(stderr);
            if (notFoundResult != null)
                return notFoundResult;

            // P2: 版本不存在
            var versionResult = AnalyzeVersionNotFound(stderr);
            if (versionResult != null)
                return versionResult;

            // P3: 包已弃用
            var deprecatedResult = AnalyzeDeprecated(stderr);
            if (deprecatedResult != null)
                return deprecatedResult;

            // 未知错误
            return new HealingResult
            {
                Matched = false,
                ErrorType = ErrorType.Unknown,
                Priority = 4,
                Description = "未知错误",
                NeedsInteraction = true,
                Message = $"安装失败，以下是错误输出：\n{stderr}\n\n建议检查错误日志或提交 Issue。",
                Options = new List<string> { "查看完整日志", "重试安装", "跳过" }
            };
        }

        /// <summary>
        /// 分析 allow-scripts 错误
        /// </summary>
        private HealingResult? AnalyzeAllowScripts(string stderr)
        {
            // 检查是否有 allow-scripts 警告
            if (!stderr.Contains("npm warn allowScripts"))
                return null;

            // 提取被阻止的包名
            var packageNames = new List<string>();
            var matches = AllowScriptsRegex.Matches(stderr);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    packageNames.Add(match.Groups[1].Value);
                }
            }

            // 尝试从建议中提取更完整的包名列表
            var suggestionMatch = AllowScriptsSuggestionRegex.Match(stderr);
            if (suggestionMatch.Success && suggestionMatch.Groups.Count > 1)
            {
                var suggestedPackages = suggestionMatch.Groups[1].Value.Split(',');
                foreach (var pkg in suggestedPackages)
                {
                    var trimmedPkg = pkg.Trim();
                    if (!string.IsNullOrEmpty(trimmedPkg) && !packageNames.Contains(trimmedPkg))
                    {
                        packageNames.Add(trimmedPkg);
                    }
                }
            }

            if (packageNames.Count == 0)
                return null;

            _logger.LogWarning($"检测到 allow-scripts 阻止: {string.Join(", ", packageNames)}");

            return new HealingResult
            {
                Matched = true,
                ErrorType = ErrorType.AllowScriptsBlocked,
                Priority = 0,
                Description = "allow-scripts 阻止了安装脚本执行",
                NeedsInteraction = false,
                PackageNames = packageNames,
                Action = new HealingAction
                {
                    Type = ActionType.AppendAllowScripts,
                    Parameters = new Dictionary<string, string>
                    {
                        { "packages", string.Join(",", packageNames) }
                    }
                }
            };
        }

        /// <summary>
        /// 分析网络错误
        /// </summary>
        private HealingResult? AnalyzeNetworkError(string stderr)
        {
            if (stderr.Contains("npm ERR! code ECONNRESET") ||
                stderr.Contains("npm ERR! code ETIMEDOUT") ||
                stderr.Contains("npm ERR! network") ||
                stderr.Contains("npm ERR! request to") && stderr.Contains("failed"))
            {
                _logger.LogWarning("检测到网络错误");

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.NetworkError,
                    Priority = 0,
                    Description = "网络连接中断或超时",
                    NeedsInteraction = false,
                    Action = new HealingAction
                    {
                        Type = ActionType.SwitchRegistry,
                        Parameters = new Dictionary<string, string>
                        {
                            { "registry", _configManager.CurrentConfig.Registry }
                        }
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 分析 Build Tools 缺失错误
        /// </summary>
        private HealingResult? AnalyzeBuildTools(string stderr)
        {
            if (stderr.Contains("gyp ERR! find VS") ||
                stderr.Contains("MSB8003") ||
                stderr.Contains("node-gyp") && stderr.Contains("not found"))
            {
                _logger.LogWarning("检测到缺少 VC++ Build Tools");

                // 提取需要编译的包名
                var packageNames = ExtractPackageNamesFromGypError(stderr);

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.MissingBuildTools,
                    Priority = 1,
                    Description = "缺少 Visual C++ 构建工具",
                    NeedsInteraction = true,
                    PackageNames = packageNames,
                    Message = $"⚠ 检测到缺少 Visual C++ 构建工具，以下包需要编译原生模块：\n" +
                              $"   - {string.Join("\n   - ", packageNames)}\n\n" +
                              $"[1] 自动静默安装 Build Tools（通过 winget，约 5-10 分钟，推荐）\n" +
                              $"[2] 跳过，尝试继续（很可能失败）\n" +
                              $"[3] 显示手动安装教程链接\n\n" +
                              $"请选择 [1/2/3]: ",
                    Options = new List<string> { "自动安装", "跳过", "显示教程" },
                    Action = new HealingAction
                    {
                        Type = _configManager.CurrentConfig.AutoInstallBuildTools
                            ? ActionType.InstallBuildTools
                            : ActionType.ShowTutorial
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 分析 Python 缺失错误
        /// </summary>
        private HealingResult? AnalyzePython(string stderr)
        {
            if (stderr.Contains("gyp ERR! find Python") ||
                stderr.Contains("Python executable") && stderr.Contains("not found"))
            {
                _logger.LogWarning("检测到缺少 Python");

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.MissingPython,
                    Priority = 1,
                    Description = "缺少 Python",
                    NeedsInteraction = true,
                    Message = "⚠ 检测到缺少 Python，某些原生模块需要 Python 进行编译。\n\n" +
                              "请安装 Python 3.8+：https://www.python.org/downloads/\n\n" +
                              "安装后请确保将 Python 添加到 PATH 环境变量。",
                    Options = new List<string> { "打开下载页面", "跳过" },
                    Action = new HealingAction
                    {
                        Type = ActionType.ShowTutorial
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 分析权限错误
        /// </summary>
        private HealingResult? AnalyzePermission(string stderr)
        {
            if (stderr.Contains("npm ERR! code EACCES") ||
                stderr.Contains("npm ERR! code EPERM"))
            {
                _logger.LogWarning("检测到权限不足");

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.PermissionDenied,
                    Priority = 2,
                    Description = "权限不足",
                    NeedsInteraction = true,
                    Message = "⚠ 权限不足，无法安装到全局目录。\n\n" +
                              "请以管理员身份重新运行 SmartInstall.exe，\n" +
                              "或配置 npm 使用其他目录：\n" +
                              "npm config set prefix \"C:\\npm-global\"",
                    Options = new List<string> { "以管理员身份重试", "取消" },
                    Action = new HealingAction
                    {
                        Type = ActionType.Abort
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 分析包不存在错误
        /// </summary>
        private HealingResult? AnalyzeNotFound(string stderr)
        {
            if (stderr.Contains("npm ERR! code E404") ||
                stderr.Contains("npm ERR! 404") && stderr.Contains("not found"))
            {
                _logger.LogWarning("检测到包不存在");

                // 尝试提取包名
                var match = Regex.Match(stderr, @"npm ERR! 404\s+'([^']+)'");
                var packageName = match.Success ? match.Groups[1].Value : "未知包";

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.PackageNotFound,
                    Priority = 2,
                    Description = "包不存在",
                    NeedsInteraction = true,
                    PackageNames = new List<string> { packageName },
                    Message = $"⚠ 包 '{packageName}' 不存在于 registry 中。\n\n" +
                              "请检查包名是否正确，或尝试其他 registry。",
                    Options = new List<string> { "检查包名", "切换 registry", "取消" },
                    Action = new HealingAction
                    {
                        Type = ActionType.Abort
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 分析版本不存在错误
        /// </summary>
        private HealingResult? AnalyzeVersionNotFound(string stderr)
        {
            if (stderr.Contains("npm ERR! code ETARGET") ||
                stderr.Contains("npm ERR! 404") && stderr.Contains("version"))
            {
                _logger.LogWarning("检测到版本不存在");

                // 尝试提取包名和版本
                var match = Regex.Match(stderr, @"npm ERR! 404\s+'([^@]+)@([^']+)'");
                var packageName = match.Success ? match.Groups[1].Value : "未知包";
                var version = match.Success ? match.Groups[2].Value : "未知版本";

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.VersionNotFound,
                    Priority = 2,
                    Description = "版本不存在",
                    NeedsInteraction = true,
                    PackageNames = new List<string> { packageName },
                    Message = $"⚠ 包 '{packageName}' 的版本 '{version}' 不存在。\n\n" +
                              "请检查版本号是否正确，或使用最新版本。",
                    Options = new List<string> { "使用最新版本", "查看可用版本", "取消" },
                    Action = new HealingAction
                    {
                        Type = ActionType.Abort
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 分析包弃用警告
        /// </summary>
        private HealingResult? AnalyzeDeprecated(string stderr)
        {
            if (stderr.Contains("npm WARN deprecated"))
            {
                _logger.LogInfo("检测到包弃用警告（非阻断）");

                return new HealingResult
                {
                    Matched = true,
                    ErrorType = ErrorType.PackageDeprecated,
                    Priority = 3,
                    Description = "包已弃用",
                    NeedsInteraction = false,
                    Message = "ℹ 某些包已弃用，但不影响安装。",
                    Options = new List<string> { "继续" },
                    Action = new HealingAction
                    {
                        Type = ActionType.Continue
                    }
                };
            }

            return null;
        }

        /// <summary>
        /// 从 gyp 错误中提取包名
        /// </summary>
        private List<string> ExtractPackageNamesFromGypError(string stderr)
        {
            var packageNames = new List<string>();

            // 匹配 "npm ERR! path C:\...\node_modules\<package>" 格式
            var pathMatches = Regex.Matches(stderr, @"npm ERR! path [^\\]+\\node_modules\\([^\\]+)");
            foreach (Match match in pathMatches)
            {
                if (match.Groups.Count > 1)
                {
                    var pkg = match.Groups[1].Value;
                    if (!packageNames.Contains(pkg))
                    {
                        packageNames.Add(pkg);
                    }
                }
            }

            // 如果没有找到，返回一些常见需要编译的包
            if (packageNames.Count == 0)
            {
                packageNames.AddRange(new[] { "node-pty", "koffi", "sharp" });
            }

            return packageNames;
        }

        /// <summary>
        /// 执行修复动作
        /// </summary>
        /// <param name="healingResult">修复结果</param>
        /// <param name="userChoice">用户选择（如果需要交互）</param>
        /// <returns>是否成功修复</returns>
        public bool ApplyFix(HealingResult healingResult, string? userChoice = null)
        {
            if (healingResult.Action == null)
                return false;

            switch (healingResult.Action.Type)
            {
                case ActionType.AppendAllowScripts:
                    if (healingResult.PackageNames != null)
                    {
                        _configManager.AppendAllowScripts(healingResult.PackageNames);
                        return true;
                    }
                    break;

                case ActionType.SwitchRegistry:
                    if (healingResult.Action.Parameters?.ContainsKey("registry") == true)
                    {
                        var registry = healingResult.Action.Parameters["registry"];
                        _configManager.SetRegistry(registry);
                        return true;
                    }
                    break;

                case ActionType.InstallBuildTools:
                    // 由外部处理安装逻辑
                    return true;

                case ActionType.ShowTutorial:
                    // 由外部处理显示逻辑
                    return true;

                case ActionType.Continue:
                    return true;

                case ActionType.Abort:
                    return false;
            }

            return false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using SmartNPM_Installer.Models;
using SmartNPM_Installer.Utils;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// 配置管理服务
    /// </summary>
    public class ConfigManager
    {
        private readonly string _configPath;
        private readonly string _npmrcPath;
        private readonly Logger _logger;
        private AppConfig _config;
        private readonly object _npmrcLock = new object();

        /// <summary>
        /// 默认配置
        /// </summary>
        private static readonly AppConfig DefaultConfig = new AppConfig
        {
            Registry = "https://registry.npmmirror.com",
            AllowScriptsWhitelist = new List<string>
            {
                "node-pty", "koffi", "sharp", "bcrypt",
                "better-sqlite3", "sqlite3", "canvas",
                "node-sass", "sass", "esbuild", "electron",
                "@deepseek-ai/dsh-subprocess-local",
                "@anthropic-ai/claude-code"
            },
            AutoInstallBuildTools = true,
            NpmCacheDir = "./sni-cache",
            MaxRetryCount = 3,
            PreferGlobalInstall = true,
            SubCommandAutoRun = false
        };

        /// <summary>
        /// 初始化配置管理器
        /// </summary>
        /// <param name="logger">日志服务</param>
        public ConfigManager(Logger logger)
        {
            _logger = logger;
            _configPath = Path.Combine(AppContext.BaseDirectory, "sni-config.json");
            _npmrcPath = GetNpmrcPath();
            _config = LoadConfig();
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public AppConfig CurrentConfig => _config;

        /// <summary>
        /// 获取 .npmrc 路径
        /// </summary>
        private string GetNpmrcPath()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "config get userconfig",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        return output;
                }
            }
            catch
            {
                // 忽略错误，使用默认路径
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".npmrc");
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        _logger.LogInfo($"已加载配置文件: {_configPath}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"加载配置文件失败: {ex.Message}");
            }

            _logger.LogInfo("使用默认配置");
            return DefaultConfig;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
                _logger.LogInfo($"配置已保存: {_configPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载 .npmrc 文件
        /// </summary>
        private NpmrcConfig LoadNpmrc()
        {
            var config = new NpmrcConfig();

            if (!File.Exists(_npmrcPath))
                return config;

            try
            {
                foreach (var line in File.ReadAllLines(_npmrcPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                        continue;

                    if (trimmed.StartsWith("#"))
                    {
                        config.Comments.Add(trimmed);
                        continue;
                    }

                    var idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        var key = trimmed.Substring(0, idx).Trim();
                        var val = trimmed.Substring(idx + 1).Trim();
                        config.Entries[key] = val;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"读取 .npmrc 文件失败: {ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// 保存 .npmrc 文件
        /// </summary>
        private void SaveNpmrc(NpmrcConfig config)
        {
            try
            {
                var lines = new List<string>();
                lines.AddRange(config.Comments);
                foreach (var kv in config.Entries)
                    lines.Add($"{kv.Key}={kv.Value}");

                File.WriteAllLines(_npmrcPath, lines);
            }
            catch (Exception ex)
            {
                _logger.LogError($"保存 .npmrc 文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 备份 .npmrc 文件
        /// </summary>
        public void BackupNpmrc()
        {
            if (!File.Exists(_npmrcPath))
                return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backupPath = $"{_npmrcPath}.sni-backup-{timestamp}";

            try
            {
                File.Copy(_npmrcPath, backupPath, true);
                _logger.LogInfo($"已备份 .npmrc 到: {backupPath}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"备份 .npmrc 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置 registry
        /// </summary>
        /// <param name="url">registry URL</param>
        public void SetRegistry(string url)
        {
            lock (_npmrcLock)
            {
                var npmrc = LoadNpmrc();
                var old = npmrc.Entries.ContainsKey("registry") ? npmrc.Entries["registry"] : null;

                if (old == url)
                    return; // 已相同，跳过

                BackupNpmrc();
                npmrc.Entries["registry"] = url;
                SaveNpmrc(npmrc);
                _logger.LogInfo($"registry: {old ?? "(未设置)"} → {url}");
            }
        }

        /// <summary>
        /// 追加 allow-scripts 白名单
        /// </summary>
        /// <param name="packages">包名列表</param>
        public void AppendAllowScripts(List<string> packages)
        {
            lock (_npmrcLock)
            {
                var npmrc = LoadNpmrc();
                var current = npmrc.Entries.ContainsKey("allow-scripts") ? npmrc.Entries["allow-scripts"] : null;

                HashSet<string> whitelist;
                if (string.IsNullOrEmpty(current) || current.Trim() == "")
                    whitelist = new HashSet<string>();
                else if (current.Trim().ToLower() == "true")
                    return; // 已完全信任，无需追加
                else
                    whitelist = new HashSet<string>(current.Split(',', StringSplitOptions.TrimEntries));

                bool changed = false;
                foreach (var pkg in packages)
                {
                    if (!whitelist.Contains(pkg))
                    {
                        whitelist.Add(pkg);
                        changed = true;
                    }
                }

                if (!changed)
                    return;

                BackupNpmrc();
                npmrc.Entries["allow-scripts"] = string.Join(",", whitelist);
                SaveNpmrc(npmrc);
                _logger.LogInfo($"allow-scripts 白名单已追加: {string.Join(", ", packages)}");
            }
        }

        /// <summary>
        /// 设置 npm 缓存目录
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        public void SetCacheDir(string relativePath)
        {
            lock (_npmrcLock)
            {
                var absPath = Path.GetFullPath(relativePath, AppContext.BaseDirectory);
                Directory.CreateDirectory(absPath);

                var npmrc = LoadNpmrc();
                npmrc.Entries["cache"] = absPath.Replace("\\", "/");
                SaveNpmrc(npmrc);

                _logger.LogInfo($"npm 缓存目录已设置: {absPath}");
            }
        }

        /// <summary>
        /// 从最近的备份恢复 .npmrc
        /// </summary>
        /// <returns>是否成功恢复</returns>
        public bool RestoreNpmrc()
        {
            lock (_npmrcLock)
            {
                var backupFiles = Directory.GetFiles(
                    Path.GetDirectoryName(_npmrcPath) ?? ".",
                    Path.GetFileName(_npmrcPath) + ".sni-backup-*")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();

                if (backupFiles == null)
                {
                    _logger.LogWarning("未找到 .npmrc 备份文件");
                    return false;
                }

                try
                {
                    File.Copy(backupFiles, _npmrcPath, true);
                    _logger.LogInfo($"已从备份恢复 .npmrc: {backupFiles}");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"恢复 .npmrc 失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取配置值
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>配置值</returns>
        public string? GetConfigValue(string key)
        {
            return key switch
            {
                "registry" => _config.Registry,
                "maxRetryCount" => _config.MaxRetryCount.ToString(),
                "preferGlobalInstall" => _config.PreferGlobalInstall.ToString(),
                "subCommandAutoRun" => _config.SubCommandAutoRun.ToString(),
                "autoInstallBuildTools" => _config.AutoInstallBuildTools.ToString(),
                _ => null
            };
        }

        /// <summary>
        /// 设置配置值
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        /// <returns>是否成功设置</returns>
        public bool SetConfigValue(string key, string value)
        {
            try
            {
                switch (key)
                {
                    case "registry":
                        _config.Registry = value;
                        break;
                    case "maxRetryCount":
                        if (int.TryParse(value, out var maxRetry))
                            _config.MaxRetryCount = maxRetry;
                        else
                            return false;
                        break;
                    case "preferGlobalInstall":
                        if (bool.TryParse(value, out var preferGlobal))
                            _config.PreferGlobalInstall = preferGlobal;
                        else
                            return false;
                        break;
                    case "subCommandAutoRun":
                        if (bool.TryParse(value, out var autoRun))
                            _config.SubCommandAutoRun = autoRun;
                        else
                            return false;
                        break;
                    case "autoInstallBuildTools":
                        if (bool.TryParse(value, out var autoInstall))
                            _config.AutoInstallBuildTools = autoInstall;
                        else
                            return false;
                        break;
                    default:
                        return false;
                }

                SaveConfig();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 应用配置
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 默认 registry
        /// </summary>
        public string Registry { get; set; } = "https://registry.npmmirror.com";

        /// <summary>
        /// allow-scripts 白名单
        /// </summary>
        public List<string> AllowScriptsWhitelist { get; set; } = new List<string>();

        /// <summary>
        /// 是否自动安装 Build Tools
        /// </summary>
        public bool AutoInstallBuildTools { get; set; } = true;

        /// <summary>
        /// npm 缓存目录
        /// </summary>
        public string NpmCacheDir { get; set; } = "./sni-cache";

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 是否优先全局安装
        /// </summary>
        public bool PreferGlobalInstall { get; set; } = true;

        /// <summary>
        /// 子命令是否自动执行
        /// </summary>
        public bool SubCommandAutoRun { get; set; } = false;
    }

    /// <summary>
    /// .npmrc 配置
    /// </summary>
    public class NpmrcConfig
    {
        /// <summary>
        /// 配置条目
        /// </summary>
        public Dictionary<string, string> Entries { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 注释行
        /// </summary>
        public List<string> Comments { get; set; } = new List<string>();
    }
}
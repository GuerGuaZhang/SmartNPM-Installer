using System;
using System.Text.RegularExpressions;

namespace SmartNPM_Installer.Models
{
    /// <summary>
    /// 解析后的命令对象
    /// </summary>
    public class ParsedCommand
    {
        /// <summary>
        /// 原始输入
        /// </summary>
        public string RawInput { get; set; } = string.Empty;

        /// <summary>
        /// 包名，如 "@deepseek-ai/dsh"
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// 版本号，如 "0.1.1-rc.2"，null 表示 latest
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 子命令，如 "web"
        /// </summary>
        public string? SubCommand { get; set; }

        /// <summary>
        /// 是否带 @scope/
        /// </summary>
        public bool IsScoped { get; set; }

        /// <summary>
        /// 作用域，如 "deepseek-ai"
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// 推断的 CLI 二进制名，如 "dsh"
        /// </summary>
        public string BinaryName { get; set; } = string.Empty;

        /// <summary>
        /// 安装来源
        /// </summary>
        public InstallSource Source { get; set; }
    }

    /// <summary>
    /// 安装来源枚举
    /// </summary>
    public enum InstallSource
    {
        /// <summary>
        /// 用户输入 npx ...
        /// </summary>
        Npx,

        /// <summary>
        /// 用户输入 npm install ...
        /// </summary>
        NpmInstall,

        /// <summary>
        /// 用户输入其他 npm 命令（npm list, npm view 等）
        /// </summary>
        NpmOther,

        /// <summary>
        /// 用户只输入了包名
        /// </summary>
        RawPackageName
    }
}
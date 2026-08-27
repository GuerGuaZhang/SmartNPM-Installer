using System;
using System.Collections.Generic;

namespace SmartNPM_Installer.Models
{
    /// <summary>
    /// 错误修复结果
    /// </summary>
    public class HealingResult
    {
        /// <summary>
        /// 是否匹配到错误模式
        /// </summary>
        public bool Matched { get; set; }

        /// <summary>
        /// 错误类型
        /// </summary>
        public ErrorType ErrorType { get; set; }

        /// <summary>
        /// 错误优先级 (P0-P3)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 错误描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 需要用户交互
        /// </summary>
        public bool NeedsInteraction { get; set; }

        /// <summary>
        /// 提示消息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 交互选项
        /// </summary>
        public List<string>? Options { get; set; }

        /// <summary>
        /// 提取的包名列表
        /// </summary>
        public List<string>? PackageNames { get; set; }

        /// <summary>
        /// 修复动作
        /// </summary>
        public HealingAction? Action { get; set; }
    }

    /// <summary>
    /// 错误类型枚举
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// allow-scripts 阻止
        /// </summary>
        AllowScriptsBlocked,

        /// <summary>
        /// 网络中断
        /// </summary>
        NetworkError,

        /// <summary>
        /// 缺少 VC++ Build Tools
        /// </summary>
        MissingBuildTools,

        /// <summary>
        /// 缺少 Python
        /// </summary>
        MissingPython,

        /// <summary>
        /// 权限不足
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// 包不存在
        /// </summary>
        PackageNotFound,

        /// <summary>
        /// 版本不存在
        /// </summary>
        VersionNotFound,

        /// <summary>
        /// 包已弃用
        /// </summary>
        PackageDeprecated,

        /// <summary>
        /// 未知错误
        /// </summary>
        Unknown
    }

    /// <summary>
    /// 修复动作
    /// </summary>
    public class HealingAction
    {
        /// <summary>
        /// 动作类型
        /// </summary>
        public ActionType Type { get; set; }

        /// <summary>
        /// 动作参数
        /// </summary>
        public Dictionary<string, string>? Parameters { get; set; }
    }

    /// <summary>
    /// 动作类型枚举
    /// </summary>
    public enum ActionType
    {
        /// <summary>
        /// 追加 allow-scripts 白名单
        /// </summary>
        AppendAllowScripts,

        /// <summary>
        /// 切换 registry
        /// </summary>
        SwitchRegistry,

        /// <summary>
        /// 安装 Build Tools
        /// </summary>
        InstallBuildTools,

        /// <summary>
        /// 安装 Python
        /// </summary>
        InstallPython,

        /// <summary>
        /// 重试安装
        /// </summary>
        RetryInstall,

        /// <summary>
        /// 显示教程
        /// </summary>
        ShowTutorial,

        /// <summary>
        /// 继续执行
        /// </summary>
        Continue,

        /// <summary>
        /// 中止安装
        /// </summary>
        Abort
    }
}
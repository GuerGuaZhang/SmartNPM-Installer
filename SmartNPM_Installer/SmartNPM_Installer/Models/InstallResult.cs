using System;

namespace SmartNPM_Installer.Models
{
    /// <summary>
    /// 安装结果
    /// </summary>
    public class InstallResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 标准输出
        /// </summary>
        public string? StandardOutput { get; set; }

        /// <summary>
        /// 标准错误输出
        /// </summary>
        public string? StandardError { get; set; }

        /// <summary>
        /// 退出代码
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// 安装耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 新增的包数量
        /// </summary>
        public int? PackagesAdded { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static InstallResult SuccessResult()
        {
            return new InstallResult { Success = true };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static InstallResult FailureResult(string errorMessage)
        {
            return new InstallResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 创建带详情的成功结果
        /// </summary>
        public static InstallResult SuccessResult(string? stdout, string? stderr, int exitCode, TimeSpan duration, int? packagesAdded = null)
        {
            return new InstallResult
            {
                Success = exitCode == 0,
                StandardOutput = stdout,
                StandardError = stderr,
                ExitCode = exitCode,
                Duration = duration,
                PackagesAdded = packagesAdded
            };
        }
    }
}
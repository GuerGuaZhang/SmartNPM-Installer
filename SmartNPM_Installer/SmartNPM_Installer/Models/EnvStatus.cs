using System;

namespace SmartNPM_Installer.Models
{
    /// <summary>
    /// 环境扫描结果
    /// </summary>
    public class EnvStatus
    {
        /// <summary>
        /// Node.js 是否安装
        /// </summary>
        public bool NodeInstalled { get; set; }

        /// <summary>
        /// Node.js 版本号
        /// </summary>
        public string? NodeVersion { get; set; }

        /// <summary>
        /// npm 版本号
        /// </summary>
        public string? NpmVersion { get; set; }

        /// <summary>
        /// 当前 registry 地址
        /// </summary>
        public string CurrentRegistry { get; set; } = "https://registry.npmjs.org";

        /// <summary>
        /// 当前 allow-scripts 配置
        /// </summary>
        public string? CurrentAllowScripts { get; set; }

        /// <summary>
        /// Python 是否安装
        /// </summary>
        public bool HasPython { get; set; }

        /// <summary>
        /// Python 路径
        /// </summary>
        public string? PythonPath { get; set; }

        /// <summary>
        /// VC++ Build Tools 是否安装
        /// </summary>
        public bool HasBuildTools { get; set; }

        /// <summary>
        /// .npmrc 文件路径
        /// </summary>
        public string? NpmrcPath { get; set; }

        /// <summary>
        /// 是否为国内镜像源
        /// </summary>
        public bool IsRegistryMirror { get; set; }

        /// <summary>
        /// HTTP 代理状态
        /// </summary>
        public string? HttpProxy { get; set; }
    }
}
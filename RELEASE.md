## 🚀 v1.0.0 首次发布

### ✨ Features
- **智能命令解析** — 支持 `npx`、`npm install`、纯包名三种输入格式，自动识别作用域包和版本号
- **自动环境扫描** — 检测 Node.js/npm/registry/allow-scripts/Python/VC++ Build Tools
- **Registry 自动切换** — 检测到非国内源时自动切换到 npmmirror
- **allow-scripts 自动修复** — 检测到阻断时自动追加白名单并重试，无需用户干预
- **错误自愈引擎** — 网络超时自动切换镜像、Build Tools 缺失提供一键安装、权限错误提示管理员运行
- **REPL 交互界面** — 彩色输出、进度条、内部命令系统（/help /scan /config /history 等）
- **配置外置** — sni-config.json 持久化用户偏好，支持运行时修改
- **日志系统** — 按日期分割文件日志，保留30天

### 📦 Install
下载 `SmartInstall.exe`（自包含单文件，~10.4MB），双击即用，无需安装 .NET 运行时。

### 📝 Changelog
- 实现完整的 npm 命令解析器（CommandParser）
- 实现环境扫描器（EnvScanner）支持 Node/npm/registry/Python/BuildTools 检测
- 实现配置管理器（ConfigManager）支持 .npmrc 幂等修改和自动备份
- 实现安装执行器（InstallExecutor）支持子进程管理和实时输出捕获
- 实现错误自愈引擎（ErrorHealer）支持 6 种错误模式匹配
- 实现 REPL 交互引擎（ReplEngine）支持 11 个内部命令

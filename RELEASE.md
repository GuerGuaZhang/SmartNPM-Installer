## 🚀 v2.0.0

### ✨ Features
- **智能命令解析** — 支持 `npx`、`npm install`、纯包名三种输入格式，自动识别作用域包和版本号
- **自动环境扫描** — 检测 Node.js/npm/registry/allow-scripts/Python/VC++ Build Tools
- **Registry 自动切换** — 检测到非国内源时自动切换到 npmmirror
- **allow-scripts 自动修复** — 自动添加新包到白名单，无需手动维护
- **错误自愈引擎** — 网络超时自动切换镜像、Build Tools 缺失提供一键安装、权限错误提示管理员运行
- **REPL 交互界面** — 彩色输出、进度条、内部命令系统（/help /scan /config /history 等）
- **配置外置** — sni-config.json 持久化用户偏好，支持运行时修改
- **日志系统** — 按日期分割文件日志，保留30天
- **子命令执行** — 安装完成后询问是否立即运行原始子命令
- **支持多种 npm 命令** — 支持 npm list、npm view、npm update 等命令

### 📦 Install
下载 `SmartInstall.exe`（自包含单文件，约 10MB），双击即用，无需安装 .NET 运行时。

### 📝 Changelog
- 添加程序图标（favicon.ico）
- 使用 Spectre.Console 实现彩色输出和表格
- 自动切换到 npmmirror 国内镜像
- 自动将新包添加到 allow-scripts 白名单
- 支持 npm list、npm view、npm update 等命令
- 修复 npm 执行路径问题（使用 cmd.exe）
- 修复缓存目录配置错误
- 添加完整的 Node.js/npm 环境变量

### 🧪 测试结果
- ✅ typescript - 安装成功
- ✅ nodemon - 安装成功
- ✅ http-server - 安装成功
- ✅ esbuild - 安装成功
- ✅ @vue/cli - 安装成功（自动添加白名单）
- ✅ prettier@3.0.0 - 安装成功
- ✅ npm list -g --depth=0 - 显示正确
- ✅ npm view typescript version - 显示正确
- ✅ npm update -g - 更新成功

---

## 🚀 v1.0.0

### ✨ Features
- **智能命令解析** — 支持 `npx`、`npm install`、纯包名三种输入格式，自动识别作用域包和版本号
- **自动环境扫描** — 检测 Node.js/npm/registry/allow-scripts/Python/VC++ Build Tools
- **Registry 自动切换** — 检测到非国内源时自动切换到 npmmirror
- **allow-scripts 自动修复** — 检测到阻断时自动追加白名单并重试，无需用户干预
- **错误自愈引擎** — 网络超时自动切换镜像、Build Tools 缺失提供一键安装、权限错误提示管理员运行
- **REPL 交互界面** — 彩色输出、进度条、内部命令系统（/help /scan /config /history 等）
- **配置外置** — sni-config.json 持久化用户偏好，支持运行时修改
- **日志系统** — 按日期分割文件日志，保留30天
- **原生模块预判** — 安装前分析依赖树，提前准备编译环境
- **子命令执行** — 安装完成后询问是否立即运行原始子命令

### 📦 Install
下载 `SmartInstall.exe`（自包含单文件，≤ 15MB），双击即用，无需安装 .NET 运行时。

### 📝 Changelog
- 实现完整的 npm 命令解析器（CommandParser）
- 实现环境扫描器（EnvScanner）支持 Node/npm/registry/Python/BuildTools 检测
- 实现配置管理器（ConfigManager）支持 .npmrc 幂等修改和自动备份
- 实现安装执行器（InstallExecutor）支持子进程管理和实时输出捕获
- 实现错误自愈引擎（ErrorHealer）支持 6 种错误模式匹配
- 实现 REPL 交互引擎（ReplEngine）支持 11 个内部命令
- 更新 README.md 和 .gitignore 文件

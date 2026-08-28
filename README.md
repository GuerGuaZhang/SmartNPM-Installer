# Smart NPM Installer (SNI)

> 🔧 智能 npm 包安装器 — 自动修复环境问题，一键完成安装

![icon](favicon.ico)

## ✨ 特性

- 🔄 **智能命令解析** — 支持 `npx`、`npm install -g`、纯包名三种输入方式
- 🛠️ **环境自动修复** — 自动切换国内镜像源、追加 `allow-scripts` 白名单
- 🔍 **原生模块预判** — 安装前分析依赖树，提前准备编译环境
- 🩹 **错误自愈引擎** — 实时捕获 stderr，自动匹配错误模式并修复重试
- 📊 **可视化进度** — 彩色输出实时展示安装阶段
- ⚙️ **安全配置管理** — 幂等修改 `.npmrc`，修改前自动备份
- 📦 **单文件便携** — 一个 `SmartInstall.exe`，U 盘随插随用，零安装

## 🚀 快速开始

### 前置条件

- Windows 10/11 x64
- Node.js 已安装（即使 npm 有问题）

### 使用方式

1. 下载 `SmartInstall.exe`（约 10MB）
2. 双击运行
3. 在提示符后粘贴任意 `npx` / `npm` 命令

```
Smart NPM Installer (SNI) v2.0
Paste npx/npm command, auto-fix & install

System Scan Results
+---------------------+--------------------------------+--------+
| Item                | Status                         | Result |
+---------------------+--------------------------------+--------+
| Node.js             | v24.19.0                       |   OK   |
| npm                 | 12.0.2                         |   OK   |
| Registry            | https://registry.npmmirror.com |   OK   |
| Allow-scripts       | Configured (16 items)          |   OK   |
| Python              | Installed                      |   OK   |
| VC++ Build Tools    | Installed                      |   OK   |
+---------------------+--------------------------------+--------+

smart-install> npm install -g @vue/cli
Executing: npm install -g @vue/cli
changed 844 packages in 30s
OK Installation successful!
```

### 支持的命令

```bash
# 安装包
npm install -g typescript
npm install -g @vue/cli
nodemon

# 查看已安装的包
npm list -g --depth=0

# 查看包版本
npm view typescript version

# 更新所有包
npm update -g
```

### 编译发布

如果你想自行编译：

```bash
dotnet publish -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=true `
  -p:TrimMode=partial `
  -p:EnableCompressionInSingleFile=true
```

产出：`SmartInstall.exe`（约 10MB）

## 📁 项目结构

```
SmartInstaller/
├── SmartNPM_Installer/
│   ├── SmartNPM_Installer/
│   │   ├── Models/
│   │   │   ├── ParsedCommand.cs      # 解析后的命令对象
│   │   │   ├── EnvStatus.cs          # 环境扫描结果
│   │   │   ├── InstallResult.cs      # 安装结果
│   │   │   └── HealingResult.cs      # 错误修复结果
│   │   ├── Services/
│   │   │   ├── CommandParser.cs      # 命令解析器
│   │   │   ├── EnvScanner.cs         # 环境扫描器
│   │   │   ├── ConfigManager.cs      # .npmrc 配置管理
│   │   │   ├── InstallExecutor.cs    # 安装执行器
│   │   │   ├── ErrorHealer.cs        # 错误自愈引擎
│   │   │   └── ReplEngine.cs         # REPL 交互引擎
│   │   ├── Utils/
│   │   │   └── Logger.cs             # 日志系统
│   │   └── Program.cs                # 入口文件
│   └── SmartNPM_Installer.csproj
├── SmartNPM_Installer.Tests/         # 单元测试项目
├── SmartNPM_Installer.sln
├── README.md
├── RELEASE.md
└── favicon.ico                       # 程序图标
```

## 📋 内部命令

| 命令 | 说明 |
|------|------|
| `/help` 或 `/?` | 显示帮助信息 |
| `/scan` | 重新执行环境扫描 |
| `/config` | 显示当前配置 |
| `/config set <key> <value>` | 修改配置项 |
| `/fix env` | 手动触发环境修复 |
| `/fix buildtools` | 手动安装 Build Tools（通过 winget） |
| `/history` | 显示本次会话的安装历史 |
| `/backup` | 备份当前 `.npmrc` |
| `/restore` | 从最近的备份恢复 `.npmrc` |
| `/clear` 或 `cls` | 清屏 |
| `exit` / `quit` | 保存状态并退出 |

## ⚙️ 配置

程序运行时会在 exe 同级目录生成 `sni-config.json`：

```json
{
  "registry": "https://registry.npmmirror.com",
  "allowScriptsWhitelist": [
    "node-pty", "koffi", "sharp", "bcrypt",
    "better-sqlite3", "sqlite3", "canvas",
    "node-sass", "sass", "esbuild", "electron"
  ],
  "autoInstallBuildTools": true,
  "maxRetryCount": 3,
  "preferGlobalInstall": true,
  "subCommandAutoRun": false
}
```

### 运行时目录结构

```
SmartInstall.exe
├── sni-config.json      # 用户偏好配置（持久化）
├── sni-state.json       # 会话状态（安装历史）
└── sni-logs/
    └── 2026-08-28.log   # 按日期分割的文本日志
```

## ❓ 常见问题

### Q: 杀毒软件报毒怎么办？
A: 这是误报。SNI 是自包含的 .NET 单文件应用，杀毒软件可能将未签名的可执行文件标记为可疑。将程序添加到白名单即可。

### Q: 运行时提示权限不足？
A: 部分操作（如安装 Build Tools）需要管理员权限。右键选择"以管理员身份运行"即可。

### Q: 如何切换回官方源？
A: 在提示符中输入 `/config set registry https://registry.npmjs.org`。

### Q: 安装 Build Tools 后需要重启吗？
A: 是的。Build Tools 安装完成后需要关闭并重新打开 SNI，让环境变量生效。

### Q: 为什么有这么多 deprecated 警告？
A: 这些警告来自 npm 包的依赖，不是 SmartInstaller 的问题。它们不影响使用，只是官方建议迁移到新版本。

## 📄 许可

MIT License

## 🔗 相关链接

- [GitHub 仓库](https://github.com/GuerGuaZhang/SmartNPM-Installer)
- [下载 Release](https://github.com/GuerGuaZhang/SmartNPM-Installer/releases)

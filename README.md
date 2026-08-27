# Smart NPM Installer (SNI)

> 🔧 自动修复 npm 安装问题的智能工具

## ✨ 特性

- 🔄 **npm 命令自动修复** - 自动检测并修复损坏的 npm 安装
- 🔍 **环境扫描** - 全面扫描 Node.js 和 npm 环境配置
- 📦 **allow-scripts 管理** - 智能管理 npm 包的脚本执行权限
- ⚙️ **.npmrc 配置** - 自动配置和优化 npmrc 文件
- 🛡️ **错误诊断** - 详细诊断 npm 安装失败的原因
- 📊 **状态报告** - 生成详细的环境状态报告

## 🚀 快速开始

### 前置条件

- Windows 10/11 操作系统
- Node.js 已安装（即使 npm 有问题）

### 安装 / 使用

1. 下载 `SmartInstall.exe`
2. 双击运行程序
3. 按照提示操作

```
# 直接运行
SmartInstall.exe

# 或者在命令行中运行
.\SmartInstall.exe
```

## 📁 项目结构

```
SmartInstaller/
├── Models/           # 数据模型
├── Services/         # 核心服务逻辑
│   ├── NpmService.cs     # npm 相关操作
│   ├── ScanService.cs    # 环境扫描
│   └── RepairService.cs  # 修复服务
├── Utils/            # 工具类
│   ├── Logger.cs         # 日志工具
│   └── ConfigHelper.cs  # 配置辅助
├── Program.cs        # 入口文件
├── SmartInstall.exe  # 发布后的可执行文件 (~10.4MB)
├── README.md         # 项目说明
└── sni-config.json   # 运行时配置文件
```

## ⚙️ 配置

程序运行时会生成 `sni-config.json` 配置文件，你可以手动编辑：

```json
{
  "autoFix": true,
  "verboseLog": false,
  "backupBeforeFix": true,
  "npmRegistry": "https://registry.npmmirror.com"
}
```

## ❓ 内部命令

在程序运行时，你可以使用以下命令：

| 命令 | 说明 |
|------|------|
| `/help` | 显示帮助信息 |
| `/scan` | 执行全面环境扫描 |
| `/config` | 查看/修改配置 |
| `/fix` | 手动触发修复 |
| `/backup` | 备份当前配置 |
| `/exit` | 退出程序 |

## 🐛 常见问题

### Q: 杀毒软件报毒怎么办？
A: 这是误报，SNI 不包含任何恶意代码。你可以将程序添加到杀毒软件白名单。

### Q: 运行时提示权限不足？
A: 右键选择"以管理员身份运行"即可。

### Q: 如何查看详细日志？
A: 运行时输入 `/config`，将 verboseLog 设置为 true，日志会保存在 `sni-logs/` 目录。

## 📄 许可

MIT License
# Smart NPM Installer (SNI) — 技术规格书与开发提示词

> **文档性质**：系统需求规格书（SRS）+ 开发提示词（Dev Prompt）
> **目标读者**：负责编码实现的大语言模型 / 编程 Agent
> **目标平台**：Windows 10/11 x64
> **输出形态**：单个自包含可执行文件（Console REPL）
> **核心语言**：C# (.NET 8+ Console Application, 自包含单文件发布)

---

## 1. 项目概述

### 1.1 问题定义
用户在 Windows 环境下通过 `npx <package>` 或 `npm install -g <package>` 安装 npm 包时，频繁遇到以下障碍：
- npm 10+ 默认禁止 `postinstall` 脚本，导致 `node-pty`、`koffi`、`sharp` 等含原生 C++ 模块的包安装后功能残缺
- 默认 registry 为官方源，国内下载慢、易超时
- 缺少 Python / Visual C++ Build Tools 时，原生模块编译失败，报错信息晦涩
- `npx` 每次都会重新下载/检查版本，不稳定且慢

### 1.2 解决方案
开发一个 **Console REPL 程序**，双击即开。用户在提示符后粘贴任意 `npx` / `npm install` 命令，程序自动完成：
1. **命令解析** — 提取包名、作用域、版本、子命令
2. **环境扫描** — 检测 Node/npm 版本、当前 registry、allow-scripts 状态、编译工具链
3. **智能修复** — 自动切换国内镜像、追加 allow-scripts 白名单、检测/提示 Build Tools
4. **全局安装** — 将 `npx` 转换为 `npm install -g`，避免重复下载
5. **错误自愈** — 实时捕获 stderr，模式匹配错误关键字，暂停→修复→重试
6. **子命令执行** — 安装完成后询问是否立即运行原始子命令（如 `web`）

### 1.3 核心原则
- **单文件便携**：`SmartInstall.exe` 一个文件，可放 U 盘随插随用
- **零污染**：只修改用户级 `~/.npmrc`（`--location=user`），不动系统级配置
- **幂等性**：多次运行同一命令，环境修复不重复、不冲突、可回滚
- **透明性**：所有自动修复操作必须实时打印日志，用户完全知情

---

## 2. 技术栈与发布配置

### 2.1 技术栈
- **语言**：C# 12+ (.NET 8)
- **项目类型**：Console Application
- **UI 增强**：Spectre.Console (NuGet 包，用于彩色输出、进度条、表格、Prompt)
- **正则引擎**：System.Text.RegularExpressions
- **进程管理**：System.Diagnostics.Process
- **文件 IO**：System.IO

### 2.2 发布配置
```bash
dotnet publish -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:TrimMode=partial \
  -p:EnableCompressionInSingleFile=true
```
- **目标体积**：≤ 15MB（含 Spectre.Console 和 .NET 运行时裁剪后）
- **运行依赖**：Windows 10 1903+（已自带 VC++ 2015-2022 运行时，但 Build Tools 需另外安装）

---

## 3. 文件与目录结构（运行时）

程序运行时会自动在 exe 同级目录创建以下结构：

```
SmartInstall.exe
├── sni-config.json          # 用户偏好配置（持久化）
├── sni-state.json           # 会话状态（安装历史、重试计数）
└── sni-logs/
    └── 2026-08-24.log       # 按日期分割的文本日志
```

### 3.1 sni-config.json 规范
```json
{
  "registry": "https://registry.npmmirror.com",
  "allowScriptsWhitelist": [
    "node-pty", "koffi", "sharp", "bcrypt",
    "better-sqlite3", "sqlite3", "canvas",
    "node-sass", "sass", "esbuild", "electron",
    "@deepseek-ai/dsh-subprocess-local"
  ],
  "autoInstallBuildTools": true,
  "npmCacheDir": "./sni-cache",
  "maxRetryCount": 3,
  "preferGlobalInstall": true,
  "subCommandAutoRun": false
}
```

### 3.2 配置加载优先级
1. 程序内置默认值
2. 覆盖为 `sni-config.json` 中的值（如果存在）
3. 运行时通过 `/config` 命令修改后回写

---

## 4. 核心模块设计

### 4.1 模块总览

```
Program.cs              # 入口：初始化 → 打印 Banner → 启动 REPL 循环
├── Models/
│   ├── ParsedCommand       # 解析后的命令对象
│   ├── EnvStatus           # 环境扫描结果
│   └── InstallResult       # 安装结果
├── Services/
│   ├── CommandParser       # 命令解析服务
│   ├── EnvScanner          # 环境扫描服务
│   ├── ConfigManager       # .npmrc 读写与幂等修改
│   ├── InstallExecutor     # 子进程执行与日志捕获
│   └── ErrorHealer         # 错误模式匹配与自愈逻辑
└── Utils/
    ├── Logger              # 文件日志 + 控制台彩色输出
    └── PathHelper          # 跨路径处理（含空格、中文路径）
```

### 4.2 数据模型

#### ParsedCommand
```csharp
public class ParsedCommand
{
    public string RawInput { get; set; }           // 原始输入
    public string PackageName { get; set; }        // 包名，如 "@deepseek-ai/dsh"
    public string? Version { get; set; }           // 版本号，如 "0.1.1-rc.2"，null 表示 latest
    public string? SubCommand { get; set; }        // 子命令，如 "web"
    public bool IsScoped { get; set; }             // 是否带 @scope/
    public string? Scope { get; set; }             // 作用域，如 "deepseek-ai"
    public string BinaryName { get; set; }         // 推断的 CLI 二进制名，如 "dsh"
    public InstallSource Source { get; set; }      // Npx / NpmInstall / RawPackageName
}

public enum InstallSource
{
    Npx,            // 用户输入 npx ...
    NpmInstall,     // 用户输入 npm install ...
    RawPackageName  // 用户只输入了包名
}
```

#### EnvStatus
```csharp
public class EnvStatus
{
    public bool NodeInstalled { get; set; }
    public string? NodeVersion { get; set; }
    public string? NpmVersion { get; set; }
    public string CurrentRegistry { get; set; } = "https://registry.npmjs.org";
    public string? CurrentAllowScripts { get; set; }  // 逗号分隔字符串或 "true"
    public bool HasPython { get; set; }
    public string? PythonPath { get; set; }
    public bool HasBuildTools { get; set; }
    public string? NpmrcPath { get; set; }
    public bool IsRegistryMirror { get; set; }
}
```

---

## 5. 命令解析器（CommandParser）

### 5.1 支持的输入格式
程序必须能解析以下所有变体：

| 输入 | 解析结果 |
|------|---------|
| `npx @deepseek-ai/dsh web` | PackageName=`@deepseek-ai/dsh`, SubCommand=`web`, Source=Npx |
| `npx -y @deepseek-ai/dsh@0.1.1-rc.2 web --port 8080` | PackageName=`@deepseek-ai/dsh`, Version=`0.1.1-rc.2`, SubCommand=`web --port 8080` |
| `npm install -g @deepseek-ai/dsh` | PackageName=`@deepseek-ai/dsh`, SubCommand=null, Source=NpmInstall |
| `npm i -g dsh` | PackageName=`dsh`, SubCommand=null, Source=NpmInstall |
| `dsh` | PackageName=`dsh`, SubCommand=null, Source=RawPackageName |
| `@scope/pkg@1.0.0` | PackageName=`@scope/pkg`, Version=`1.0.0` |

### 5.2 解析算法

**Step 1：识别命令类型**
- 如果以 `npx` 开头 → Source=Npx
- 如果以 `npm install` 或 `npm i` 开头 → Source=NpmInstall
- 否则 → Source=RawPackageName

**Step 2：提取包标识符（核心正则）**
```csharp
// 匹配包名的正则（支持作用域、版本、子命令）
// 注意：要处理 npx 的 -y, --yes, -p, --package 等参数

private static readonly Regex PackageRegex = new Regex(
    @"^(?:npx\s+(?:-y\s+|-p\s+)?|npm\s+(?:install|i)\s+(?:-g\s+|--global\s+)?)?" +  // 前缀
    @"(@[^/\s]+/[^@\s]+|[^@\s]+)" +  // 包名（含作用域）
    @"(?:@([^\s]+))?" +  // 可选版本
    @"(?:\s+(.+))?" +  // 剩余部分作为子命令
    @"$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

**Step 3：推断二进制名**
- 有作用域的包（`@scope/name`）：二进制名通常等于 `name`
- 无作用域的包：二进制名通常等于包名
- 例外：某些包二进制名与包名不同（如 `@angular/cli` → `ng`），此阶段先按包名推断，安装后通过 `npm ls -g --depth=0` 或读取 `package.json` 的 `bin` 字段修正

**Step 4：子命令清理**
- 如果 Source=Npx，子命令是 npx 执行完安装后要传给包 CLI 的参数
- 如果 Source=NpmInstall 或 RawPackageName，子命令通常为 null
- 如果子命令以 `--` 开头，保留原样；否则作为独立参数传递

### 5.3 边界情况
- **输入只有空格或空行**：忽略，重新提示
- **输入 `exit` / `quit`**：优雅退出，保存会话状态
- **输入 `/` 开头**：转交给内部命令处理器（见第 9 节）
- **无法解析**：打印 "❌ 无法识别命令格式，请使用 npx <pkg> 或 npm install -g <pkg>"，不退出
- **包名含非法字符**：提前校验，拒绝执行（防止命令注入）

---

## 6. 环境扫描器（EnvScanner）

### 6.1 扫描项与检测方法

| 扫描项 | 检测命令/方法 | 成功标志 | 失败处理 |
|--------|--------------|---------|---------|
| Node.js | `node --version` | 返回 `vxx.x.x` | 报错：Node.js 未安装，程序终止 |
| npm | `npm --version` | 返回版本号 | 同上 |
| 当前 registry | `npm config get registry` | URL 字符串 | 默认为官方源 |
| allow-scripts | `npm config get allow-scripts` | 字符串或 null | null 表示未配置 |
| Python | `where python` 或 `where python3` | 返回路径 | 标记缺失，后续提示 |
| VC++ Build Tools | 查注册表 `HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64` | 存在 `Installed` = 1 | 标记缺失 |
| .npmrc 路径 | `npm config get userconfig` | 返回路径 | 默认 `%USERPROFILE%\.npmrc` |

### 6.2 注册表检测细节（C#）
```csharp
using Microsoft.Win32;

bool HasBuildTools()
{
    // 检查 VS 2022 Build Tools
    var key = Registry.LocalMachine.OpenSubKey(
        @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
    if (key != null)
    {
        var installed = key.GetValue("Installed");
        return installed != null && (int)installed == 1;
    }
    // 再检查 VS 2019 (15.0)
    key = Registry.LocalMachine.OpenSubKey(
        @"SOFTWARE\Microsoft\VisualStudio\15.0\VC\Runtimes\x64");
    return key != null && (int)key.GetValue("Installed") == 1;
}
```

### 6.3 扫描结果展示
启动时或执行 `/scan` 时，以表格形式展示：
```
[系统扫描结果]
┌─────────────────────┬──────────────────────────────┬────────┐
│ 项目                │ 状态                         │ 结果   │
├─────────────────────┼──────────────────────────────┼────────┤
│ Node.js             │ v20.11.0                     │ ✓      │
│ npm                 │ v10.5.0                      │ ✓      │
│ Registry            │ https://registry.npmmirror.com │ ✓    │
│ Allow-scripts       │ 已配置 3 项白名单             │ ✓      │
│ Python              │ C:\Python311\python.exe      │ ✓      │
│ VC++ Build Tools    │ 未检测到                     │ ⚠      │
└─────────────────────┴──────────────────────────────┴────────┘
```

---

## 7. 配置管理器（ConfigManager）

### 7.1 核心职责
- 读取/解析 `.npmrc` 文件
- **幂等地**修改配置项（追加不重复、修改不破坏、删除可回滚）
- 修改前自动备份 `.npmrc` 到 `.npmrc.sni-backup-YYYYMMDD-HHMMSS`

### 7.2 .npmrc 解析规则
.npmrc 格式为 `key=value`，每行一个，支持注释 `#`。

```csharp
public class NpmrcConfig
{
    public Dictionary<string, string> Entries { get; set; } = new();
    public List<string> Comments { get; set; } = new();

    public void Load(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed.StartsWith("#")) { Comments.Add(trimmed); continue; }
            var idx = trimmed.IndexOf('=');
            if (idx > 0)
            {
                var key = trimmed.Substring(0, idx).Trim();
                var val = trimmed.Substring(idx + 1).Trim();
                Entries[key] = val;
            }
        }
    }

    public void Save(string path)
    {
        var lines = new List<string>();
        lines.AddRange(Comments);
        foreach (var kv in Entries)
            lines.Add($"{kv.Key}={kv.Value}");
        File.WriteAllLines(path, lines);
    }
}
```

### 7.3 关键操作的幂等性

#### 操作 A：切换 registry
```csharp
void SetRegistry(string url)
{
    var npmrc = LoadNpmrc();
    var old = npmrc.Entries.GetValueOrDefault("registry");
    if (old == url) return;  // 已相同，跳过

    BackupNpmrc();
    npmrc.Entries["registry"] = url;
    npmrc.Save();
    Logger.Info($"registry: {old ?? "(未设置)"} → {url}");
}
```

#### 操作 B：追加 allow-scripts 白名单（最复杂）
```csharp
void AppendAllowScripts(List<string> packages)
{
    var npmrc = LoadNpmrc();
    var current = npmrc.Entries.GetValueOrDefault("allow-scripts");

    HashSet<string> whitelist;
    if (current == null || current.Trim() == "")
        whitelist = new HashSet<string>();
    else if (current.Trim().ToLower() == "true")
        return;  // 已完全信任，无需追加
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

    if (!changed) return;

    BackupNpmrc();
    npmrc.Entries["allow-scripts"] = string.Join(",", whitelist);
    npmrc.Save();
    Logger.Info($"allow-scripts 白名单已追加: {string.Join(", ", packages)}");
}
```

**重要边界**：
- 如果当前 `allow-scripts=true`（完全信任），不要改成白名单模式，保持 true
- 追加时保持原有白名单条目顺序，新条目追加到末尾
- 包名去重（大小写敏感，因为 npm 包名大小写敏感）

#### 操作 C：设置 npm 缓存目录（可选，用于便携）
```csharp
void SetCacheDir(string relativePath)
{
    var absPath = Path.GetFullPath(relativePath, AppContext.BaseDirectory);
    Directory.CreateDirectory(absPath);
    var npmrc = LoadNpmrc();
    npmrc.Entries["cache"] = absPath.Replace("\\", "/");
    npmrc.Save();
}
```

---

## 8. 安装执行器（InstallExecutor）

### 8.1 命令构造
根据 ParsedCommand 构造最终 npm 命令：

```csharp
string BuildInstallCommand(ParsedCommand cmd)
{
    var sb = new StringBuilder("npm install -g ");

    if (cmd.IsScoped)
        sb.Append($"@{cmd.Scope}/{cmd.PackageName.Split('/').Last()}");
    else
        sb.Append(cmd.PackageName);

    if (cmd.Version != null)
        sb.Append($"@{cmd.Version}");

    // 如果 registry 已配置，这里不需要再加 --registry，因为 .npmrc 已设置
    // 但如果用户通过 /config 指定了临时源，可以加上

    return sb.ToString();
}
```

### 8.2 子进程管理
使用 `ProcessStartInfo` 启动 npm，必须：
- `RedirectStandardOutput = true`
- `RedirectStandardError = true`
- `UseShellExecute = false`
- `CreateNoWindow = true`（我们自己在 Console 里渲染）
- `WorkingDirectory` 设为 exe 同级目录
- 环境变量继承父进程，但可注入 `NODE_OPTIONS` 等

```csharp
var psi = new ProcessStartInfo("npm", args)
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true,
    WorkingDirectory = AppContext.BaseDirectory
};

using var proc = new Process { StartInfo = psi };

// 实时捕获输出
proc.OutputDataReceived += (s, e) => { if (e.Data != null) OnOutput(e.Data); };
proc.ErrorDataReceived += (s, e) => { if (e.Data != null) OnError(e.Data); };

proc.Start();
proc.BeginOutputReadLine();
proc.BeginErrorReadLine();
proc.WaitForExit();
```

### 8.3 进度显示
npm 本身不提供结构化进度。通过 stderr/stdout 的文本特征推断：

| 输出特征 | 进度阶段 | UI 展示 |
|---------|---------|---------|
| `idealTree:lib: sill idealTree buildDeps` | 解析依赖树 | `[░░░░░░░░░░░░░░░░░░] 解析依赖...` |
| `reify:xxx: timing reifyNode:xxx` | 下载包 | `[████░░░░░░░░░░░░░░] 下载中...` |
| `run-script: postinstall` | 执行安装脚本 | `[████████████░░░░░░] 编译原生模块...` |
| `added 452 packages in 1m` | 完成 | `[████████████████████] 完成` |

使用 Spectre.Console 的 `AnsiConsole.Progress()` 展示进度条，阶段切换时更新描述文字。

---

## 9. 错误自愈引擎（ErrorHealer）

这是整个系统**最关键**的模块。它通过正则模式匹配 stderr 输出，触发修复动作。

### 9.1 错误模式匹配表

| 优先级 | 匹配正则/关键字 | 错误类型 | 修复动作 | 是否需要交互 |
|--------|----------------|---------|---------|------------|
| P0 | `npm warn allow-scripts` | allow-scripts 阻止 | 提取包名 → 追加白名单 → 重试 | 否（全自动） |
| P0 | `npm ERR! code ECONNRESET` | 网络中断 | 切换备用镜像 → 重试 | 否 |
| P0 | `npm ERR! code ETIMEDOUT` | 网络超时 | 同上 | 否 |
| P1 | `gyp ERR! find VS` | 缺少 VC++ Build Tools | 提示用户，提供 [自动安装/手动教程/跳过] 选项 | 是 |
| P1 | `gyp ERR! find Python` | 缺少 Python | 提示用户下载 Python | 是 |
| P1 | `MSB8003` | 项目文件格式不兼容 | 提示 Build Tools 版本过旧 | 是 |
| P2 | `npm ERR! code EACCES` / `EPERM` | 权限不足 | 提示以管理员身份重新运行 | 是 |
| P2 | `npm ERR! code ENOENT` | 包不存在 | 提示检查包名拼写 | 是 |
| P2 | `npm ERR! code ETARGET` | 版本不存在 | 提示可用版本列表 | 是 |
| P3 | `npm WARN deprecated` | 包已弃用 | 仅警告，不阻断 | 否 |
| P3 | `funding` 信息 | 捐赠提示 | 忽略 | 否 |

### 9.2 状态机设计
```
[开始安装]
    │
    ▼
[启动 npm 子进程] ──→ [实时流处理]
    │                      │
    │                      ▼
    │              [输出缓冲区累积]
    │                      │
    │                      ▼
    │         [正则模式匹配？]
    │              │
    │      ┌───────┴───────┐
    │      ▼               ▼
    │   [匹配到]        [未匹配]
    │      │               │
    │      ▼               │
    │ [判断优先级]         │
    │      │               │
    │   ┌──┴──┐            │
    │   ▼     ▼            │
    │ 自动   需交互        │
    │   │     │            │
    │   ▼     ▼            │
    │ 立即   暂停进程      │
    │ 修复   打印选项      │
    │   │    等待输入     │
    │   │       │         │
    │   └──┬────┘         │
    │      ▼               │
    │ [应用修复]           │
    │      │               │
    │      ▼               │
    │ [重试计数+1]         │
    │      │               │
    │   ┌──┴──┐            │
    │   ▼     ▼            │
    │ ≤max   >max          │
    │   │     │            │
    │   ▼     ▼            │
    │ 重试   回滚配置      │
    │   │   报错退出       │
    │   │                  │
    │   └──────→ [继续安装] ←┘
    │                  │
    │                  ▼
    │            [进程退出？]
    │                  │
    │           ┌──────┴──────┐
    │           ▼              ▼
    │        成功码0        失败码≠0
    │           │              │
    │           ▼              ▼
    │      [安装成功]    [进入错误匹配]
    │           │              │
    │           ▼              ▼
    │    [询问子命令]    [已处理？]
    │                        │
    │                   ┌────┴────┐
    │                   ▼         ▼
    │                  是        否
    │                   │         │
    │                   ▼         ▼
    │                [重试]   [未知错误]
    │                           打印日志
    │                           建议用户
    │                           提交 Issue
```

### 9.3 allow-scripts 自动修复详细逻辑
当 stderr 中出现：
```
npm warn deprecated node-domexception@1.0.0: Use your platform's native DOMException instead

added 452 packages in 1m

65 packages are looking for funding
  run `npm fund` for details
npm warn allowScripts 5 packages have install scripts not yet covered by allowScripts:
npm warn allowScripts   @deepseek-ai/dsh-subprocess-local@0.1.1-rc.2 (postinstall: node scripts/ensure-spawn-helper.mjs)
npm warn allowScripts   koffi@3.1.6 (install: node ./cnoke.cjs -P . -D src/koffi --prebuild --release)
npm warn allowScripts   node-pty@1.2.0-beta.15 (install: node scripts/prebuild.js || node-gyp rebuild; postinstall: node scripts/post-install.js)
npm warn allowScripts   @google/genai@1.52.0 (preinstall: echo 'preinstall: no-op')
npm warn allowScripts   protobufjs@7.6.5 (postinstall: node scripts/postinstall)
npm warn allowScripts
npm warn allowScripts Run `npm install -g --allow-scripts=@deepseek-ai/dsh-subprocess-local,koffi,node-pty,@google/genai,protobufjs` to allow these scripts once, or `npm config set allow-scripts=@deepseek-ai/dsh-subprocess-local,koffi,node-pty,@google/genai,protobufjs --location=user` to allow them for all global installs.
```

修复流程：
1. 正则提取所有被阻止的包名：`npm warn allowScripts\s+([@\w-]+)@`
2. 收集到列表：`["@deepseek-ai/dsh-subprocess-local", "koffi"]`
3. 调用 `ConfigManager.AppendAllowScripts(list)`
4. 终止当前 npm 进程（`proc.Kill()`）
5. 打印："检测到 allow-scripts 阻止，已自动追加白名单，正在重试..."
6. 重试计数 +1，重新构造命令并启动

### 9.4 Build Tools 安装交互
当检测到 `gyp ERR! find VS`：
```
⚠ 检测到缺少 Visual C++ 构建工具，以下包需要编译原生模块：
   - node-pty
   - koffi

[1] 自动静默安装 Build Tools（通过 winget，约 5-10 分钟，推荐）
[2] 跳过，尝试继续（很可能失败）
[3] 显示手动安装教程链接

请选择 [1/2/3]: 
```

选择 1 后执行：
```csharp
Process.Start(new ProcessStartInfo("winget", 
    "install Microsoft.VisualStudio.2022.BuildTools --silent --override \"--wait --quiet --add ProductLang En-us --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\"")
{
    UseShellExecute = true,  // winget 需要 ShellExecute
    Verb = "runas"           // 请求管理员权限
});
```

安装完成后，**必须提示用户关闭并重新打开 SmartInstall.exe**（因为环境变量需要新会话才能生效）。或者程序自己重新扫描环境变量并刷新 `PATH`。

---

## 10. REPL 交互设计

### 10.1 启动流程
```
╔══════════════════════════════════════════════════╗
║  Smart NPM Installer (SNI) v1.0                 ║
║  粘贴 npx/npm 命令，自动完成环境修复与全局安装   ║
╚══════════════════════════════════════════════════╝

[系统扫描] Node.js v20.11.0 | npm v10.5.0 | Registry: npmmirror ✓
[配置状态] allow-scripts: 3 项白名单 | Build Tools: 未安装 ⚠

提示: 输入 /help 查看所有命令，输入 exit 退出

smart-install> 
```

### 10.2 内部命令（以 `/` 开头）

| 命令 | 功能 |
|------|------|
| `/help` 或 `/?` | 显示帮助信息 |
| `/scan` | 重新执行环境扫描 |
| `/config` | 以表格形式展示当前 sni-config.json 内容 |
| `/config set <key> <value>` | 修改配置项，如 `/config set registry https://registry.npmmirror.com` |
| `/fix env` | 手动触发环境修复（registry + allow-scripts） |
| `/fix buildtools` | 手动触发 Build Tools 安装 |
| `/history` | 显示本次会话的安装历史 |
| `/clear` 或 `cls` | 清屏 |
| `/backup` | 手动备份当前 .npmrc |
| `/restore` | 从最近的备份恢复 .npmrc |
| `exit` 或 `quit` | 保存状态并退出 |

### 10.3 安装完成后的交互
```
▶ 安装完成 ✓  耗时 42s  |  新增 452 个包

检测到子命令 "web"，是否立即执行？ [Y/n/always/never] 
```

- `Y`（默认，直接回车）：执行 `dsh web`
- `n`：不执行，返回提示符
- `always`：记住选择，以后遇到此包的此子命令自动执行（写入 sni-config.json 的 `autoRunSubCommands`）
- `never`：记住选择，以后不再询问此包的此子命令

子命令执行时，**不要阻塞 REPL**。即：子进程启动后，REPL 把控制权交给子命令的 stdin/stdout（类似 `cmd /c dsh web` 的交互体验），子命令退出后自动回到 `smart-install>` 提示符。

---

## 11. 原生模块预判（Pre-flight Analysis）

在安装前，通过 `npm view` 预判依赖树中是否包含已知原生模块包，提前进入准备状态。

```csharp
async Task<List<string>> PredictNativeModules(string packageName)
{
    var nativePackages = new HashSet<string>
    {
        "node-pty", "koffi", "sharp", "bcrypt", "better-sqlite3",
        "sqlite3", "canvas", "node-sass", "sass", "esbuild",
        "electron", "zeromq", "usb", "serialport", "ffi-napi",
        "ref-napi", "cpu-features", "tree-sitter"
    };

    var result = new List<string>();

    // 执行 npm view 获取依赖树（深度 2 层足够）
    var proc = RunProcess("npm", $"view {packageName} dependencies --json");
    var deps = ParseJson(proc.Stdout);

    foreach (var dep in deps.Keys)
    {
        if (nativePackages.Contains(dep))
            result.Add(dep);

        // 递归一层子依赖（可选，性能考虑只查一层）
        var subProc = RunProcess("npm", $"view {dep} dependencies --json");
        var subDeps = ParseJson(subProc.Stdout);
        foreach (var subDep in subDeps.Keys)
            if (nativePackages.Contains(subDep) && !result.Contains(subDep))
                result.Add(subDep);
    }

    return result;
}
```

如果预判到原生模块，提前打印：
```
▶ 依赖分析...
   ⚠ 预判到原生模块依赖: node-pty, koffi
   将自动检查编译环境并追加 allow-scripts 白名单
```

---

## 12. 日志系统（Logger）

### 12.1 日志级别
- `DEBUG`：开发调试信息（默认不显示在控制台，只写文件）
- `INFO`：正常流程信息（绿色/白色，显示在控制台）
- `WARN`：警告（黄色，显示在控制台）
- `ERROR`：错误（红色，显示在控制台）
- `FATAL`：致命错误，程序终止（红色加粗，显示在控制台）

### 12.2 日志格式
```
[2026-08-24 17:38:12] [INFO] 系统扫描完成，Node.js v20.11.0
[2026-08-24 17:38:15] [WARN] 检测到 allow-scripts 阻止: koffi
[2026-08-24 17:38:15] [INFO] 已自动追加 allow-scripts 白名单
[2026-08-24 17:38:45] [INFO] 安装完成，耗时 42s
```

### 12.3 文件输出
- 路径：`sni-logs/YYYY-MM-DD.log`
- 按日期轮转，单文件不超过 10MB
- 程序启动时清理 30 天前的日志

---

## 13. 边界情况与异常处理

### 13.1 路径含空格
所有涉及路径的命令参数必须用 `""` 包裹：
```csharp
// 错误
$"npm install -g {packagePath}"

// 正确
$"npm install -g \"{packagePath}\""
```

### 13.2 中文用户名
Windows 用户名为中文时，`%USERPROFILE%` 含中文，.npmrc 路径也含中文。C# 的 `ProcessStartInfo` 和 `File.ReadAllText` 天然支持 Unicode，无需额外处理，但**日志文件必须以 UTF-8 无 BOM 写入**。

### 13.3 并发安装
如果用户在上一个安装还没完成时输入了新命令，应该：
- 拒绝新命令，提示："当前有安装任务正在进行，请等待完成或按 Ctrl+C 取消"
- 或者：排队执行（更复杂，MVP 阶段建议拒绝）

### 13.4 网络代理
如果用户系统配置了 HTTP 代理（`HTTP_PROXY` 环境变量），npm 会自动读取。程序不需要额外处理，但日志中应打印当前代理状态。

### 13.5 .npmrc 被其他程序锁定
某些 IDE（如 VS Code）可能正在读写 .npmrc。修改前应先尝试文件锁，失败时提示用户关闭其他程序。

### 13.6 磁盘空间不足
安装前检查目标盘（通常是 C 盘）剩余空间，如果 < 1GB，提前警告。

---

## 14. 关键算法伪代码汇总

### 14.1 主 REPL 循环
```csharp
void MainLoop()
{
    PrintBanner();
    var env = EnvScanner.Scan();
    PrintEnvTable(env);

    while (true)
    {
        Console.Write("smart-install> ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input)) continue;
        if (input == "exit" || input == "quit") { SaveState(); break; }
        if (input.StartsWith("/")) { HandleInternalCommand(input); continue; }

        var cmd = CommandParser.Parse(input);
        if (cmd == null) { Logger.Error("无法解析命令"); continue; }

        var result = InstallManager.Install(cmd, env);
        if (result.Success && cmd.SubCommand != null)
        {
            var shouldRun = PromptSubCommand(cmd);
            if (shouldRun) RunSubCommand(cmd);
        }
    }
}
```

### 14.2 安装管理器（含自愈）
```csharp
InstallResult Install(ParsedCommand cmd, EnvStatus env)
{
    // 1. 预判原生模块
    var nativeModules = PredictNativeModules(cmd.PackageName);
    if (nativeModules.Any())
    {
        ConfigManager.AppendAllowScripts(nativeModules);
        if (!env.HasBuildTools)
            Logger.Warn("缺少 Build Tools，安装可能失败");
    }

    // 2. 确保 registry
    ConfigManager.SetRegistry(Config.Registry);

    // 3. 构造并执行
    var installCmd = BuildInstallCommand(cmd);
    var retryCount = 0;

    while (retryCount < Config.MaxRetryCount)
    {
        var executor = new InstallExecutor(installCmd);
        var output = executor.RunWithCapture();

        if (executor.ExitCode == 0)
            return InstallResult.Success();

        // 4. 错误分析
        var healing = ErrorHealer.Analyze(output.Stderr);
        if (healing == null)  // 未知错误
            return InstallResult.Failure(output.Stderr);

        if (healing.NeedsInteraction)
        {
            var choice = PromptUser(healing.Message, healing.Options);
            if (choice == "abort") return InstallResult.Failure("用户中止");
            healing.Apply(choice);
        }
        else
        {
            healing.ApplyAutoFix();
        }

        retryCount++;
        Logger.Info($"第 {retryCount} 次重试...");
    }

    return InstallResult.Failure("超过最大重试次数");
}
```

---

## 15. 交付 checklist

编码完成后，请确认以下功能均已实现：

- [ ] 单文件发布，体积 ≤ 15MB
- [ ] 双击运行，无需安装
- [ ] 支持粘贴 `npx ...` / `npm install ...` / 纯包名 三种输入
- [ ] 正确解析作用域包（`@scope/name`）和版本号
- [ ] 自动切换 registry 到国内镜像
- [ ] 自动检测并追加 allow-scripts 白名单（幂等）
- [ ] 实时彩色日志输出（使用 Spectre.Console）
- [ ] 进度条展示（至少分阶段：解析/下载/编译/完成）
- [ ] 错误模式匹配：allow-scripts、网络错误、gyp 错误、权限错误
- [ ] 遇到 allow-scripts 自动修复并重试（无需用户交互）
- [ ] 遇到 gyp 错误提示用户并提供 winget 自动安装选项
- [ ] 安装完成后询问是否执行子命令
- [ ] `/help`、`/scan`、`/config`、`/history`、`exit` 内部命令
- [ ] .npmrc 修改前自动备份
- [ ] 日志文件按日期分割，保留 30 天
- [ ] 配置外置（sni-config.json），支持用户自定义
- [ ] 中文路径、含空格路径正确处理
- [ ] 程序退出时保存会话状态

---

## 16. 附录：npm 错误输出样本（用于测试正则）

### 样本 A：allow-scripts 阻止
```
npm warn deprecated node-domexception@1.0.0: Use your platform's native DOMException instead

added 452 packages in 1m

65 packages are looking for funding
  run `npm fund` for details
npm warn allowScripts 5 packages have install scripts not yet covered by allowScripts:
npm warn allowScripts   @deepseek-ai/dsh-subprocess-local@0.1.1-rc.2 (postinstall: node scripts/ensure-spawn-helper.mjs)
npm warn allowScripts   koffi@3.1.6 (install: node ./cnoke.cjs -P . -D src/koffi --prebuild --release)
npm warn allowScripts   node-pty@1.2.0-beta.15 (install: node scripts/prebuild.js || node-gyp rebuild; postinstall: node scripts/post-install.js)
npm warn allowScripts   @google/genai@1.52.0 (preinstall: echo 'preinstall: no-op')
npm warn allowScripts   protobufjs@7.6.5 (postinstall: node scripts/postinstall)
npm warn allowScripts
npm warn allowScripts Run `npm install -g --allow-scripts=@deepseek-ai/dsh-subprocess-local,koffi,node-pty,@google/genai,protobufjs` to allow these scripts once, or `npm config set allow-scripts=@deepseek-ai/dsh-subprocess-local,koffi,node-pty,@google/genai,protobufjs --location=user` to allow them for all global installs.
```

### 样本 B：gyp 错误
```
npm ERR! code 1
npm ERR! path C:\Users\13335\AppData\Roaming\npm\node_modules\@deepseek-ai\dsh\node_modules\node-pty
npm ERR! command failed
npm ERR! command C:\WINDOWS\system32\cmd.exe /d /s /c node scripts/prebuild.js || node-gyp rebuild
npm ERR! gyp info it worked if it ends with ok
npm ERR! gyp info using node-gyp@10.0.1
npm ERR! gyp info using node@20.11.0 | win32 | x64
npm ERR! gyp ERR! find VS
npm ERR! gyp ERR! find VS msvs_version not set from command line or npm config
npm ERR! gyp ERR! find VS VCINSTALLDIR not set, not running in VS Command Prompt
npm ERR! gyp ERR! find VS could not use PowerShell to find Visual Studio 2017 or newer
npm ERR! gyp ERR! find VS looking for Visual Studio 2015
npm ERR! gyp ERR! find VS - not found
npm ERR! gyp ERR! find VS not looking for VS2013 as it is only supported up to Node.js 8
npm ERR! gyp ERR! find VS
npm ERR! gyp ERR! find VS **************************************************************
npm ERR! gyp ERR! find VS You need to install the latest version of Visual Studio
npm ERR! gyp ERR! find VS including the "Desktop development with C++" workload.
npm ERR! gyp ERR! find VS For more information consult the documentation at:
npm ERR! gyp ERR! find VS https://github.com/nodejs/node-gyp#on-windows
npm ERR! gyp ERR! find VS **************************************************************
```

---

**文档结束。请基于此规格书进行编码实现。**

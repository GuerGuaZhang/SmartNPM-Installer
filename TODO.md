# Smart NPM Installer (SNI) 待办事项

## 📋 当前待办

### 1. 项目整理
- [x] 删除根目录下的空目录 (Models, Services, Utils)
- [x] 检查并清理 bin/obj 构建产物
- [x] 验证 .gitignore 配置是否正确

### 2. 代码质量
- [x] 添加单元测试项目 (38个测试用例)
- [ ] 实现代码覆盖率检查
- [ ] 添加静态代码分析

### 3. 功能完善
- [ ] 实现错误自愈引擎的扩展模式匹配
- [ ] 添加更多原生模块的白名单
- [ ] 实现配置导入/导出功能
- [ ] 添加多语言支持

### 4. 文档完善
- [x] 更新技术规格书，添加最新功能说明
- [ ] 编写详细的 API 文档
- [ ] 添加贡献指南 (CONTRIBUTING.md)

### 5. 发布准备
- [x] 创建 GitHub Release 流程
- [ ] 添加自动构建工作流 (GitHub Actions)
- [ ] 准备版本发布说明

## 🐛 已知问题

### 1. 功能问题
- 无

### 2. 性能问题
- 无

### 3. 兼容性问题
- 仅支持 Windows 10/11 x64

## 📝 开发笔记

### 项目结构说明
```
SmartInstaller/
├── SmartNPM_Installer/           # 主解决方案目录
│   ├── SmartNPM_Installer/       # 项目源代码
│   │   ├── Models/               # 数据模型
│   │   ├── Services/             # 核心服务
│   │   ├── Utils/                # 工具类
│   │   └── Program.cs            # 入口文件
│   └── SmartNPM_Installer.csproj # 项目文件
├── SmartNPM_Installer.Tests/     # 单元测试项目
│   ├── CommandParserTests.cs
│   ├── EnvScannerTests.cs
│   ├── ErrorHealerTests.cs
│   ├── ConfigManagerTests.cs
│   ├── LoggerTests.cs
│   └── SmartNPM_Installer.Tests.csproj
├── SmartNPM_Installer.sln        # 解决方案文件
├── README.md                     # 项目文档
├── RELEASE.md                    # 发布文档
├── TODO.md                       # 待办事项
└── SmartNPM_Installer_技术规格书.md # 技术规格书
```

### 构建命令
```bash
# 开发构建
dotnet build

# 发布构建
dotnet publish -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=true `
  -p:TrimMode=partial `
  -p:EnableCompressionInSingleFile=true
```

## 🎯 优先级说明

- **高**: 必须完成的功能或修复
- **中**: 建议完成的功能或优化
- **低**: 可选的功能或改进

---
*最后更新: 2026-08-27*

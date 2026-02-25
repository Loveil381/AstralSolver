# AstralSolver 构建指引 (BUILDING.md)

## 前置要求
为了成功构建 AstralSolver 插件，您的开发环境必须满足以下要求：

- **.NET SDK**: [10.0.101+](https://dotnet.microsoft.com/download/dotnet/10.0)
- **IDE**: Visual Studio 2026 (预览版) 或 Rider 2025.3+
- **XIVLauncher / Dalamud**: 必须已安装 Dalamud 环境且至少启动过一次游戏以确保必要的运行库已解压。

## 构建步骤

1. **克隆仓库**:
   ```bash
   git clone https://github.com/AstralSolver/AstralSolver.git
   cd AstralSolver
   ```

2. **还原依赖**:
   ```bash
   dotnet restore AstralSolver.sln
   ```

3. **编译项目**:
   ```bash
   dotnet build AstralSolver.sln
   ```
   *注意：如果遇到关于 `WindowsBase` 的编译警告 (MSB3277)，这是 Dalamud v14 SDK 的已知问题，通常可以忽略。*

## 开发与调试

### 添加开发插件路径
1. 启动游戏并进入 Dalamud。
2. 打开插件设置 (`/xlsettings`)。
3. 导航到 **"实验性功能 (Experimental)"** 选项卡。
4. 在 **"测试插件目录 (Dev Plugin Locations)"** 中，添加本项目编译输出目录的绝对路径：
   `C:\您的路径\AstralSolver\AstralSolver\bin\Debug`
5. 点击添加并保存，插件应该会自动加载。

### 测试
运行单元测试：
```bash
dotnet test
```

## 贡献指引
- 遵循 `.agent/rules/` 中的命名规范与架构设计。
- 所有 C# 代码变更必须附带中文 XML 注释。
- 热路径计算遵循性能限制（单帧 < 5ms）。

# AstralSolver - 构建指南

## 前置环境
1. **.NET 10 SDK**：请确保已安装 .NET 10
2. **XIVLauncher** 及 **Dalamud（API Level 14）** 开发环境

## 获取代码
```bash
git clone https://github.com/AstralSolver/AstralSolver.git
cd AstralSolver
```

## 构建项目
```bash
dotnet build AstralSolver.sln
```
构建产物通常位于 `AstralSolver/bin/Debug/net10.0-windows/` 目录下。

## 运行测试
```bash
dotnet test
```

## XIVLauncher 本地调试
1. 打开游戏，输入 `/xlsettings` 打开 Dalamud 设置。
2. 进入 **Experimental** 标签页。
3. 找到 **Dev Plugin Folders**（开发插件目录），添加你在 `dotnet build` 后生成的文件夹路径（例如 `C:\projects\AstralSolver\AstralSolver\bin\Debug\net10.0-windows`）。
4. 保存即可在 Dalamud 的已安装插件中看到 AstralSolver 进行热重载调试（DevTools）。

## 常见构建问题 FAQ

**Q: 构建时出现 WindowsBase 相关的警告?**
A: 这是正常的依赖传递警告。因为我们目标框架为 `net10.0-windows`，引用的部分外部程序库偶尔会携带 WPF 兼容性相关的提示。不影响最终插件工作，可以放心忽略。

**Q: 找不到 Dalamud.NET.Sdk?**
A: 请确保本机不仅装有 XIVLauncher，还在设置中点选了在开发模式下获取 SDK 或者游戏已经加载过至少一次正确的 Dalamud 版本。

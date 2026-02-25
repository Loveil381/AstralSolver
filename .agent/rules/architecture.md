# 项目架构设计 (Architecture)

## 1. 目录结构
项目强制采用以下分层目录结构：

### 主插件项目 (`AstralSolver/`)
- `Plugin.cs` — 插件主入口点
- `Configuration.cs` — 用户配置文件
- `Core/` — 核心引擎
  - `BattleState.cs`
  - `DecisionEngine.cs`
  - `StateTracker.cs`
  - `ActionQueue.cs`
- `Jobs/` — 职业模块
  - `IJobModule.cs` (接口)
  - `BaseJobModule.cs` (基类)
  - `Healer/AstrologianModule.cs` (占星术士)
- `Navigator/` — 导航UI
  - `NavigatorRenderer.cs`
  - `DualRailTimeline.cs`
  - `ReasonEngine.cs`
  - `PerformanceScorer.cs`
- `Localization/` — 多语言
  - `Loc.cs`
  - `zh_CN.json`
  - `ja_JP.json`
  - `en_US.json`
- `UI/` — 通用UI
  - `MainWindow.cs`
  - `OverlayWindow.cs`
  - `Widgets/`
- `Utils/` — 工具
  - `Constants.cs`
  - `GameDataHelper.cs`
  - `Logger.cs`

### 测试项目 (`AstralSolver.Tests/`)
- 基于 xUnit 框架，包含测试。

## 2. 架构原则
- **解耦通信**: 要求模块间用事件/委托解耦通信。
- **依赖注入**: 必须使用 Dalamud 依赖注入管理服务。
- **禁止单例**: 绝对禁止使用静态单例模式，通过传入依赖或DI使用功能。

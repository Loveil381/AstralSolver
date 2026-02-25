# AstralSolver - AI驱动的FFXIV智能战斗辁E��
*AI-powered intelligent combat assistant for Final Fantasy XIV.*

**当前版本**: v0.4.0-alpha (见E�E引擎桁E��)

**最终目栁E*: 三层混吁EI决策引擎 (见E�E + ML推琁E+ LLM战略)。详见E[docs/VISION.md](docs/VISION.md)

## 功�E特色
智能决策引擎: 占星术士三线并行�E筁E治疁E输�E+发牌)
双轨时间轴Navigator: GCD和oGCD刁E��显示, 每个动作附带琁E��
4档显示模式适应不同玩家水平
训绁E��弁E 实时证E�E帮助提升操佁E
多语言: 中斁E日斁E��生支持E

## 支持职丁E
当前版本: 占星术士 (Astrologian) 
计划中: 白魔法币E 学老E 贤老E 以及DPS/坦克职丁E

## 截图
*(Coming soon)*

## 安裁E��況E
### 前置要汁E
- FFXIV 已安裁E
- XIVLauncher 已安裁E��可正常启动游戁E
- Dalamud 已启用 (XIVLauncher默认启用)

### 安裁E��骤
1. 打开游戁E 持EEscape 打开系统菜十E
2. 选择 **Dalamud Settings**
3. 进�E **Experimental** 栁E��
4. 在 Custom Plugin Repositories 中添加: `https://raw.githubusercontent.com/Loveil381/AstralSolver/main/pluginmaster.json`
5. 保存并关闭设置
6. 打开 Plugin Installer (输�E `/xlplugins`)
7. 搜索 **AstralSolver** 并安裁E

### 首次使用
1. 输�E `/astral` 打开设置面板
2. 选择运行模弁E推荐新手�E用导航器模弁E
3. 选择语言
4. 进�E战斗即可看到Navigator时间轴

### 命令
- `/astral`: 打开/关闭设置面板
- `/astraltoggle`: 快速启用/禁用插件

## 配置说昁E
设置面板主要包含5个配置栁E��页。总览页用于快速谁E��运行模式与基本启用状态E��导航器页能详绁E��定双轨时间轴吁E�E素皁E��示、大小与位置�E�占星页专管该职业的治疗�E值及发牌对齐策略等智能行为�E�语言页提供翻译�E换并支持实时功�E颁E��；�E于页展示版本并提供相关简介、E

## 开叁E
### 极E��环墁E
- .NET 10 SDK
- Visual Studio 2026 戁EJetBrains Rider 2025.3+
- Dalamud开发环墁E(参老EBUILDING.md)

### 极E��
```bash
dotnet build AstralSolver.sln
```

### 测证E
```bash
dotnet test
```

## 项目结构
项目核忁E��辑�E为数据釁E��决筁ECore)与职业定制逻辁EJobs)�E�界面和见E��引导交由双轨时间轴导航渲柁ENavigator)与基础图形界面屁EUI)�E�同时拥有健壮皁E�E置工具(Utils)、文案统一管琁ELocalization)机制及要E��周全皁E��层驱动测证ETests)、E

## 技术栁E
.NET 10, C# 14, Dalamud SDK v14, ImGui, Lumina, FFXIVClientStructs

## 许可证E
MIT License

## 致谢
- Dalamud 开发团阁E
- RotationSolver Reborn 项目(参老E
- The Balance FFXIV 攻略社区
- FFXIVClientStructs 项目



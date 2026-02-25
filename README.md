# AstralSolver - AI驱动的FFXIV智能战斗辅助
*AI-powered intelligent combat assistant for Final Fantasy XIV.*

## 功能特色
智能决策引擎: 占星术士三线并行决策(治疗+输出+发牌)
双轨时间轴Navigator: GCD和oGCD分离显示, 每个动作附带理由
4档显示模式适应不同玩家水平
训练模式: 实时评分帮助提升操作
多语言: 中文/日文原生支持

## 支持职业
当前版本: 占星术士 (Astrologian) 
计划中: 白魔法师, 学者, 贤者, 以及DPS/坦克职业

## 截图
*(Coming soon)*

## 安装方法
### 前置要求
- FFXIV 已安装
- XIVLauncher 已安装并可正常启动游戏
- Dalamud 已启用 (XIVLauncher默认启用)

### 安装步骤
1. 打开游戏, 按 Escape 打开系统菜单
2. 选择 **Dalamud Settings**
3. 进入 **Experimental** 标签
4. 在 Custom Plugin Repositories 中添加: `https://raw.githubusercontent.com/AstralSolver/AstralSolver/main/pluginmaster.json`
5. 保存并关闭设置
6. 打开 Plugin Installer (输入 `/xlplugins`)
7. 搜索 **AstralSolver** 并安装

### 首次使用
1. 输入 `/astral` 打开设置面板
2. 选择运行模式(推荐新手先用导航器模式)
3. 选择语言
4. 进入战斗即可看到Navigator时间轴

### 命令
- `/astral`: 打开/关闭设置面板
- `/astraltoggle`: 快速启用/禁用插件

## 配置说明
设置面板主要包含5个配置标签页。总览页用于快速调整运行模式与基本启用状态；导航器页能详细设定双轨时间轴各元素的显示、大小与位置；占星页专管该职业的治疗阈值及发牌对齐策略等智能行为；语言页提供翻译切换并支持实时功能预览；关于页展示版本并提供相关简介。

## 开发
### 构建环境
- .NET 10 SDK
- Visual Studio 2026 或 JetBrains Rider 2025.3+
- Dalamud开发环境 (参考 BUILDING.md)

### 构建
```bash
dotnet build AstralSolver.sln
```

### 测试
```bash
dotnet test
```

## 项目结构
项目核心逻辑分为数据采集决策(Core)与职业定制逻辑(Jobs)，界面和视觉引导交由双轨时间轴导航渲染(Navigator)与基础图形界面层(UI)，同时拥有健壮的配置工具(Utils)、文案统一管理(Localization)机制及覆盖周全的底层驱动测试(Tests)。

## 技术栈
.NET 10, C# 14, Dalamud SDK v14, ImGui, Lumina, FFXIVClientStructs

## 许可证
MIT License

## 致谢
- Dalamud 开发团队
- RotationSolver Reborn 项目(参考)
- The Balance FFXIV 攻略社区
- FFXIVClientStructs 项目

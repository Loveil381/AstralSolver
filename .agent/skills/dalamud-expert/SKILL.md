---
name: dalamud-expert
description: Dalamud插件开发专家。当涉及FFXIV游戏数据读取、ImGui界面绘制、Dalamud API使用时激活。
---

# Dalamud 专家知识库

## 1. Dalamud 核心服务列表
可用的核心能力与接口：
- `IPartyList`: 小队成员状态获取
- `IClientState`: 本地玩家及登录状态
- `ICondition`: 游戏状态检测 (如战斗中)
- `IFramework`: 每帧更新钩子 (Framework.Update)
- `IPluginLog`: 用于安全打印日志信息

## 2. ImGui 绘制原则
- **Begin/End 配对**: 使用 `ImGui.Begin()` 成功返回时必须调用配套的 `ImGui.End()`。
- **覆盖层 Flags**: UI覆层不可阻挡用户操作，正确设置如 `NoInputs`, `NoBackground`, `NoTitleBar` 等 flags。
- **TextureProvider**: 所有的图标读取必须通过 TextureProvider 进行，严禁直接读取硬盘文件。

## 3. FFXIVClientStructs
- 深刻理解 `ActionManager` 用法，负责精准评估技能状态、CD、GCD等。

## 4. 注意事项
- 绝不在**非主线程**直接访问游戏数据，必须同步回主循环。
- `Hook` 申请的内存与回调函数必须在 `Dispose` 内完美释放，防止内存泄漏或崩溃。
- 用 `Signature` 扫描替代硬编码地址，以增强应对游戏端更新的能力。

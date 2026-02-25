# AstralSolver (星灵求解器)

![AstralSolver](https://img.shields.io/badge/Dalamud-Plugin-blue)
![C#](https://img.shields.io/badge/Language-C%23_12-green)
![.NET](https://img.shields.io/badge/Framework-.NET_8-purple)

**AstralSolver** 是一款专为 **最终幻想14 (FFXIV)** 设计的 Dalamud 智能战斗辅助插件。

## 🌟 核心功能

- **Navigator 导航模式**：在画面核心区域进行无级预测与提示，展示双轨时间轴。
- **Auto 自动战斗**：无缝代管角色循环输出，严密对齐 120 秒团辅期。
- **占星术士 (AST) 专精引擎**：
  - 自动最优发牌判决 (基于近/远程与爆发期监控)
  - 高级治疗模型 (压榨流/安全流/均衡流)，优先 oGCD 处理。
- **多语言支持**：内置 `zh_CN`, `ja_JP`, `en_US`。

## 🛠️ 项目架构

严格执行无魔法数字与事件解耦设计。采用完全的 IoC / DI 策略挂载模块：
- `Core/`：状态追踪器、决策核心与包封装引擎。
- `Jobs/`：各职业智能模块 (以 AstrologianModule 为核心)。
- `Navigator/`：UI 指引与智能原因生成器。

## 📜 许可证 (License)

本项目基于 [MIT License](./LICENSE) 开源发布。

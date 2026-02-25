# AstralSolver 最终愿景 (North Star)

> 「让AI真正理解你的战斗，而不是死记循环。」

## 架构目标: 三层混合决策引擎

### Layer 1: 硬性规则层 (当前已实现)
- GCD/oGCD物理约束、技能CD、资源检查
- 确定性，0ms延迟
- 对应文件: Core/DecisionEngine.cs, Jobs/AstrologianModule.cs

### Layer 2: 轻量ML决策模型 (Phase 10+)
- 基于FFLogs 99%+玩家日志训练的ONNX小模型
- 输入: 战斗状态向量(~200维)
- 输出: 技能概率分布 + 优先级分数
- 推理延迟目标: <5ms
- 对应计划文件: Core/MLInferenceEngine.cs (未创建)

### Layer 3: LLM战略顾问 (Phase 12+)
- 战前: 分析Boss，生成战略计划
- 战中: 每30秒异步评估整体表现
- 战后: 分析日志，提供改进建议
- 非实时路径，不影响帧率
- 对应计划文件: Core/LLMAdvisor.cs (未创建)

## 核心差异化功能

### 1. 双轨时间轴 Navigator (已实现框架)
- GCD轨 + oGCD轨分离显示
- 4级显示模式: 极简/标准/专家/教学
- 对应文件: Navigator/NavigatorRenderer.cs

### 2. 理由引擎 (已实现框架)
- 每个推荐附带"为什么"的解释
- 多语言模板系统
- 对应文件: Navigator/ReasonEngine.cs

### 3. Hold Signal 等待信号 (已定义数据结构)
- 表达"现在不按，等X秒后与爆发对齐"
- 对应数据: Core/DecisionModels.cs → HoldSignal

### 4. 占星专属卡牌导航面板 (已实现框架)
- 实时手牌状态、最优发牌方案、队友爆发窗口追踪
- 对应文件: Navigator/NavigatorRenderer.cs → DrawAstrologianPanel

### 5. 教学模式实时评分 (已实现框架)
- 操作后即时评分 + 战后报告
- 对应文件: Navigator/PerformanceScorer.cs

### 6. Co-healer行为学习 (Phase 11+)
- 追踪副治疗的技能使用模式
- 预测队友是否会处理即将到来的伤害
- 避免过度治疗，最大化DPS时间

### 7. 一键安装器 (Phase 13+)
- 检测环境 → 自动配置XIVLauncher/Dalamud → 安装插件
- 支持中日英三语

## 路线图

| Phase | 内容 | 状态 |
|-------|------|------|
| 1-3 | 项目骨架搭建 | ✅ 完成 |
| 4 | StateTracker 游戏状态采集 | ✅ 完成 |
| 5 | DecisionEngine + AstrologianModule | ✅ 完成 |
| 6 | Navigator UI 双轨时间轴 | ✅ 完成 |
| 7 | 发布准备 v0.4.0-alpha | ✅ 完成 |
| 8 | Bug修复 + 7.4数据对齐 | 🔄 进行中 |
| 9 | 实战验证循环 | ⬚ 待开始 |
| 10 | ML推理层 (FFLogs训练) | ⬚ 规划中 |
| 11 | Co-healer协同 + 伤害预测 | ⬚ 规划中 |
| 12 | LLM战略顾问集成 | ⬚ 规划中 |
| 13 | 一键安装器 + 第二职业 | ⬚ 规划中 |

## 竞品定位

| 维度 | RSR | AstralSolver目标 |
|------|-----|-----------------|
| 决策方式 | if-else规则 | ML模型 + 规则护栏 + LLM战略 |
| 占星发牌 | 简单条件匹配 | 实时追踪全队爆发窗口+DPS贡献 |
| 治疗判断 | 血线阈值触发 | 伤害预测 + co-healer行为学习 |
| 新版本适应 | 人工重写规则 | 从FFLogs日志重新训练 |
| 安装 | 多步手动配置 | 一键安装器 |
| 语言 | 中/英/韩 | 原生中/日/英 |

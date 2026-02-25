using System;

namespace AstralSolver.Core;

// ═══════════════════════════════════════════════════
//  决策数据模型
//  所有决策相关的数据类型集中定义于此文件。
//  设计原则：全部使用 readonly record struct 或 sealed record，
//  最大限度减少堆分配。
// ═══════════════════════════════════════════════════

// ── 枚举 ─────────────────────────────────────────────

/// <summary>决策理由的优先级（用于 Navigator UI 排序与着色）</summary>
public enum ReasonPriority : byte
{
    /// <summary>一般信息（灰色）</summary>
    Info,
    /// <summary>重要决策（黄色）</summary>
    Important,
    /// <summary>紧急/保命（红色）</summary>
    Critical,
}

/// <summary>决策引擎工作模式</summary>
public enum DecisionMode : byte
{
    /// <summary>全自动：决策 → ActionQueue 自动释放</summary>
    Auto,
    /// <summary>导航：仅显示推荐，不自动执行</summary>
    Navigator,
    /// <summary>训练：记录玩家操作与引擎推荐的差异</summary>
    Training,
    /// <summary>禁用：引擎不运行</summary>
    Disabled,
}

// ── 核心数据结构 ────────────────────────────────────────

/// <summary>
/// GCD 技能推荐项。
/// 表示引擎推荐在下一个 GCD 窗口释放的技能。
/// </summary>
public readonly record struct GcdAction
{
    /// <summary>技能 ID（对应 ActionManager 的 ActionId）</summary>
    public uint ActionId { get; init; }
    /// <summary>技能显示名（多语言，供 Navigator UI 渲染）</summary>
    public string ActionName { get; init; }
    /// <summary>优先级（越高越优先，相同优先级按数组顺序）</summary>
    public float Priority { get; init; }
    /// <summary>目标对象 ID（0 = 自身或默认目标）</summary>
    public uint TargetObjectId { get; init; }
}

/// <summary>
/// oGCD 穿插推荐项。
/// 表示应在指定 GCD 之后的能力窗口中释放的 oGCD 技能。
/// </summary>
public readonly record struct OgcdInsert
{
    /// <summary>技能 ID</summary>
    public uint ActionId { get; init; }
    /// <summary>技能显示名</summary>
    public string ActionName { get; init; }
    /// <summary>优先级</summary>
    public float Priority { get; init; }
    /// <summary>插入位置：在第 N 个 GCD 之后释放（0 = 当前 GCD 之后）</summary>
    public int InsertAfterGcdIndex { get; init; }
    /// <summary>目标对象 ID（0 = 自身或默认目标）</summary>
    public uint TargetObjectId { get; init; }
}

/// <summary>
/// 等待信号：指示引擎建议暂停按键，等待特定时机。
/// 例：等待 0.3 秒以实现双插 oGCD 窗口对齐。
/// </summary>
public readonly record struct HoldSignal
{
    /// <summary>建议等待时长（秒）</summary>
    public float Duration { get; init; }
    /// <summary>等待原因（供日志和 UI 显示）</summary>
    public string Reason { get; init; }
}

/// <summary>
/// 决策理由条目。
/// 每个推荐技能附带一条理由，供 Navigator UI 显示"为什么这么做"。
/// </summary>
public readonly record struct ReasonEntry
{
    /// <summary>关联的技能 ID（0 = 全局理由）</summary>
    public uint ActionId { get; init; }
    /// <summary>多语言模板 Key（用于 Loc 查表）</summary>
    public string TemplateKey { get; init; }
    /// <summary>已格式化的理由文本（直接显示）</summary>
    public string FormattedText { get; init; }
    /// <summary>理由优先级</summary>
    public ReasonPriority Priority { get; init; }
}

// ── 占星专属面板 ────────────────────────────────────────

/// <summary>
/// 发牌计划项。
/// 表示引擎推荐将某张卡牌发给某个目标。
/// </summary>
public readonly record struct CardPlayPlan
{
    /// <summary>卡牌类型</summary>
    public AstCard Card { get; init; }
    /// <summary>推荐目标名称</summary>
    public string TargetName { get; init; }
    /// <summary>推荐目标职业 ID</summary>
    public byte TargetJobId { get; init; }
    /// <summary>推荐理由</summary>
    public string Reason { get; init; }
}

/// <summary>
/// 占星术士专属 UI 面板数据。
/// 由 AstrologianModule 生成，通过 JobDecision.JobSpecificPanel 传递给 Navigator。
/// </summary>
public sealed record AstrologianPanel
{
    /// <summary>当前仪表盘状态快照</summary>
    public required AstrologianState GaugeState { get; init; }
    /// <summary>发牌计划（最多 4 张，PlayI/II/III + MinorArcana）</summary>
    public required CardPlayPlan[] CardPlans { get; init; }
    /// <summary>距离下次可抽卡剩余时间（秒）</summary>
    public float NextDrawIn { get; init; }
    /// <summary>当前推荐的出牌目标名称</summary>
    public string SuggestedTarget { get; init; } = string.Empty;
}

// ── 决策输出 ────────────────────────────────────────────

/// <summary>
/// 职业模块输出的决策结果。
/// 由各职业的 IJobModule.Evaluate() 返回，
/// 再由 DecisionEngine 合并安全层覆盖后生成最终 DecisionPacket。
/// </summary>
public sealed record JobDecision
{
    /// <summary>推荐的 GCD 队列（按优先级排列，最多 5 个）</summary>
    public required GcdAction[] GcdQueue { get; init; }
    /// <summary>推荐穿插的 oGCD 列表</summary>
    public required OgcdInsert[] OgcdInserts { get; init; }
    /// <summary>等待信号（可选）</summary>
    public HoldSignal? Hold { get; init; }
    /// <summary>每个决策的理由列表</summary>
    public required ReasonEntry[] Reasons { get; init; }
    /// <summary>职业专属面板数据（如 AstrologianPanel），由各模块自行填充</summary>
    public object? JobSpecificPanel { get; init; }
    /// <summary>决策置信度（0.0~1.0，1.0=完全确信）</summary>
    public float Confidence { get; init; } = 1.0f;

    /// <summary>空决策（无任何推荐）</summary>
    public static readonly JobDecision Empty = new()
    {
        GcdQueue = Array.Empty<GcdAction>(),
        OgcdInserts = Array.Empty<OgcdInsert>(),
        Reasons = Array.Empty<ReasonEntry>(),
    };
}

/// <summary>
/// 最终决策包：引擎输出给 Navigator UI 和 ActionQueue 的完整决策。
/// 包含职业模块决策 + 安全层覆盖 + 运行模式信息。
/// </summary>
public sealed record DecisionPacket
{
    /// <summary>推荐的 GCD 队列</summary>
    public required GcdAction[] GcdQueue { get; init; }
    /// <summary>推荐穿插的 oGCD 列表</summary>
    public required OgcdInsert[] OgcdInserts { get; init; }
    /// <summary>等待信号</summary>
    public HoldSignal? Hold { get; init; }
    /// <summary>决策理由列表</summary>
    public required ReasonEntry[] Reasons { get; init; }
    /// <summary>职业专属面板数据</summary>
    public object? JobPanel { get; init; }
    /// <summary>决策置信度</summary>
    public float Confidence { get; init; }
    /// <summary>当前决策模式</summary>
    public DecisionMode Mode { get; init; }

    /// <summary>空决策包（引擎未就绪时使用）</summary>
    public static readonly DecisionPacket Empty = new()
    {
        GcdQueue = Array.Empty<GcdAction>(),
        OgcdInserts = Array.Empty<OgcdInsert>(),
        Reasons = Array.Empty<ReasonEntry>(),
        Mode = DecisionMode.Disabled,
    };
}

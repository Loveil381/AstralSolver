using System;
using AstralSolver.Utils;

namespace AstralSolver.Core;

// ═══════════════════════════════════════════════════
//  基础数据类型
// ═══════════════════════════════════════════════════

/// <summary>
/// 状态效果（Buff / Debuff）信息快照
/// </summary>
public readonly record struct StatusEffect
{
    /// <summary>状态 ID</summary>
    public ushort StatusId { get; init; }
    /// <summary>剩余时间（秒），0 表示永久或不可知</summary>
    public float RemainingTime { get; init; }
    /// <summary>层数（如 DoT 多层）</summary>
    public byte StackCount { get; init; }
    /// <summary>施加该效果的游戏对象 ID</summary>
    public uint SourceId { get; init; }
}

/// <summary>
/// 玩家自身的每帧状态快照
/// </summary>
public readonly record struct PlayerState
{
    /// <summary>当前生命值</summary>
    public uint HP { get; init; }
    /// <summary>最大生命值</summary>
    public uint MaxHP { get; init; }
    /// <summary>当前魔力值</summary>
    public uint MP { get; init; }
    /// <summary>最大魔力值</summary>
    public uint MaxMP { get; init; }
    /// <summary>当前职业 ID</summary>
    public byte JobId { get; init; }
    /// <summary>等级（含同步等级）</summary>
    public byte Level { get; init; }
    /// <summary>X 轴坐标</summary>
    public float PosX { get; init; }
    /// <summary>Y 轴坐标（游戏中纵轴）</summary>
    public float PosY { get; init; }
    /// <summary>Z 轴坐标（高度）</summary>
    public float PosZ { get; init; }
    /// <summary>当前 GCD 总时间（秒），受速度属性影响</summary>
    public float GcdTotal { get; init; }
    /// <summary>当前 GCD 剩余冷却时间（秒）</summary>
    public float GcdRemaining { get; init; }
    /// <summary>当前生命值百分比（0.0 ~ 1.0）</summary>
    public float HpPercent => MaxHP > 0 ? (float)HP / MaxHP : 0f;
    /// <summary>当前玩家所有增益状态列表</summary>
    public StatusEffect[] Buffs { get; init; }
}

/// <summary>
/// 小队成员的每帧状态快照
/// </summary>
public readonly record struct PartyMemberState
{
    /// <summary>游戏对象 ID（可用于 <see cref="Dalamud.Plugin.Services.IObjectTable.SearchByEntityId"/>）</summary>
    public uint ObjectId { get; init; }
    /// <summary>角色名称</summary>
    public string Name { get; init; }
    /// <summary>职业 ID</summary>
    public byte JobId { get; init; }
    /// <summary>当前生命值</summary>
    public uint HP { get; init; }
    /// <summary>最大生命值</summary>
    public uint MaxHP { get; init; }
    /// <summary>当前生命值百分比（0.0 ~ 1.0）</summary>
    public float HpPercent => MaxHP > 0 ? (float)HP / MaxHP : 0f;
    /// <summary>X 轴坐标</summary>
    public float PosX { get; init; }
    /// <summary>Y 轴坐标（游戏中纵轴）</summary>
    public float PosY { get; init; }
    /// <summary>Z 轴坐标（高度）</summary>
    public float PosZ { get; init; }
    /// <summary>该成员与玩家的距离（米）</summary>
    public float DistanceFromPlayer { get; init; }
    /// <summary>该成员当前的增益状态列表</summary>
    public StatusEffect[] Buffs { get; init; }
}

/// <summary>
/// 当前选中目标的每帧状态快照
/// </summary>
public readonly record struct TargetState
{
    /// <summary>游戏对象 ID</summary>
    public uint ObjectId { get; init; }
    /// <summary>目标名称</summary>
    public string Name { get; init; }
    /// <summary>当前生命值</summary>
    public uint HP { get; init; }
    /// <summary>最大生命值</summary>
    public uint MaxHP { get; init; }
    /// <summary>当前生命值百分比（0.0 ~ 1.0）</summary>
    public float HpPercent => MaxHP > 0 ? (float)HP / MaxHP : 0f;
    /// <summary>目标身上的负面状态（Debuff）列表</summary>
    public StatusEffect[] Debuffs { get; init; }
    /// <summary>目标是否正在施法</summary>
    public bool IsCasting { get; init; }
    /// <summary>施法技能 ID（未施法时为 0）</summary>
    public uint CastActionId { get; init; }
    /// <summary>施法已过时间（秒）</summary>
    public float CastProgress { get; init; }
    /// <summary>施法总时间（秒）</summary>
    public float CastTotal { get; init; }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 📌 数据来源: FFXIVClientStructs AstrologianGauge
// 📌 验证日期: 2026-02-23
// 📌 兼容游戏版本: FFXIV 7.x (Dawntrail)
//
// AstrologianGauge 内存布局 (StructLayout Size=0x30):
//   [0x08] short  Cards       — 4 张卡牌的压缩存储，每张占 4 bits:
//     bits  0-3:  CurrentCards[0] — PlayI   出牌槽位
//     bits  4-7:  CurrentCards[1] — PlayII  出牌槽位
//     bits  8-11: CurrentCards[2] — PlayIII 出牌槽位
//     bits 12-15: CurrentArcana  — Minor Arcana 小奥秘卡槽位
//   [0x0A] byte   CurrentDraw — 下次抽卡类型: 0=Astral星极, 1=Umbral灵极
//
// Dawntrail 7.0 占星重大变更：
//   - 印记(Seals) / Astrosigns / Astrodyne 系统 → 完全删除
//   - 随机翻牌 → 匹配制抽牌（正极Draw常局出 Balance/Arrow/Spire/Lord）
//   - 每次抽卡同时得到 4 张，通过 PlayI/PlayII/PlayIII 分别出牌
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// 占星卡牌枚举，对应 FFXIVClientStructs.AstrologianCard
/// </summary>
public enum AstCard : byte
{
    /// <summary>空槽位 / 未抽牌</summary>
    None    = 0,
    /// <summary>天良（The Balance）— 近战/坦分配 +6% 伤害</summary>
    Balance = 1,
    /// <summary>钿木（The Bole）— 目标 -10% 受到伤害</summary>
    Bole    = 2,
    /// <summary>射手（The Arrow）— 目标回复量 +10%</summary>
    Arrow   = 3,
    /// <summary>镇魂（The Spear）— 远战/奥运分配 +6% 伤害</summary>
    Spear   = 4,
    /// <summary>山泽（The Ewer）— 单目标 HoT</summary>
    Ewer    = 5,
    /// <summary>高塔（The Spire）— 单目标盾</summary>
    Spire   = 6,
    /// <summary>王冠（Lord of Crowns）— AoE 伤害（小奥秘卡）</summary>
    Lord    = 7,
    /// <summary>女公爵（Lady of Crowns）— AoE 治疗（小奥秘卡）</summary>
    Lady    = 8,
}

/// <summary>
/// 占星抽卡类型（下次抽卡是星极还是灵极）
/// </summary>
public enum AstDraw : byte
{
    /// <summary>星极吸引 (Astral Draw)，常局出 Balance / Arrow / Spire / Lord</summary>
    Astral = 0,
    /// <summary>灵极吸引 (Umbral Draw)，常局出 Spear / Bole / Ewer / Lady</summary>
    Umbral = 1,
}

/// <summary>
/// 占星术士专属仪表盘状态快照（7.x Dawntrail 版，非占星时不采集）
/// </summary>
public readonly record struct AstrologianState
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 📌 数据来源: FFXIVClientStructs AstrologianGauge
    // 📌 验证日期: 2026-02-23 | FFXIV 7.x (Dawntrail)
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// PlayI 槽位手牌（Cards bits 0-3）。
    /// Astral Draw 时此槽为 Balance/Arrow/Spire 之一；Umbral Draw 时为 Spear/Bole/Ewer 之一。
    /// </summary>
    public AstCard CardPlayI { get; init; }

    /// <summary>PlayII 槽位手牌（Cards bits 4-7）</summary>
    public AstCard CardPlayII { get; init; }

    /// <summary>PlayIII 槽位手牌（Cards bits 8-11）</summary>
    public AstCard CardPlayIII { get; init; }

    /// <summary>
    /// 小奥秘卡槽位（Cards bits 12-15）。
    /// Lord of Crowns（AoE伤害）或 Lady of Crowns（AoE治疗），无小奥秘卡时为 None。
    /// </summary>
    public AstCard CurrentArcana { get; init; }

    /// <summary>
    /// 下次抽卡类型（Astral=星极吸引；常出 Balance/Arrow/Spire/Lord，
    /// Umbral=灵极吸引；常出 Spear/Bole/Ewer/Lady）
    /// </summary>
    public AstDraw CurrentDraw { get; init; }

    /// <summary>抽卡（Astral/Umbral Draw）剩余冷却时间（秒）。两种 Draw 共享一个 CD。</summary>
    public float DrawCooldown { get; init; }

    /// <summary>PlayI 出牌剩余冷却时间（秒）</summary>
    public float PlayICooldown { get; init; }

    /// <summary>PlayII 出牌剩余冷却时间（秒）</summary>
    public float PlayIICooldown { get; init; }

    /// <summary>PlayIII 出牌剩余冷却时间（秒）</summary>
    public float PlayIIICooldown { get; init; }

    /// <summary>Minor Arcana 出牌剩余冷却时间（秒）</summary>
    public float MinorArcanaCooldown { get; init; }

    /// <summary>占卜（Divination）剩余冷却时间（秒）</summary>
    public float DivinationCooldown { get; init; }

    /// <summary>当前是否可以抽卡（Draw CD 已满且手牌未满）</summary>
    public bool CanDraw { get; init; }

    /// <summary>PlayI 槽位是否有牌可出</summary>
    public bool CanPlayI { get; init; }

    /// <summary>PlayII 槽位是否有牌可出</summary>
    public bool CanPlayII { get; init; }

    /// <summary>PlayIII 槽位是否有牌可出</summary>
    public bool CanPlayIII { get; init; }

    /// <summary>小奥秘卡槽位是否有牌可出（Lord/Lady of Crowns）</summary>
    public bool CanPlayArcana { get; init; }

    /// <summary>占卜是否可用</summary>
    public bool CanUseDivination { get; init; }

    /// <summary>
    /// 手牌中共有几张有效卡牌（PlayI/II/III 中 CardPlayX != None 的数量，不含小奥秘卡）
    /// </summary>
    public int HandCount =>
        (CardPlayI   != AstCard.None ? 1 : 0) +
        (CardPlayII  != AstCard.None ? 1 : 0) +
        (CardPlayIII != AstCard.None ? 1 : 0);
}


// ═══════════════════════════════════════════════════
//  战斗快照主体
// ═══════════════════════════════════════════════════

/// <summary>
/// 战斗快照：表示游戏中某一帧的完整不可变状态。
/// 使用 sealed record class 以避免大量字段的栈拷贝开销。
/// </summary>
public sealed record class BattleSnapshot
{
    /// <summary>帧序号（从插件启动后累计，用于时序比较）</summary>
    public long FrameNumber { get; init; }
    /// <summary>该帧的采集时间戳</summary>
    public DateTime Timestamp { get; init; }
    /// <summary>是否处于战斗状态（ICondition[ConditionFlag.InCombat]）</summary>
    public bool IsInCombat { get; init; }
    /// <summary>本次战斗已持续时间（秒），非战斗时为 0</summary>
    public double CombatDurationSeconds { get; init; }
    /// <summary>玩家自身状态</summary>
    public PlayerState Player { get; init; }
    /// <summary>小队成员状态（固定 8 槽，未使用槽为 null）</summary>
    public PartyMemberState?[] PartyMembers { get; init; } = new PartyMemberState?[8];
    /// <summary>实际小队人数（包含玩家自身）</summary>
    public int PartyMemberCount { get; init; }
    /// <summary>当前选中目标的状态，无目标时为 null</summary>
    public TargetState? CurrentTarget { get; init; }
    /// <summary>占星术士专属状态，非占星职业时为 null</summary>
    public AstrologianState? Astrologian { get; init; }

    // ═════════════════════════════════════════
    //  便捷属性
    // ═════════════════════════════════════════

    /// <summary>当前职业是否为占星术士</summary>
    public bool IsAstrologian => Player.JobId == Constants.JobIds.Astrologian;

    // ═════════════════════════════════════════
    //  空快照单例（替代 null 使用）
    // ═════════════════════════════════════════

    /// <summary>
    /// 默认空快照实例，所有字段为初始默认值。
    /// 用于插件启动时 StateTracker 未就绪的情况。
    /// </summary>
    public static readonly BattleSnapshot Empty = new()
    {
        FrameNumber = -1,
        Timestamp = DateTime.MinValue,
        IsInCombat = false,
        CombatDurationSeconds = 0,
        Player = default,
        PartyMembers = new PartyMemberState?[8],
        PartyMemberCount = 0,
        CurrentTarget = null,
        Astrologian = null,
    };

    // ═════════════════════════════════════════
    //  辅助方法（零 LINQ，预分配数组）
    // ═════════════════════════════════════════

    /// <summary>
    /// 返回所有非 null 队员按 HP% 升序排列的数组（HP 最低的排在前面）。
    /// 使用预分配临时数组与简单插入排序，避免堆分配。
    /// </summary>
    /// <returns>按 HP% 升序排列的非 null 队员数组</returns>
    public PartyMemberState[] GetPartyMembersSortedByHpPercent()
    {
        // ⚡ 性能关键：无 LINQ，手动分配
        int validCount = 0;
        for (int i = 0; i < PartyMembers.Length; i++)
            if (PartyMembers[i].HasValue) validCount++;

        PartyMemberState[] result = new PartyMemberState[validCount];
        int idx = 0;
        for (int i = 0; i < PartyMembers.Length; i++)
        {
            if (PartyMembers[i].HasValue)
                result[idx++] = PartyMembers[i]!.Value;
        }

        // 插入排序（队伍人数通常 ≤ 8，插入排序在此规模下最优）
        for (int i = 1; i < result.Length; i++)
        {
            PartyMemberState key = result[i];
            int j = i - 1;
            while (j >= 0 && result[j].HpPercent > key.HpPercent)
            {
                result[j + 1] = result[j];
                j--;
            }
            result[j + 1] = key;
        }

        return result;
    }

    /// <summary>
    /// 返回当前 HP% 最低的非 null 队员，队伍为空时返回 null。
    /// </summary>
    /// <returns>HP% 最低的队员，或 null</returns>
    public PartyMemberState? GetLowestHpPartyMember()
    {
        // ⚡ 性能关键：单次遍历，无 LINQ
        PartyMemberState? lowest = null;
        float lowestPct = float.MaxValue;

        for (int i = 0; i < PartyMembers.Length; i++)
        {
            if (!PartyMembers[i].HasValue) continue;
            float pct = PartyMembers[i]!.Value.HpPercent;
            if (pct < lowestPct)
            {
                lowestPct = pct;
                lowest = PartyMembers[i];
            }
        }

        return lowest;
    }

    /// <summary>
    /// 统计当前 HP% 低于指定阈值的队伍成员数量（包含玩家）。
    /// </summary>
    /// <param name="hpPercent">HP 百分比阈值（0.0 ~ 1.0）</param>
    /// <returns>HP% 低于阈值的成员数量</returns>
    public int GetPartyMembersBelow(float hpPercent)
    {
        int count = 0;
        for (int i = 0; i < PartyMembers.Length; i++)
        {
            if (PartyMembers[i].HasValue && PartyMembers[i]!.Value.HpPercent < hpPercent)
                count++;
        }
        return count;
    }
}

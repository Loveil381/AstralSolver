using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using AstralSolver.Core;
using AstralSolver.Utils;

namespace AstralSolver.Jobs.Healer;

/// <summary>
/// 占星术士决策模块 — AstralSolver 的核心智能引擎。
/// 同时处理三条决策线：输出、治疗、发牌，并实时权衡优先级。
/// 
/// 决策流程（按优先级从高到低）：
///   阶段A: 紧急治疗检查（保命 > 一切）
///   阶段B: 卡牌决策（抽卡/出牌/占卜，oGCD 穿插）
///   阶段C: 输出循环（DoT → 填充 → 开幕序列）
///   阶段D: 预判治疗（Earthly Star / Exaltation / Horoscope）
/// 
/// 参考: docs/reference/astrologian-guide-7x.md
/// </summary>
public sealed class AstrologianModule : BaseJobModule
{
    // ── 决策阈值常量 ─────────────────────────────────────
    private const float SELF_HP_EMERGENCY    = 0.40f; // 玩家HP%低于此值触发自保
    private const float TANK_HP_EMERGENCY    = 0.30f; // 坦克HP%低于此值触发紧急治疗
    private const float ALLY_HP_CRITICAL     = 0.25f; // 队友HP%低于此值视为危险
    private const int   AOE_HEAL_THRESHOLD   = 3;     // 多少人低血量时触发AOE治疗
    private const float DOT_REFRESH_THRESHOLD = 3.0f; // DoT剩余时间低于此值时刷新
    private const float BURST_WINDOW_MARGIN  = 5.0f;  // 爆发窗口判定的偏移秒数
    private const float PREEMPTIVE_HP_THRESHOLD = 0.80f; // 队伍平均HP低于此值考虑预判治疗

    // ── 接口实现 ─────────────────────────────────────────
    public override byte JobId => Constants.JobIds.Astrologian;
    public override string JobName => "占星术士";

    // ── 私有状态 ─────────────────────────────────────────
    private bool _openerStarted;  // 开幕序列是否已启动
    private int  _openerStep;     // 开幕序列当前步骤

    // ── 构造 ─────────────────────────────────────────────

    public AstrologianModule(IPluginLog log) : base(log) { }

    // ── 生命周期 ─────────────────────────────────────────

    public override void OnCombatStart()
    {
        base.OnCombatStart();
        _openerStarted = false;
        _openerStep = 0;
        Log.Debug("[占星术士] 开幕状态已初始化");
    }

    public override void OnCombatEnd()
    {
        base.OnCombatEnd();
    }

    // ═══════════════════════════════════════════════════
    //  核心决策方法
    // ═══════════════════════════════════════════════════

    public override JobDecision Evaluate(BattleSnapshot snapshot)
    {
        // ⚡ 性能关键：使用 List 收集本帧决策，避免多次数组分配
        var gcdQueue   = new List<GcdAction>(5);
        var ogcdList   = new List<OgcdInsert>(8);
        var reasonList = new List<ReasonEntry>(12);

        // 获取占星状态（非占星职业时为 null → 返回空决策）
        var astState = snapshot.Astrologian;
        if (!astState.HasValue)
            return JobDecision.Empty;

        var ast = astState.Value;

        // ─── 阶段A: 紧急治疗检查 ────────────────────────
        EvaluateEmergencyHealing(snapshot, ast, ogcdList, gcdQueue, reasonList);

        // ─── 阶段B: 卡牌决策 ────────────────────────────
        EvaluateCardDecisions(snapshot, ast, ogcdList, reasonList);

        // ─── 阶段C: 输出循环 ────────────────────────────
        EvaluateOffenseRotation(snapshot, ast, gcdQueue, ogcdList, reasonList);

        // ─── 阶段D: 预判治疗 ────────────────────────────
        EvaluatePreemptiveHealing(snapshot, ast, ogcdList, reasonList);

        // ─── 构建面板 ────────────────────────────────────
        var panel = BuildAstPanel(snapshot, ast);

        return new JobDecision
        {
            GcdQueue        = gcdQueue.ToArray(),
            OgcdInserts     = ogcdList.ToArray(),
            Reasons         = reasonList.ToArray(),
            JobSpecificPanel = panel,
            Confidence      = 0.8f, // 初版置信度（后续由逻辑完善度提升）
        };
    }

    // ═══════════════════════════════════════════════════
    //  阶段A: 紧急治疗
    // ═══════════════════════════════════════════════════

    private void EvaluateEmergencyHealing(
        BattleSnapshot snap, AstrologianState ast,
        List<OgcdInsert> ogcds, List<GcdAction> gcds, List<ReasonEntry> reasons)
    {
        // A1: 自己HP低 → Essential Dignity 自保
        if (snap.Player.HpPercent < SELF_HP_EMERGENCY)
        {
            // 用 Essential Dignity 自保（如果有充能）
            ogcds.Add(Ogcd(Constants.AstActionIds.EssentialDignity, "先天禀赋(自保)", priority: 100f));
            reasons.Add(MakeReason(Constants.AstActionIds.EssentialDignity,
                "emergency.self", $"⚠️ 自身HP {snap.Player.HpPercent * 100:F0}% < {SELF_HP_EMERGENCY * 100:F0}%，紧急自保",
                ReasonPriority.Critical));
        }

        // A2: 坦克HP低 → Essential Dignity 给坦克
        var lowestMember = snap.GetLowestHpPartyMember();
        if (lowestMember.HasValue)
        {
            var target = lowestMember.Value;
            bool isTank = Constants.MeleeJobs.Contains((uint)target.JobId) &&
                          (target.JobId == 19 || target.JobId == 21 || target.JobId == 32 || target.JobId == 37);

            if (isTank && target.HpPercent < TANK_HP_EMERGENCY)
            {
                ogcds.Add(Ogcd(Constants.AstActionIds.EssentialDignity, "先天禀赋(坦克)", priority: 95f, targetId: target.ObjectId));
                reasons.Add(MakeReason(Constants.AstActionIds.EssentialDignity,
                    "emergency.tank", $"🚨 坦克 {target.Name} HP {target.HpPercent * 100:F0}%，紧急治疗",
                    ReasonPriority.Critical));
            }
        }

        // A3: 多人低血量 → 根据人数决策
        int criticalCount = snap.GetPartyMembersBelow(ALLY_HP_CRITICAL);
        if (criticalCount >= AOE_HEAL_THRESHOLD)
        {
            // 3+ 人危险 → AOE 治疗 GCD
            gcds.Add(Gcd(Constants.AstActionIds.AspectedHelios, "星辉天地合相(AOE急救)", priority: 90f));
            reasons.Add(MakeReason(Constants.AstActionIds.AspectedHelios,
                "emergency.aoe", $"🚨 {criticalCount} 名队友HP低于 {ALLY_HP_CRITICAL * 100:F0}%，AOE急救",
                ReasonPriority.Critical));
        }
        else if (criticalCount >= 1 && lowestMember.HasValue)
        {
            // 1-2 人危险 → oGCD 单体治疗
            var target = lowestMember.Value;

            // 优先级链: Essential Dignity > Celestial Intersection
            ogcds.Add(Ogcd(Constants.AstActionIds.EssentialDignity, "先天禀赋(单体急救)", priority: 88f, targetId: target.ObjectId));
            reasons.Add(MakeReason(Constants.AstActionIds.EssentialDignity,
                "emergency.single", $"⚠️ {target.Name} HP {target.HpPercent * 100:F0}%，单体急救",
                ReasonPriority.Critical));

            ogcds.Add(Ogcd(Constants.AstActionIds.CelestialIntersection, "天宫交嵌(补盾)", priority: 82f, targetId: target.ObjectId));
        }
    }

    // ═══════════════════════════════════════════════════
    //  阶段B: 卡牌决策
    // ═══════════════════════════════════════════════════

    private void EvaluateCardDecisions(
        BattleSnapshot snap, AstrologianState ast,
        List<OgcdInsert> ogcds, List<ReasonEntry> reasons)
    {
        // B1: 抽卡
        if (ast.CanDraw && ast.DrawCooldown <= 0f)
        {
            uint drawAction = ast.CurrentDraw == AstDraw.Astral
                ? Constants.AstActionIds.AstralDraw
                : Constants.AstActionIds.UmbralDraw;
            string drawName = ast.CurrentDraw == AstDraw.Astral ? "星极抽卡" : "灵极抽卡";
            ogcds.Add(Ogcd(drawAction, drawName, priority: 70f));
            reasons.Add(MakeReason(drawAction, "card.draw", $"🃏 抽卡CD已就绪 ({drawName})", ReasonPriority.Important));
        }

        // B2: 出牌 — Play I (攻击卡: Balance/Spear)
        if (ast.CanPlayI)
        {
            var (targetId, targetName, targetJobId) = SelectCardTarget(snap, ast.CardPlayI);
            ogcds.Add(Ogcd(Constants.AstActionIds.PlayI, $"出牌I({ast.CardPlayI})", priority: 65f, targetId: targetId));
            reasons.Add(MakeReason(Constants.AstActionIds.PlayI, "card.play1",
                $"🃏 发牌I {ast.CardPlayI} → {targetName}(JobId:{targetJobId})", ReasonPriority.Important));
        }

        // B3: 出牌 — Play II (防御卡: Bole/Ewer)
        if (ast.CanPlayII)
        {
            var (targetId, targetName, targetJobId) = SelectCardTarget(snap, ast.CardPlayII);
            ogcds.Add(Ogcd(Constants.AstActionIds.PlayII, $"出牌II({ast.CardPlayII})", priority: 60f, targetId: targetId));
            reasons.Add(MakeReason(Constants.AstActionIds.PlayII, "card.play2",
                $"🃏 发牌II {ast.CardPlayII} → {targetName}", ReasonPriority.Info));
        }

        // B4: 出牌 — Play III (回复卡: Arrow/Spire)
        if (ast.CanPlayIII)
        {
            var (targetId, targetName, targetJobId) = SelectCardTarget(snap, ast.CardPlayIII);
            ogcds.Add(Ogcd(Constants.AstActionIds.PlayIII, $"出牌III({ast.CardPlayIII})", priority: 55f, targetId: targetId));
            reasons.Add(MakeReason(Constants.AstActionIds.PlayIII, "card.play3",
                $"🃏 发牌III {ast.CardPlayIII} → {targetName}", ReasonPriority.Info));
        }

        // B5: Minor Arcana (Lord=输出 / Lady=治疗)
        if (ast.CanPlayArcana)
        {
            string arcanaName = ast.CurrentArcana == AstCard.Lord ? "领主之冠(输出)" : "贵妇之冠(治疗)";
            ogcds.Add(Ogcd(Constants.AstActionIds.MinorArcana, arcanaName, priority: 50f));
            reasons.Add(MakeReason(Constants.AstActionIds.MinorArcana, "card.arcana",
                $"🃏 小奥秘卡: {arcanaName}", ReasonPriority.Info));
        }

        // B6: 占卜 (Divination) — 对齐爆发窗口
        if (ast.CanUseDivination && IsInBurstWindow(snap))
        {
            ogcds.Add(Ogcd(Constants.AstActionIds.Divination, "占卜(团辅)", priority: 75f));
            reasons.Add(MakeReason(Constants.AstActionIds.Divination, "card.divination",
                "⭐ 占卜对齐团队爆发窗口", ReasonPriority.Important));
        }
    }

    // ═══════════════════════════════════════════════════
    //  阶段C: 输出循环
    // ═══════════════════════════════════════════════════

    private void EvaluateOffenseRotation(
        BattleSnapshot snap, AstrologianState ast,
        List<GcdAction> gcds, List<OgcdInsert> ogcds, List<ReasonEntry> reasons)
    {
        // 如果此帧已经插入了紧急治疗 GCD，跳过输出 GCD
        if (gcds.Count > 0) return;

        // GCD 未就绪时不产生 GCD 推荐
        if (!IsGcdReady(snap)) return;

        // 是否有目标
        bool hasTarget = snap.CurrentTarget.HasValue;

        // C1: DoT 维护（Combust III）
        if (hasTarget)
        {
            // 检查目标身上是否有我的 Combust DoT
            float dotRemaining = 0f;
            if (snap.CurrentTarget.HasValue)
            {
                dotRemaining = GetBuffRemainingTime(
                    snap.CurrentTarget.Value.Debuffs, (ushort)Constants.AstStatusIds.Combust);
            }

            if (dotRemaining < DOT_REFRESH_THRESHOLD)
            {
                gcds.Add(Gcd(Constants.AstActionIds.Combust, "烧灼III(DoT)", priority: 50f));
                reasons.Add(MakeReason(Constants.AstActionIds.Combust, "offense.dot",
                    $"🔥 刷新DoT（剩余{dotRemaining:F1}s < {DOT_REFRESH_THRESHOLD:F0}s）", ReasonPriority.Info));
                return;
            }
        }

        // C2: 默认填充 — Malefic
        if (hasTarget)
        {
            gcds.Add(Gcd(Constants.AstActionIds.Malefic, "坠星(填充)", priority: 10f));
            reasons.Add(MakeReason(Constants.AstActionIds.Malefic, "offense.filler",
                "✨ 填充GCD", ReasonPriority.Info));
        }
    }

    // ═══════════════════════════════════════════════════
    //  阶段D: 预判治疗
    // ═══════════════════════════════════════════════════

    private void EvaluatePreemptiveHealing(
        BattleSnapshot snap, AstrologianState ast,
        List<OgcdInsert> ogcds, List<ReasonEntry> reasons)
    {
        // D1: 队伍平均HP低 → Earthly Star 预放
        float avgHp = CalculatePartyAverageHp(snap);
        if (avgHp < PREEMPTIVE_HP_THRESHOLD && avgHp > 0f)
        {
            ogcds.Add(Ogcd(Constants.AstActionIds.EarthlyStar, "天星地星(预放)", priority: 35f));
            reasons.Add(MakeReason(Constants.AstActionIds.EarthlyStar, "preemptive.star",
                $"🌟 队伍平均HP {avgHp * 100:F0}% < {PREEMPTIVE_HP_THRESHOLD * 100:F0}%，预放地星",
                ReasonPriority.Info));
        }

        // D2: 预判大伤害 → Exaltation 给坦克
        if (snap.CurrentTarget.HasValue && snap.CurrentTarget.Value.IsCasting)
        {
            // 查找坦克目标
            var tank = FindTank(snap);
            if (tank.HasValue)
            {
                ogcds.Add(Ogcd(Constants.AstActionIds.Exaltation, "崇高(预判减伤)", priority: 40f, targetId: tank.Value.ObjectId));
                reasons.Add(MakeReason(Constants.AstActionIds.Exaltation, "preemptive.exaltation",
                    $"🛡️ 预判减伤 → {tank.Value.Name}（Boss正在施法）",
                    ReasonPriority.Important));
            }
        }

        // D3: Horoscope 预判
        int belowThreshold = snap.GetPartyMembersBelow(0.90f);
        if (belowThreshold >= 2)
        {
            ogcds.Add(Ogcd(Constants.AstActionIds.Horoscope, "天宫星象(预判)", priority: 30f));
            reasons.Add(MakeReason(Constants.AstActionIds.Horoscope, "preemptive.horoscope",
                "🔮 Horoscope 预判：多名队友HP未满", ReasonPriority.Info));
        }
    }

    // ═══════════════════════════════════════════════════
    //  发牌目标选择算法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 发牌目标选择核心算法。
    /// 依据卡牌属性（近/远），评分每位队友并选择最优目标。
    /// ⚡ 零 LINQ 实现，手动遍历。
    /// </summary>
    internal (uint targetId, string targetName, byte targetJobId) SelectCardTarget(
        BattleSnapshot snap, AstCard card)
    {
        // 默认：发给自己
        uint bestId = 0;
        string bestName = "自己";
        byte bestJobId = snap.Player.JobId;
        float bestScore = float.MinValue;

        // 判断卡牌属性：日属性 = 近战加成高
        bool isMeleeCard = card == AstCard.Balance;
        // 月属性 = 远程加成高
        bool isRangedCard = card == AstCard.Spear;

        // ⚡ 性能优化：IsInBurstWindow 每帧只计算一次，避免在循环内重复调用
        bool inBurstWindow = IsInBurstWindow(snap);

        // ⚡ 性能关键：手动遍历，无 LINQ
        for (int i = 0; i < snap.PartyMembers.Length; i++)
        {
            if (!snap.PartyMembers[i].HasValue) continue;
            var member = snap.PartyMembers[i]!.Value;

            // 评分算法
            float score = 0f;

            // 1. 职业类型匹配分
            // MeleeJobs/RangedJobs 是 IReadOnlySet<uint>，JobId 是 byte，需显式转换避免隐式转换警告
            bool memberIsMelee = Constants.MeleeJobs.Contains((uint)member.JobId);
            bool memberIsRanged = Constants.RangedJobs.Contains((uint)member.JobId);

            if (isMeleeCard && memberIsMelee)       score += 50f; // 近战卡给近战 +50
            else if (isMeleeCard && memberIsRanged)  score += 30f; // 近战卡给远程 +30
            else if (isRangedCard && memberIsRanged)  score += 50f; // 远程卡给远程 +50
            else if (isRangedCard && memberIsMelee)   score += 30f; // 远程卡给近战 +30
            else                                      score += 20f; // 功能卡（无匹配偏好）

            // 2. 角色优先级 — DPS > 坦克 > 治疗
            bool isTank = member.JobId == 19 || member.JobId == 21 || member.JobId == 32 || member.JobId == 37;
            bool isHealer = member.JobId == 24 || member.JobId == 28 || member.JobId == 33 || member.JobId == 40;

            if (!isTank && !isHealer)      score += 100f; // DPS
            else if (isTank)               score += 50f;  // 坦克
            else                           score += 30f;  // 治疗

            // 3. Buff 惩罚 — 已有同类卡 Buff 则 -100
            ushort cardBuffId = GetCardBuffStatusId(card);
            if (cardBuffId != 0 && HasBuff(member.Buffs, cardBuffId))
                score -= 100f;

            // 4. 爆发加分 — 使用循环外预计算的结果（避免每次循环重新判断）
            if (inBurstWindow)
                score += 40f;

            // 5. 血量惩罚 — 快死的不值得发
            if (member.HpPercent < 0.50f)
                score -= 20f;

            if (score > bestScore)
            {
                bestScore = score;
                bestId = member.ObjectId;
                bestName = member.Name;
                bestJobId = member.JobId;
            }
        }

        return (bestId, bestName, bestJobId);
    }

    // ═══════════════════════════════════════════════════
    //  爆发窗口检测
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 检测当前是否在团队爆发窗口内。
    /// 使用 Divination Buff 存在性 + 战斗时间 120s 周期判定。
    /// </summary>
    internal bool IsInBurstWindow(BattleSnapshot snap)
    {
        // 方法1: 检查自身是否有占卜 Buff
        if (HasBuff(snap.Player.Buffs, (ushort)Constants.AstStatusIds.Divination))
            return true;

        // 方法2: 战斗时间是否在 120s 倍数的窗口内（±5s）
        if (CombatTime < 1.0) return true; // 开幕期视为爆发窗口

        double remainder = CombatTime % 120.0;
        return remainder < BURST_WINDOW_MARGIN || remainder > (120.0 - BURST_WINDOW_MARGIN);
    }

    // ═══════════════════════════════════════════════════
    //  辅助方法
    // ═══════════════════════════════════════════════════

    /// <summary>计算队伍平均HP百分比</summary>
    private static float CalculatePartyAverageHp(BattleSnapshot snap)
    {
        float total = 0f;
        int count = 0;
        for (int i = 0; i < snap.PartyMembers.Length; i++)
        {
            if (!snap.PartyMembers[i].HasValue) continue;
            total += snap.PartyMembers[i]!.Value.HpPercent;
            count++;
        }
        return count > 0 ? total / count : 1f; // 空队伍视为满血
    }

    /// <summary>在小队中查找坦克（返回第一个坦克）</summary>
    private static PartyMemberState? FindTank(BattleSnapshot snap)
    {
        for (int i = 0; i < snap.PartyMembers.Length; i++)
        {
            if (!snap.PartyMembers[i].HasValue) continue;
            byte jid = snap.PartyMembers[i]!.Value.JobId;
            if (jid == 19 || jid == 21 || jid == 32 || jid == 37) // PLD/WAR/DRK/GNB
                return snap.PartyMembers[i]!.Value;
        }
        return null;
    }

    /// <summary>获取卡牌对应的 Buff 状态 ID（用于检测重复 Buff）</summary>
    private static ushort GetCardBuffStatusId(AstCard card) => card switch
    {
        AstCard.Balance => (ushort)Constants.AstStatusIds.Balance,
        AstCard.Spear   => (ushort)Constants.AstStatusIds.Spear,
        AstCard.Bole    => (ushort)Constants.AstStatusIds.Bole,
        AstCard.Arrow   => (ushort)Constants.AstStatusIds.Arrow,
        AstCard.Ewer    => (ushort)Constants.AstStatusIds.Ewer,
        AstCard.Spire   => (ushort)Constants.AstStatusIds.Spire,
        _ => 0,
    };

    /// <summary>构建占星面板 UI 数据</summary>
    private AstrologianPanel BuildAstPanel(BattleSnapshot snap, AstrologianState ast)
    {
        var plans = new List<CardPlayPlan>(4);

        if (ast.CardPlayI != AstCard.None)
        {
            var (tid, tname, tjob) = SelectCardTarget(snap, ast.CardPlayI);
            plans.Add(new CardPlayPlan { Card = ast.CardPlayI, TargetName = tname, TargetJobId = tjob, Reason = "PlayI 攻击卡" });
        }
        if (ast.CardPlayII != AstCard.None)
        {
            var (tid, tname, tjob) = SelectCardTarget(snap, ast.CardPlayII);
            plans.Add(new CardPlayPlan { Card = ast.CardPlayII, TargetName = tname, TargetJobId = tjob, Reason = "PlayII 防御卡" });
        }
        if (ast.CardPlayIII != AstCard.None)
        {
            var (tid, tname, tjob) = SelectCardTarget(snap, ast.CardPlayIII);
            plans.Add(new CardPlayPlan { Card = ast.CardPlayIII, TargetName = tname, TargetJobId = tjob, Reason = "PlayIII 回复卡" });
        }
        if (ast.CurrentArcana != AstCard.None)
        {
            plans.Add(new CardPlayPlan { Card = ast.CurrentArcana, TargetName = "全体", TargetJobId = 0, Reason = "小奥秘卡" });
        }

        return new AstrologianPanel
        {
            GaugeState = ast,
            CardPlans = plans.ToArray(),
            NextDrawIn = ast.DrawCooldown,
            SuggestedTarget = plans.Count > 0 ? plans[0].TargetName : string.Empty,
        };
    }
}

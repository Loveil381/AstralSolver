using System;
using Xunit;
using NSubstitute;
using Dalamud.Plugin.Services;
using AstralSolver.Core;
using AstralSolver.Jobs.Healer;
using AstralSolver.Tests.TestHelpers;
using AstralSolver.Utils;

namespace AstralSolver.Tests.Jobs;

/// <summary>
/// 占星术士决策模块单元测试。
/// 覆盖四阶段决策流程：紧急治疗、卡牌决策、输出循环、预判治疗。
/// </summary>
public sealed class AstrologianModuleTests
{
    private readonly AstrologianModule _module;
    private readonly IPluginLog _log;

    public AstrologianModuleTests()
    {
        _log = Substitute.For<IPluginLog>();
        _module = new AstrologianModule(_log);
        _module.OnCombatStart();
    }

    // ═══════════════════════════════════════════════════
    //  输出相关测试
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Evaluate_GcdReady_ReturnsMaleficAsFiller()
    {
        // GCD 就绪 + 有目标 + DoT 剩余充足 → 返回 Malefic 填充
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        Assert.True(decision.GcdQueue.Length >= 1, "应至少有1个GCD推荐");
        Assert.Equal(Constants.AstActionIds.Malefic, decision.GcdQueue[0].ActionId);
    }

    [Fact]
    public void Evaluate_DotBelow3s_ReturnsCombust()
    {
        // DoT 剩余 < 3s → 优先刷新 Combust
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 2.0f } })
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        Assert.True(decision.GcdQueue.Length >= 1);
        Assert.Equal(Constants.AstActionIds.Combust, decision.GcdQueue[0].ActionId);
    }

    [Fact]
    public void Evaluate_DotAbove3s_DoesNotRefresh()
    {
        // DoT 剩余 > 3s → 不刷新，用 Malefic 填充
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 15f } })
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        Assert.True(decision.GcdQueue.Length >= 1);
        Assert.Equal(Constants.AstActionIds.Malefic, decision.GcdQueue[0].ActionId);
    }

    [Fact]
    public void Evaluate_NoTarget_NoOffenseGcd()
    {
        // 无目标 → 不产生输出 GCD
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasOffenseGcd = false;
        for (int i = 0; i < decision.GcdQueue.Length; i++)
        {
            if (decision.GcdQueue[i].ActionId == Constants.AstActionIds.Malefic ||
                decision.GcdQueue[i].ActionId == Constants.AstActionIds.Combust)
                hasOffenseGcd = true;
        }
        Assert.False(hasOffenseGcd, "无目标时不应有输出GCD");
    }

    // ═══════════════════════════════════════════════════
    //  治疗相关测试
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Evaluate_TankLowHp_InsertsEssentialDignity()
    {
        // 坦克 HP < 30% → Essential Dignity 给坦克
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithPartyMember("战士", jobId: 21, hp: 20000, maxHp: 120000) // 16.7%
            .WithTarget("Boss")
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasEd = false;
        for (int i = 0; i < decision.OgcdInserts.Length; i++)
        {
            if (decision.OgcdInserts[i].ActionId == Constants.AstActionIds.EssentialDignity)
            { hasEd = true; break; }
        }
        Assert.True(hasEd, "坦克血低时应插入先天禀赋");
    }

    [Fact]
    public void Evaluate_SelfLowHp_InsertsSelfHeal()
    {
        // 自己 HP < 40% → Essential Dignity 自保
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33, hp: 30000, maxHp: 100000) // 30%
            .WithGcdRemaining(0f)
            .WithTarget("Boss")
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasCriticalReason = false;
        for (int i = 0; i < decision.Reasons.Length; i++)
        {
            if (decision.Reasons[i].Priority == ReasonPriority.Critical &&
                decision.Reasons[i].TemplateKey == "emergency.self")
            { hasCriticalReason = true; break; }
        }
        Assert.True(hasCriticalReason, "自身HP低时应有紧急自保理由");
    }

    [Fact]
    public void Evaluate_ThreeAlliesLowHp_UsesAoeHeal()
    {
        // >= 3人 HP < 25% → AOE 治疗 GCD
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithPartyMember("战士", jobId: 21, hp: 20000, maxHp: 120000)
            .WithPartyMember("龙骑", jobId: 22, hp: 15000, maxHp: 100000)
            .WithPartyMember("黑魔", jobId: 25, hp: 10000, maxHp: 90000)
            .WithTarget("Boss")
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasAoeHeal = false;
        for (int i = 0; i < decision.GcdQueue.Length; i++)
        {
            if (decision.GcdQueue[i].ActionId == Constants.AstActionIds.AspectedHelios)
            { hasAoeHeal = true; break; }
        }
        Assert.True(hasAoeHeal, "3+人低血量时应使用AOE治疗");
    }

    [Fact]
    public void Evaluate_OgcdHealPriorityOverGcd()
    {
        // 1-2人低HP → 应产生 oGCD 治疗（Essential Dignity）
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithPartyMember("战士", jobId: 21, hp: 20000, maxHp: 120000) // ~17%
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasOgcdHeal = false;
        for (int i = 0; i < decision.OgcdInserts.Length; i++)
        {
            if (decision.OgcdInserts[i].ActionId == Constants.AstActionIds.EssentialDignity)
            { hasOgcdHeal = true; break; }
        }
        Assert.True(hasOgcdHeal, "1-2人低血量时应使用oGCD治疗");
    }

    [Fact]
    public void Evaluate_FullHpParty_NoHealingDecisions()
    {
        // 队伍全满血 → 无紧急治疗决策
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33, hp: 100000, maxHp: 100000)
            .WithGcdRemaining(0f)
            .WithPartyMember("战士", jobId: 21, hp: 120000, maxHp: 120000)
            .WithPartyMember("龙骑", jobId: 22, hp: 100000, maxHp: 100000)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasCriticalHeal = false;
        for (int i = 0; i < decision.Reasons.Length; i++)
        {
            if (decision.Reasons[i].Priority == ReasonPriority.Critical)
            { hasCriticalHeal = true; break; }
        }
        Assert.False(hasCriticalHeal, "全满血时不应有紧急治疗决策");
    }

    // ═══════════════════════════════════════════════════
    //  发牌相关测试
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Evaluate_CanDraw_InsertsDrawOgcd()
    {
        // CanDraw = true 且 DrawCooldown <= 0 → 插入抽卡 oGCD
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState(canDraw: true, drawCooldown: 0f, currentDraw: AstDraw.Astral)
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasDraw = false;
        for (int i = 0; i < decision.OgcdInserts.Length; i++)
        {
            if (decision.OgcdInserts[i].ActionId == Constants.AstActionIds.AstralDraw ||
                decision.OgcdInserts[i].ActionId == Constants.AstActionIds.UmbralDraw)
            { hasDraw = true; break; }
        }
        Assert.True(hasDraw, "CanDraw为true时应插入抽卡oGCD");
    }

    [Fact]
    public void Evaluate_HasPlayableCard_InsertsPlay()
    {
        // 有可打出的卡 → 插入 Play oGCD
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithPartyMember("龙骑", jobId: 22, hp: 100000, maxHp: 100000)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState(cardPlayI: AstCard.Balance, canPlayI: true)
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasPlay = false;
        for (int i = 0; i < decision.OgcdInserts.Length; i++)
        {
            if (decision.OgcdInserts[i].ActionId == Constants.AstActionIds.PlayI)
            { hasPlay = true; break; }
        }
        Assert.True(hasPlay, "有可打出的卡时应插入发牌oGCD");
    }

    [Fact]
    public void SelectCardTarget_PrefersMeleeDps()
    {
        // Balance（近战卡）→ 应优先选择近战DPS
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithPartyMember("黑魔", jobId: 25, hp: 100000, maxHp: 100000, objectId: 1001)
            .WithPartyMember("龙骑", jobId: 22, hp: 100000, maxHp: 100000, objectId: 1002)
            .WithPartyMember("战士", jobId: 21, hp: 120000, maxHp: 120000, objectId: 1003)
            .WithCombatState(duration: 30.0)
            .WithAstrologianState()
            .Build();

        var (targetId, targetName, targetJobId) = _module.SelectCardTarget(snap, AstCard.Balance);

        Assert.Equal((byte)22, targetJobId);
        Assert.Equal("龙骑", targetName);
    }

    [Fact]
    public void SelectCardTarget_AvoidsExistingBuff()
    {
        // 目标已有 Balance Buff → 应选择其他目标
        var balanceBuff = new StatusEffect
        {
            StatusId = (ushort)Constants.AstStatusIds.Balance,
            RemainingTime = 10f,
        };

        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithPartyMember("龙骑A", jobId: 22, hp: 100000, maxHp: 100000, objectId: 1001, buffs: new[] { balanceBuff })
            .WithPartyMember("武僧", jobId: 20, hp: 100000, maxHp: 100000, objectId: 1002)
            .WithCombatState(duration: 30.0)
            .WithAstrologianState()
            .Build();

        var (targetId, targetName, targetJobId) = _module.SelectCardTarget(snap, AstCard.Balance);

        Assert.Equal(1002u, targetId);
        Assert.Equal("武僧", targetName);
    }

    [Fact]
    public void Evaluate_DivinationInBurstWindow_InsertsDivination()
    {
        // Divination 就绪 + 爆发窗口 → 插入占卜
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState(canUseDivination: true, divinationCooldown: 0f)
            .WithCombatState(duration: 0.5) // 开幕期 = 爆发窗口
            .Build();

        var decision = _module.Evaluate(snap);

        bool hasDivination = false;
        for (int i = 0; i < decision.OgcdInserts.Length; i++)
        {
            if (decision.OgcdInserts[i].ActionId == Constants.AstActionIds.Divination)
            { hasDivination = true; break; }
        }
        Assert.True(hasDivination, "爆发窗口内应使用占卜");
    }

    // ═══════════════════════════════════════════════════
    //  综合测试
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Evaluate_EmergencyHealPriorityOverOffense()
    {
        // 有人快死 + GCD就绪 → AOE治疗GCD应出现（而非输出GCD）
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithPartyMember("战士", jobId: 21, hp: 10000, maxHp: 120000) // 8%
            .WithPartyMember("龙骑", jobId: 22, hp: 5000, maxHp: 100000)  // 5%
            .WithPartyMember("黑魔", jobId: 25, hp: 8000, maxHp: 90000)   // 9%
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState()
            .Build();

        var decision = _module.Evaluate(snap);

        Assert.True(decision.GcdQueue.Length >= 1);
        Assert.NotEqual(Constants.AstActionIds.Malefic, decision.GcdQueue[0].ActionId);
    }

    [Fact]
    public void Evaluate_CardAsOgcdBetweenGcds()
    {
        // 有卡牌可出 + GCD就绪 → 卡牌应作为 oGCD 穿插
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(0f)
            .WithPartyMember("龙骑", jobId: 22, hp: 100000, maxHp: 100000)
            .WithTarget("Boss", debuffs: new[] { new StatusEffect { StatusId = (ushort)Constants.AstStatusIds.Combust, RemainingTime = 20f } })
            .WithAstrologianState(cardPlayI: AstCard.Balance, canPlayI: true)
            .Build();

        var decision = _module.Evaluate(snap);

        Assert.True(decision.GcdQueue.Length > 0, "应有GCD推荐");
        Assert.True(decision.OgcdInserts.Length > 0, "卡牌应作为oGCD穿插");
    }

    [Fact]
    public void Evaluate_EmptySnapshot_DoesNotCrash()
    {
        // BattleSnapshot.Empty → 应返回空决策，不崩溃
        var decision = _module.Evaluate(BattleSnapshot.Empty);

        Assert.NotNull(decision);
        Assert.Equal(0, decision.GcdQueue.Length);
        Assert.Equal(0, decision.OgcdInserts.Length);
    }

    // ═══════════════════════════════════════════════════
    //  爆发窗口检测测试
    // ═══════════════════════════════════════════════════

    [Fact]
    public void IsInBurstWindow_OpenerPhase_ReturnsTrue()
    {
        // 开幕期（CombatTime < 1s）→ 视为爆发窗口
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithCombatState(duration: 0.5)
            .WithAstrologianState()
            .Build();

        Assert.True(_module.IsInBurstWindow(snap));
    }

    [Fact]
    public void IsInBurstWindow_At120s_ReturnsTrue()
    {
        // 战斗时间接近 120s → 视为爆发窗口
        var snap = new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithPlayerBuffs() // 无占卜buff
            .WithCombatState(duration: 119.0) // 120s - 1s = 在窗口内
            .WithAstrologianState()
            .Build();

        Assert.True(_module.IsInBurstWindow(snap));
    }
}

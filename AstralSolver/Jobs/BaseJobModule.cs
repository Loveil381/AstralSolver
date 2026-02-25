using System;
using Dalamud.Plugin.Services;
using AstralSolver.Core;

namespace AstralSolver.Jobs;

/// <summary>
/// 职业逻辑模块基类。
/// 提供子类共享的工具方法、GCD/oGCD 构建辅助，以及战斗状态跟踪。
/// 所有方法均为零 LINQ、最小分配设计。
/// </summary>
public abstract class BaseJobModule : IJobModule
{
    // ── 依赖 ─────────────────────────────────────────────
    protected readonly IPluginLog Log;

    // ── 接口实现 ─────────────────────────────────────────
    public abstract byte JobId { get; }
    public abstract string JobName { get; }

    // ── 战斗状态跟踪 ─────────────────────────────────────
    /// <summary>本次战斗已持续的秒数</summary>
    protected double CombatTime { get; private set; }

    /// <summary>是否处于开幕期（战斗开始后 15 秒内）</summary>
    protected bool IsOpenerPhase => CombatTime < 15.0;

    protected BaseJobModule(IPluginLog log)
    {
        Log = log;
    }

    // ── 生命周期 ─────────────────────────────────────────

    /// <summary>战斗开始时重置内部状态</summary>
    public virtual void OnCombatStart()
    {
        CombatTime = 0;
        Log.Debug("[{0}] 战斗开始，内部状态已重置", JobName);
    }

    /// <summary>战斗结束时清理</summary>
    public virtual void OnCombatEnd()
    {
        Log.Debug("[{0}] 战斗结束，持续 {1:F1}s", JobName, CombatTime);
    }

    // ── 核心方法 ─────────────────────────────────────────

    /// <summary>由子类实现的决策逻辑</summary>
    public abstract JobDecision Evaluate(BattleSnapshot snapshot);

    /// <summary>
    /// 在 Evaluate 前调用以更新战斗计时（由 DecisionEngine 驱动）
    /// </summary>
    internal void UpdateCombatTime(double combatDurationSeconds)
    {
        CombatTime = combatDurationSeconds;
    }

    // ═══════════════════════════════════════════════════
    //  受保护的工具方法（供子类使用）
    // ═══════════════════════════════════════════════════

    /// <summary>GCD 是否已就绪（剩余时间 ≤ 0.1s 视为可用）</summary>
    protected static bool IsGcdReady(BattleSnapshot snap)
        => snap.Player.GcdRemaining <= 0.1f;

    /// <summary>在状态列表中查找指定 status ID 是否存在</summary>
    protected static bool HasBuff(StatusEffect[] buffs, ushort statusId)
    {
        // ⚡ 性能关键：手动遍历，不用 LINQ
        for (int i = 0; i < buffs.Length; i++)
        {
            if (buffs[i].StatusId == statusId) return true;
        }
        return false;
    }

    /// <summary>获取指定状态的剩余时间（秒），未找到返回 0</summary>
    protected static float GetBuffRemainingTime(StatusEffect[] buffs, ushort statusId)
    {
        for (int i = 0; i < buffs.Length; i++)
        {
            if (buffs[i].StatusId == statusId) return buffs[i].RemainingTime;
        }
        return 0f;
    }

    /// <summary>技能是否已就绪（冷却 ≤ 0）</summary>
    protected static bool IsActionReady(float cooldown)
        => cooldown <= 0f;

    /// <summary>安全计算 HP 百分比（0.0~1.0）</summary>
    protected static float CalculateHpPercent(uint hp, uint maxHp)
        => maxHp == 0 ? 0f : (float)hp / maxHp;

    /// <summary>
    /// 在小队中按照评分函数找到最优目标。
    /// ⚡ 零 LINQ 实现：手动遍历，找评分最高的有效成员。
    /// </summary>
    /// <param name="snap">战斗快照</param>
    /// <param name="scorer">评分函数，返回值越高越优先</param>
    /// <returns>最优目标，找不到时返回 null</returns>
    protected static PartyMemberState? FindBestTarget(
        BattleSnapshot snap,
        Func<PartyMemberState, float> scorer)
    {
        PartyMemberState? best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < snap.PartyMembers.Length; i++)
        {
            if (!snap.PartyMembers[i].HasValue) continue;
            var member = snap.PartyMembers[i]!.Value;
            float score = scorer(member);
            if (score > bestScore)
            {
                bestScore = score;
                best = member;
            }
        }

        return best;
    }

    // ═══════════════════════════════════════════════════
    //  GCD / oGCD 构建辅助（工厂方法）
    // ═══════════════════════════════════════════════════

    /// <summary>创建 GCD 推荐项</summary>
    protected static GcdAction Gcd(uint actionId, string actionName, float priority = 0f, uint targetId = 0)
        => new()
        {
            ActionId = actionId,
            ActionName = actionName,
            Priority = priority,
            TargetObjectId = targetId,
        };

    /// <summary>创建 oGCD 穿插推荐项</summary>
    protected static OgcdInsert Ogcd(uint actionId, string actionName, float priority = 0f, int insertAfterGcd = 0, uint targetId = 0)
        => new()
        {
            ActionId = actionId,
            ActionName = actionName,
            Priority = priority,
            InsertAfterGcdIndex = insertAfterGcd,
            TargetObjectId = targetId,
        };

    /// <summary>创建决策理由条目</summary>
    protected static ReasonEntry MakeReason(uint actionId, string templateKey, string formattedText, ReasonPriority priority = ReasonPriority.Info)
        => new()
        {
            ActionId = actionId,
            TemplateKey = templateKey,
            FormattedText = formattedText,
            Priority = priority,
        };
}

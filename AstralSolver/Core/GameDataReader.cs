using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using AstralSolver.Utils;

namespace AstralSolver.Core;

/// <summary>
/// <see cref="IGameDataReader"/> 的真实实现，封装所有 unsafe FFXIVClientStructs 调用。
/// 每帧采集时由 <see cref="StateTracker"/> 调用。
/// </summary>
internal sealed class GameDataReader : IGameDataReader
{
    private const int MaxStatusSlots = 60; // 游戏内 StatusManager 最多 60 个状态槽

    /// <summary>
    /// 从本地玩家对象读取玩家状态快照
    /// </summary>
    /// <param name="player">本地玩家游戏对象</param>
    /// <param name="gcdTotal">GCD 总时间（秒）</param>
    /// <param name="gcdRemaining">GCD 剩余冷却时间（秒）</param>
    /// <returns>不可变玩家状态快照</returns>
    public PlayerState ReadPlayerState(IPlayerCharacter player, float gcdTotal, float gcdRemaining)
    {
        // ⚡ 性能关键：直接构造 readonly record struct，无额外分配
        return new PlayerState
        {
            HP           = player.CurrentHp,
            MaxHP        = player.MaxHp,
            MP           = player.CurrentMp,
            MaxMP        = player.MaxMp,
            JobId        = (byte)player.ClassJob.RowId,
            Level        = (byte)player.Level,
            PosX         = player.Position.X,
            PosY         = player.Position.Y,
            PosZ         = player.Position.Z,
            GcdTotal     = gcdTotal,
            GcdRemaining = gcdRemaining,
            Buffs        = ReadStatusList(player),
        };
    }

    /// <summary>
    /// 通过 ActionManager 读取当前 GCD 速度（总时间与剩余时间）
    /// </summary>
    /// <returns>(GCD总时间, GCD剩余时间) 元组，均为秒</returns>
    public unsafe (float Total, float Remaining) ReadGcdInfo()
    {
        // ⚡ 性能关键：直接指针访问，无托管分配
        try
        {
            var am = ActionManager.Instance();
            if (am == null) return (2.5f, 0f);

            float total   = am->GetRecastTime(ActionType.Action, Constants.ActionIds.CommonGcd);
            float elapsed = am->GetRecastTimeElapsed(ActionType.Action, Constants.ActionIds.CommonGcd);
            float remaining = MathF.Max(0f, total - elapsed);
            return (total > 0f ? total : 2.5f, remaining);
        }
        catch
        {
            return (2.5f, 0f);
        }
    }

    /// <summary>
    /// 从玩家对象读取所有 Buff 状态列表
    /// </summary>
    /// <param name="player">目标玩家对象</param>
    /// <returns>有效 Buff 的状态快照数组</returns>
    public StatusEffect[] ReadStatusList(IPlayerCharacter player)
    {
        return ReadBattleCharaStatus(player);
    }

    /// <summary>
    /// 从小队成员对象读取 Buff 状态列表
    /// </summary>
    /// <param name="member">小队成员战斗角色对象</param>
    /// <returns>有效 Buff 的状态快照数组</returns>
    public StatusEffect[] ReadPartyMemberStatusList(IBattleChara member)
    {
        return ReadBattleCharaStatus(member);
    }

    /// <summary>
    /// 从目标 NPC 读取 Debuff 状态列表
    /// </summary>
    /// <param name="target">敌方战斗 NPC 对象</param>
    /// <returns>有效 Debuff 的状态快照数组</returns>
    public StatusEffect[] ReadTargetDebuffs(IBattleNpc target)
    {
        return ReadBattleCharaStatus(target);
    }

    /// <summary>
    /// 从 IBattleChara 接口统一读取状态列表（玩家/NPC/小队成员通用）
    /// </summary>
    private static StatusEffect[] ReadBattleCharaStatus(IBattleChara chara)
    {
        // ⚡ 性能关键：先计算有效状态数，再精确分配
        try
        {
            var statusList = chara.StatusList;
            int validCount = 0;
            for (int i = 0; i < statusList.Length; i++)
            {
                var s = statusList[i];
                if (s != null && s.StatusId != 0) validCount++;
            }

            if (validCount == 0) return Array.Empty<StatusEffect>();

            var result = new StatusEffect[validCount];
            int idx = 0;
            for (int i = 0; i < statusList.Length; i++)
            {
                var s = statusList[i];
                if (s == null || s.StatusId == 0) continue;
                result[idx++] = new StatusEffect
                {
                    StatusId      = (ushort)s.StatusId,
                    RemainingTime = s.RemainingTime,
                    StackCount    = (byte)s.Param,
                    SourceId      = s.SourceId,
                };
            }
            return result;
        }
        catch
        {
            return Array.Empty<StatusEffect>();
        }
    }

    /// <summary>
    /// 读取占星术士专属仪表盘状态（仅 JobId == 33 时执行）
    /// </summary>
    /// <param name="jobId">当前职业 ID</param>
    /// <returns>占星状态快照，非占星时为 null</returns>
    public AstrologianState? ReadAstrologianGauge(byte jobId)
    {
        if (jobId != Constants.JobIds.Astrologian) return null;

        try
        {
            return ReadAstrologianGaugeUnsafe();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 占星仪表盘实际读取（使用 AstrologianGauge struct + ActionManager CD）
    /// </summary>
    private unsafe AstrologianState? ReadAstrologianGaugeUnsafe()
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 📌 数据来源: FFXIVClientStructs AstrologianGauge
        // 📌 验证日期: 2026-02-23
        // 📌 兑容游戏版本: FFXIV 7.x (Dawntrail)
        // 📌 参考: RotationSolverReborn AST 实现
        //
        // AstrologianGauge.Cards (short) 位列布局:
        //   bits  0-3:  CurrentCards[0] = PlayI   槽位
        //   bits  4-7:  CurrentCards[1] = PlayII  槽位
        //   bits  8-11: CurrentCards[2] = PlayIII 槽位
        //   bits 12-15: CurrentArcana          = Minor Arcana 小奥秘卡
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // ① 获取占星仪表盘（直接通过 Astrologian 字段访问，无需 Get<T>()）
        var gaugeMgr = FFXIVClientStructs.FFXIV.Client.Game.JobGaugeManager.Instance();
        if (gaugeMgr == null) return null;
        var gauge = gaugeMgr->Astrologian;  // AstrologianGauge（union 字段 @ 0x08）

        // ② 解析卡牌位域（每张占 4-bit）— ⚡ 性能关键
        AstCard cardI   = (AstCard)(0xF & (gauge.Cards >> 0));
        AstCard cardII  = (AstCard)(0xF & (gauge.Cards >> 4));
        AstCard cardIII = (AstCard)(0xF & (gauge.Cards >> 8));
        AstCard arcana  = (AstCard)(0xF & (gauge.Cards >> 12));
        AstDraw draw    = (AstDraw)gauge.CurrentDraw;

        // ③ 读取各技能冷却 ― 通过 ActionManager 
        var am = ActionManager.Instance();
        if (am == null) return null;

        // AstralDraw / UmbralDraw 共用一个按键 ID，这里用 AstralDraw 来获取Draw CD
        float drawT  = am->GetRecastTime(ActionType.Action, Constants.AstActionIds.AstralDraw);
        float drawE  = am->GetRecastTimeElapsed(ActionType.Action, Constants.AstActionIds.AstralDraw);
        float drawCd = MathF.Max(0f, drawT - drawE);

        float playIT  = am->GetRecastTime(ActionType.Action, Constants.AstActionIds.PlayI);
        float playIE  = am->GetRecastTimeElapsed(ActionType.Action, Constants.AstActionIds.PlayI);
        float playICd = MathF.Max(0f, playIT - playIE);

        float playIIT  = am->GetRecastTime(ActionType.Action, Constants.AstActionIds.PlayII);
        float playIIE  = am->GetRecastTimeElapsed(ActionType.Action, Constants.AstActionIds.PlayII);
        float playIICd = MathF.Max(0f, playIIT - playIIE);

        float playIIIT  = am->GetRecastTime(ActionType.Action, Constants.AstActionIds.PlayIII);
        float playIIIE  = am->GetRecastTimeElapsed(ActionType.Action, Constants.AstActionIds.PlayIII);
        float playIIICd = MathF.Max(0f, playIIIT - playIIIE);

        float minorT  = am->GetRecastTime(ActionType.Action, Constants.AstActionIds.MinorArcana);
        float minorE  = am->GetRecastTimeElapsed(ActionType.Action, Constants.AstActionIds.MinorArcana);
        float minorCd = MathF.Max(0f, minorT - minorE);

        float divT  = am->GetRecastTime(ActionType.Action, Constants.AstActionIds.Divination);
        float divE  = am->GetRecastTimeElapsed(ActionType.Action, Constants.AstActionIds.Divination);
        float divCd = MathF.Max(0f, divT - divE);

        // ④ 手牌数量（判断是否可抽牌：全满 3 张时不可再抽）
        int handCount = (cardI   != AstCard.None ? 1 : 0) +
                        (cardII  != AstCard.None ? 1 : 0) +
                        (cardIII != AstCard.None ? 1 : 0);

        return new AstrologianState
        {
            CardPlayI           = cardI,
            CardPlayII          = cardII,
            CardPlayIII         = cardIII,
            CurrentArcana       = arcana,
            CurrentDraw         = draw,
            DrawCooldown        = drawCd,
            PlayICooldown       = playICd,
            PlayIICooldown      = playIICd,
            PlayIIICooldown     = playIIICd,
            MinorArcanaCooldown = minorCd,
            DivinationCooldown  = divCd,
            // 是否可抽牌: CD 为 0 且手牌未满
            CanDraw             = drawCd <= 0f && handCount < 3,
            // 各出牌槽位: 有牌且 GCD 冷却已完毕
            CanPlayI            = cardI   != AstCard.None && playICd   <= 0f,
            CanPlayII           = cardII  != AstCard.None && playIICd  <= 0f,
            CanPlayIII          = cardIII != AstCard.None && playIIICd <= 0f,
            // 小奥秘卡: 有牌且 CD 已完毕
            CanPlayArcana       = arcana  != AstCard.None && minorCd   <= 0f,
            CanUseDivination    = divCd   <= 0f,
        };
    }

    /// <summary>
    /// 通过 ActionManager 尝试释放技能。
    /// 返回 true 表示游戏接受了指令（UseAction 返回 1）。
    /// ⚡ 性能关键：直接指针调用，无托管分配。
    /// </summary>
    public unsafe bool TryUseAction(uint actionId, ulong targetObjectId = 0xE000_0000UL)
    {
        try
        {
            var am = ActionManager.Instance();
            if (am == null) return false;
            // UseAction 返回 bool（true = 成功发出，false = 无法使用）
            return am->UseAction(ActionType.Action, actionId, targetObjectId);
        }
        catch
        {
            return false;
        }
    }
}


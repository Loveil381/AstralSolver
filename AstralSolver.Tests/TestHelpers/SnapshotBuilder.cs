using System;
using System.Collections.Generic;
using AstralSolver.Core;
using AstralSolver.Utils;

namespace AstralSolver.Tests.TestHelpers;

/// <summary>
/// Builder 模式的快照构造辅助类。
/// 用于在测试中方便地构建特定场景的 BattleSnapshot。
/// 
/// 用法:
/// var snap = new SnapshotBuilder()
///     .WithPlayer(jobId: 33, hp: 80000, maxHp: 100000)
///     .WithGcdRemaining(0.0f)
///     .WithPartyMember("坦克", jobId: 19, hp: 30000, maxHp: 120000)
///     .WithTarget("Boss", hp: 5000000, maxHp: 10000000)
///     .WithAstrologianState(canDraw: true, drawCooldown: 0f)
///     .Build();
/// </summary>
public class SnapshotBuilder
{
    // ── 玩家默认值 ───────────────────────────────────────
    private byte _playerJobId = Constants.JobIds.Astrologian;
    private uint _playerHp = 100000;
    private uint _playerMaxHp = 100000;
    private uint _playerMp = 10000;
    private uint _playerMaxMp = 10000;
    private byte _playerLevel = 100;
    private float _gcdTotal = 2.50f;
    private float _gcdRemaining = 0f;
    private StatusEffect[] _playerBuffs = Array.Empty<StatusEffect>();

    // ── 队伍 ─────────────────────────────────────────────
    private readonly List<PartyMemberState> _partyMembers = new();

    // ── 目标 ─────────────────────────────────────────────
    private TargetState? _target;

    // ── 占星状态 ─────────────────────────────────────────
    private AstrologianState? _astState;

    // ── 战斗状态 ─────────────────────────────────────────
    private bool _isInCombat = true;
    private double _combatDuration = 30.0;
    private long _frameNumber = 100;

    // ═══════════════════════════════════════════════════
    //  Builder 方法
    // ═══════════════════════════════════════════════════

    public SnapshotBuilder WithPlayer(
        byte jobId = 33, uint hp = 100000, uint maxHp = 100000,
        uint mp = 10000, uint maxMp = 10000, byte level = 100)
    {
        _playerJobId = jobId;
        _playerHp = hp;
        _playerMaxHp = maxHp;
        _playerMp = mp;
        _playerMaxMp = maxMp;
        _playerLevel = level;
        return this;
    }

    public SnapshotBuilder WithPlayerBuffs(params StatusEffect[] buffs)
    {
        _playerBuffs = buffs;
        return this;
    }

    public SnapshotBuilder WithGcdRemaining(float remaining, float total = 2.50f)
    {
        _gcdRemaining = remaining;
        _gcdTotal = total;
        return this;
    }

    public SnapshotBuilder WithPartyMember(
        string name, byte jobId, uint hp, uint maxHp,
        uint objectId = 0, StatusEffect[]? buffs = null)
    {
        if (objectId == 0) objectId = (uint)(1000 + _partyMembers.Count);
        _partyMembers.Add(new PartyMemberState
        {
            ObjectId = objectId,
            Name = name,
            JobId = jobId,
            HP = hp,
            MaxHP = maxHp,
            PosX = 100f,
            PosY = 100f,
            PosZ = 0f,
            DistanceFromPlayer = 5f,
            Buffs = buffs ?? Array.Empty<StatusEffect>(),
        });
        return this;
    }

    public SnapshotBuilder WithTarget(
        string name = "Boss", uint hp = 5000000, uint maxHp = 10000000,
        uint objectId = 9999, bool isCasting = false, uint castActionId = 0,
        StatusEffect[]? debuffs = null)
    {
        _target = new TargetState
        {
            ObjectId = objectId,
            Name = name,
            HP = hp,
            MaxHP = maxHp,
            Debuffs = debuffs ?? Array.Empty<StatusEffect>(),
            IsCasting = isCasting,
            CastActionId = castActionId,
            CastProgress = 0f,
            CastTotal = 0f,
        };
        return this;
    }

    public SnapshotBuilder WithAstrologianState(
        AstCard cardPlayI = AstCard.None,
        AstCard cardPlayII = AstCard.None,
        AstCard cardPlayIII = AstCard.None,
        AstCard currentArcana = AstCard.None,
        AstDraw currentDraw = AstDraw.Astral,
        float drawCooldown = 55f,
        float divinationCooldown = 120f,
        bool canDraw = false,
        bool canPlayI = false,
        bool canPlayII = false,
        bool canPlayIII = false,
        bool canPlayArcana = false,
        bool canUseDivination = false)
    {
        _astState = new AstrologianState
        {
            CardPlayI = cardPlayI,
            CardPlayII = cardPlayII,
            CardPlayIII = cardPlayIII,
            CurrentArcana = currentArcana,
            CurrentDraw = currentDraw,
            DrawCooldown = drawCooldown,
            PlayICooldown = canPlayI ? 0f : 1f,
            PlayIICooldown = canPlayII ? 0f : 1f,
            PlayIIICooldown = canPlayIII ? 0f : 1f,
            MinorArcanaCooldown = canPlayArcana ? 0f : 1f,
            DivinationCooldown = divinationCooldown,
            CanDraw = canDraw,
            CanPlayI = canPlayI,
            CanPlayII = canPlayII,
            CanPlayIII = canPlayIII,
            CanPlayArcana = canPlayArcana,
            CanUseDivination = canUseDivination,
        };
        return this;
    }

    public SnapshotBuilder WithCombatState(bool inCombat = true, double duration = 30.0)
    {
        _isInCombat = inCombat;
        _combatDuration = duration;
        return this;
    }

    public SnapshotBuilder WithFrameNumber(long frame)
    {
        _frameNumber = frame;
        return this;
    }

    // ═══════════════════════════════════════════════════
    //  构建
    // ═══════════════════════════════════════════════════

    public BattleSnapshot Build()
    {
        // 构建队伍数组（8 槽）
        var partyArray = new PartyMemberState?[8];
        for (int i = 0; i < _partyMembers.Count && i < 8; i++)
            partyArray[i] = _partyMembers[i];

        // 默认占星状态（如果玩家是占星且未手动设置）
        var astState = _astState;
        if (astState == null && _playerJobId == Constants.JobIds.Astrologian)
        {
            astState = new AstrologianState(); // 全部默认值
        }

        return new BattleSnapshot
        {
            FrameNumber = _frameNumber,
            Timestamp = DateTime.UtcNow,
            IsInCombat = _isInCombat,
            CombatDurationSeconds = _combatDuration,
            Player = new PlayerState
            {
                HP = _playerHp,
                MaxHP = _playerMaxHp,
                MP = _playerMp,
                MaxMP = _playerMaxMp,
                JobId = _playerJobId,
                Level = _playerLevel,
                PosX = 100f,
                PosY = 100f,
                PosZ = 0f,
                GcdTotal = _gcdTotal,
                GcdRemaining = _gcdRemaining,
                Buffs = _playerBuffs,
            },
            PartyMembers = partyArray,
            PartyMemberCount = _partyMembers.Count,
            CurrentTarget = _target,
            Astrologian = astState,
        };
    }
}

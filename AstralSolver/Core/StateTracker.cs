using System;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using AstralSolver.Utils;

namespace AstralSolver.Core;

/// <summary>
/// 状态追踪器：每帧从 Dalamud API 读取游戏状态，构建不可变快照 <see cref="BattleSnapshot"/>。
/// 这是整个插件的数据地基，内部用 <see cref="IGameDataReader"/> 隔离所有 unsafe 调用。
/// </summary>
public sealed class StateTracker : IDisposable
{
    // ── 依赖项 ───────────────────────────────────────────────────────────────
    private readonly IObjectTable    _objectTable;
    private readonly IPartyList      _partyList;
    private readonly ICondition      _condition;
    private readonly ITargetManager  _targetManager;
    private readonly IFramework      _framework;
    private readonly IPluginLog      _log;
    private readonly Configuration   _config;
    private readonly IGameDataReader _dataReader;

    // ── 状态 ─────────────────────────────────────────────────────────────────
    private readonly SnapshotRing<BattleSnapshot> _ring;
    private BattleSnapshot _current = BattleSnapshot.Empty;

    /// <summary>是否已处于战斗状态（由条件标志驱动）</summary>
    private bool _isInCombat;
    /// <summary>上一帧是否在战斗中</summary>
    private bool _wasInCombat;
    /// <summary>战斗开始时间</summary>
    private DateTime _combatStartTime;
    /// <summary>是否已检测到本地玩家</summary>
    private bool _playerDetected;
    /// <summary>本次战斗已采集的帧数</summary>
    private long _combatFrameCount;

    // ── 性能监控 ──────────────────────────────────────────────────────────────
    private readonly Stopwatch _sw = new();
    private long   _frameCount;
    private int    _idleFrameCounter;
    private double _totalFrameMs;
    private double _maxFrameMs;
    private bool   _disposed;

    // ── 预分配工作数组（避免每帧堆分配）────────────────────────────────────────
    private readonly PartyMemberState?[] _partyWorkBuffer = new PartyMemberState?[8];

    /// <summary>
    /// 创建 StateTracker 实例并立即开始监听框架更新事件。
    /// </summary>
    /// <param name="objectTable">对象表服务（获取本地玩家）</param>
    /// <param name="partyList">小队列表服务</param>
    /// <param name="condition">条件标志服务（战斗/副本判断）</param>
    /// <param name="targetManager">目标管理服务（获取当前选中目标）</param>
    /// <param name="framework">框架更新钩子</param>
    /// <param name="log">日志服务</param>
    /// <param name="config">插件配置</param>
    /// <param name="dataReader">游戏原始数据读取器（可被 Mock）</param>
    public StateTracker(
        IObjectTable    objectTable,
        IPartyList      partyList,
        ICondition      condition,
        ITargetManager  targetManager,
        IFramework      framework,
        IPluginLog      log,
        Configuration   config,
        IGameDataReader dataReader)
    {
        _objectTable   = objectTable;
        _partyList     = partyList;
        _condition     = condition;
        _targetManager = targetManager;
        _framework     = framework;
        _log           = log;
        _config        = config;
        _dataReader    = dataReader;

        // 预分配环形缓冲区（容量由配置决定）
        _ring = new SnapshotRing<BattleSnapshot>(config.SnapshotRingCapacity);

        // 注册每帧更新事件
        _framework.Update += OnFrameworkUpdate;

        _log.Information("[StateTracker] 初始化完成，缓冲容量={0} 帧", config.SnapshotRingCapacity);
    }

    // ── 公开属性 ─────────────────────────────────────────────────────────────

    /// <summary>最新的战斗快照，StateTracker 未就绪时返回 <see cref="BattleSnapshot.Empty"/></summary>
    public BattleSnapshot Current => _current;

    /// <summary>当前是否处于战斗状态</summary>
    public bool IsInCombat => _isInCombat;

    /// <summary>自插件启动后，StateTracker 已处理的总帧数</summary>
    public long FrameCount => _frameCount;

    /// <summary>平均每帧采集耗时（毫秒）</summary>
    public double AverageFrameTimeMs => _frameCount > 0 ? _totalFrameMs / _frameCount : 0.0;

    /// <summary>
    /// 获取最近 N 帧的快照历史记录（从旧到新排列）。
    /// </summary>
    /// <param name="count">要获取的帧数</param>
    /// <returns>历史快照数组</returns>
    public BattleSnapshot[] GetHistory(int count) => _ring.GetLastN(count);

    // ── 核心帧更新循环 ────────────────────────────────────────────────────────

    /// <summary>
    /// 框架更新钩子，每帧由 <see cref="IFramework"/> 触发。
    /// </summary>
    /// <param name="framework">框架实例（未使用）</param>
    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            TickFrame();
        }
        catch (Exception ex)
        {
            // ⚠ 帧回调内任何异常只记录，绝不向外抛出
            _log.Error(ex, "[StateTracker] OnFrameworkUpdate 发生未处理异常（第 {0} 帧）", _frameCount);
        }
    }

    /// <summary>
    /// 实际帧处理逻辑，从 OnFrameworkUpdate 内调用以便 try-catch 包裹。
    /// </summary>
    private void TickFrame()
    {
        // ① 检查本地玩家是否存在
        var localPlayer = _objectTable.LocalPlayer;
        if (localPlayer == null) return;

        // a. 首次检测到玩家
        if (!_playerDetected)
        {
            _playerDetected = true;
            // 输出详细的 ClassJob 信息，方便在游戏内对照 JobId 是否与 Constants 定义匹配
            _log.Information(
                "[StateTracker] 检测到玩家: {0} | Lv.{1} | ClassJob.RowId={2} | 预期占星 JobId=33",
                localPlayer.Name,
                localPlayer.Level,
                localPlayer.ClassJob.RowId);
        }

        // ② 读取当前帧战斗状态
        _isInCombat = _condition[ConditionFlag.InCombat];

        // b. 退出战斗检测
        if (!_isInCombat && _wasInCombat)
        {
            var duration = (DateTime.UtcNow - _combatStartTime).TotalSeconds;
            _log.Information("[StateTracker] 🏁 战斗结束，持续 {0:F1}秒，共采集 {1} 帧",
                duration, _combatFrameCount);
        }

        // ③ 进入战斗检测（仅状态重置与基础 log）
        bool justEnteredCombat = _isInCombat && !_wasInCombat;
        if (justEnteredCombat)
        {
            _log.Information("[StateTracker] ⚔️ 进入战斗");
            _combatStartTime = DateTime.UtcNow;
            _combatFrameCount = 0;
        }

        // ④ 降频采集逻辑：非战斗中每 N 帧才采集一次
        if (!_isInCombat)
        {
            _idleFrameCounter++;
            if (_idleFrameCounter < _config.IdlePollingInterval)
            {
                _wasInCombat = _isInCombat;
                return;
            }
            _idleFrameCounter = 0;
        }
        else
        {
            _idleFrameCounter = 0;
            _combatFrameCount++; // 战斗中每帧计数
        }

        // ⑤ 计时并构建快照
        _sw.Restart();
        var snapshot = BuildSnapshot(localPlayer);
        _sw.Stop();

        // f. 采集耗时预警
        double elapsedMs = _sw.Elapsed.TotalMilliseconds;
        if (elapsedMs > 2.0)
        {
            _log.Warning("[StateTracker] ⚠️ 帧采集超时: {0:F2}ms（阈值 2ms）", elapsedMs);
        }

        // e. 占星模式激活日志
        if (justEnteredCombat && snapshot.Astrologian.HasValue)
        {
            var ast = snapshot.Astrologian.Value;
            _log.Information("[StateTracker] 🃏 占星模式激活 | 手牌:{0}张 | 占卜CD:{1:F1}s",
                ast.HandCount, ast.DivinationCooldown);
        }

        // ⑥ 更新统计并推入环形缓冲
        _totalFrameMs += elapsedMs;
        if (elapsedMs > _maxFrameMs) _maxFrameMs = elapsedMs;
        _frameCount++;
        _wasInCombat = _isInCombat;

        _current = snapshot;
        _ring.Push(snapshot);

        // ⑦ 每 300 帧输出一次性能摘要
        if (_frameCount % 300 == 0)
        {
            _log.Information(
                "[StateTracker] 📊 采集性能 | 平均:{0:F2}ms 最大:{1:F2}ms | 队伍:{2}人 | 目标:{3}",
                AverageFrameTimeMs, _maxFrameMs, snapshot.PartyMemberCount, snapshot.CurrentTarget != null ? "有" : "无");
            _maxFrameMs = 0; // 重置最大值时间窗
        }
    }

    // ── 快照构建 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 构建当帧的 <see cref="BattleSnapshot"/>。
    /// 所有 unsafe 调用均委托给 <see cref="IGameDataReader"/>。
    /// </summary>
    /// <param name="localPlayer">本地玩家对象（已确认非 null）</param>
    /// <returns>不可变快照</returns>
    private BattleSnapshot BuildSnapshot(IPlayerCharacter localPlayer)
    {
        // ─ GCD 信息 ───────────────────────────────────────────────────────────
        // ⚡ 性能关键：优先读取 GCD，其结果注入 PlayerState
        var (gcdTotal, gcdRemaining) = _dataReader.ReadGcdInfo();

        // ─ 玩家状态 ───────────────────────────────────────────────────────────
        var playerState = _dataReader.ReadPlayerState(localPlayer, gcdTotal, gcdRemaining);

        // ─ 战斗持续时间 ────────────────────────────────────────────────────────
        double combatDuration = _isInCombat && _combatStartTime != DateTime.MinValue
            ? (DateTime.UtcNow - _combatStartTime).TotalSeconds
            : 0.0;

        // ─ 小队成员状态 ────────────────────────────────────────────────────────
        // ⚡ 性能关键：复用预分配工作数组，写完后复制到新数组
        for (int i = 0; i < _partyWorkBuffer.Length; i++)
            _partyWorkBuffer[i] = null;

        int partyCount = 0;
        int partyLen   = _partyList.Length;
        for (int i = 0; i < partyLen && i < 8; i++)
        {
            var member = _partyList[i];
            if (member == null) continue;

            // 尝试通过 EntityId 在对象表中定位到实际的 IBattleChara
            var memberChara = _objectTable.SearchByEntityId(member.EntityId) as IBattleChara;

            float dx = memberChara != null ? memberChara.Position.X - localPlayer.Position.X : 0f;
            float dy = memberChara != null ? memberChara.Position.Y - localPlayer.Position.Y : 0f;
            float dz = memberChara != null ? memberChara.Position.Z - localPlayer.Position.Z : 0f;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

            StatusEffect[] memberBuffs = memberChara != null
                ? _dataReader.ReadPartyMemberStatusList(memberChara)
                : Array.Empty<StatusEffect>();

            _partyWorkBuffer[partyCount++] = new PartyMemberState
            {
                ObjectId           = member.EntityId,
                Name               = member.Name.ToString(),
                JobId              = (byte)member.ClassJob.RowId,
                HP                 = member.CurrentHP,
                MaxHP              = member.MaxHP,
                PosX               = memberChara?.Position.X ?? 0f,
                PosY               = memberChara?.Position.Y ?? 0f,
                PosZ               = memberChara?.Position.Z ?? 0f,
                DistanceFromPlayer = dist,
                Buffs              = memberBuffs,
            };
        }

        // 将工作数组快照复制到不可变数组
        var partySnapshot = new PartyMemberState?[8];
        for (int i = 0; i < partyCount; i++)
            partySnapshot[i] = _partyWorkBuffer[i];

        // ─ 目标状态 ───────────────────────────────────────────────────────────
        TargetState? targetState = null;
        if (_targetManager.Target is IBattleNpc targetNpc)
        {
            targetState = new TargetState
            {
                ObjectId    = targetNpc.EntityId,
                Name        = targetNpc.Name.ToString(),
                HP          = targetNpc.CurrentHp,
                MaxHP       = targetNpc.MaxHp,
                Debuffs     = _dataReader.ReadTargetDebuffs(targetNpc),
                IsCasting   = targetNpc.IsCasting,
                CastActionId = targetNpc.CastActionId,
                CastProgress = targetNpc.CurrentCastTime,
                CastTotal   = targetNpc.TotalCastTime,
            };
        }

        // ─ 占星仪表盘 ─────────────────────────────────────────────────────────
        AstrologianState? astState = _dataReader.ReadAstrologianGauge(playerState.JobId);

        // ─ 组装最终快照 ────────────────────────────────────────────────────────
        return new BattleSnapshot
        {
            FrameNumber           = _frameCount,
            Timestamp             = DateTime.UtcNow,
            IsInCombat            = _isInCombat,
            CombatDurationSeconds = combatDuration,
            Player                = playerState,
            PartyMembers          = partySnapshot,
            PartyMemberCount      = partyCount,
            CurrentTarget         = targetState,
            Astrologian           = astState,
        };
    }

    // ── 资源释放 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 释放 StateTracker 资源，取消框架更新注册并清空缓冲区。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _framework.Update -= OnFrameworkUpdate;
        _ring.Clear();

        _log.Information("[StateTracker] 已释放，共处理 {0} 帧", _frameCount);
    }
}

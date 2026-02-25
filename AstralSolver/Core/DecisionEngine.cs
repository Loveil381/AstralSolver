using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin.Services;

namespace AstralSolver.Core;

/// <summary>
/// 决策引擎：插件的"大脑"。
/// 接收 StateTracker 的 BattleSnapshot，经过分层决策后输出 DecisionPacket。
/// 分层架构：安全层（保命）→ 职业逻辑层（输出+卡牌）→ 合并输出。
/// </summary>
public sealed class DecisionEngine : IDisposable
{
    // ── 依赖 ─────────────────────────────────────────────
    private readonly StateTracker  _stateTracker;
    private readonly Configuration _config;
    private readonly IPluginLog    _log;

    // ── 职业模块注册表 ────────────────────────────────────
    private readonly Dictionary<byte, Jobs.IJobModule> _jobModules = new();

    // ── 状态 ─────────────────────────────────────────────
    private DecisionPacket _lastPacket = DecisionPacket.Empty;
    private bool _wasInCombat;
    private bool _disposed;

    // ── 性能监控 ─────────────────────────────────────────
    private readonly Stopwatch _sw = new();
    private long   _decisionCount;
    private double _totalDecisionMs;
    private double _maxDecisionMs;

    // ── 安全层阈值 ────────────────────────────────────────
    /// <summary>玩家 HP% 低于此值触发自保</summary>
    private const float SelfHpThreshold = 0.30f;
    /// <summary>队友 HP% 低于此值触发紧急治疗提升</summary>
    private const float AllyHpThreshold = 0.20f;

    // ── 事件 ─────────────────────────────────────────────
    /// <summary>每次决策更新后触发（供 Navigator UI 订阅）</summary>
    public event Action<DecisionPacket>? OnDecisionUpdated;

    /// <summary>最新的决策包</summary>
    public DecisionPacket LastPacket => _lastPacket;

    // ── 构造 ─────────────────────────────────────────────

    /// <summary>
    /// 创建决策引擎实例。
    /// </summary>
    /// <param name="stateTracker">状态追踪器（数据源）</param>
    /// <param name="config">插件配置</param>
    /// <param name="log">日志服务</param>
    public DecisionEngine(StateTracker stateTracker, Configuration config, IPluginLog log)
    {
        _stateTracker = stateTracker;
        _config       = config;
        _log          = log;

        _log.Information("[DecisionEngine] 初始化完成");
    }

    // ── 职业模块注册 ─────────────────────────────────────

    /// <summary>
    /// 注册职业决策模块。
    /// 同一职业 ID 重复注册将覆盖旧模块。
    /// </summary>
    public void RegisterJobModule(byte jobId, Jobs.IJobModule module)
    {
        _jobModules[jobId] = module;
        _log.Information("[DecisionEngine] 注册职业模块: {0} (JobId={1})", module.JobName, jobId);
    }

    // ── 核心更新循环 ─────────────────────────────────────

    /// <summary>
    /// 每帧由 Plugin.OnUpdate 调用。
    /// 执行完整的分层决策：安全层 → 职业模块 → 合并输出。
    /// </summary>
    public void Update()
    {
        // ① 获取最新快照
        var snapshot = _stateTracker.Current;
        if (snapshot == BattleSnapshot.Empty) return;

        // ② 战斗状态切换检测（无论是否在战斗中都需执行，以确保 OnCombatEnd 被调用）
        HandleCombatStateTransitions(snapshot);

        // 非战斗状态不产生决策
        if (!snapshot.IsInCombat)
        {
            // 首次检测到非战斗状态的玩家时，仍然输出职业信息
            if (_decisionCount == 0)
            {
                _log.Information("[DecisionEngine] 非战斗状态 | JobId={0} | 已注册模块: [{1}]",
                    snapshot.Player.JobId, string.Join(", ", _jobModules.Keys));
            }
            return;
        }

        // ③ 判断决策模式
        var mode = MapConfigToDecisionMode();
        if (mode == DecisionMode.Disabled) return;

        _sw.Restart();

        // ④ 安全层检查
        var safetyReasons = new List<ReasonEntry>();
        var safetyOgcds   = new List<OgcdInsert>();
        SafetyCheck(snapshot, safetyReasons, safetyOgcds);

        // ⑤ 查找并调用职业模块
        JobDecision jobDecision = JobDecision.Empty;
        byte jobId = snapshot.Player.JobId;

        if (_jobModules.TryGetValue(jobId, out var module))
        {
            // 更新基类的战斗计时
            if (module is Jobs.BaseJobModule baseModule)
                baseModule.UpdateCombatTime(snapshot.CombatDurationSeconds);

            jobDecision = module.Evaluate(snapshot);
        }
        else
        {
            // 找不到对应职业模块时输出警告，方便上线调试职业 ID 是否匹配
            _log.Warning("[DecisionEngine] 未找到 JobId={0} 的职业模块，已注册的模块: [{1}]",
                jobId, string.Join(", ", _jobModules.Keys));
        }

        // ⑥ 合并安全层覆盖 + 职业决策 → 最终 DecisionPacket
        var packet = MergeDecision(jobDecision, safetyReasons, safetyOgcds, mode);

        _sw.Stop();

        // ⑦ 更新引擎状态 & 触发事件
        _lastPacket = packet;
        _decisionCount++;
        double elapsedMs = _sw.Elapsed.TotalMilliseconds;
        _totalDecisionMs += elapsedMs;
        if (elapsedMs > _maxDecisionMs) _maxDecisionMs = elapsedMs;

        OnDecisionUpdated?.Invoke(packet);

        // ⑧ 性能警告 & 定期日志
        if (elapsedMs > 3.0)
        {
            _log.Warning("[DecisionEngine] ⚠️ 决策超时: {0:F2}ms（阈值 3ms）", elapsedMs);
        }

        if (_decisionCount % 300 == 0)
        {
            double avgMs = _decisionCount > 0 ? _totalDecisionMs / _decisionCount : 0;
            _log.Information("[DecisionEngine] 📊 决策性能 | 平均:{0:F2}ms 最大:{1:F2}ms | 模式:{2}",
                avgMs, _maxDecisionMs, mode);
            _maxDecisionMs = 0;
        }
    }

    // ── 安全层 ───────────────────────────────────────────

    /// <summary>
    /// 安全层检查：玩家/队友生命危机时强制插入保命动作。
    /// </summary>
    private void SafetyCheck(
        BattleSnapshot snapshot,
        List<ReasonEntry> reasons,
        List<OgcdInsert> ogcds)
    {
        // 自保：玩家 HP < 30%
        if (snapshot.Player.HpPercent < SelfHpThreshold)
        {
            reasons.Add(new ReasonEntry
            {
                ActionId = 0,
                TemplateKey = "safety.self_low_hp",
                FormattedText = $"⚠️ 玩家 HP 低于 {SelfHpThreshold * 100:F0}%，优先自保",
                Priority = ReasonPriority.Critical,
            });
        }

        // 紧急治疗：有队友 HP < 20%
        int criticalCount = snapshot.GetPartyMembersBelow(AllyHpThreshold);
        if (criticalCount > 0)
        {
            reasons.Add(new ReasonEntry
            {
                ActionId = 0,
                TemplateKey = "safety.ally_critical",
                FormattedText = $"🚨 {criticalCount} 名队友 HP 低于 {AllyHpThreshold * 100:F0}%，紧急治疗",
                Priority = ReasonPriority.Critical,
            });
        }
    }

    // ── 战斗状态切换 ─────────────────────────────────────

    /// <summary>检测战斗进入/退出，通知已注册的职业模块</summary>
    private void HandleCombatStateTransitions(BattleSnapshot snapshot)
    {
        bool inCombat = snapshot.IsInCombat;

        // 进入战斗
        if (inCombat && !_wasInCombat)
        {
            foreach (var kvp in _jobModules)
                kvp.Value.OnCombatStart();
        }

        // 退出战斗（理论上 Update 在战斗中才调用，但做防御性检查）
        if (!inCombat && _wasInCombat)
        {
            foreach (var kvp in _jobModules)
                kvp.Value.OnCombatEnd();
        }

        _wasInCombat = inCombat;
    }

    // ── 决策合并 ─────────────────────────────────────────

    /// <summary>
    /// 将职业决策与安全层覆盖合并为最终 DecisionPacket。
    /// 安全层理由追加到理由列表头部（优先显示）。
    /// </summary>
    private static DecisionPacket MergeDecision(
        JobDecision jobDecision,
        List<ReasonEntry> safetyReasons,
        List<OgcdInsert> safetyOgcds,
        DecisionMode mode)
    {
        // 合并理由：安全层在前，职业决策在后
        var allReasons = new ReasonEntry[safetyReasons.Count + jobDecision.Reasons.Length];
        for (int i = 0; i < safetyReasons.Count; i++)
            allReasons[i] = safetyReasons[i];
        for (int i = 0; i < jobDecision.Reasons.Length; i++)
            allReasons[safetyReasons.Count + i] = jobDecision.Reasons[i];

        // 合并 oGCD：安全层在前
        var allOgcds = new OgcdInsert[safetyOgcds.Count + jobDecision.OgcdInserts.Length];
        for (int i = 0; i < safetyOgcds.Count; i++)
            allOgcds[i] = safetyOgcds[i];
        for (int i = 0; i < jobDecision.OgcdInserts.Length; i++)
            allOgcds[safetyOgcds.Count + i] = jobDecision.OgcdInserts[i];

        return new DecisionPacket
        {
            GcdQueue    = jobDecision.GcdQueue,
            OgcdInserts = allOgcds,
            Hold        = jobDecision.Hold,
            Reasons     = allReasons,
            JobPanel    = jobDecision.JobSpecificPanel,
            Confidence  = jobDecision.Confidence,
            Mode        = mode,
        };
    }

    // ── 配置映射 ─────────────────────────────────────────

    /// <summary>将用户配置转换为决策模式</summary>
    private DecisionMode MapConfigToDecisionMode()
    {
        if (!_config.IsEnabled) return DecisionMode.Disabled;
        return _config.Mode;
    }

    // ── 资源释放 ─────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _jobModules.Clear();
        _log.Information("[DecisionEngine] 已释放，共执行 {0} 次决策", _decisionCount);
    }
}

using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AstralSolver.Core;

// ═══════════════════════════════════════════════════
//  接口定义
// ═══════════════════════════════════════════════════

/// <summary>
/// 技能执行器接口。
/// 在自动模式下，将 DecisionEngine 输出的决策转化为游戏内操作。
/// </summary>
public interface IActionExecutor : IDisposable
{
    /// <summary>
    /// 提交新的决策包。会替换当前待执行列表（而非追加）。
    /// 只在 DecisionMode.Auto 时实际生效。
    /// </summary>
    void SubmitDecision(DecisionPacket packet);

    /// <summary>每帧由 Plugin.OnUpdate 调用，负责按时序执行技能</summary>
    void Tick(BattleSnapshot snapshot);

    /// <summary>当前队列中待执行的动作数（GCD + oGCD）</summary>
    int PendingCount { get; }

    /// <summary>上一个成功执行的技能 ID（0 = 尚未执行过）</summary>
    uint LastExecutedAction { get; }

    /// <summary>是否处于暂停状态（连续失败超阈值时自动置true）</summary>
    bool IsPaused { get; set; }
}

// ═══════════════════════════════════════════════════
//  ActionQueue 实现
// ═══════════════════════════════════════════════════

/// <summary>
/// 技能执行队列。
/// 接收 DecisionPacket，按 GCD/oGCD 时序向游戏发出技能指令。
/// 
/// 安全保护：
///   - Navigator/Disabled 模式 → 不发出任何操作
///   - IsPaused = true       → 不发出任何操作
///   - 连续失败 5 次         → 自动暂停并警告
/// </summary>
public sealed class ActionQueue : IActionExecutor
{
    // ── 依赖 ─────────────────────────────────────────────
    private readonly IPluginLog    _log;
    private readonly Configuration _config;
    private readonly IGameDataReader _dataReader;

    // ── 待执行状态 ────────────────────────────────────────
    private GcdAction?          _pendingGcd;
    private OgcdInsert[]        _pendingOgcds = Array.Empty<OgcdInsert>();
    private DecisionMode        _currentMode  = DecisionMode.Disabled;

    // ── 安全保护 ─────────────────────────────────────────
    private int  _consecutiveFailures;
    private bool _isPaused;
    private bool _disposed;

    // ── 统计 ─────────────────────────────────────────────
    private uint _lastExecutedAction;
    private int  _totalExecuted;

    // ── 常量 ─────────────────────────────────────────────
    private const int   MAX_CONSECUTIVE_FAILURES = 5;
    /// <summary>GCD 剩余时间低于此值视为"GCD 窗口已就绪"</summary>
    private const float GCD_READY_THRESHOLD = 0.5f;
    /// <summary>GCD 剩余时间高于此值且低于 GcdTotal-0.5 时，可穿插 oGCD</summary>
    private const float OGCD_WINDOW_MIN = 1.5f;

    // ── 接口属性 ─────────────────────────────────────────
    public int  PendingCount       => (_pendingGcd.HasValue ? 1 : 0) + _pendingOgcds.Length;
    public uint LastExecutedAction => _lastExecutedAction;
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused == value) return;
            _isPaused = value;
            _log.Information("[ActionQueue] 暂停状态变更 → {0}", value ? "已暂停 ⏸️" : "已恢复 ▶️");
        }
    }

    // ── 构造 ─────────────────────────────────────────────

    public ActionQueue(IPluginLog log, Configuration config, IGameDataReader dataReader)
    {
        _log        = log;
        _config     = config;
        _dataReader = dataReader;
        _log.Information("[ActionQueue] 初始化完成");
    }

    // ═══════════════════════════════════════════════════
    //  核心方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 提交新的决策包。本次决策替换（而非追加）之前的待执行列表。
    /// </summary>
    public void SubmitDecision(DecisionPacket packet)
    {
        // Navigator / Disabled 模式不接受自动执行决策
        if (packet.Mode != DecisionMode.Auto)
        {
            _currentMode = packet.Mode;
            _pendingGcd   = null;
            _pendingOgcds = Array.Empty<OgcdInsert>();
            return;
        }

        _currentMode = packet.Mode;

        // 取第一个 GCD（优先级最高）
        _pendingGcd = packet.GcdQueue.Length > 0 ? packet.GcdQueue[0] : (GcdAction?)null;

        // 全量替换 oGCD 列表
        _pendingOgcds = packet.OgcdInserts;
    }

    /// <summary>
    /// 每帧调用。检查 GCD/oGCD 时机后在合适的窗口执行技能。
    /// </summary>
    public void Tick(BattleSnapshot snapshot)
    {
        // ① 安全保护
        if (_isPaused)                        return;
        if (_currentMode != DecisionMode.Auto) return;
        if (snapshot == BattleSnapshot.Empty)  return;

        float gcdRemaining = snapshot.Player.GcdRemaining;
        float gcdTotal     = snapshot.Player.GcdTotal;

        // ② GCD 窗口 — GCD 剩余时间 ≤ 0.5s
        if (gcdRemaining <= GCD_READY_THRESHOLD && _pendingGcd.HasValue)
        {
            var gcd = _pendingGcd.Value;
            if (ExecuteAction(gcd.ActionId, gcd.TargetObjectId))
            {
                _log.Debug("[ActionQueue] ✅ GCD 释放: {0} (ID:{1})", gcd.ActionName, gcd.ActionId);
                _pendingGcd = null;
            }
            return; // 本帧只发 GCD，oGCD 等下帧
        }

        // ③ oGCD 窗口 — GCD 冷却中段，可穿插 oGCD
        if (gcdRemaining > OGCD_WINDOW_MIN && gcdRemaining < gcdTotal - GCD_READY_THRESHOLD)
        {
            ExecutePendingOgcds();
        }
    }

    // ═══════════════════════════════════════════════════
    //  私有执行逻辑
    // ═══════════════════════════════════════════════════

    /// <summary>执行所有待穿插的 oGCD（每次最多穿插 2 个，避免过于激进）</summary>
    private void ExecutePendingOgcds()
    {
        if (_pendingOgcds.Length == 0) return;

        int executed = 0;
        var remaining = new List<OgcdInsert>(_pendingOgcds.Length);

        for (int i = 0; i < _pendingOgcds.Length; i++)
        {
            if (executed >= 2) // 双插限制
            {
                remaining.Add(_pendingOgcds[i]);
                continue;
            }

            var ogcd = _pendingOgcds[i];
            if (ExecuteAction(ogcd.ActionId, ogcd.TargetObjectId))
            {
                _log.Debug("[ActionQueue] ✅ oGCD 释放: {0} (ID:{1})", ogcd.ActionName, ogcd.ActionId);
                executed++;
            }
            else
            {
                remaining.Add(ogcd); // 执行失败的保留到下帧
            }
        }

        _pendingOgcds = remaining.ToArray();
    }

    /// <summary>
    /// 通过 IGameDataReader 执行技能，封装失败计数和日志。
    /// </summary>
    /// <returns>true = 成功；false = 失败</returns>
    private bool ExecuteAction(uint actionId, uint targetObjectId)
    {
        // ⚡ 性能关键：targetObjectId == 0 时使用游戏默认目标占位 ID
        ulong resolvedTarget = targetObjectId > 0
            ? targetObjectId
            : 0xE000_0000UL; // FFXIV 游戏内"当前目标"占位 ID

        bool success = _dataReader.TryUseAction(actionId, resolvedTarget);

        if (success)
        {
            _lastExecutedAction  = actionId;
            _consecutiveFailures = 0;
            _totalExecuted++;
        }
        else
        {
            _consecutiveFailures++;
            _log.Warning("[ActionQueue] ❌ 技能释放失败: ID={0}（连续失败 {1} 次）",
                actionId, _consecutiveFailures);

            // 连续失败超阈值 → 自动暂停并报警
            if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
            {
                IsPaused = true;
                _log.Error("[ActionQueue] ⚠️ 连续失败 {0} 次，已自动暂停执行！请检查技能配置或目标状态。",
                    _consecutiveFailures);
            }
        }

        return success;
    }

    // ── 资源释放 ─────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pendingGcd   = null;
        _pendingOgcds = Array.Empty<OgcdInsert>();
        _log.Information("[ActionQueue] 已释放，共成功执行 {0} 次技能", _totalExecuted);
    }
}

using System;
using Xunit;
using NSubstitute;
using Dalamud.Plugin.Services;
using AstralSolver.Core;
using AstralSolver.Tests.TestHelpers;
using AstralSolver.Utils;

namespace AstralSolver.Tests.Core;

/// <summary>
/// ActionQueue 技能执行队列单元测试。
/// 通过 Mock IGameDataReader 替代真实 ActionManager 调用。
/// </summary>
public sealed class ActionQueueTests
{
    private readonly ActionQueue     _queue;
    private readonly IPluginLog      _log;
    private readonly Configuration   _config;
    private readonly IGameDataReader _dataReader;

    public ActionQueueTests()
    {
        _log        = Substitute.For<IPluginLog>();
        _config     = new Configuration();
        _dataReader = Substitute.For<IGameDataReader>();

        // 默认：TryUseAction 返回成功
        _dataReader.TryUseAction(Arg.Any<uint>(), Arg.Any<ulong>()).Returns(true);

        _queue = new ActionQueue(_log, _config, _dataReader);
    }

    // ── 辅助方法 ─────────────────────────────────────────

    /// <summary>构建最简单的 Auto 模式 DecisionPacket</summary>
    private static DecisionPacket MakeAutoPacket(uint actionId = 1234u, uint targetId = 0u) =>
        new()
        {
            GcdQueue = new[]
            {
                new GcdAction { ActionId = actionId, ActionName = "测试技能", Priority = 10f, TargetObjectId = targetId }
            },
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons     = Array.Empty<ReasonEntry>(),
            Mode        = DecisionMode.Auto,
            Confidence  = 1f,
        };

    /// <summary>构建 GCD 已就绪（剩余 0s）的快照</summary>
    private static BattleSnapshot MakeGcdReadySnapshot(float gcdRemaining = 0f, float gcdTotal = 2.5f) =>
        new SnapshotBuilder()
            .WithPlayer(jobId: 33)
            .WithGcdRemaining(gcdRemaining, gcdTotal)
            .WithAstrologianState()
            .Build();

    // ═══════════════════════════════════════════════════
    //  测试1: SubmitDecision 后 PendingCount > 0
    // ═══════════════════════════════════════════════════

    [Fact]
    public void SubmitDecision_WithGcdAction_PendingCountIsPositive()
    {
        var packet = MakeAutoPacket();

        _queue.SubmitDecision(packet);

        Assert.True(_queue.PendingCount > 0, "SubmitDecision 后 PendingCount 应 > 0");
    }

    // ═══════════════════════════════════════════════════
    //  测试2: IsPaused 时 Tick 不执行
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Tick_WhenPaused_DoesNotExecuteAnyAction()
    {
        _queue.SubmitDecision(MakeAutoPacket());
        _queue.IsPaused = true;

        var snapshot = MakeGcdReadySnapshot(gcdRemaining: 0f);
        _queue.Tick(snapshot);

        // TryUseAction 不应被调用
        _dataReader.DidNotReceive().TryUseAction(Arg.Any<uint>(), Arg.Any<ulong>());
    }

    // ═══════════════════════════════════════════════════
    //  测试3: Navigator 模式不执行技能
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Tick_NavigatorMode_DoesNotExecuteAction()
    {
        // 提交 Navigator 模式决策 → 队列应清空且不执行
        var navigatorPacket = new DecisionPacket
        {
            GcdQueue    = new[] { new GcdAction { ActionId = 9999u, ActionName = "不应执行", Priority = 10f } },
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons     = Array.Empty<ReasonEntry>(),
            Mode        = DecisionMode.Navigator,
            Confidence  = 1f,
        };
        _queue.SubmitDecision(navigatorPacket);

        var snapshot = MakeGcdReadySnapshot(gcdRemaining: 0f);
        _queue.Tick(snapshot);

        _dataReader.DidNotReceive().TryUseAction(Arg.Any<uint>(), Arg.Any<ulong>());
        Assert.Equal(0, _queue.PendingCount);
    }

    // ═══════════════════════════════════════════════════
    //  测试4: 连续失败 5 次后自动暂停
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Tick_FiveConsecutiveFailures_AutoPauses()
    {
        // 让 TryUseAction 始终返回 false（失败）
        _dataReader.TryUseAction(Arg.Any<uint>(), Arg.Any<ulong>()).Returns(false);

        // GCD 已就绪的快照
        var snapshot = MakeGcdReadySnapshot(gcdRemaining: 0f);

        // 连续提交 + Tick 5 次，让失败计数累积
        for (int i = 0; i < 5; i++)
        {
            _queue.SubmitDecision(MakeAutoPacket());
            _queue.Tick(snapshot);
        }

        // 第 5 次失败后应自动暂停
        Assert.True(_queue.IsPaused, "连续失败5次后应自动暂停");
    }

    // ═══════════════════════════════════════════════════
    //  测试5: 新 SubmitDecision 替换旧待执行列表
    // ═══════════════════════════════════════════════════

    [Fact]
    public void SubmitDecision_Twice_ReplacesOldDecision()
    {
        // 提交第一个决策
        _queue.SubmitDecision(MakeAutoPacket(actionId: 1111u));

        // 提交第二个决策 — 应替换，而非累加
        _queue.SubmitDecision(MakeAutoPacket(actionId: 2222u));

        // PendingCount 应为 1（只剩一个 GCD），不是 2
        Assert.Equal(1, _queue.PendingCount);

        // 执行后应是第二个技能的 ID
        var snapshot = MakeGcdReadySnapshot(gcdRemaining: 0f);
        _queue.Tick(snapshot);

        _dataReader.Received(1).TryUseAction(2222u, Arg.Any<ulong>());
        _dataReader.DidNotReceive().TryUseAction(1111u, Arg.Any<ulong>());
    }
}

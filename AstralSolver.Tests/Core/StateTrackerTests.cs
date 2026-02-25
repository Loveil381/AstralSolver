using System.Threading;
using Xunit;
using NSubstitute;
using Dalamud.Plugin.Services;
using AstralSolver.Core;
using AstralSolver.Tests.TestHelpers;

namespace AstralSolver.Tests.Core;

/// <summary>
/// StateTracker 单元测试（使用 IGameDataReader Mock 隔离 unsafe 代码）
/// </summary>
public sealed class StateTrackerTests
{
    // ── 辅助：构建测试用 Configuration ───────────────────────────────────────
    private static Configuration MakeConfig() => new()
    {
        SnapshotRingCapacity = 60,
        IdlePollingInterval  = 10,
    };

    // ── 辅助：构建 StateTracker 实例（注入全部 Mock）────────────────────────
    private static StateTracker MakeTracker(
        MockDalamudServices svc,
        IGameDataReader?    dataReader = null)
    {
        var reader = dataReader ?? Substitute.For<IGameDataReader>();
        return new StateTracker(
            svc.ObjectTable,
            svc.PartyList,
            svc.Condition,
            svc.TargetManager,
            svc.Framework,
            svc.Log,
            MakeConfig(),
            reader);
    }

    // ── 测试1：构造后 Current 为 BattleSnapshot.Empty ─────────────────────────
    [Fact]
    public void AfterConstruction_CurrentIsBattleSnapshotEmpty()
    {
        var svc = new MockDalamudServices();
        svc.SetupDefault();

        using var tracker = MakeTracker(svc);

        // 构造完成后尚未触发任何帧，Current 应为空快照
        Assert.Equal(BattleSnapshot.Empty, tracker.Current);
        Assert.Equal(0, tracker.FrameCount);
        Assert.False(tracker.IsInCombat);
    }

    // ── 测试2：Dispose 后 FrameCount 不再增长 ────────────────────────────────
    [Fact]
    public void AfterDispose_FrameCountDoesNotGrow()
    {
        var svc = new MockDalamudServices();
        svc.SetupDefault();

        StateTracker tracker = MakeTracker(svc);

        // 记录当前帧数并 Dispose
        long countBefore = tracker.FrameCount;
        tracker.Dispose();

        // Dispose 已注销 Framework.Update 事件，帧数应保持不变
        Assert.Equal(countBefore, tracker.FrameCount);

        // 验证 Framework.Update 事件被取消注册（NSubstitute 验证）
        svc.Framework.Received().Update -= Arg.Any<IFramework.OnUpdateDelegate>();
    }

    // ── 测试3：配置中的 SnapshotRingCapacity 被正确应用 ──────────────────────
    [Fact]
    public void Config_SnapshotRingCapacity_IsApplied()
    {
        var svc = new MockDalamudServices();
        svc.SetupDefault();

        var config = new Configuration { SnapshotRingCapacity = 120, IdlePollingInterval = 5 };
        var reader = Substitute.For<IGameDataReader>();

        using var tracker = new StateTracker(
            svc.ObjectTable, svc.PartyList, svc.Condition,
            svc.TargetManager, svc.Framework, svc.Log, config, reader);

        // GetHistory 应能接受 120 帧的请求而不抛出异常
        var history = tracker.GetHistory(120);
        Assert.NotNull(history);
        Assert.Empty(history); // 尚未有任何帧
    }
}

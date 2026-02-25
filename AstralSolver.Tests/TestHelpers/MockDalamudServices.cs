using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using NSubstitute;

namespace AstralSolver.Tests.TestHelpers;

/// <summary>
/// 集中管理所有 Dalamud 服务的 NSubstitute Mock，供 StateTracker 测试使用。
/// </summary>
internal sealed class MockDalamudServices
{
    /// <summary>模拟的对象表服务</summary>
    public IObjectTable ObjectTable { get; } = Substitute.For<IObjectTable>();

    /// <summary>模拟的小队列表服务</summary>
    public IPartyList PartyList { get; } = Substitute.For<IPartyList>();

    /// <summary>模拟的条件标志服务</summary>
    public ICondition Condition { get; } = Substitute.For<ICondition>();

    /// <summary>模拟的目标管理服务</summary>
    public ITargetManager TargetManager { get; } = Substitute.For<ITargetManager>();

    /// <summary>模拟的框架更新服务</summary>
    public IFramework Framework { get; } = Substitute.For<IFramework>();

    /// <summary>模拟的插件日志服务</summary>
    public IPluginLog Log { get; } = Substitute.For<IPluginLog>();

    /// <summary>
    /// 配置 ObjectTable.LocalPlayer 返回 null（无玩家场景）
    /// </summary>
    public void SetLocalPlayer(IPlayerCharacter? player)
    {
        ObjectTable.LocalPlayer.Returns(player);
    }

    /// <summary>
    /// 配置 Condition 的 InCombat 标志
    /// </summary>
    public void SetInCombat(bool inCombat)
    {
        Condition[ConditionFlag.InCombat].Returns(inCombat);
    }

    /// <summary>
    /// 默认配置：无玩家、不在战斗中、小队 0 人
    /// </summary>
    public void SetupDefault()
    {
        SetLocalPlayer(null);
        SetInCombat(false);
        PartyList.Length.Returns(0);
        TargetManager.Target.Returns((Dalamud.Game.ClientState.Objects.Types.IGameObject?)null);
    }
}

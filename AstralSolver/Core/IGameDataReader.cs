using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace AstralSolver.Core;

/// <summary>
/// 游戏原始数据读取接口，封装所有 unsafe FFXIVClientStructs 调用。
/// 通过接口隔离，便于单元测试时使用 Mock 替代真实实现。
/// </summary>
public interface IGameDataReader
{
    /// <summary>
    /// 从本地玩家对象读取玩家状态（HP、MP、职业、等级、坐标、Buff）。
    /// GCD 信息由 <see cref="ReadGcdInfo"/> 单独提供。
    /// </summary>
    /// <param name="player">本地玩家游戏对象</param>
    /// <param name="gcdTotal">GCD 总时间（秒）</param>
    /// <param name="gcdRemaining">GCD 剩余冷却时间（秒）</param>
    /// <returns>不可变的玩家状态快照</returns>
    PlayerState ReadPlayerState(IPlayerCharacter player, float gcdTotal, float gcdRemaining);

    /// <summary>
    /// 读取当前 GCD 速度信息（总时间与剩余时间）。
    /// 通过 ActionManager 查询代理技能 CD 实现。
    /// </summary>
    /// <returns>(GCD总时间, GCD剩余时间) 元组，均为秒</returns>
    (float Total, float Remaining) ReadGcdInfo();

    /// <summary>
    /// 从玩家对象读取所有 Buff 状态列表。
    /// </summary>
    /// <param name="player">目标玩家对象</param>
    /// <returns>所有有效 Buff 的状态快照数组</returns>
    StatusEffect[] ReadStatusList(IPlayerCharacter player);

    /// <summary>
    /// 从小队成员对象读取 Buff 状态列表。
    /// </summary>
    /// <param name="member">小队成员对象（IBattleChara）</param>
    /// <returns>所有有效 Buff 的状态快照数组</returns>
    StatusEffect[] ReadPartyMemberStatusList(IBattleChara member);

    /// <summary>
    /// 从目标 NPC 读取 Debuff 状态列表。
    /// </summary>
    /// <param name="target">敌方战斗 NPC 对象</param>
    /// <returns>所有有效 Debuff 的状态快照数组</returns>
    StatusEffect[] ReadTargetDebuffs(IBattleNpc target);

    /// <summary>
    /// 读取占星术士专属仪表盘状态（只有当 jobId == 33 时才读取）。
    /// </summary>
    /// <param name="jobId">当前职业 ID</param>
    /// <returns>占星状态快照，非占星职业时返回 null</returns>
    AstrologianState? ReadAstrologianGauge(byte jobId);

    /// <summary>
    /// 通过 ActionManager 尝试释放技能（ActionType.Action）。
    /// 封装 unsafe 调用，供 ActionQueue 使用。
    /// </summary>
    /// <param name="actionId">技能 ID</param>
    /// <param name="targetObjectId">目标对象 ID（0xE000_0000UL = 当前目标占位）</param>
    /// <returns>true = 成功发出指令；false = 失败或 ActionManager 不可用</returns>
    bool TryUseAction(uint actionId, ulong targetObjectId = 0xE000_0000UL);
}

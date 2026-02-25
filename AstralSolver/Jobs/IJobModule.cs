using AstralSolver.Core;

namespace AstralSolver.Jobs;

/// <summary>
/// 职业决策模块接口。
/// 每个职业实现此接口，提供针对该职业的战斗决策逻辑。
/// 设计原则：Evaluate 必须在 3ms 内完成，只依赖 BattleSnapshot（可测试）。
/// </summary>
public interface IJobModule
{
    /// <summary>职业 ID（与 FFXIV ClassJob RowId 一致）</summary>
    byte JobId { get; }

    /// <summary>职业显示名称（多语言，供 UI 显示）</summary>
    string JobName { get; }

    /// <summary>
    /// 根据当前战斗快照生成职业决策。
    /// 这是核心方法，必须在 3ms 内完成。
    /// </summary>
    /// <param name="snapshot">不可变战斗快照</param>
    /// <returns>职业决策结果</returns>
    JobDecision Evaluate(BattleSnapshot snapshot);

    /// <summary>
    /// 战斗开始时调用，重置内部状态（如开幕计时器、卡牌追踪器）。
    /// </summary>
    void OnCombatStart();

    /// <summary>
    /// 战斗结束时调用，清理资源并可选地输出统计数据。
    /// </summary>
    void OnCombatEnd();
}

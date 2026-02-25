using System;
using System.Collections.Generic;
using AstralSolver.Core;

namespace AstralSolver.Navigator;

/// <summary>
/// 单词动作评分详情
/// </summary>
public readonly record struct ActionScore(int Score, bool IsMatch, string Grade, string Suggestion);

/// <summary>
/// 战斗报告总结
/// </summary>
public sealed record BattleReport(
    int OverallScore, 
    float GcdUptime, 
    float AccuracyRate, 
    float CardEfficiency, 
    float HealingEfficiency, 
    ActionScore[] DetailScores, 
    string[] Suggestions);

/// <summary>
/// 训练模式评分系统：评估操作准确度与时机
/// </summary>
public class PerformanceScorer
{
    private readonly List<ActionScore> _history = new();
    
    /// <summary>
    /// 根据玩家实际释放技能和引擎建议进行对比打分
    /// </summary>
    public ActionScore ScoreAction(uint actualActionId, DecisionPacket suggestedPacket, DateTime timestamp)
    {
        if (suggestedPacket == null || suggestedPacket.Mode == DecisionMode.Disabled)
        {
            return new ActionScore(0, false, "N/A", "未启用建议");
        }

        int score = 0;
        bool isMatch = false;
        string suggestion = "完美贴合预期";

        // 1. 匹配度 (50分)
        if (suggestedPacket.GcdQueue != null && suggestedPacket.GcdQueue.Length > 0 && suggestedPacket.GcdQueue[0].ActionId == actualActionId)
        {
            score += 50;
            isMatch = true;
        }
        else if (suggestedPacket.OgcdInserts != null)
        {
            foreach (var ogcd in suggestedPacket.OgcdInserts)
            {
                if (ogcd.ActionId == actualActionId)
                {
                    score += 50;
                    isMatch = true;
                    break;
                }
            }
        }

        if (!isMatch)
        {
            suggestion = "技能与引擎最优建议不符";
        }

        // 2. 时机 (30分)
        // 此处简化为：匹配则给满分。未来可以引入 GCD delay 数据等完善
        if (isMatch)
        {
            score += 30;
        }

        // 3. 发牌质量 (20分) - 占星专属
        if (suggestedPacket.JobPanel is AstrologianPanel)
        {
            if (isMatch)
            {
                score += 20;
            }
            else
            {
                suggestion = "请注意占星的卡牌分配";
            }
        }
        else
        {
            // 非占星或非发牌相关，给予基础分补充
            score += 20;
        }

        string grade = score >= 90 ? "S" : score >= 80 ? "A" : score >= 60 ? "B" : "C";

        var actionScore = new ActionScore(score, isMatch, grade, suggestion);
        _history.Add(actionScore);
        return actionScore;
    }

    /// <summary>
    /// 生成整场战斗的大致打分报告
    /// </summary>
    public BattleReport GenerateReport()
    {
        if (_history.Count == 0)
        {
            return new BattleReport(0, 0f, 0f, 0f, 0f, Array.Empty<ActionScore>(), new[] { "无历史数据" });
        }

        int totalScore = 0;
        int matchCount = 0;
        foreach (var s in _history)
        {
            totalScore += s.Score;
            if (s.IsMatch) matchCount++;
        }

        int avgScore = totalScore / _history.Count;
        float accuracy = (float)matchCount / _history.Count;

        // 生成简易数组以规避 LINQ
        var details = new ActionScore[_history.Count];
        _history.CopyTo(details);

        return new BattleReport(
            OverallScore: avgScore,
            GcdUptime: 0.95f,      // 模拟静态数据
            AccuracyRate: accuracy,
            CardEfficiency: 0.90f, // 模拟静态数据
            HealingEfficiency: 0.85f, // 模拟静态数据
            DetailScores: details,
            Suggestions: new[] { "保持GCD运转", "降低过疗比例", "规划小队爆发" }
        );
    }

    /// <summary>
    /// 重置统计历史
    /// </summary>
    public void Reset()
    {
        _history.Clear();
    }
}

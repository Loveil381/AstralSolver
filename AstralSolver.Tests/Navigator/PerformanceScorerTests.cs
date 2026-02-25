using Xunit;
using AstralSolver.Core;
using AstralSolver.Navigator;
using System;

namespace AstralSolver.Tests.Navigator;

public class PerformanceScorerTests
{
    private readonly PerformanceScorer _sut = new();
    private readonly DateTime _now = DateTime.Now;

    [Fact]
    public void ScoreAction_PerfectMatch_ReturnsFullScore()
    {
        var packet = new DecisionPacket {
            Mode = DecisionMode.Training,
            GcdQueue = new GcdAction[] { new() { ActionId = 100 } },
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons = Array.Empty<ReasonEntry>()
        };

        var score = _sut.ScoreAction(100, packet, _now);
        Assert.Equal(100, score.Score);
        Assert.True(score.IsMatch);
    }

    [Fact]
    public void ScoreAction_CompleteMismatch_ReturnsLowScore()
    {
        var packet = new DecisionPacket {
            Mode = DecisionMode.Training,
            GcdQueue = new GcdAction[] { new() { ActionId = 100 } },
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons = Array.Empty<ReasonEntry>()
        };

        var score = _sut.ScoreAction(999, packet, _now);
        Assert.True(score.Score < 50);
        Assert.False(score.IsMatch);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var packet = new DecisionPacket {
            Mode = DecisionMode.Training,
            GcdQueue = new GcdAction[] { new() { ActionId = 100 } },
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons = Array.Empty<ReasonEntry>()
        };

        _sut.ScoreAction(100, packet, _now);
        var report1 = _sut.GenerateReport();
        Assert.True(report1.OverallScore > 0);

        _sut.Reset();
        var report2 = _sut.GenerateReport();
        Assert.Equal(0, report2.OverallScore);
    }

    [Fact]
    public void GenerateReport_Empty_ReturnsDefault()
    {
        var report = _sut.GenerateReport();
        Assert.Equal(0, report.OverallScore);
        Assert.Empty(report.DetailScores);
    }
}

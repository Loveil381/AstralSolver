using Xunit;
using AstralSolver.Core;
using AstralSolver.Navigator;
using System;

namespace AstralSolver.Tests.Navigator;

public class DualRailTimelineTests
{
    private readonly DualRailTimeline _sut = new();

    [Fact]
    public void Calculate_EmptyGcdQueue_ReturnsEmptyLayout()
    {
        var packet = new DecisionPacket { GcdQueue = Array.Empty<GcdAction>(), OgcdInserts = Array.Empty<OgcdInsert>(), Reasons = Array.Empty<ReasonEntry>(), Mode = DecisionMode.Navigator };
        var result = _sut.Calculate(packet, 0, 0, 48f);
        Assert.Empty(result.GcdPositions);
        Assert.Empty(result.OgcdPositions);
    }

    [Fact]
    public void Calculate_5Gcds_ArrangedCorrectly()
    {
        var packet = new DecisionPacket {
            GcdQueue = new GcdAction[] { new() { ActionId = 1 }, new() { ActionId = 2 }, new() { ActionId = 3 }, new() { ActionId = 4 }, new() { ActionId = 5 } },
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons = Array.Empty<ReasonEntry>(),
            Mode = DecisionMode.Navigator
        };
        var result = _sut.Calculate(packet, 10, 10, 40f);
        Assert.Equal(5, result.GcdPositions.Length);
        Assert.True(result.GcdPositions[0].IsHighlighted);
        Assert.False(result.GcdPositions[1].IsHighlighted);
        Assert.True(result.GcdPositions[1].X > result.GcdPositions[0].X);
    }

    [Fact]
    public void Calculate_Ogcd_InsertedCorrectly()
    {
        var packet = new DecisionPacket {
            GcdQueue = new GcdAction[] { new() { ActionId = 1 }, new() { ActionId = 2 } },
            OgcdInserts = new OgcdInsert[] { new() { ActionId = 99, InsertAfterGcdIndex = 0 } },
            Reasons = Array.Empty<ReasonEntry>(),
            Mode = DecisionMode.Navigator
        };
        var result = _sut.Calculate(packet, 10, 10, 40f);
        Assert.Single(result.OgcdPositions);
        Assert.True(result.OgcdPositions[0].X > result.GcdPositions[0].X);
        Assert.True(result.OgcdPositions[0].X < result.GcdPositions[1].X);
    }
}

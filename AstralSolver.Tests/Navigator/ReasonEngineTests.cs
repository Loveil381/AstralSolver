using Xunit;
using AstralSolver.Core;
using AstralSolver.Navigator;
using System;

namespace AstralSolver.Tests.Navigator;

public class ReasonEngineTests
{
    private readonly ReasonEngine _sut = new();

    [Fact]
    public void Format_UnknownKey_FallbacksToKey()
    {
        var entry = new ReasonEntry { TemplateKey = "Unknown_Key_123" };
        var result = _sut.Format(entry);
        Assert.Equal("[Unknown_Key_123]", result);
    }

    [Fact]
    public void FormatAll_OrdersByPriority()
    {
        var entries = new ReasonEntry[] {
            new() { TemplateKey = "InfoKey", Priority = ReasonPriority.Info },
            new() { TemplateKey = "CritKey", Priority = ReasonPriority.Critical },
            new() { TemplateKey = "WarnKey", Priority = ReasonPriority.Important }
        };
        var result = _sut.FormatAll(entries);
        Assert.Equal(3, result.Length);
        Assert.Equal("[CritKey]", result[0]);
        Assert.Equal("[WarnKey]", result[1]);
        Assert.Equal("[InfoKey]", result[2]);
    }
}

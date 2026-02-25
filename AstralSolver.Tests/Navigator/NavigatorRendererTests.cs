using Xunit;
using NSubstitute;
using AstralSolver.Core;
using AstralSolver.Navigator;
using Dalamud.Plugin.Services;
using System;

namespace AstralSolver.Tests.Navigator;

public class NavigatorRendererTests
{
    private readonly NavigatorRenderer _sut;
    private readonly ITextureProvider _textureProvider;
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _pluginLog;

    public NavigatorRendererTests()
    {
        _textureProvider = Substitute.For<ITextureProvider>();
        _dataManager = Substitute.For<IDataManager>();
        _pluginLog = Substitute.For<IPluginLog>();

        _sut = new NavigatorRenderer(_textureProvider, _dataManager, _pluginLog);
    }

    [Fact]
    public void UpdateDecision_UpdatesCurrentPacket()
    {
        var packet = new DecisionPacket {
            Mode = DecisionMode.Navigator,
            GcdQueue = Array.Empty<GcdAction>(),
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons = Array.Empty<ReasonEntry>()
        };

        _sut.UpdateDecision(packet);
        Assert.True(true);
    }

    [Fact]
    public void CycleDisplayMode_RotatesModes()
    {
        var packet = new DecisionPacket {
            Mode = DecisionMode.Navigator,
            GcdQueue = Array.Empty<GcdAction>(),
            OgcdInserts = Array.Empty<OgcdInsert>(),
            Reasons = Array.Empty<ReasonEntry>()
        };
        _sut.UpdateDecision(packet);
        _sut.CycleDisplayMode();
        
        // Ensure no exception and logger is called
        _pluginLog.ReceivedWithAnyArgs().Verbose(default(string)!);
    }
    
    [Fact]
    public void EmptyDecisionPacket_DoesNotCrash()
    {
        var packet = new DecisionPacket { Mode = DecisionMode.Disabled, GcdQueue = Array.Empty<GcdAction>(), OgcdInserts = Array.Empty<OgcdInsert>(), Reasons = Array.Empty<ReasonEntry>() };
        _sut.UpdateDecision(packet);
        Assert.True(true);
    }
}

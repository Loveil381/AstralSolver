using System;
using System.Numerics;
using AstralSolver.Navigator;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AstralSolver.UI;

/// <summary>
/// 战斗悬浮窗：用于显示双轨时间轴和其他动态信息的覆盖层。
/// WindowFlags: NoTitleBar | NoScrollbar | NoBackground | NoInputs(点击穿透) |
///              AlwaysAutoResize | NoFocusOnAppearing | NoNav
/// </summary>
public class OverlayWindow : Window, IDisposable
{
    private readonly NavigatorRenderer _renderer;

    public OverlayWindow(NavigatorRenderer renderer)
        : base("AstralSolver Overlay",
               ImGuiWindowFlags.NoTitleBar       |
               ImGuiWindowFlags.NoScrollbar      |
               ImGuiWindowFlags.NoBackground     |
               ImGuiWindowFlags.NoInputs         |
               ImGuiWindowFlags.AlwaysAutoResize |
               ImGuiWindowFlags.NoFocusOnAppearing |
               ImGuiWindowFlags.NoNav)
    {
        _renderer = renderer;

        // 初始位置：可通过 Configuration 中的偏移量修改
        PositionCondition = ImGuiCond.FirstUseEver;
        Position = new Vector2(100, 100);
    }

    public void UpdatePosition(float x, float y)
    {
        Position = new Vector2(x, y);
        PositionCondition = ImGuiCond.Always;
    }

    /// <summary>由 Dalamud Windowing System 每帧调用</summary>
    public override void Draw()
    {
        _renderer.Render();
    }

    public void Dispose()
    {
        // 渲染器的纹理缓存生命周期由 DI 容器负责
        GC.SuppressFinalize(this);
    }
}

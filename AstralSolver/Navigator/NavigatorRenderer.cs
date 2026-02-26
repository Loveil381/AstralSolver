using System;
using System.Collections.Generic;
using System.Numerics;
using AstralSolver;     // NavigatorDisplayMode 定义在 Configuration.cs 的 AstralSolver 命名空间
using AstralSolver.Core;
using AstralSolver.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace AstralSolver.Navigator;
/// <summary>
/// 导航 UI 渲染器：负责在屏幕核心区域渲染即将建议的技能双轨时间轴等。
/// 由 OverlayWindow.Draw 调用。
/// </summary>
public class NavigatorRenderer
{
    private DecisionPacket? _currentPacket;
    private NavigatorDisplayMode _displayMode = NavigatorDisplayMode.Standard;

    private readonly ITextureProvider _textureProvider;
    private readonly IDataManager _dataManager;
    private readonly IPluginLog _pluginLog;
    private readonly DualRailTimeline _timelineCalculator = new();
    private readonly ReasonEngine _reasonEngine = new();

    // 图标 ID 缓存：actionId → iconId（来自 Lumina Action 表）
    // ITextureProvider 内部自带纹理缓存，此处只需缓存 actionId→iconId 的映射关系。
    private readonly Dictionary<uint, ushort> _actionIconCache = new();

    // ── 布局常量 ───────────────────────────────────────────────
    private const float ICON_SIZE       = 48f;
    private const float ICON_SIZE_SMALL = 36f;
    private const float ICON_SPACING    = 8f;
    private const float RAIL_SPACING    = 12f;
    private const float REASON_HEIGHT   = 24f;
    private const float PANEL_PADDING   = 10f;

    // ── 颜色常量 ───────────────────────────────────────────────
    // 使用辅助方法而非静态字段，避免静态初始化时 ImGui 未就绪
    private static readonly uint COLOR_PANEL_BG      = ToColor(0.10f, 0.10f, 0.10f, 0.80f);
    private static readonly uint COLOR_TEXT_DIM      = ToColor(0.70f, 0.70f, 0.70f, 1.00f);
    private static readonly uint COLOR_TEXT_NORMAL   = ToColor(1.00f, 1.00f, 1.00f, 1.00f);
    private static readonly uint COLOR_TEXT_WARN     = ToColor(1.00f, 0.80f, 0.20f, 1.00f);
    private static readonly uint COLOR_TEXT_CRIT     = ToColor(1.00f, 0.30f, 0.30f, 1.00f);
    private static readonly uint COLOR_GCD_HIGHLIGHT = ToColor(1.00f, 0.80f, 0.20f, 1.00f);
    private static readonly uint COLOR_OGCD          = ToColor(0.40f, 0.80f, 1.00f, 1.00f);
    private static readonly uint COLOR_ICON_GCD      = ToColor(0.25f, 0.45f, 0.65f, 1.00f);
    private static readonly uint COLOR_ICON_OGCD     = ToColor(0.55f, 0.25f, 0.65f, 1.00f);
    private static readonly uint COLOR_AST           = ToColor(0.80f, 0.60f, 1.00f, 1.00f);

    private static uint ToColor(float r, float g, float b, float a)
        => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    public NavigatorRenderer(ITextureProvider textureProvider, IDataManager dataManager, IPluginLog pluginLog)
    {
        _textureProvider = textureProvider;
        _dataManager     = dataManager;
        _pluginLog       = pluginLog;
    }

    /// <summary>更新当前显示的决策包</summary>
    public void UpdateDecision(DecisionPacket packet)
    {
        _currentPacket = packet;
    }

    /// <summary>循环切换显示模式: Minimal → Standard → Expert → Training → Minimal</summary>
    public void CycleDisplayMode()
    {
        _displayMode = _displayMode switch
        {
            NavigatorDisplayMode.Minimal  => NavigatorDisplayMode.Standard,
            NavigatorDisplayMode.Standard => NavigatorDisplayMode.Expert,
            NavigatorDisplayMode.Expert   => NavigatorDisplayMode.Training,
            NavigatorDisplayMode.Training => NavigatorDisplayMode.Minimal,
            _                             => NavigatorDisplayMode.Standard
        };
        _pluginLog.Verbose($"UI 显示模式切换为: {_displayMode}");
    }

    /// <summary>
    /// 主绘制方法，由 OverlayWindow.Draw 调用。
    /// 流程: 等待文本 → 背景面板 → StatusBar → GcdRail → OgcdRail → ReasonBar → AstrologianPanel
    /// </summary>
    public void Render()
    {
        var drawList = ImGui.GetWindowDrawList();
        var pMin = ImGui.GetCursorScreenPos();

        // 1. 无数据或禁用时绘制待机 UI
        if (_currentPacket == null
            || _currentPacket.Mode == DecisionMode.Disabled
            || _currentPacket.GcdQueue == null
            || _currentPacket.GcdQueue.Length == 0)
        {
            var p = ImGui.GetCursorScreenPos();
            var bgMax = new Vector2(p.X + 260, p.Y + 40);
            drawList.AddRectFilled(p, bgMax, COLOR_PANEL_BG, 8f);
            drawList.AddText(new Vector2(p.X + 10, p.Y + 10), COLOR_TEXT_DIM, 
                $"[AstralSolver] {Loc.GetString("UI_Hold")}");
            ImGui.SetCursorScreenPos(bgMax);
            ImGui.Dummy(Vector2.Zero);
            return;
        }

        var packet = _currentPacket;

        // 2. 计算时间轴布局
        float startX = pMin.X + PANEL_PADDING;
        float startY = pMin.Y + PANEL_PADDING + 20f; // 为状态栏留空
        var layout = _timelineCalculator.Calculate(packet, startX, startY, ICON_SIZE);

        float panelWidth  = Math.Max(300f, layout.Bounds.Width + PANEL_PADDING * 2);
        float panelHeight = layout.Bounds.Height + PANEL_PADDING * 2 + 20f;
        if (_displayMode != NavigatorDisplayMode.Minimal)
            panelHeight += REASON_HEIGHT;
        if (packet.JobPanel is AstrologianPanel
            && _displayMode != NavigatorDisplayMode.Minimal)
            panelHeight += 40f; // 占星面板额外高度

        var pMax = new Vector2(pMin.X + panelWidth, pMin.Y + panelHeight);

        // 3. 绘制半透明背景面板（圆角矩形）
        drawList.AddRectFilled(pMin, pMax, COLOR_PANEL_BG, 8f);

        // 4. 状态栏：左侧职业+模式，右侧置信度
        DrawStatusBar(drawList, new Vector2(startX, pMin.Y + PANEL_PADDING), panelWidth - PANEL_PADDING * 2);

        // 5. GCD 轨迹：从左到右，第一个图标稍大并有高亮边框
        DrawGcdRail(drawList, layout.GcdPositions);

        // 6. oGCD 轨迹：在 GCD 之间绘制较小图标
        if (_displayMode != NavigatorDisplayMode.Minimal)
            DrawOgcdRail(drawList, layout.OgcdPositions);

        float currentY = layout.Bounds.Y + layout.Bounds.Height + 10f;

        // 7. 理由栏：Critical=红 / Important=黄 / Info=白
        if (_displayMode != NavigatorDisplayMode.Minimal)
        {
            DrawReasonBar(drawList, packet.Reasons, new Vector2(startX, currentY));
            currentY += REASON_HEIGHT;
        }

        // 8. 占星专属面板（非占星时隐藏）
        if (packet.JobPanel is AstrologianPanel astPanel && _displayMode != NavigatorDisplayMode.Minimal)
            DrawAstrologianPanel(drawList, astPanel, new Vector2(startX, currentY));

        // 驱动 AlwaysAutoResize 按内容尺寸调整
        ImGui.SetCursorScreenPos(pMax);
        ImGui.Dummy(Vector2.Zero);
    }

    // ── 内部绘制方法 ─────────────────────────────────────────

    private void DrawStatusBar(ImDrawListPtr drawList, Vector2 pos, float width)
    {
        string modeText = $"[{_displayMode}] {Loc.GetString("Plugin_Name")}";
        drawList.AddText(pos, COLOR_TEXT_DIM, modeText);

        if (_displayMode == NavigatorDisplayMode.Expert
            || _displayMode == NavigatorDisplayMode.Training)
        {
            string confText = $"{Loc.GetString("UI_Confidence")}: {_currentPacket!.Confidence:P0}";
            var textSize = ImGui.CalcTextSize(confText);
            drawList.AddText(new Vector2(pos.X + width - textSize.X, pos.Y), COLOR_TEXT_WARN, confText);
        }
    }

    private void DrawGcdRail(ImDrawListPtr drawList, IconPosition[] positions)
    {
        int limit = _displayMode == NavigatorDisplayMode.Minimal
            ? Math.Min(3, positions.Length)
            : positions.Length;
        for (int i = 0; i < limit; i++)
        {
            var p = positions[i];
            DrawIcon(drawList, p.ActionId, new Vector2(p.X, p.Y), p.Size, isGcd: true);
            if (p.IsHighlighted)
            {
                drawList.AddRect(
                    new Vector2(p.X - 2, p.Y - 2),
                    new Vector2(p.X + p.Size + 2, p.Y + p.Size + 2),
                    COLOR_GCD_HIGHLIGHT, 4f, ImDrawFlags.None, 2f);
            }
        }
    }

    private void DrawOgcdRail(ImDrawListPtr drawList, IconPosition[] positions)
    {
        foreach (var p in positions)
        {
            DrawIcon(drawList, p.ActionId, new Vector2(p.X, p.Y), p.Size, isGcd: false);
            drawList.AddRect(
                new Vector2(p.X - 1, p.Y - 1),
                new Vector2(p.X + p.Size + 1, p.Y + p.Size + 1),
                COLOR_OGCD, 2f);
        }
    }

    private void DrawReasonBar(ImDrawListPtr drawList, ReasonEntry[] reasons, Vector2 pos)
    {
        if (reasons == null || reasons.Length == 0) return;
        var formatted = _reasonEngine.FormatAll(reasons);
        if (formatted.Length == 0) return;

        uint color = COLOR_TEXT_NORMAL;
        foreach (var r in reasons)
        {
            if (r.Priority == ReasonPriority.Critical) { color = COLOR_TEXT_CRIT; break; }
            if (r.Priority == ReasonPriority.Important) color = COLOR_TEXT_WARN;
        }
        drawList.AddText(pos, color, $"▶ {formatted[0]}");
    }

    private void DrawAstrologianPanel(ImDrawListPtr drawList, AstrologianPanel astPanel, Vector2 pos)
    {
        string cardInfo = astPanel.CardPlans != null && astPanel.CardPlans.Length > 0
            ? $"{Loc.GetString("Card_PlayI")}: {astPanel.CardPlans[0].TargetName}"
            : $"{Loc.GetString("Card_AstralDraw")} / {Loc.GetString("Card_UmbralDraw")}";
        drawList.AddText(pos, COLOR_AST, $"[AST] {cardInfo} | CD: {astPanel.NextDrawIn:F1}s");
    }

    /// <summary>
    /// 通过 Lumina Action 表将技能 ID 转换为图标 ID，结果进本地缓存。
    /// 查找失败（Action 不存在或 iconId 为 0）时返回 0。
    /// </summary>
    private ushort GetIconId(uint actionId)
    {
        if (_actionIconCache.TryGetValue(actionId, out ushort cached))
            return cached;

        ushort iconId = 0;
        try
        {
            var sheet = _dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null)
            {
                var row = sheet.GetRowOrDefault(actionId);
                if (row.HasValue)
                    iconId = row.Value.Icon;
            }
        }
        catch (Exception ex)
        {
            _pluginLog.Warning(ex, $"[NavigatorRenderer] Lumina Action 表查询失败 actionId={actionId}");
        }

        // 缓存结果（包括查询失败的 0，避免每帧重试）
        _actionIconCache[actionId] = iconId;
        return iconId;
    }

    /// <summary>
    /// 绘制技能图标。
    /// 方案A: 通过 Lumina Action 表获取 iconId，再经 ITextureProvider 加载真实游戏图标并绘制。
    /// 方案B: iconId 为 0 或纹理获取失败时，降级为彩色方块 + 技能 ID 文字兜底。
    /// </summary>
    private void DrawIcon(ImDrawListPtr drawList, uint actionId, Vector2 pos, float size, bool isGcd)
    {
        var pMax = new Vector2(pos.X + size, pos.Y + size);

        // 方案A: 通过真实 iconId 获取游戏内置图标纹理
        try
        {
            ushort iconId = GetIconId(actionId);
            if (iconId > 0)
            {
                var wrap = _textureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                if (wrap != null)
                {
                    // Dalamud v14: IDalamudTextureWrap.Handle 是 ImTextureID 类型
                    drawList.AddImage(wrap.Handle, pos, pMax);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _pluginLog.Warning(ex, $"[NavigatorRenderer] 图标绘制失败 actionId={actionId}，降级为方块");
        }

        // 方案B: 彩色方块兜底（iconId 为 0 或纹理加载失败）
        uint bgColor = isGcd ? COLOR_ICON_GCD : COLOR_ICON_OGCD;
        drawList.AddRectFilled(pos, pMax, bgColor, 4f);
        string label = (actionId % 1000).ToString();
        var labelSize = ImGui.CalcTextSize(label);
        var labelPos  = new Vector2(pos.X + (size - labelSize.X) * 0.5f, pos.Y + (size - labelSize.Y) * 0.5f);
        drawList.AddText(labelPos, COLOR_TEXT_NORMAL, label);
    }
}

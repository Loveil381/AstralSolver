using System;
using System.Numerics;
using System.Reflection;
using AstralSolver.Core;
using AstralSolver.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AstralSolver.UI;

/// <summary>
/// 插件配置主窗口
/// </summary>
public class MainWindow : Window, IDisposable
{
    private readonly Configuration _config;
    private readonly IPluginLog _pluginLog;

    // 状态数据，用于在总览显示
    private string _currentJob = "Unknown";
    private double _lastTrackerTimeMs = 0;
    private double _lastEngineTimeMs = 0;

    // 可用的语言选项
    private readonly string[] _languages = { "zh_CN", "ja_JP", "en_US" };
    private readonly string[] _languageNames = { "简体中文", "日本語", "English" };

    // 可用的运行模式和显示模式
    private readonly DecisionMode[] _modes = { DecisionMode.Auto, DecisionMode.Navigator, DecisionMode.Training, DecisionMode.Disabled };
    private readonly string[] _modeKeys = { "ui.mode.auto", "ui.mode.navigator", "ui.mode.training", "ui.mode.disabled" };
    
    private readonly string[] _navDisplays = { "Config_Nav_Minimal", "Config_Nav_Normal", "Config_Nav_Detailed", "Config_Nav_Expert" };

    public MainWindow(Configuration config, IPluginLog pluginLog) : base("AstralSolver Settings", ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _pluginLog = pluginLog;
        
        Size = new Vector2(500, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    /// <summary>
    /// 提供给外部刷新状态指标用
    /// </summary>
    public void UpdateStatus(string job, double trackerMs, double engineMs)
    {
        _currentJob = job;
        _lastTrackerTimeMs = trackerMs;
        _lastEngineTimeMs = engineMs;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("AstralSolverTabs"))
        {
            if (ImGui.BeginTabItem(Loc.GetString("ui.tab.overview")))
            {
                DrawOverviewTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Loc.GetString("ui.tab.navigator")))
            {
                DrawNavigatorTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Loc.GetString("ui.tab.astrologian")))
            {
                DrawAstrologianTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Loc.GetString("ui.tab.language")))
            {
                DrawLanguageTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Loc.GetString("ui.tab.about")))
            {
                DrawAboutTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawOverviewTab()
    {
        bool changed = false;

        ImGui.Spacing();
        bool enabled = _config.IsEnabled;
        if (ImGui.Checkbox(Loc.GetString("ui.enable"), ref enabled))
        {
            _config.IsEnabled = enabled;
            changed = true;
        }
        ImGui.Separator();

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.GetString("Config_Mode"));
        
        for (int i = 0; i < _modes.Length; i++)
        {
            if (ImGui.RadioButton(Loc.GetString(_modeKeys[i]), _config.Mode == _modes[i]))
            {
                _config.Mode = _modes[i];
                changed = true;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("--- 内部性能指标 / Performance ---");
        ImGui.TextUnformatted($"Current Job: {_currentJob}");
        ImGui.TextUnformatted($"StateTracker Update: {_lastTrackerTimeMs:F3} ms");
        ImGui.TextUnformatted($"DecisionEngine Time: {_lastEngineTimeMs:F3} ms");

        if (changed) _config.Save();
    }

    private void DrawNavigatorTab()
    {
        bool changed = false;
        ImGui.Spacing();

        // 显示模式下拉
        int currentDisplay = _config.Navigator.DisplayMode;
        if (ImGui.BeginCombo(Loc.GetString("ui.navigator.display_mode"), Loc.GetString(_navDisplays[currentDisplay])))
        {
            for (int i = 0; i < _navDisplays.Length; i++)
            {
                bool isSelected = (currentDisplay == i);
                if (ImGui.Selectable(Loc.GetString(_navDisplays[i]), isSelected))
                {
                    _config.Navigator.DisplayMode = i;
                    changed = true;
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();

        // 图标大小和位置
        float size = _config.Navigator.IconSize;
        if (ImGui.SliderFloat(Loc.GetString("ui.navigator.icon_size"), ref size, 32f, 64f))
        {
            _config.Navigator.IconSize = size;
            changed = true;
        }

        float ox = _config.Navigator.OffsetX;
        if (ImGui.SliderFloat(Loc.GetString("ui.navigator.offset_x"), ref ox, -1000f, 1000f))
        {
            _config.Navigator.OffsetX = ox;
            changed = true;
        }

        float oy = _config.Navigator.OffsetY;
        if (ImGui.SliderFloat(Loc.GetString("ui.navigator.offset_y"), ref oy, -1000f, 1000f))
        {
            _config.Navigator.OffsetY = oy;
            changed = true;
        }

        float alpha = _config.Navigator.Opacity;
        if (ImGui.SliderFloat(Loc.GetString("ui.navigator.opacity"), ref alpha, 0.1f, 1.0f))
        {
            _config.Navigator.Opacity = alpha;
            changed = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 元素开关
        bool ogcd = _config.Navigator.ShowOgcdRail;
        if (ImGui.Checkbox(Loc.GetString("ui.navigator.show_ogcd"), ref ogcd)) { _config.Navigator.ShowOgcdRail = ogcd; changed = true; }

        bool reason = _config.Navigator.ShowReasonBar;
        if (ImGui.Checkbox(Loc.GetString("ui.navigator.show_reason"), ref reason)) { _config.Navigator.ShowReasonBar = reason; changed = true; }

        bool ast = _config.Navigator.ShowAstPanel;
        if (ImGui.Checkbox(Loc.GetString("ui.navigator.show_ast_panel"), ref ast)) { _config.Navigator.ShowAstPanel = ast; changed = true; }

        bool perf = _config.Navigator.ShowPerformanceData;
        if (ImGui.Checkbox(Loc.GetString("ui.navigator.show_perf"), ref perf)) { _config.Navigator.ShowPerformanceData = perf; changed = true; }

        if (changed) _config.Save();
    }

    private void DrawAstrologianTab()
    {
        bool changed = false;
        ImGui.Spacing();

        // HP阈值
        float emergency = _config.Astrologian.EmergencyHpThreshold * 100f;
        if (ImGui.SliderFloat(Loc.GetString("ui.ast.emergency_hp"), ref emergency, 10f, 50f, "%.0f%%"))
        {
            _config.Astrologian.EmergencyHpThreshold = emergency / 100f;
            changed = true;
        }

        float preemptive = _config.Astrologian.PreemptiveHpThreshold * 100f;
        if (ImGui.SliderFloat(Loc.GetString("ui.ast.preemptive_hp"), ref preemptive, 50f, 95f, "%.0f%%"))
        {
            _config.Astrologian.PreemptiveHpThreshold = preemptive / 100f;
            changed = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 发牌策略
        bool smartCard = _config.Astrologian.SmartCardPlay;
        if (ImGui.Checkbox(Loc.GetString("ui.ast.smart_card"), ref smartCard))
        {
            _config.Astrologian.SmartCardPlay = smartCard;
            changed = true;
        }

        bool burstDps = _config.Astrologian.PrioritizeBurstDps;
        if (ImGui.Checkbox(Loc.GetString("ui.ast.burst_priority"), ref burstDps))
        {
            _config.Astrologian.PrioritizeBurstDps = burstDps;
            changed = true;
        }

        bool avoidDying = _config.Astrologian.AvoidDyingTargets;
        if (ImGui.Checkbox(Loc.GetString("ui.ast.avoid_dying"), ref avoidDying))
        {
            _config.Astrologian.AvoidDyingTargets = avoidDying;
            changed = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 占卜与DoT
        if (ImGui.RadioButton(Loc.GetString("ui.ast.divination_align"), _config.Astrologian.DivinationMode == 0))
        {
            _config.Astrologian.DivinationMode = 0;
            changed = true;
        }
        if (ImGui.RadioButton(Loc.GetString("ui.ast.divination_asap"), _config.Astrologian.DivinationMode == 1))
        {
            _config.Astrologian.DivinationMode = 1;
            changed = true;
        }

        float dot = _config.Astrologian.DotRefreshThreshold;
        if (ImGui.SliderFloat(Loc.GetString("ui.ast.dot_threshold"), ref dot, 1.0f, 5.0f, "%.1fs"))
        {
            _config.Astrologian.DotRefreshThreshold = dot;
            changed = true;
        }

        if (changed) _config.Save();
    }

    private void DrawLanguageTab()
    {
        bool changed = false;
        ImGui.Spacing();

        int currentIndex = Array.IndexOf(_languages, _config.Language);
        if (currentIndex == -1) currentIndex = 0;

        if (ImGui.BeginCombo(Loc.GetString("Config_Language"), _languageNames[currentIndex]))
        {
            for (int i = 0; i < _languages.Length; i++)
            {
                bool isSelected = (currentIndex == i);
                if (ImGui.Selectable(_languageNames[i], isSelected))
                {
                    _config.Language = _languages[i];
                    Loc.SetLanguage(_languages[i]);
                    changed = true;
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted(Loc.GetString("ui.lang.preview"));
        ImGui.Indent();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "- " + Loc.GetString("Reason_EmergencyHeal"));
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "- " + Loc.GetString("Reason_DoTRefresh"));
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "- " + Loc.GetString("Reason_PlayMeleeCard"));
        ImGui.Unindent();

        if (changed) _config.Save();
    }

    private void DrawAboutTab()
    {
        ImGui.Spacing();
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        
        ImGui.TextUnformatted($"{Loc.GetString("Plugin_Name")}");
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), $"{Loc.GetString("ui.about.version")} {version}");
        
        ImGui.Spacing();
        ImGui.TextWrapped(Loc.GetString("ui.about.description"));
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        ImGui.TextColored(new Vector4(0.3f, 0.6f, 0.9f, 1.0f), "GitHub: https://github.com/AstralSolver/AstralSolver");
    }
}

using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

using AstralSolver.Core;

namespace AstralSolver;

/// <summary>
/// 导航辅助显示的详细程度
/// </summary>
public enum NavigatorDisplayMode
{
    Minimal = 0,
    Standard = 1,
    Expert = 2,
    Training = 3
}

/// <summary>
/// 用户配置持久化模型
/// </summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ── 通用设置 ──────────────────────────────────────────────
    
    /// <summary>是否启用插件主逻辑</summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>运行模式</summary>
    public DecisionMode Mode { get; set; } = DecisionMode.Navigator;
    
    /// <summary>语言设置：zh_CN, ja_JP, en_US</summary>
    public string Language { get; set; } = "zh_CN";

    // ── 导航器设置 ────────────────────────────────────────────
    
    public NavigatorConfig Navigator { get; set; } = new();

    [Serializable]
    public class NavigatorConfig
    {
        public int DisplayMode { get; set; } = 1; // 0=Minimal,1=Standard,2=Expert,3=Training
        public float IconSize { get; set; } = 48f;
        public float OffsetX { get; set; } = 0f;
        public float OffsetY { get; set; } = 0f;
        public float Opacity { get; set; } = 0.85f;
        public bool ShowOgcdRail { get; set; } = true;
        public bool ShowReasonBar { get; set; } = true;
        public bool ShowAstPanel { get; set; } = true;
        public bool ShowPerformanceData { get; set; } = false;
    }

    // ── 占星设置 ──────────────────────────────────────────────
    
    public AstrologianConfig Astrologian { get; set; } = new();

    [Serializable]
    public class AstrologianConfig
    {
        public float EmergencyHpThreshold { get; set; } = 0.30f;
        public float PreemptiveHpThreshold { get; set; } = 0.80f;
        public bool SmartCardPlay { get; set; } = true;
        public bool PrioritizeBurstDps { get; set; } = true;
        public bool AvoidDyingTargets { get; set; } = true;
        public int DivinationMode { get; set; } = 0; // 0=对齐120s窗口, 1=CD好了就用
        public float DotRefreshThreshold { get; set; } = 3.0f;
    }

    // ── 高级/底层设置 ─────────────────────────────────────────

    /// <summary>环形缓冲区容量（帧数），默认 18000 约等于 5 分钟 @ 60fps</summary>
    public int SnapshotRingCapacity { get; set; } = 18000;

    /// <summary>非战斗状态时的采集间隔帧数（降频采集）</summary>
    public int IdlePollingInterval { get; set; } = 10;

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    /// <summary>
    /// 初始化并保存接口引用
    /// </summary>
    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    /// <summary>
    /// 保存当前配置到磁盘
    /// </summary>
    public void Save()
    {
        _pluginInterface?.SavePluginConfig(this);
    }
}

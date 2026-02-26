using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using AstralSolver.Core;
using AstralSolver.Jobs.Healer;
using AstralSolver.Navigator;
using AstralSolver.UI;
using AstralSolver.Localization;
using AstralSolver.Utils;

namespace AstralSolver;

/// <summary>
/// 插件主入口点。
/// 构建并持有所有核心模块的生命周期，
/// 完整的创建链和销毁链均在此文件内管理。
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    public string Name => "AstralSolver";

    // ── Dalamud 注入服务（静态 + PluginService 特性）──────────────────────
    [PluginService] public static IDalamudPluginInterface PluginInterface  { get; private set; } = null!;
    [PluginService] public static ICommandManager        CommandManager    { get; private set; } = null!;
    [PluginService] public static IFramework             Framework         { get; private set; } = null!;
    [PluginService] public static IPartyList             PartyList         { get; private set; } = null!;
    [PluginService] public static IClientState           ClientState       { get; private set; } = null!;
    [PluginService] public static ICondition             Condition         { get; private set; } = null!;
    [PluginService] public static IPluginLog             PluginLog         { get; private set; } = null!;
    [PluginService] public static IObjectTable           ObjectTable       { get; private set; } = null!;
    [PluginService] public static IPlayerState           PlayerStateService{ get; private set; } = null!;
    [PluginService] public static ITargetManager         TargetManager     { get; private set; } = null!;
    [PluginService] public static ITextureProvider       TextureProvider   { get; private set; } = null!;
    [PluginService] public static IDataManager           DataManager       { get; private set; } = null!;
    [PluginService] public static IChatGui               ChatGui           { get; private set; } = null!;

    // ── 配置（公共，供各模块访问）──────────────────────────────────────────
    public Configuration Configuration { get; init; }

    // ── 核心模块字段 ───────────────────────────────────────────────────────
    private readonly StateTracker       _stateTracker;
    private readonly DecisionEngine     _decisionEngine;
    private readonly ActionQueue        _actionQueue;
    private readonly NavigatorRenderer  _navigatorRenderer;
    private readonly OverlayWindow      _overlayWindow;
    private readonly MainWindow         _mainWindow;
    private readonly WindowSystem       _windowSystem;

    // ── 构造函数参数与静态属性双重注入（Dalamud v14 要求）────────────────────
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager         commandManager,
        IFramework              framework,
        IPartyList              partyList,
        IClientState            clientState,
        ICondition              condition,
        IPluginLog              pluginLog,
        IObjectTable            objectTable,
        IPlayerState            playerState,
        ITargetManager          targetManager,
        ITextureProvider        textureProvider,
        IDataManager            dataManager,
        IChatGui                chatGui)
    {
        // ── 0. 注入 Dalamud 服务到静态访问器 ─────────────────────────────
        PluginInterface     = pluginInterface;
        CommandManager      = commandManager;
        Framework           = framework;
        PartyList           = partyList;
        ClientState         = clientState;
        Condition           = condition;
        PluginLog           = pluginLog;
        ObjectTable         = objectTable;
        PlayerStateService  = playerState;
        TargetManager       = targetManager;
        TextureProvider     = textureProvider;
        DataManager         = dataManager;
        ChatGui             = chatGui;

        // ── a. 配置：从磁盘加载，或创建默认值 ───────────────────────────
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(pluginInterface);

        // ── b. 本地化：先注入日志，再加载语言包 ──────────────────────────
        Loc.Initialize(pluginLog);
        Loc.LoadLanguage(Configuration.Language);

        // ── c. GameDataReader：封装所有 unsafe FFXIVClientStructs 读取 ──
        var dataReader = new GameDataReader();

        // ── d. StateTracker：每帧采集游戏状态，内部订阅 IFramework.Update ─
        _stateTracker = new StateTracker(
            objectTable,
            partyList,
            condition,
            targetManager,
            framework,
            pluginLog,
            Configuration,
            dataReader);

        // ── e. DecisionEngine：分层决策（安全层→职业模块→合并输出）────────
        _decisionEngine = new DecisionEngine(_stateTracker, Configuration, pluginLog);

        // ── f. AstrologianModule：实例化并注册到 DecisionEngine ──────────
        _decisionEngine.RegisterJobModule(
            Constants.JobIds.Astrologian,
            new AstrologianModule(pluginLog));

        // ── g. ActionQueue：技能执行队列（GCD/oGCD 时序发出指令）─────────
        _actionQueue = new ActionQueue(pluginLog, Configuration, dataReader);

        // ── h. ReasonEngine + DualRailTimeline + PerformanceScorer ───────
        //     三者均为轻量值类型组件，由 NavigatorRenderer 内部直接持有。
        //     此处显式实例化是为了文档化"完整创建链"步骤语义。
        var reasonEngine      = new ReasonEngine();
        var dualRailTimeline  = new DualRailTimeline();
        var performanceScorer = new PerformanceScorer();

        // ── k. NavigatorRenderer：双轨时间轴 UI 渲染器 ───────────────────
        _navigatorRenderer = new NavigatorRenderer(textureProvider, dataManager, pluginLog);

        // ── l. OverlayWindow：战斗中覆盖层（NoInputs 点击穿透）───────────
        _overlayWindow = new OverlayWindow(_navigatorRenderer);

        // ── m. MainWindow：设置主界面（TabBar 多标签页）──────────────────
        _mainWindow = new MainWindow(Configuration, pluginLog);

        // ── n. WindowSystem：Dalamud 窗口管理器，统一管理所有 ImGui 窗口 ──
        _windowSystem = new WindowSystem("AstralSolver");
        _windowSystem.AddWindow(_overlayWindow);
        _windowSystem.AddWindow(_mainWindow);
        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;

        // ── o. 注册聊天命令 ───────────────────────────────────────────────
        commandManager.AddHandler("/astral", new CommandInfo(OnAstralCommand)
        {
            HelpMessage = Loc.GetString("Command_Astral_Help")
        });
        commandManager.AddHandler("/astraltoggle", new CommandInfo(OnToggleCommand)
        {
            HelpMessage = Loc.GetString("Command_AstralToggle_Help")
        });

        // ── p. 订阅事件 ───────────────────────────────────────────────────
        // 每次决策完成后：① 刷新 Navigator UI  ② Auto 模式下推送给 ActionQueue
        _decisionEngine.OnDecisionUpdated += _navigatorRenderer.UpdateDecision;
        _decisionEngine.OnDecisionUpdated += OnDecisionForAutoMode;

        // 每帧更新（决策+执行）
        framework.Update += OnFrameworkUpdate;

        _overlayWindow.IsOpen = Configuration.IsEnabled
            && Configuration.Mode != DecisionMode.Disabled;

        PluginLog.Information("[Plugin] ✅ AstralSolver 初始化完成 | 版本: {0} | 模式: {1}",
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
            Configuration.Mode);
    }

    // ═══════════════════════════════════════════════════
    //  命令处理
    // ═══════════════════════════════════════════════════

    /// <summary>/astral — 切换 MainWindow 的显示状态（打开/关闭设置界面）</summary>
    private void OnAstralCommand(string command, string args)
    {
        _mainWindow.IsOpen = !_mainWindow.IsOpen;
        PluginLog.Information("[Plugin] /astral → MainWindow {0}", _mainWindow.IsOpen ? "打开 ✅" : "关闭 ❌");
    }

    /// <summary>/astraltoggle — 切换插件启用/禁用状态</summary>
    private void OnToggleCommand(string command, string args)
    {
        Configuration.IsEnabled = !Configuration.IsEnabled;
        Configuration.Save();

        _overlayWindow.IsOpen = Configuration.IsEnabled
            && Configuration.Mode != DecisionMode.Disabled;

        // 在聊天框中输出状态提示，方便玩家在战斗中快速确认
        string state = Configuration.IsEnabled ? "启用" : "禁用";
        ChatGui.Print($"[AstralSolver] 插件已{state}");
        PluginLog.Information("[Plugin] /astraltoggle → 插件 {0}", state);
    }

    // ═══════════════════════════════════════════════════
    //  每帧更新钩子
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 每帧更新。
    /// StateTracker 已通过自己内部的 IFramework.Update 完成本帧采集，
    /// 此处只负责决策触发和技能执行。
    /// </summary>
    private void OnFrameworkUpdate(IFramework fw)
    {
        try
        {
            _overlayWindow.IsOpen = Configuration.IsEnabled
                && Configuration.Mode != DecisionMode.Disabled;

            _overlayWindow.UpdatePosition(
                Configuration.NavigatorOffsetX,
                Configuration.NavigatorOffsetY);

            // ActionQueue 每帧都需要 Tick（内部自己判断是否执行）
            _actionQueue.Tick(_stateTracker.Current);

            // 无论是否在战斗，每帧都更新 UI 状态（非常轻量）
            var current = _stateTracker.Current;
            if (current != BattleSnapshot.Empty && current.FrameNumber > 0)
            {
                string jobName = GetJobName(current.Player.JobId);
                _mainWindow.UpdateStatus(jobName, _stateTracker.AverageFrameTimeMs, 0);
            }

            // 以下条件全部满足才触发决策引擎
            if (!Configuration.IsEnabled)              return;
            if (!_stateTracker.IsInCombat)             return;
            if (_stateTracker.Current.FrameNumber <= 0) return;

            _decisionEngine.Update();
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "[Plugin] 每帧更新(OnFrameworkUpdate)发生未处理异常");
        }
    }

    // ═══════════════════════════════════════════════════
    //  决策事件回调
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Auto 模式专用：将最新决策推送给 ActionQueue，让 Tick 按时序执行。
    /// Navigator/Training/Disabled 时 ActionQueue 内部会直接忽略。
    /// </summary>
    private void OnDecisionForAutoMode(DecisionPacket packet)
    {
        if (Configuration.Mode == DecisionMode.Auto)
            _actionQueue.SubmitDecision(packet);
    }

    // ═══════════════════════════════════════════════════
    //  资源释放（严格逆创建顺序）
    // ═══════════════════════════════════════════════════

    public void Dispose()
    {
        // ── 1. 取消事件订阅（最优先，防止后续 Dispose 中回调残留指针）────
        Framework.Update -= OnFrameworkUpdate;
        _decisionEngine.OnDecisionUpdated -= _navigatorRenderer.UpdateDecision;
        _decisionEngine.OnDecisionUpdated -= OnDecisionForAutoMode;

        // ── 2. 注销聊天命令 ───────────────────────────────────────────────
        CommandManager.RemoveHandler("/astral");
        CommandManager.RemoveHandler("/astraltoggle");

        // ── 3. 注销 UI 绘制钩子并释放窗口 ───────────────────────────────
        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _mainWindow.Dispose();
        _overlayWindow.Dispose();
        // NavigatorRenderer 不实现 IDisposable，纹理缓存随 GC 释放

        // ── 4. 按依赖逆序释放核心模块 ────────────────────────────────────
        _actionQueue.Dispose();
        _decisionEngine.Dispose();
        _stateTracker.Dispose();

        // ── 5. 保存最终配置 ───────────────────────────────────────────────
        Configuration.Save();

        PluginLog.Information("[Plugin] AstralSolver 已卸载，配置已保存");
    }

    private static string GetJobName(byte jobId)
    {
        return jobId switch
        {
            33 => "AST (占星術士)",
            24 => "WHM (白魔道士)",
            28 => "SCH (学者)",
            40 => "SGE (贤者)",
            0  => "None",
            _  => $"JobId={jobId}",
        };
    }
}

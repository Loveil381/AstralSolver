using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin.Services;

namespace AstralSolver.Localization;

/// <summary>
/// 多语言本地化管理类。
/// 语言包 JSON 文件从插件 DLL 所在目录加载（Dalamud 运行时路径）。
/// 键未找到时返回 [key] 占位符，并通过日志输出一次性警告（避免每帧刷屏）。
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> _strings = new();
    private static string _currentLanguage = "zh_CN";

    // 用于避免重复输出"键未找到"警告的去重集合
    private static readonly HashSet<string> _warnedKeys = new();

    // 日志引用，在插件初始化时从外部注入
    private static IPluginLog? _log;

    /// <summary>
    /// 注入日志服务（在 Plugin 构造函数调用 LoadLanguage 之前调用）
    /// </summary>
    public static void Initialize(IPluginLog log) => _log = log;

    /// <summary>
    /// 加载指定的语言包文件。
    /// 路径从插件 DLL 所在目录解析，支持 Localization/ 子目录和直接放置两种布局。
    /// </summary>
    /// <param name="languageCode">语言代码，如 zh_CN</param>
    public static void LoadLanguage(string languageCode)
    {
        SetLanguage(languageCode);
    }

    /// <summary>
    /// 切换当前使用的语言包，并重新加载对应 JSON 文件。
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
        _warnedKeys.Clear(); // 切换语言时清空已警告键集合

        try
        {
            // 从插件 DLL 所在目录定位语言包文件
            // Dalamud 运行时 AssemblyLocation 指向实际安装/加载路径
            var pluginDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (pluginDir == null)
            {
                _log?.Error("[Loc] 无法获取插件目录（AssemblyLocation.DirectoryName 为 null）");
                return;
            }

            // 尝试路径 1: 与 DLL 同级目录（DalamudPackager 默认扁平化输出）
            var flatPath = Path.Combine(pluginDir, $"{languageCode}.json");
            // 尝试路径 2: Localization 子目录（开发时 csproj 复制目标）
            var subDirPath = Path.Combine(pluginDir, "Localization", $"{languageCode}.json");

            string? filePath = null;
            if (File.Exists(flatPath))
                filePath = flatPath;
            else if (File.Exists(subDirPath))
                filePath = subDirPath;

            // 找不到目标语言时回退到英文
            if (filePath == null)
            {
                _log?.Error("[Loc] 语言文件未找到: {0}（已尝试路径: {1} | {2}）", languageCode, flatPath, subDirPath);

                var fallbackFlat   = Path.Combine(pluginDir, "en_US.json");
                var fallbackSubDir = Path.Combine(pluginDir, "Localization", "en_US.json");
                if (File.Exists(fallbackFlat))        filePath = fallbackFlat;
                else if (File.Exists(fallbackSubDir)) filePath = fallbackSubDir;
            }

            if (filePath == null)
            {
                _log?.Error("[Loc] 回退 en_US.json 也不存在，UI 将显示原始键名。");
                return;
            }

            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null)
            {
                _strings = dict;
                _log?.Information("[Loc] ✅ 已加载语言包: {0} ({1} 个键) | 路径: {2}", languageCode, dict.Count, filePath);
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "[Loc] 加载语言包异常，语言={0}", languageCode);
        }
    }

    /// <summary>
    /// 根据键获取当前语言对应的字符串。
    /// 如果键不存在，返回 [key] 占位符，并首次遇到时输出一次性警告日志。
    /// </summary>
    /// <param name="key">多语言键名</param>
    /// <returns>翻译字符串；键缺失时返回 [key]</returns>
    public static string GetString(string key)
    {
        if (_strings.TryGetValue(key, out var val))
            return val;

        // 首次遇到缺失键时输出一次性警告（HashSet 去重，避免每帧刷日志）
        if (_warnedKeys.Add(key))
        {
            _log?.Warning("[Loc] 翻译键未找到: {0}, 当前语言: {1}, 已加载键数: {2}",
                key, _currentLanguage, _strings.Count);
        }

        return $"[{key}]";
    }
}

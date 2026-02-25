using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin;

namespace AstralSolver.Localization;

/// <summary>
/// 多语言本地化管理类
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> _strings = new();
    private static string _currentLanguage = "en_US";

    /// <summary>
    /// 加载指定的语言包文件
    /// </summary>
    /// <param name="languageCode">语言代码，如 zh_CN</param>
    public static void LoadLanguage(string languageCode)
    {
        SetLanguage(languageCode);
    }

    /// <summary>
    /// 切换当前使用的语言包
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
        try
        {
            var pluginDir = Plugin.PluginInterface.AssemblyLocation.DirectoryName;
            if (pluginDir == null) return;
            
            var filePath = Path.Combine(pluginDir, "Localization", $"{languageCode}.json");
            if (!File.Exists(filePath))
            {
                // 如果找不到指定语言包，回退到英文
                filePath = Path.Combine(pluginDir, "Localization", "en_US.json");
            }
            
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null)
                {
                    _strings = dict;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error(ex, "加载语言包失败");
        }
    }

    /// <summary>
    /// 根据键获取对应语言的字符串
    /// </summary>
    /// <param name="key">多语言键名</param>
    /// <returns>获取到的字符串，如果缺失则返回键名</returns>
    public static string GetString(string key)
    {
        return _strings.TryGetValue(key, out var val) ? val : $"[{key}]";
    }
}

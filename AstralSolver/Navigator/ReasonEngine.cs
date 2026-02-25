using System;
using AstralSolver.Core;
using AstralSolver.Localization;

namespace AstralSolver.Navigator;

/// <summary>
/// 原因引擎：负责将内部的数值权重评估，转换为人类可读的语言解释
/// </summary>
public class ReasonEngine
{
    /// <summary>
    /// 将决策理由模板键转化为多语言文本
    /// </summary>
    public string Format(ReasonEntry entry)
    {
        // 尝试通过 Loc 获取多语言文本，获取不到时 Loc 本身会降级返回键名
        return Loc.GetString(entry.TemplateKey);
    }

    /// <summary>
    /// 批量格式化决策理由，并按优先级排序 (Critical > Important > Info)
    /// </summary>
    public string[] FormatAll(ReasonEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
            return Array.Empty<string>();

        // 避免使用 LINQ，手写排序和转换
        var list = new ReasonEntry[entries.Length];
        Array.Copy(entries, list, entries.Length);
        
        // Priority 是 byte，越大的枚举值排前面 (Critical > Important > Info) 这样排序对么？
        // 其实可以只判断枚举的整数值： b.Priority - a.Priority。
        Array.Sort(list, (a, b) => b.Priority.CompareTo(a.Priority));

        var result = new string[list.Length];
        for (int i = 0; i < list.Length; i++)
        {
            result[i] = Format(list[i]);
        }
        return result;
    }
}

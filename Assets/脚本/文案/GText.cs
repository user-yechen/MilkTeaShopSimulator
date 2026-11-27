using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 全局文案表：从 Resources/文案/文案表.csv 读取，key → 中文。
/// 用法：GText.Get("ui.shop.title") / GText.Format("ui.day.end_body", a, b)
/// 改文案只改 CSV，不改代码。
/// </summary>
public static class GText
{
    public const string ResourcePath = "文案/文案表";

    static Dictionary<string, string> table;
    static readonly HashSet<string> warned = new HashSet<string>();

    public static int Count
    {
        get { EnsureLoaded(); return table != null ? table.Count : 0; }
    }

    public static string Get(string key, string fallback = null)
    {
        EnsureLoaded();
        string value;
        if (table != null && table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            return value;

        if (fallback != null) return fallback;
        if (!string.IsNullOrEmpty(key) && warned.Add(key))
            Debug.LogWarning("[GText] 缺少文案：" + key);
        return key;
    }

    public static string Format(string key, params object[] args)
    {
        string template = Get(key);
        try { return string.Format(template, args); }
        catch (System.FormatException)
        {
            Debug.LogWarning("[GText] 占位符不匹配：" + key);
            return template;
        }
    }

    public static bool Has(string key)
    {
        EnsureLoaded();
        return table != null && table.ContainsKey(key);
    }

    /// <summary>强制重载（编辑器里改完 CSV 可调用）</summary>
    public static void Reload()
    {
        table = null;
        warned.Clear();
        EnsureLoaded();
    }

    static void EnsureLoaded()
    {
        if (table != null) return;

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogError("[GText] 找不到 Resources/" + ResourcePath + ".csv");
            table = new Dictionary<string, string>();
            return;
        }

        var rows = ParseRows(asset.text);
        table = new Dictionary<string, string>(rows.Count);
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 3) continue;
            string key = row[0].Trim();
            if (string.IsNullOrEmpty(key)) continue;
            // CSV 里的 \n 两个字符还原成换行
            string value = row[2].Replace("\\n", "\n");
            if (!table.ContainsKey(key)) table.Add(key, value);
        }
    }

    /// <summary>解析 CSV（支持引号、逗号、换行、BOM）。第 0 行是表头。</summary>
    public static List<string[]> ParseRows(string csv)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(csv)) return rows;
        if (csv.Length > 0 && csv[0] == '\uFEFF') csv = csv.Substring(1);

        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            if (c == '"') { inQuotes = true; continue; }
            if (c == ',') { row.Add(field.ToString()); field.Length = 0; continue; }
            if (c == '\r') continue;
            if (c == '\n')
            {
                row.Add(field.ToString());
                field.Length = 0;
                if (!IsEmptyRow(row)) rows.Add(row.ToArray());
                row.Clear();
                continue;
            }
            field.Append(c);
        }

        row.Add(field.ToString());
        if (!IsEmptyRow(row)) rows.Add(row.ToArray());
        return rows;
    }

    static bool IsEmptyRow(List<string> row)
    {
        for (int i = 0; i < row.Count; i++)
            if (!string.IsNullOrEmpty(row[i].Trim())) return false;
        return true;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 价格表读取（每次游戏运行前读一次）：
/// 启动时（BeforeSceneLoad，早于场景物体 Awake）读 Resources/价格表.txt，
/// 格式「物件ID,等级,价格」（# 注释，等级可省默认 1，中英文逗号均可）。
/// ShopItem 用 GetPrice(itemId, itemLevel, fallback) 查价；查不到用 Inspector 里的 price 兜底。
/// 注意：txt 必须在 Resources 下，否则打包后读不到（经验 21）。
/// </summary>
public static class ShopPriceTable
{
    static readonly Dictionary<string, int> prices = new Dictionary<string, int>();
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoLoad() => Load();

    /// <summary>读取并解析价格表；重复调用只生效一次</summary>
    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        TextAsset asset = Resources.Load<TextAsset>("价格表");
        if (asset == null)
        {
            Debug.LogWarning("[ShopPriceTable] Resources/价格表.txt 不存在，商品价格全部用 ShopItem 里的默认值");
            return;
        }

        int count = 0;
        foreach (string rawLine in asset.text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            string[] parts = line.Split(',', '，');
            if (parts.Length < 2)
            {
                Debug.LogWarning("[ShopPriceTable] 跳过无法解析的行：" + line);
                continue;
            }

            string id = parts[0].Trim();
            int level = 1;
            string pricePart;
            if (parts.Length >= 3 && int.TryParse(parts[1].Trim(), out int lv))
            {
                level = lv;
                pricePart = parts[2];
            }
            else
            {
                pricePart = parts[1];   // 只有「ID,价格」两段
            }

            if (!int.TryParse(pricePart.Trim(), out int price))
            {
                Debug.LogWarning("[ShopPriceTable] 价格不是数字，跳过：" + line);
                continue;
            }

            prices[Key(id, level)] = price;
            count++;
        }
        Debug.Log($"[ShopPriceTable] 已读取 {count} 条价格");
    }

    /// <summary>查价格；表里没有则返回 fallback</summary>
    public static int GetPrice(string id, int level, int fallback)
    {
        Load();
        return id != null && prices.TryGetValue(Key(id, Mathf.Max(1, level)), out int p) ? p : fallback;
    }

    /// <summary>表里是否有该条目</summary>
    public static bool Has(string id, int level)
    {
        Load();
        return id != null && prices.ContainsKey(Key(id, Mathf.Max(1, level)));
    }

    static string Key(string id, int level) => id + "@" + level;
}

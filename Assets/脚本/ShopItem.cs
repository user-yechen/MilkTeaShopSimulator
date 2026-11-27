using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店物品（挂在「物品容器」预制件上）：
/// 未拥有 → 显示金额，点击弹「确认购买」窗口，钱包足够则扣款并把物件设为 itemLevel；
/// 已拥有或已有更高等级 → 显示「已拥有」、隐藏金额，点击只弹提示。
/// 拥有判定：gameTime.json 里该物件等级 ≥ itemLevel（等级与天数/钱包同档存储）。
/// 用法：把本组件加到商店 Content 下每个物品容器上，Inspector 填 itemId / itemLevel / price；
///       金额 Text（名字含 金额/价格/Price）和「已拥有」标记留空会自动按名字找。
/// </summary>
[RequireComponent(typeof(Button))]
public class ShopItem : MonoBehaviour
{
    [Header("商品设置")]
    [Tooltip("物件 ID，与 MachineLevel / 存档里的规范 ID 一致（如 前制作台、奶茶机）")]
    public string itemId = "前制作台";
    [Tooltip("显示名，留空用物件 ID")]
    public string displayName = "";
    [Tooltip("购买后达到的等级（1=首次拥有，2=升到 2 级…）；已有同级或更高即视为已拥有")]
    public int itemLevel = 1;
    [Tooltip("价格（元）；价格表.txt 里有该条目时以价格表为准")]
    public int price = 100;

    [Header("子物体引用（留空按名字自动找）")]
    [Tooltip("显示金额的 Text（名字含 金额/价格/Price）")]
    public Text priceText;
    [Tooltip("「已拥有」标记物体（名字含 已拥有）")]
    public GameObject ownedMark;

    static readonly List<ShopItem> all = new List<ShopItem>();

    string Name => string.IsNullOrEmpty(displayName) ? itemId : displayName;

    /// <summary>实际售价：价格表优先，没有则用 Inspector 里的 price</summary>
    public int EffectivePrice => ShopPriceTable.GetPrice(itemId, itemLevel, price);

    void Awake()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        if (priceText == null) priceText = FindTextByKeywords("金额", "价格", "Price");
        if (ownedMark == null) ownedMark = FindChildByKeyword("已拥有");

        // 这两个的活跃状态互斥（见 Refresh）；找不到就不会被显隐，预制件默认状态会导致两边同时显示
        if (priceText == null)
            Debug.LogWarning($"[ShopItem] {name}（{itemId}）找不到金额 Text（名字需含 金额/价格/Price），价格无法自动显隐");
        if (ownedMark == null)
            Debug.LogWarning($"[ShopItem] {name}（{itemId}）找不到「已拥有」标记（名字需含 已拥有），拥有状态无法自动显隐");
    }

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        Refresh();
    }

    void OnDisable() => all.Remove(this);

    /// <summary>已拥有：存档里该物件等级 ≥ itemLevel</summary>
    public bool IsOwned => GameTimeStore.GetMachineLevel(itemId, 1) >= itemLevel;

    /// <summary>按拥有状态刷新显示（已拥有=隐藏金额显示标记）</summary>
    public void Refresh()
    {
        bool owned = IsOwned;
        if (priceText != null)
        {
            priceText.gameObject.SetActive(!owned);
            if (!owned) priceText.text = EffectivePrice + " 元";
        }
        if (ownedMark != null) ownedMark.SetActive(owned);
    }

    /// <summary>刷新商店里所有商品（购买后调用）</summary>
    public static void RefreshAll()
    {
        foreach (ShopItem item in all) item.Refresh();
    }

    void OnClick()
    {
        if (IsOwned)
        {
            ShopPurchaseDialog.ShowMessage("已拥有「" + Name + "」");
            return;
        }
        int wallet = GameClock.Instance != null ? GameClock.Instance.Wallet : 0;
        int cost = EffectivePrice;
        if (wallet < cost)
        {
            ShopPurchaseDialog.ShowMessage("余额不足\n购买「" + Name + "」需要 " + cost + " 元\n当前钱包：" + wallet + " 元");
            return;
        }
        ShopPurchaseDialog.ShowConfirm(Name, cost, Purchase);
    }

    void Purchase()
    {
        int cost = EffectivePrice;
        if (GameClock.Instance == null || !GameClock.Instance.TrySpend(cost))
        {
            ShopPurchaseDialog.ShowMessage("余额不足，购买失败");
            return;
        }

        MachineLevel ml = MachineLevel.Get(itemId);
        if (ml != null) ml.SetLevel(itemLevel);            // SetLevel 内部写存档
        else GameTimeStore.SetMachineLevel(itemId, itemLevel);

        RefreshAll();
        Debug.Log($"[ShopItem] 购买成功：{Name} → {itemLevel} 级，花费 {cost} 元");
    }

    Text FindTextByKeywords(params string[] keywords)
    {
        foreach (Text t in GetComponentsInChildren<Text>(true))
            foreach (string k in keywords)
                if (t.name.Contains(k)) return t;
        return null;
    }

    GameObject FindChildByKeyword(string keyword)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t != transform && t.name.Contains(keyword)) return t.gameObject;
        return null;
    }
}

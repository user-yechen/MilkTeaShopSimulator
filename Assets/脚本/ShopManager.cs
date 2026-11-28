using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 营业流程管理：
/// 1) 营业时间 8:00-16:00（由 GameClock 推进），期间由 PasserbyWalk 循环接待顾客；
/// 2) 每位顾客拿到奶茶后结算营收：10 元 + 随机 1~5 元小费，并实时刷新场景中“营收额”的数值文字；
/// 3) 到 16:00 结算：当天营业额并入钱包（存进 gameTime.json），弹出“今日营业结束”面板，底部“再玩一次”按钮重开一天；
/// 4) “再玩一次”会清零营收（营收额每天从 0 开始）、把时钟拨回 8:00、清空前台奶茶并让顾客重新等待进店。
/// 场景中没有手动挂载时，会在加载后自动挂到 Canvas 上。
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("营业时间")]
    [Range(0, 23)] public int openHour = 8;
    [Range(0, 59)] public int openMinute = 0;
    [Range(0, 23)] public int closeHour = 16;
    [Range(0, 59)] public int closeMinute = 0;

    [Header("营收")]
    [Tooltip("每位顾客的固定收入（元）")]
    public int basePay = 10;
    [Tooltip("小费下限（元，含）")]
    public int minTip = 1;
    [Tooltip("小费上限（元，含）")]
    public int maxTip = 5;

    public int Revenue { get; private set; }
    public bool DayEnded { get; private set; }

    [Header("营收额 / 钱包 UI（按场景物体名自动查找）")]
    [Tooltip("“营收额”面板物体名")]
    public string revenuePanelName = "营收额";
    [Tooltip("“钱包”面板物体名，也可填余额/存款等")]
    public string walletPanelName = "钱包";
    [Tooltip("面板里显示数值的 Text 子物体名（营收额和钱包下都叫「金额」）")]
    public string valueTextName = "金额";
    [Tooltip("营收额显示格式，{0} = 当天营业额")]
    public string revenueFormat = "{0} 元";
    [Tooltip("钱包显示格式，{0} = 钱包余额（旁边有金币图标，默认只显示数字）")]
    public string walletFormat = "{0}";

    RectTransform panel;      // 营业结束面板
    Text resultText;          // 营收文字
    Button restartButton;     // “再玩一次”按钮
    Button doubleButton;      // “看广告双倍领取”按钮
    bool doubledToday;        // 今天是否已经领过双倍
    Text revenueText;         // 场景中“营收额”的数值 Text
    Text walletText;          // 场景中“钱包”一栏的 Text
    int shownRevenue = -1;    // 已显示的营业额，用于避免每帧重复写文字
    int shownWallet = -1;

    void Awake()
    {
        Instance = this;
        EnsureUI();
        EnsureStatLabels();
    }

    void Update()
    {
        RefreshStatLabels();
        if (DayEnded) return;

        GameClock clock = GameClock.Instance;
        if (clock == null) return;
        if (clock.IsAtOrAfter(closeHour, closeMinute)) EndDay();
    }

    /// <summary>是否处于营业时间内且当天尚未结束</summary>
    public bool IsOpen
    {
        get
        {
            GameClock clock = GameClock.Instance;
            if (clock == null) return !DayEnded;
            return !DayEnded
                && clock.IsAtOrAfter(openHour, openMinute)
                && !clock.IsAtOrAfter(closeHour, closeMinute);
        }
    }

    /// <summary>顾客拿到奶茶后结算：固定收入 + 随机小费</summary>
    public void CompleteOrder()
    {
        if (DayEnded) return;
        Revenue += basePay + Random.Range(minTip, maxTip + 1);
        RefreshStatLabels();
    }

    void EndDay()
    {
        DayEnded = true;
        doubledToday = false;
        GameClock clock = GameClock.Instance;
        if (clock != null)
        {
            clock.enabled = false;      // 停在 16:00
            clock.AddToWallet(Revenue); // 打烊结算：当天营业额并入钱包，并写入 JSON 存档
        }
        RefreshStatLabels();
        EnsureUI();
        UpdateEndText();
        if (doubleButton != null) doubleButton.gameObject.SetActive(true);
        panel.SetAsLastSibling();   // 盖住其他运行时创建的 UI
        panel.gameObject.SetActive(true);
    }

    void UpdateEndText()
    {
        if (resultText == null) return;
        GameClock clock = GameClock.Instance;
        int wallet = clock != null ? clock.Wallet : 0;
        resultText.text = "今日营业结束\n今日营收：" + Revenue + " 元\n钱包：" + wallet + " 元";
    }

    /// <summary>打烊面板：看激励视频，把今天的营收再领一遍（双倍）</summary>
    public void OnDoubleRevenue()
    {
        if (!DayEnded || doubledToday) return;

        AdService.Instance.ShowRewarded("day_end_double", ok =>
        {
            if (!ok || doubledToday) return;
            doubledToday = true;

            GameClock clock = GameClock.Instance;
            if (clock != null) clock.AddToWallet(Revenue);   // 再发一遍 = 双倍

            RefreshStatLabels();
            UpdateEndText();
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);
        });
    }

    /// <summary>“再玩一次”：重开一天（营收额每天从 0 开始计数）</summary>
    public void RestartDay()
    {
        Revenue = 0;
        doubledToday = false;
        if (doubleButton != null) doubleButton.gameObject.SetActive(false);
        RefreshStatLabels();
        DayEnded = false;
        if (panel != null) panel.gameObject.SetActive(false);

        GameClock clock = GameClock.Instance;
        if (clock != null)
        {
            clock.enabled = true;
            clock.NextDay(openHour, openMinute);   // 天数 +1、时钟回 8:00，并写入 JSON 存档
        }

        // 清空前台上残留的奶茶
        CounterCupDisplay counter = FindObjectOfType<CounterCupDisplay>();
        if (counter != null)
            while (counter.CupCount > 0) counter.TakeOneCup();

        foreach (PasserbyWalk walker in FindObjectsOfType<PasserbyWalk>(true))
            walker.RestartDay();
    }

    /// <summary>
    /// 在场景里找「面板 → 数值 Text」。营收额和钱包下面都叫「金额」，所以先在面板子树里找同名 Text，
    /// 找不到再退回面板下第一个 Text，最后才全局按名字兜底。
    /// </summary>
    Text FindStatText(string panelName, string textName)
    {
        Transform panel = FindTransform(panelName);
        if (panel != null)
        {
            Text exact = null;
            Text any = null;
            foreach (Transform t in panel.GetComponentsInChildren<Transform>(true))
            {
                Text text = t.GetComponent<Text>();
                if (text == null) continue;
                if (any == null) any = text;
                if (exact == null && t.name == textName) exact = text;
            }
            if (exact != null) return exact;
            if (any != null) return any;
        }

        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t.name != textName) continue;
            Text text = t.GetComponent<Text>();
            if (text != null) return text;
        }
        return null;
    }

    /// <summary>按名字找场景物体：先精确匹配，再退回包含匹配</summary>
    static Transform FindTransform(string targetName)
    {
        Transform contained = null;
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t.name == targetName) return t;
            if (contained == null && t.name.Contains(targetName)) contained = t;
        }
        return contained;
    }

    /// <summary>查找“营收额”和“钱包”的数值 Text，找不到只警告，不新建物体</summary>
    void EnsureStatLabels()
    {
        if (revenueText == null) revenueText = FindStatText(revenuePanelName, valueTextName);
        if (revenueText == null)
            Debug.LogWarning("[ShopManager] 场景中没有找到营收额数值文字（面板'" + revenuePanelName + "' / 文字'" + valueTextName + "'）。");

        if (walletText == null) walletText = FindStatText(walletPanelName, valueTextName);
        if (walletText == null)
            Debug.LogWarning("[ShopManager] 场景中没有找到钱包数值文字（面板'" + walletPanelName + "' / 文字'" + valueTextName + "'）。");

        RefreshStatLabels();
    }

    /// <summary>把营业额 / 钱包余额写进场景 UI，数值没变时不重复写</summary>
    void RefreshStatLabels()
    {
        if (revenueText != null && shownRevenue != Revenue)
        {
            shownRevenue = Revenue;
            revenueText.text = string.Format(revenueFormat, Revenue);
        }

        GameClock clock = GameClock.Instance;
        if (walletText != null && clock != null && shownWallet != clock.Wallet)
        {
            shownWallet = clock.Wallet;
            walletText.text = string.Format(walletFormat, clock.Wallet);
        }
    }

    void EnsureUI()
    {
        if (panel != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("营业Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject panelGo = new GameObject("营业结束面板", typeof(RectTransform), typeof(Image));
        panel = (RectTransform)panelGo.transform;
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        resultText = CreateText(panel, "营业结束文字", 42,
            new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.86f));

        doubleButton = CreateButton(panel, "看广告双倍领取",
            new Vector2(0.5f, 0.34f), new Vector2(300f, 84f), 28);
        doubleButton.onClick.AddListener(OnDoubleRevenue);

        restartButton = CreateButton(panel, "再玩一次",
            new Vector2(0.5f, 0.20f), new Vector2(240f, 84f), 30);
        restartButton.onClick.AddListener(RestartDay);

        doubleButton.gameObject.SetActive(false);
        panelGo.SetActive(false);
    }

    Text CreateText(RectTransform parent, string name, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text text = go.GetComponent<Text>();
        text.font = UIFont.Get();
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    Button CreateButton(RectTransform parent, string label, Vector2 anchor, Vector2 size, int fontSize)
    {
        GameObject go = new GameObject(label + "按钮", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = new Color(1f, 0.62f, 0.24f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        CreateText(rt, "文字", fontSize, Vector2.zero, Vector2.one).text = label;
        return button;
    }

    /// <summary>场景中没有手动挂载时，自动挂到 Canvas 上</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectOfType<ShopManager>() != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        GameObject host = canvas != null ? canvas.gameObject : new GameObject("ShopManager");
        host.AddComponent<ShopManager>();
    }
}

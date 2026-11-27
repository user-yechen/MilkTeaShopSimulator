using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 购买确认弹窗（运行时创建，模态，带半透明遮罩）：
/// ShowConfirm(名称, 价格, onConfirm) —— 确认购买 / 取消 两个按钮；
/// ShowMessage(文本) —— 只有「知道了」（已拥有、余额不足等提示用）。
/// </summary>
public static class ShopPurchaseDialog
{
    static GameObject overlay;
    static Text msgText;
    static Button confirmButton, cancelButton, okButton;
    static System.Action pendingConfirm;

    public static void ShowConfirm(string itemName, int price, System.Action onConfirm)
    {
        Ensure();
        msgText.text = "确认购买「" + itemName + "」？\n价格：" + price + " 元";
        pendingConfirm = onConfirm;
        confirmButton.gameObject.SetActive(true);
        cancelButton.gameObject.SetActive(true);
        okButton.gameObject.SetActive(false);
        Show();
    }

    public static void ShowMessage(string msg)
    {
        Ensure();
        msgText.text = msg;
        pendingConfirm = null;
        confirmButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(false);
        okButton.gameObject.SetActive(true);
        Show();
    }

    static void Show()
    {
        overlay.transform.SetAsLastSibling();   // 盖住商店界面等其它 UI
        overlay.SetActive(true);
    }

    static void Close()
    {
        pendingConfirm = null;
        if (overlay != null) overlay.SetActive(false);
    }

    static void Ensure()
    {
        if (overlay != null) return;
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[ShopPurchaseDialog] 场景里没有 Canvas");
            return;
        }
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 全屏遮罩（拦截点击，模态）
        overlay = new GameObject("购买确认弹窗", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = (RectTransform)overlay.transform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // 居中面板
        GameObject panel = MakePanel("面板", overlay.transform, new Vector2(460f, 240f), new Color(0.97f, 0.94f, 0.88f, 1f));

        msgText = MakeText("提示文字", panel.transform, font, "", 22, new Vector2(0f, 30f), new Vector2(420f, 120f));
        msgText.color = new Color(0.25f, 0.2f, 0.15f);

        confirmButton = MakeButton("确认购买", panel.transform, font, "确认购买", 20, new Vector2(-110f, -70f), new Color(0.45f, 0.75f, 0.45f));
        confirmButton.onClick.AddListener(() =>
        {
            System.Action act = pendingConfirm;
            Close();
            act?.Invoke();
        });
        cancelButton = MakeButton("取消", panel.transform, font, "取消", 20, new Vector2(110f, -70f), new Color(0.8f, 0.55f, 0.45f));
        cancelButton.onClick.AddListener(Close);
        okButton = MakeButton("知道了", panel.transform, font, "知道了", 20, new Vector2(0f, -70f), new Color(0.55f, 0.65f, 0.8f));
        okButton.onClick.AddListener(Close);

        overlay.SetActive(false);
    }

    static GameObject MakePanel(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return go;
    }

    static Text MakeText(string name, Transform parent, Font font, string content, int fontSize, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        Text t = go.GetComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    static Button MakeButton(string name, Transform parent, Font font, string label, int fontSize, Vector2 pos, Color color)
    {
        GameObject go = MakePanel(name, parent, new Vector2(160f, 56f), color);
        ((RectTransform)go.transform).anchoredPosition = pos;
        Button btn = go.AddComponent<Button>();
        Text t = MakeText("文字", go.transform, font, label, fontSize, Vector2.zero, new Vector2(160f, 56f));
        t.color = Color.white;
        return btn;
    }
}

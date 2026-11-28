using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 摇杆位置调整模式（设置界面 → 自定义摇杆位置）：
/// Begin() 进入：隐藏设置面板，屏幕顶部出提示、底部出「确定位置 / 重置默认」按钮；
/// 期间 VirtualJoystick.positionEditMode = true，按住摇杆拖动即可整体移动；
/// 确定 → 保存位置到 PlayerPrefs 并退出；重置 → 清存档回场景默认位置（不退出，可继续拖）。
/// 全部 UI 运行时创建，无需在场景/预制件里预先摆放。
/// </summary>
public static class JoystickPositionAdjuster
{
    static GameObject adjustUI;

    /// <summary>进入调整模式（由 ComingSoonTip 绑定「自定义摇杆位置」按钮调用）</summary>
    public static void Begin(GameObject settingsPanel)
    {
        VirtualJoystick joystick = Object.FindObjectOfType<VirtualJoystick>(true);
        if (joystick == null)
        {
            Debug.LogWarning("[JoystickPositionAdjuster] 场景里找不到 VirtualJoystick，无法调整摇杆位置");
            return;
        }
        if (adjustUI != null) return;   // 已在调整模式中，防重入

        VirtualJoystick.positionEditMode = true;
        if (settingsPanel != null) settingsPanel.SetActive(false);

        BuildUI(joystick);
    }

    static void Confirm(VirtualJoystick joystick)
    {
        joystick.SavePosition();
        Exit();
    }

    static void Exit()
    {
        VirtualJoystick.positionEditMode = false;
        if (adjustUI != null) Object.Destroy(adjustUI);
        adjustUI = null;
    }

    static void BuildUI(VirtualJoystick joystick)
    {
        Canvas canvas = joystick.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[JoystickPositionAdjuster] 找不到 Canvas，调整 UI 无法显示");
            Exit();
            return;
        }

        Font font = UIFont.Get();

        // 根（全屏但不加 Graphic → 不挡摇杆的射线检测）
        adjustUI = new GameObject("摇杆位置调整UI", typeof(RectTransform));
        adjustUI.transform.SetParent(canvas.transform, false);
        RectTransform rootRT = adjustUI.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // 顶部提示
        Text hint = MakeText("提示", rootRT, font, "按住摇杆拖动到顺手的位置，再点「确定位置」", 26,
            new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(800f, 60f));
        hint.color = new Color(1f, 1f, 1f, 0.95f);
        Outline outline = hint.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);

        // 底部按钮
        MakeButton("确定位置按钮", rootRT, font, "确定位置", 30,
            new Vector2(0f, 110f), new Vector2(240f, 70f), new Color(0.2f, 0.7f, 0.3f, 0.95f),
            () => Confirm(joystick));
        MakeButton("重置默认按钮", rootRT, font, "重置默认", 24,
            new Vector2(0f, 200f), new Vector2(200f, 56f), new Color(0.45f, 0.45f, 0.45f, 0.95f),
            joystick.ResetPosition);
    }

    static Text MakeText(string name, Transform parent, Font font, string content, int fontSize,
        Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return text;
    }

    static void MakeButton(string name, Transform parent, Font font, string label, int fontSize,
        Vector2 pos, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // 底部居中
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.color = bg;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        MakeText("Text", go.transform, font, label, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size);
    }
}

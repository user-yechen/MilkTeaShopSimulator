using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左上角图标（地图 / 设置 / 商店）点击入口：
///   - 「地图」图标 → 显示「功能未开放」提示框，3 秒后自动隐藏。
///   - 「设置」图标 → 从 Resources 加载「设置界面」预制件并实例化显示。
///   - 「商店」图标 → 打开商店界面（场景优先，否则 Resources 加载「预制件/商店界面」），并自动给「关闭按钮」补 PanelCloseButton。
/// 运行时自动挂到场景里的「左上角UI」上，不需要手动挂组件。
/// </summary>
public class ComingSoonTip : MonoBehaviour
{
    [Header("提示框（地图用）")]
    public string tipObjectName = "提示框";
    public string tipLabel = "功能未开放";
    public float showSeconds = 3f;

    [Header("设置界面预制件路径（放在 Resources 下，无需 .prefab 后缀）")]
    public string settingsPrefabPath = "预制件/设置界面";

    [Header("商店界面预制件路径（Resources 下）")]
    public string shopPrefabPath = "预制件/商店界面";

    [Header("图标名字关键字（名字包含即匹配）")]
    public string mapIconKeyword = "地图";
    public string settingsIconKeyword = "设置";
    public string shopIconKeyword = "商店";

    GameObject tipObject;
    Text tipText;
    Coroutine hideRoutine;
    GameObject settingsPanelInstance;   // 运行时实例化的设置界面
    GameObject shopPanelInstance;       // 场景里找到的 / 运行时实例化的商店界面

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        GameObject root = FindSceneObject("左上角UI");
        if (root != null && root.GetComponent<ComingSoonTip>() == null)
            root.AddComponent<ComingSoonTip>();
    }

    void Awake()
    {
        // 提示框（给地图图标用）
        tipObject = FindSceneObject(tipObjectName);
        if (tipObject == null)
            tipObject = FindObjectByNameKeyword("提示", "未开放", "Toast", "Tip");

        if (tipObject != null)
        {
            tipText = tipObject.GetComponentInChildren<Text>(true);
            if (tipText == null)
                Debug.LogWarning($"[ComingSoonTip] 提示框 {tipObject.name} 下没有 Text 组件，只能显示/隐藏、无法改文字");
            tipObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[ComingSoonTip] 场景里找不到提示框（按名字「" + tipObjectName + "」查找），地图图标点击不会有反应");
        }

        BindIcons();
    }

    /// <summary>
    /// 给左上角UI下的每个子图标绑定点击回调。
    /// 含「设置」→ OpenSettingsPanel；含「商店」→ OpenShopPanel；其他 → ShowTip 兜底。
    /// </summary>
    void BindIcons()
    {
        foreach (Transform icon in transform)
        {
            Button button = icon.GetComponent<Button>();
            if (button == null)
            {
                Graphic graphic = icon.GetComponent<Graphic>();
                if (graphic == null) continue;
                button = icon.gameObject.AddComponent<Button>();
                button.targetGraphic = graphic;
            }

            if (icon.name.Contains(settingsIconKeyword))
                button.onClick.AddListener(OpenSettingsPanel);
            else if (icon.name.Contains(shopIconKeyword))
                button.onClick.AddListener(OpenShopPanel);
            else
                button.onClick.AddListener(ShowTip);
        }
    }

    // ===== 地图图标：显示未开放提示 =====

    public void ShowTip()
    {
        ShowCustom(tipLabel, showSeconds);
    }

    // ===== 通用文字提示（流体不足/容量显示等动态文本用）=====

    static ComingSoonTip instance;

    void OnEnable() => instance = this;
    void OnDisable() { if (instance == this) instance = null; }

    /// <summary>静态入口：任意脚本提示一段文字，seconds 秒后自动隐藏（复用场景「提示框」）</summary>
    public static void Show(string text, float seconds = 3f)
    {
        if (instance == null) return;
        instance.ShowCustom(text, seconds);
    }

    void ShowCustom(string text, float seconds)
    {
        if (tipObject == null) return;

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        if (tipText != null) tipText.text = text;

        tipObject.SetActive(true);
        hideRoutine = StartCoroutine(HideAfterDelay(seconds));
    }

    IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        tipObject.SetActive(false);
        hideRoutine = null;
    }

    // ===== 设置图标：加载预制件并显示 =====

    public void OpenSettingsPanel()
    {
        // 如果已经存在实例，直接激活
        if (settingsPanelInstance != null)
        {
            settingsPanelInstance.SetActive(true);
            return;
        }

        // 从 Resources 加载预制件
        GameObject prefab = Resources.Load<GameObject>(settingsPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[ComingSoonTip] Resources 下找不到预制件：" + settingsPrefabPath
                + "。请把设置界面预制件放到 Assets/Resources/ 目录下，或修改 settingsPrefabPath 字段。");
            return;
        }

        // 找到 Canvas 作为父物体
        Canvas canvas = FindObjectOfType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform.root;

        settingsPanelInstance = Instantiate(prefab, parent);
        settingsPanelInstance.name = prefab.name;
        settingsPanelInstance.SetActive(true);
        UIFont.ApplyAll();

        EnsureSettingsButtons(settingsPanelInstance);
    }

    /// <summary>
    /// 给设置界面里的按钮补运行时逻辑（幂等）：
    /// 名字含「关闭」→ 补 PanelCloseButton；含「摇杆」→ 进入摇杆位置调整模式。
    /// （「退出游戏」等其余按钮暂不接管）
    /// </summary>
    void EnsureSettingsButtons(GameObject panel)
    {
        foreach (Button b in panel.GetComponentsInChildren<Button>(true))
        {
            if (b.name.Contains("关闭"))
            {
                if (b.GetComponent<PanelCloseButton>() == null)
                    b.gameObject.AddComponent<PanelCloseButton>();
            }
            else if (b.name.Contains("摇杆"))
            {
                GameObject settingsPanel = panel;
                b.onClick.AddListener(() => JoystickPositionAdjuster.Begin(settingsPanel));
            }
        }
    }

    // ===== 商店图标：打开商店界面 =====

    public void OpenShopPanel()
    {
        if (shopPanelInstance == null)
        {
            // 先在场景里找（含未激活），没有再从 Resources 加载预制件
            shopPanelInstance = FindSceneObject("商店界面");
            if (shopPanelInstance == null)
            {
                GameObject shopPrefab = Resources.Load<GameObject>(shopPrefabPath);
                if (shopPrefab == null)
                {
                    Debug.LogError("[ComingSoonTip] 场景里找不到「商店界面」，Resources 下也没有：" + shopPrefabPath
                        + "。请把商店界面预制件放到 Assets/Resources/ 下，或修改 shopPrefabPath 字段。");
                    return;
                }

                Canvas canvas = FindObjectOfType<Canvas>();
                Transform parent = canvas != null ? canvas.transform : transform.root;
                shopPanelInstance = Instantiate(shopPrefab, parent);
                shopPanelInstance.name = shopPrefab.name;
            }

            EnsureShopCloseButton(shopPanelInstance);
        }

        shopPanelInstance.SetActive(true);
        UIFont.ApplyAll();
    }

    /// <summary>给商店界面里名字含「关闭」的 Button 补 PanelCloseButton（幂等，已有则跳过）</summary>
    static void EnsureShopCloseButton(GameObject panel)
    {
        foreach (Button b in panel.GetComponentsInChildren<Button>(true))
        {
            if (!b.name.Contains("关闭")) continue;
            if (b.GetComponent<PanelCloseButton>() == null)
                b.gameObject.AddComponent<PanelCloseButton>();
        }
    }

    // ===== 工具 =====

    static GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        foreach (Transform t in FindObjectsOfType<Transform>(true))
            if (t.gameObject.name == objectName) return t.gameObject;
        return null;
    }

    static GameObject FindObjectByNameKeyword(params string[] keywords)
    {
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            string n = t.gameObject.name;
            foreach (string k in keywords)
                if (n.Contains(k)) return t.gameObject;
        }
        return null;
    }
}

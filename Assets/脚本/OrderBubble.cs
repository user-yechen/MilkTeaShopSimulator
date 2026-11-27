using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路人点单对话框：运行时自动挂到「路人」物体上（需场景里有对话气泡 SpriteRenderer 子物体）。
/// 订单槽位固定顺序：杯型 → 液体 → 温度 → 配料 → 盖料；当前只实现杯型（中杯/大杯随机）+ 液体（牛奶）。
/// 显示时机（2026-11-09）：开局气泡+图标全部隐藏；订阅 PasserbyWalk.OnArrivedAtCounter，
/// 路人走到前台停下后才显示气泡，图标按槽位顺序逐个出现，每个间隔 iconInterval(默认 1s)；
/// 订阅 OnLeft，顾客离开/换日时全部隐藏。
/// 图标从 Resources/贴图/ 加载，统一世界高度，sortingOrder 比气泡高 1。
/// 订单数据公开在 CupType / FluidName，后续交付核对直接读这两个字段。
/// </summary>
public class OrderBubble : MonoBehaviour
{
    [Tooltip("单个图标的世界高度（米）")]
    public float iconHeight = 0.44f;   // 2026-11-09 从 0.22 放大 1 倍
    [Tooltip("图标中心间距（米），<=0 时按 iconHeight*1.15")]
    public float iconSpacing = 0f;
    [Tooltip("图标相对气泡中心的偏移（本地坐标）")]
    public Vector3 iconOffset = Vector3.zero;
    [Tooltip("图标逐个出现的间隔（秒）")]
    public float iconInterval = 1f;

    /// <summary>杯型："中杯" 或 "大杯"（开局随机）</summary>
    public string CupType { get; private set; }
    /// <summary>液体：目前固定 "牛奶"</summary>
    public string FluidName { get; private set; }

    SpriteRenderer bubble;
    PasserbyWalk walker;
    bool showing;   // 点餐中=true；任何其他状态 Update 强制隐藏
    readonly List<GameObject> icons = new List<GameObject>();
    Coroutine revealRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
        {
            if (!go.scene.IsValid() || !go.activeInHierarchy) continue;
            if (go.GetComponent<RectTransform>() != null) continue;          // 排除 UI
            if (!go.name.Contains("路人")) continue;
            if (go.GetComponent<OrderBubble>() == null)
                go.AddComponent<OrderBubble>();
        }
    }

    void Start()
    {
        bubble = FindBubble();
        if (bubble == null)
        {
            Debug.LogWarning($"[OrderBubble] {name} 下没找到对话气泡（SpriteRenderer 名字含 对话/气泡）");
            return;
        }

        GenerateOrder();
        BuildIcons();
        SetVisible(false);   // 走到前台之前不显示

        walker = GetComponent<PasserbyWalk>();
        if (walker != null)
        {
            walker.OnArrivedAtCounter += ShowSequentially;
            walker.OnLeft += Hide;
        }
    }

    /// <summary>保险：只要路人不在点餐状态（走路/离开/等开门），对话框一律隐藏，不再只依赖事件时机</summary>
    void Update()
    {
        if (bubble == null) return;
        if (walker == null || !walker.IsOrdering)
        {
            if (showing || bubble.enabled) Hide();
        }
    }

    void OnDestroy()
    {
        PasserbyWalk walker = GetComponent<PasserbyWalk>();
        if (walker != null)
        {
            walker.OnArrivedAtCounter -= ShowSequentially;
            walker.OnLeft -= Hide;
        }
    }

    void GenerateOrder()
    {
        CupType = Random.value < 0.5f ? "中杯" : "大杯";
        FluidName = "牛奶";
    }

    /// <summary>槽位顺序：杯型/液体/温度/配料/盖料；返回当前已启用槽位的图标名</summary>
    string[] CurrentIconNames()
    {
        return new string[]
        {
            CupType == "大杯" ? "大奶茶杯" : "中奶茶杯",   // 杯型
            FluidName,                                    // 液体（"牛奶"）
            // 温度/配料/盖料：后续加，比如 "热"、"珍珠"、"奶盖"
        };
    }

    SpriteRenderer FindBubble()
    {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.name.Contains("对话") || sr.name.Contains("气泡")) return sr;
        return null;
    }

    void BuildIcons()
    {
        string[] names = CurrentIconNames();
        float spacing = iconSpacing > 0f ? iconSpacing : iconHeight * 1.15f;

        Transform row = new GameObject("订单图标").transform;
        row.SetParent(bubble.transform, false);
        row.localPosition = iconOffset;

        float startX = -spacing * (names.Length - 1) * 0.5f;   // 水平居中
        for (int i = 0; i < names.Length; i++)
        {
            Sprite sprite = Resources.Load<Sprite>("贴图/" + names[i]);
            if (sprite == null)
            {
                Debug.LogWarning($"[OrderBubble] 缺图标 Resources/贴图/{names[i]}.png");
                continue;
            }
            GameObject icon = new GameObject("图标_" + names[i], typeof(SpriteRenderer));
            icon.transform.SetParent(row, false);
            icon.transform.localPosition = new Vector3(startX + i * spacing, 0f, 0f);

            SpriteRenderer sr = icon.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = bubble.sortingOrder + 1;

            // 统一世界高度：本地缩放 = 目标高度 / (sprite 原始高度 × 父链缩放)
            float worldH = sprite.bounds.size.y;
            if (worldH > 0.0001f)
            {
                float parentScale = bubble.transform.lossyScale.y;
                icon.transform.localScale = Vector3.one * (iconHeight / worldH / Mathf.Max(parentScale, 0.0001f));
            }

            icon.SetActive(false);   // 等逐个显示
            icons.Add(icon);
        }
    }

    /// <summary>路人走到前台停下：显示气泡，图标按槽位顺序逐个出现（每个间隔 iconInterval 秒）</summary>
    void ShowSequentially()
    {
        showing = true;
        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        SetVisible(false);
        if (bubble != null) bubble.enabled = true;

        // 逐个展示：每个图标停留 iconInterval 秒后消失，再显示下一个
        foreach (GameObject icon in icons)
        {
            icon.SetActive(true);
            yield return new WaitForSeconds(iconInterval);
            icon.SetActive(false);
        }

        // 全部播完后，整份订单图标一起显示（给顾客停留期间核对用）
        foreach (GameObject icon in icons) icon.SetActive(true);
        revealRoutine = null;
    }

    /// <summary>顾客离开/换日：全部隐藏</summary>
    void Hide()
    {
        showing = false;
        if (revealRoutine != null) { StopCoroutine(revealRoutine); revealRoutine = null; }
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (bubble != null) bubble.enabled = visible;
        foreach (GameObject icon in icons) icon.SetActive(visible);
    }
}

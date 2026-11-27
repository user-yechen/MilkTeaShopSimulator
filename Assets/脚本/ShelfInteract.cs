using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 货架取物交互（自动挂主摄像机，无需手动设置）：
/// 玩家靠近置物区（ShelfZone）时，屏幕下方弹出「获取{识别标识}」按钮；
/// 点击后物品实例化并挂到玩家手上（相机前下方，与拿杯同一套 holdOffset 思路）。
/// 手上已有货架物品时不再出按钮（杯子不冲突，可同时拿）。
/// </summary>
public class ShelfInteract : MonoBehaviour
{
    [Tooltip("离置物区多远内出现取物按钮")]
    public float interactDistance = 3f;
    [Tooltip("物品拿在手里相对相机的位置")]
    public Vector3 holdOffset = new Vector3(0.35f, -0.25f, 0.6f);
    [Tooltip("手持时相对世界尺寸的缩放（和手持奶茶杯同一套：0.4）")]
    public float holdScaleFactor = 0.4f;

    /// <summary>玩家手里当前拿着的货架物品（其他脚本只读访问）</summary>
    public static GameObject HeldItem { get; private set; }

    Camera cam;
    readonly List<ShelfZone> zones = new List<ShelfZone>();
    readonly List<Button> activeButtons = new List<Button>();
    float rescanTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectsOfType<ShelfInteract>(true).Length > 0) return;
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[ShelfInteract] 场景里没有主摄像机");
            return;
        }
        mainCam.gameObject.AddComponent<ShelfInteract>();
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        RescanZones();
    }

    void RescanZones()
    {
        zones.Clear();
        zones.AddRange(FindObjectsOfType<ShelfZone>(true));
    }

    void Update()
    {
        // 置物区可能后加（商店买了新货架等），2 秒重扫一次
        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = 2f;
            RescanZones();
        }

        if (cam == null) return;
        Vector3 playerPos = cam.transform.position;

        // 收集附近的置物区
        List<ShelfZone> near = null;
        if (HeldItem == null)
        {
            foreach (ShelfZone zone in zones)
            {
                if (zone != null && zone.IsNear(playerPos, interactDistance))
                    (near ?? (near = new List<ShelfZone>())).Add(zone);
            }
        }

        SyncButtons(near);
    }

    void SyncButtons(List<ShelfZone> near)
    {
        int need = near != null ? near.Count : 0;
        if (need == activeButtons.Count)
        {
            bool same = true;
            for (int i = 0; i < need; i++)
                if (activeButtons[i].GetComponent<ZoneTag>().zone != near[i]) { same = false; break; }
            if (same) return;   // 列表没变，不重建（按钮每帧重建会吞点击）
        }

        foreach (Button b in activeButtons) if (b != null) Destroy(b.gameObject);
        activeButtons.Clear();
        if (need == 0) return;
        if (CupInteract.Instance == null) return;   // 交互滚动视图还没建好，本帧跳过

        for (int i = 0; i < need; i++)
        {
            ShelfZone zone = near[i];
            Button btn = CupInteract.Instance.AddSharedButton("获取" + zone.Identifier);
            if (btn == null) continue;
            ZoneTag tag = btn.GetComponent<ZoneTag>();
            if (tag == null) tag = btn.gameObject.AddComponent<ZoneTag>();
            tag.zone = zone;
            btn.onClick.AddListener(() => TakeItem(zone));
            activeButtons.Add(btn);
        }
    }

    void TakeItem(ShelfZone zone)
    {
        if (zone == null || HeldItem != null) return;
        GameObject item = zone.SpawnItem();
        if (item == null) return;

        Vector3 worldScale = item.transform.lossyScale;   // 挂到相机前先记下世界尺寸
        item.transform.SetParent(cam.transform, false);
        item.transform.localPosition = holdOffset;
        item.transform.localRotation = Quaternion.identity;
        item.transform.localScale = worldScale * holdScaleFactor;   // 参考手持奶茶杯：世界尺寸 ×0.4
        HeldItem = item;
        Debug.Log($"[ShelfInteract] 拿到「{zone.Identifier}」");
    }

    /// <summary>放下/用掉手中物品（其他系统消耗后调用）</summary>
    public static void ClearHeldItem() => HeldItem = null;

    /// <summary>挂在取物按钮上记它对应哪个置物区（按钮去重用）</summary>
    class ZoneTag : MonoBehaviour { public ShelfZone zone; }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把做好的奶茶杯摆在前台台面上：自前到后逐行排列，每行自左向右。
/// 排列范围取台面长度的一半并居中，相邻杯子按固定间距摆放。
/// </summary>
public class CounterCupDisplay : MonoBehaviour
{
    [Header("台面定位")]
    [Tooltip("前台物体名（场景或预制体实例根节点名）")]
    public string counterName = "前台";
    [Tooltip("台面子物体名（找不到时按包含该关键字模糊匹配）")]
    public string surfaceName = "台面";
    [Tooltip("台面深度方向边缘留白（米）")]
    public float margin = 0.12f;

    [Header("排列规则")]
    [Tooltip("相邻奶茶的间距（米）")]
    public float cupSpacing = 0.5f;
    [Tooltip("排列范围长度占台面长度的比例（0.5 = 台面长度的一半），范围居中在台面")]
    [Range(0.1f, 1f)]
    public float layoutLengthRatio = 0.5f;

    [Header("排列方向")]
    [Tooltip("反转“自前到后”的方向")]
    public bool flipFrontBack = false;
    [Tooltip("反转“自左向右”的方向")]
    public bool flipLeftRight = false;
    [Tooltip("摆到台面上的杯子相对其正立朝向的额外旋转（默认绕 X 轴 180 度）")]
    public Vector3 counterCupEuler = new Vector3(180f, 0f, 0f);

    Transform counter;                     // 前台根节点
    Transform container;                   // 展示杯容器（避免继承前台的拉伸缩放）
    Renderer[] surfaceRenderers;           // 台面渲染器（用于求上表面范围）
    readonly List<Transform> cups = new List<Transform>();

    public int CupCount => cups.Count;

    void Start()
    {
        ResolveCounter();
    }

    /// <summary>展示杯使用独立的容器，避免前台非等比缩放把杯子拉变形</summary>
    Transform DisplayContainer()
    {
        if (container != null) return container;

        Transform parent = null;
        if (counter != null)
        {
            Transform counterParent = counter.parent;
            Vector3 scale = counterParent != null ? counterParent.lossyScale : Vector3.one;
            bool uniform = Mathf.Abs(scale.x - scale.y) < 0.001f && Mathf.Abs(scale.x - scale.z) < 0.001f;
            if (counterParent != null && uniform) parent = counterParent;
        }

        GameObject go = new GameObject("前台奶茶展示");
        container = go.transform;
        container.SetParent(parent, false);
        container.localPosition = Vector3.zero;
        container.localRotation = Quaternion.identity;
        container.localScale = Vector3.one;
        return container;
    }

    /// <summary>查找前台与台面，成功返回 true</summary>
    public bool ResolveCounter()
    {
        if (surfaceRenderers != null && surfaceRenderers.Length > 0) return true;

        GameObject counterGo = GameObject.Find(counterName);
        if (counterGo == null)
        {
            // 名称不完全一致时，在整个场景里按关键字找一个含“前台”的根物体
            foreach (GameObject go in FindObjectsOfType<GameObject>())
            {
                if (go.transform.parent == null && go.name.Contains(counterName))
                {
                    counterGo = go;
                    break;
                }
            }
        }
        if (counterGo == null)
        {
            WarnOnce("没找到前台物体：" + counterName);
            return false;
        }

        counter = counterGo.transform;

        Transform surface = counter.Find(surfaceName);
        if (surface == null)
        {
            foreach (Transform child in counter.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains(surfaceName))
                {
                    surface = child;
                    break;
                }
            }
        }
        if (surface == null)
        {
            // 新前台模型没有叫"台面"的子节点时，回退到根节点自己（运行正常，静默不警告）
            surface = counter;
        }

        surfaceRenderers = surface.GetComponentsInChildren<Renderer>();
        return true;
    }

    bool warned;
    void WarnOnce(string message)
    {
        if (warned) return;   // 每帧查询时不刷屏
        warned = true;
        Debug.LogWarning("[CounterCupDisplay] " + message);
    }

    /// <summary>某个世界坐标是否在台面附近（与 CupInteract 使用同一套包围盒判定）</summary>
    public bool IsNear(Vector3 worldPosition, float maxDistance)
    {
        if (!ResolveCounter()) return false;
        return CupInteract.IsNearBounds(SurfaceBounds(), worldPosition, maxDistance);
    }

    /// <summary>用玩家手里这杯（含自身属性）在台面上生成一个展示杯，并重排所有杯子</summary>
    public bool TryPlaceCup(Cup source)
    {
        if (source == null || !ResolveCounter()) return false;

        Transform holder = DisplayContainer();

        // 先保存源杯子的液体状态（scale/position），再 Instantiate —— Cup.Awake 会把液体重置
        Transform sourceLiq = source.transform.Find("液体");
        Vector3 liqScale = sourceLiq != null ? sourceLiq.localScale : Vector3.zero;
        Vector3 liqPos   = sourceLiq != null ? sourceLiq.localPosition : Vector3.zero;

        Transform cupObj = Instantiate(source.transform);
        cupObj.SetParent(holder, true);
        cupObj.gameObject.SetActive(true);

        // 手持的是缩小版且跟着摄像机旋转，展示时用正立朝向 + 默认大小
        cupObj.localScale = Vector3.one;
        cupObj.localRotation = Quaternion.identity;

        // 展示杯只保留外观，不需要属性组件与碰撞；但订单信息要抄一份给顾客匹配
        Cup displayCup = cupObj.GetComponent<Cup>();
        if (displayCup != null)
        {
            var info = cupObj.gameObject.AddComponent<CounterCupInfo>();
            info.cupType = displayCup.cupType;
            info.teaType = displayCup.teaType;
            Destroy(displayCup);
        }
        foreach (Collider c in cupObj.GetComponentsInChildren<Collider>())
            Destroy(c);

        // 恢复液体状态（Cup.Awake 可能把它重置成 scaleY=0.01 的奶茶饼）
        Transform liq = cupObj.Find("液体");
        if (liq != null && liqScale != Vector3.zero)
        {
            liq.localScale = liqScale;
            liq.localPosition = liqPos;
        }

        return Place(cupObj);
    }

    /// <summary>把一个杯子登记到台面上并重排全部杯子</summary>
    public bool Place(Transform cup)
    {
        if (cup == null || !ResolveCounter()) return false;

        cup.SetParent(DisplayContainer(), true);
        cups.RemoveAll(t => t == null);
        if (!cups.Contains(cup)) cups.Add(cup);

        Layout();
        return true;
    }

    /// <summary>取走台面上的一杯奶茶（优先最前排），并重排剩余杯子</summary>
    public bool TakeOneCup()
    {
        cups.RemoveAll(t => t == null);
        if (cups.Count == 0) return false;

        Transform cup = cups[0];
        cups.RemoveAt(0);
        Destroy(cup.gameObject);
        Layout();
        return true;
    }

    /// <summary>台面上是否有符合订单的奶茶（杯型 + 液体类型都匹配）</summary>
    public bool HasCup(Cup.CupType type, string teaType) => FindMatch(type, teaType) != null;

    Transform FindMatch(Cup.CupType type, string teaType)
    {
        cups.RemoveAll(t => t == null);
        foreach (Transform t in cups)
        {
            Cup cup = t.GetComponentInChildren<Cup>();
            Cup.CupType ct;
            string tea;
            if (cup != null)
            {
                ct = cup.cupType;
                tea = cup.teaType;
            }
            else
            {
                var info = t.GetComponentInChildren<CounterCupInfo>();
                if (info == null) continue;
                ct = info.cupType;
                tea = info.teaType;
            }

            if (ct != type) continue;
            if (!string.IsNullOrEmpty(teaType) && tea != teaType) continue;
            return t;
        }
        return null;
    }

    /// <summary>取走台面上符合订单的一杯奶茶（杯型 + 液体类型都匹配才取），没有匹配的返回 false</summary>
    public bool TakeCup(Cup.CupType type, string teaType)
    {
        Transform cup = FindMatch(type, teaType);
        if (cup == null) return false;
        cups.Remove(cup);
        Destroy(cup.gameObject);
        Layout();
        return true;
    }

    /// <summary>在台面中央的限定范围内，按固定间距自前到后、自左向右排布</summary>
    void Layout()
    {
        cups.RemoveAll(t => t == null);
        int count = cups.Count;
        if (count == 0) return;

        Bounds bounds = SurfaceBounds();
        Vector3 center = bounds.center;

        // 列方向默认取前台的右方向（左→右），行方向取前台的朝前方向（前→后）
        Vector3 axisColumn = Vector3.ProjectOnPlane(counter.right, Vector3.up);
        if (axisColumn.sqrMagnitude < 0.0001f) axisColumn = Vector3.right;
        axisColumn.Normalize();

        Vector3 axisRow = Vector3.ProjectOnPlane(counter.forward, Vector3.up);
        if (axisRow.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(axisRow.normalized, axisColumn)) > 0.99f)
            axisRow = Vector3.Cross(Vector3.up, axisColumn);   // 与列方向垂直的水平方向
        axisRow.Normalize();

        if (flipLeftRight) axisColumn = -axisColumn;
        if (flipFrontBack) axisRow = -axisRow;

        // 台面在水平面上沿两个轴向的半长（台面可能绕 Y 旋转过）
        float halfColumn = Mathf.Abs(axisColumn.x) * bounds.extents.x + Mathf.Abs(axisColumn.z) * bounds.extents.z;
        float halfRow = Mathf.Abs(axisRow.x) * bounds.extents.x + Mathf.Abs(axisRow.z) * bounds.extents.z;

        // 排列范围：长度 = 台面长度 × layoutLengthRatio，居中在台面上；深度按台面深度留白
        float rangeHalfColumn = Mathf.Max(0f, halfColumn * layoutLengthRatio);
        float rangeHalfRow = Mathf.Max(0f, halfRow - margin);

        float spacing = Mathf.Max(0.01f, cupSpacing);
        int maxColumns = Mathf.Max(1, Mathf.FloorToInt(rangeHalfColumn * 2f / spacing) + 1);
        int maxRows = Mathf.Max(1, Mathf.FloorToInt(rangeHalfRow * 2f / spacing) + 1);

        // 行列尽量接近正方形，且不超过范围容量
        int columns = Mathf.RoundToInt(Mathf.Sqrt(count * (rangeHalfColumn + 0.01f) / (rangeHalfRow + 0.01f)));
        columns = Mathf.Clamp(columns, 1, Mathf.Min(count, maxColumns));
        int rows = Mathf.CeilToInt(count / (float)columns);
        if (rows > maxRows)
        {
            rows = maxRows;
            columns = Mathf.Clamp(Mathf.CeilToInt(count / (float)rows), 1, maxColumns);
            rows = Mathf.CeilToInt(count / (float)columns);
        }

        // 整个网格以台面中心为基准居中，首行在最前、每行自左向右
        float gridWidth = (columns - 1) * spacing;
        float gridDepth = (rows - 1) * spacing;

        for (int i = 0; i < count; i++)
        {
            Transform cup = cups[i];
            int row = i / columns;
            int column = i % columns;

            Vector3 position = center
                + axisColumn * (-gridWidth * 0.5f + column * spacing)
                + axisRow * (gridDepth * 0.5f - row * spacing);

            float height;
            float centerOffsetY = CupVerticalOffset(cup, out height);
            position.y = bounds.max.y + height * 0.5f + centerOffsetY;   // 杯底贴合台面
            cup.position = position;
        }
    }

    Bounds SurfaceBounds()
    {
        Bounds bounds = surfaceRenderers[0].bounds;
        for (int i = 1; i < surfaceRenderers.Length; i++)
            bounds.Encapsulate(surfaceRenderers[i].bounds);
        return bounds;
    }

    /// <summary>杯子的渲染包围盒中心与轴心在竖直方向上的偏差（轴心不在杯底时用来贴面）</summary>
    float CupVerticalOffset(Transform cup, out float height)
    {
        Bounds bounds = CupBounds(cup);
        height = bounds.size.y;
        return cup.position.y - bounds.center.y;
    }

    Bounds CupBounds(Transform cup)
    {
        Renderer[] renderers = cup.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Vector3 p = cup.position;
            return new Bounds(p, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}

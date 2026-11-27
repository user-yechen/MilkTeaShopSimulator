using UnityEngine;

/// <summary>
/// 加热机设备组件：挂在加热机 GameObject 上。
/// 管理左右两个杯位：哪个空、哪个存着、存的是中还是大杯。
/// 交互层（CupInteract）只负责"谁在附近 + 弹按钮"，按钮点了调这里的方法。
/// </summary>
public class HeaterMachine : MonoBehaviour
{
    [Header("场景节点名")]
    [Tooltip("加热机左侧玩家杯子杯位节点名")]
    public string leftSlotName = "杯位左";
    [Tooltip("加热机右侧玩家杯子杯位节点名")]
    public string rightSlotName = "杯位右";
    [Tooltip("加热机自带中杯外观名前缀（找到后隐藏；忽略下划线）")]
    public string builtInMediumPrefix = "奶茶杯中";
    [Tooltip("加热机自带大杯外观名前缀（找到后隐藏；忽略下划线）")]
    public string builtInLargePrefix = "奶茶杯大";

    [Header("展示参数")]
    [Tooltip("杯子放在加热机杯位上的放大倍数（取回手中后恢复原大小）")]
    public float cupDisplayScale = 1.5f;

    [Header("杯位 Y 坐标（放杯时自动修改杯位节点本身的 localPosition.y）")]
    [Tooltip("中杯放上去时，目标杯位点会自动移到这个 Y 值（= 奶茶机对应值 − 0.35）")]
    public float mediumCupSlotY = -1.00f;
    [Tooltip("大杯放上去时，目标杯位点会自动移到这个 Y 值（= 奶茶机对应值 − 0.35）")]
    public float largeCupSlotY = -0.90f;

    Vector3 leftSlotOriginalPos, rightSlotOriginalPos;
    Transform leftSlot, rightSlot;
    Cup leftCup, rightCup;
    Vector3 leftHandScale, rightHandScale;
    Renderer[] cachedRenderers;   // 近距离判定用，避免每帧 GetComponentsInChildren 分配
    bool initialized;

    void Start() => Initialize();

    void Initialize()
    {
        if (initialized) return;
        initialized = true;

        leftSlot = FindChildByName(transform, leftSlotName);
        rightSlot = FindChildByName(transform, rightSlotName);
        if (leftSlot == null || rightSlot == null)
            Debug.LogWarning($"[HeaterMachine] {name} 下找不到杯位节点「{leftSlotName}」「{rightSlotName}」");
        else
        {
            leftSlotOriginalPos = leftSlot.localPosition;
            rightSlotOriginalPos = rightSlot.localPosition;
        }

        HideBuiltInCups(transform);
    }

    // ===== 对外查询 =====

    public bool IsNear(Vector3 playerPos, float distance)
    {
        Initialize();
        if (cachedRenderers == null) cachedRenderers = GetComponentsInChildren<Renderer>();
        if (cachedRenderers.Length == 0)
            return Vector3.Distance(playerPos, transform.position) <= distance;

        Bounds bounds = cachedRenderers[0].bounds;
        for (int i = 1; i < cachedRenderers.Length; i++) bounds.Encapsulate(cachedRenderers[i].bounds);

        float dx = Mathf.Max(0f, Mathf.Abs(playerPos.x - bounds.center.x) - bounds.extents.x);
        float dy = Mathf.Max(0f, Mathf.Abs(playerPos.y - bounds.center.y) - bounds.extents.y);
        float dz = Mathf.Max(0f, Mathf.Abs(playerPos.z - bounds.center.z) - bounds.extents.z);
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz) <= distance;
    }

    public bool HasFreeSlot => (leftSlot != null && leftCup == null)
                            || (rightSlot != null && rightCup == null);

    /// <summary>有没有该类型的已加热杯子（返回第一个命中的）</summary>
    public Cup FindHeatedCup(Cup.CupType type)
    {
        if (leftCup  != null && leftCup.cupType  == type) return leftCup;
        if (rightCup != null && rightCup.cupType == type) return rightCup;
        return null;
    }

    // ===== 对外操作 =====

    /// <summary>把玩家手中的装奶茶杯放进加热机（优先空位）。返回：这杯原来在手中的 worldScale</summary>
    public Vector3 PlaceCup(Cup cup)
    {
        Initialize();
        if (cup == null || !cup.isContained || cup.IsHot || !HasFreeSlot) return Vector3.one;

        bool toLeft = leftSlot != null && leftCup == null;
        Transform slot = toLeft ? leftSlot : rightSlot;
        if (slot == null) return Vector3.one;

        Vector3 originalWorldScale = cup.transform.lossyScale;
        cup.SetTemperature(Cup.Temperature.Hot);

        // 根据杯型移动目标杯位节点到对应 Y
        float slotY = cup.cupType == Cup.CupType.Large ? largeCupSlotY : mediumCupSlotY;
        Vector3 origPos = toLeft ? leftSlotOriginalPos : rightSlotOriginalPos;
        slot.localPosition = new Vector3(origPos.x, slotY, origPos.z);

        Transform cupObj = cup.transform;
        cupObj.SetParent(slot, false);
        cupObj.localPosition = Vector3.zero;
        cupObj.localRotation = Quaternion.identity;
        cupObj.localScale = Vector3.one;

        if (toLeft) { leftCup = cup;  leftHandScale  = originalWorldScale; }
        else        { rightCup = cup; rightHandScale = originalWorldScale; }
        cachedRenderers = null;   // 层级里多了个杯子，重新取一次渲染器

        return originalWorldScale;
    }

    /// <summary>取走指定杯型的加热奶茶。返回 (Cup, 它原来在手中的 worldScale)；没有返回 (null, Vector3.one)</summary>
    public (Cup cup, Vector3 handScale) TakeCup(Cup.CupType type)
    {
        Initialize();
        if (leftCup != null && leftCup.cupType == type)
        {
            Cup c = leftCup; Vector3 s = leftHandScale;
            leftCup = null; leftHandScale = Vector3.one;
            // 杯位恢复原始位置
            if (leftSlot != null) leftSlot.localPosition = leftSlotOriginalPos;
            cachedRenderers = null;
            return (c, s);
        }
        if (rightCup != null && rightCup.cupType == type)
        {
            Cup c = rightCup; Vector3 s = rightHandScale;
            rightCup = null; rightHandScale = Vector3.one;
            if (rightSlot != null) rightSlot.localPosition = rightSlotOriginalPos;
            cachedRenderers = null;
            return (c, s);
        }
        return (null, Vector3.one);
    }

    // ===== 内部 =====

    static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) return t;
        return null;
    }

    void HideBuiltInCups(Transform parent)
    {
        foreach (Transform child in parent)
        {
            string n = child.name.Replace("_", "");
            if (n.StartsWith(builtInMediumPrefix.Replace("_", ""))
                || n.StartsWith(builtInLargePrefix.Replace("_", "")))
                child.gameObject.SetActive(false);
            else
                HideBuiltInCups(child);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectsOfType<HeaterMachine>(true).Length > 0) return;

        // 加热机 GameObject 名字可能和预期不同（比如用墙壁 Prefab 搭出来没改名）。
        // 用子节点特征反查：同时拥有 leftSlotName="杯位左" 和 rightSlotName="杯位右" **直接**子节点的那个 Transform。
        // 必须查直接子节点：递归查找会挂到「前制作台」等父级容器上，机器移出父级后会挂错对象。
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            bool hasLeft = false, hasRight = false;
            foreach (Transform c in t)
            {
                if (c.name == "杯位左") hasLeft = true;
                if (c.name == "杯位右") hasRight = true;
            }
            if (hasLeft && hasRight)
            {
                t.gameObject.AddComponent<HeaterMachine>();
                return;
            }
        }
    }
}

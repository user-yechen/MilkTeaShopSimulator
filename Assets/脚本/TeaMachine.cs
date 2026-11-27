using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 奶茶机设备组件：挂在奶茶机 GameObject 上。
/// 自己管理杯位、出料口、存着的杯子、以及"放杯→播流体→Fill"的注入流程。
/// 交互层（CupInteract）只负责"谁在附近 + 弹按钮"，按钮点了调这里的方法。
/// </summary>
public class TeaMachine : MonoBehaviour
{
    [Header("场景节点名")]
    [Tooltip("奶茶机里玩家杯子放的杯位节点名")]
    public string slotName = "杯位";
    [Tooltip("奶茶机出料口节点名（注入时流体从这里流出）；留空或找不到时退化为瞬间填充")]
    public string nozzleName = "出料口";
    [Tooltip("奶茶机自带的中杯外观模板名前缀（找到后隐藏）")]
    public string builtInMediumName = "中奶茶杯";
    [Tooltip("奶茶机自带的大杯外观模板名前缀（找到后隐藏）")]
    public string builtInLargeName = "大奶茶杯";

    [Header("展示参数")]
    [Tooltip("杯子放在机器杯位上的放大倍数（取回手中后恢复原大小）")]
    public float cupDisplayScale = 1.5f;

    [Header("杯位 Y 坐标（放杯时自动修改杯位节点本身的 localPosition.y）")]
    [Tooltip("中杯放上去时，杯位点会自动移到这个 Y 值")]
    public float mediumCupSlotY = -0.65f;
    [Tooltip("大杯放上去时，杯位点会自动移到这个 Y 值")]
    public float largeCupSlotY = -0.55f;

    [Header("流体容量（每种流体一罐，上限 20000ml，初始为空，须先注入才能出液；每日清空）")]
    [Tooltip("单罐容量上限（ml）")]
    public float tankCapacityMl = 20000f;
    [Tooltip("大杯每杯消耗（ml）")]
    public float largeCupConsumeMl = 500f;
    [Tooltip("中杯每杯消耗（ml）")]
    public float mediumCupConsumeMl = 300f;

    /// <summary>一种流体的剩余容量罐</summary>
    [System.Serializable]
    public class FluidTank
    {
        public string fluidName;   // 对应 FluidCellButton.fluidName / FluidProvider.fluidName
        public float ml;           // 当前剩余
        public float maxMl;        // 上限
    }

    readonly List<FluidTank> tanks = new List<FluidTank>();

    Vector3 slotOriginalPosition;  // 杯位节点的原始 localPosition，取杯后恢复
    Transform slot;      // 玩家杯子挂在这个节点上
    Transform nozzle;     // 流体从这个位置流出
    Cup slotCup;          // 当前在机器里的那杯
    Vector3 handScale;    // 那杯在玩家手中时的 worldScale，取回时恢复用
    Renderer[] cachedRenderers;   // 近距离判定用，避免每帧 GetComponentsInChildren 分配
    bool initialized;

    void Start() => Initialize();

    void Initialize()
    {
        if (initialized) return;
        initialized = true;

        slot = FindChildByName(transform, slotName);
        nozzle = FindChildByName(transform, nozzleName);
        if (slot == null)
            Debug.LogWarning($"[TeaMachine] {name} 下找不到杯位节点「{slotName}」，注入功能不可用");
        else
            slotOriginalPosition = slot.localPosition;   // 记住杯位原始位置，取杯后恢复
        if (nozzle == null)
        {
            // 场景里没有出料口节点（换模型/移机器时容易丢）：在杯位正上方自动补一个，保住出液动画。
            // 想精确控制水流位置的话，手动在奶茶机下建一个名为「出料口」的子物体即可，自动节点会让位。
            if (slot != null)
            {
                GameObject auto = new GameObject("出料口(自动)");
                auto.transform.SetParent(transform, false);
                auto.transform.position = slot.position + Vector3.up * 0.5f;
                nozzle = auto.transform;
                Debug.LogWarning($"[TeaMachine] {name} 下找不到出料口节点「{nozzleName}」，已在杯位上方自动生成（位置仅供参考，建议手动建一个精确的）");
            }
            else
                Debug.LogWarning($"[TeaMachine] {name} 下找不到出料口节点「{nozzleName}」，注入动画将退化为瞬间填充");
        }

        // 隐藏机器自带的奶茶杯外观模板（玩家用自己的杯子）
        HideBuiltInByPrefix(transform, builtInMediumName);
        HideBuiltInByPrefix(transform, builtInLargeName);
    }

    // ===== 对外查询（给 CupInteract 按钮可见性判定用）=====

    /// <summary>玩家位置离机器多近（用包围盒最近点距离）</summary>
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

    /// <summary>是否有可用杯位（场景里有 + 当前空着）</summary>
    public bool HasSlot => slot != null && slotCup == null;

    /// <summary>杯位里是否装了对应杯型的奶茶</summary>
    public bool HasTea(Cup.CupType type) =>
        slotCup != null && slotCup.isContained && slotCup.cupType == type;

    /// <summary>当前杯位里那杯（只读引用，供 UI 读取杯型/液体类型等）</summary>
    public Cup CurrentCup => slotCup;

    // ===== 流体容量 =====

    /// <summary>查某流体罐；没有返回 null</summary>
    public FluidTank GetTank(string fluidName) =>
        string.IsNullOrEmpty(fluidName) ? null : tanks.Find(t => t.fluidName == fluidName);

    /// <summary>取罐，没有就建一罐**空的**（不注入对应流体就不能出液）</summary>
    FluidTank GetOrCreateTank(string fluidName)
    {
        FluidTank t = GetTank(fluidName);
        if (t == null)
        {
            t = new FluidTank { fluidName = fluidName, ml = 0f, maxMl = tankCapacityMl };
            tanks.Add(t);
        }
        return t;
    }

    public float GetRemaining(string fluidName) => GetOrCreateTank(fluidName).ml;
    public float GetCapacity(string fluidName) => GetOrCreateTank(fluidName).maxMl;

    /// <summary>该流体是否还有剩余空间可注入（满了就不出补充按钮）</summary>
    public bool HasSpace(string fluidName) =>
        !string.IsNullOrEmpty(fluidName) && GetOrCreateTank(fluidName).ml < GetOrCreateTank(fluidName).maxMl - 0.01f;

    /// <summary>注入流体：加至上限为止，多出部分不返还；返回实际注入量</summary>
    public float AddFluid(string fluidName, float ml)
    {
        FluidTank t = GetOrCreateTank(fluidName);
        float add = Mathf.Min(t.maxMl - t.ml, Mathf.Max(0f, ml));
        t.ml += add;
        return add;
    }

    /// <summary>消耗流体：不足返回 false 且不扣</summary>
    public bool TryConsume(string fluidName, float ml)
    {
        if (string.IsNullOrEmpty(fluidName)) return true;   // 无名流体不计量
        FluidTank t = GetOrCreateTank(fluidName);
        if (t.ml < ml) return false;
        t.ml -= ml;
        return true;
    }

    /// <summary>按杯型算每杯消耗量</summary>
    public float ConsumeFor(Cup.CupType type) =>
        type == Cup.CupType.Large ? largeCupConsumeMl : mediumCupConsumeMl;

    /// <summary>清空机内所有流体（每日打烊/跨天时调用）</summary>
    public void ClearAllFluids()
    {
        foreach (FluidTank t in tanks) t.ml = 0f;
        Debug.Log($"[TeaMachine] {name} 机内流体已清空（新的一天）");
    }

    void OnEnable() => GameClock.OnNewDay += ClearAllFluids;
    void OnDisable() => GameClock.OnNewDay -= ClearAllFluids;

    // ===== 对外操作 =====

    /// <summary>把玩家手中的空杯放进机器。返回：这杯原来在手中时的 worldScale（CupInteract 用来恢复）</summary>
    public Vector3 PlaceCup(Cup cup)
    {
        Initialize();
        if (cup == null || cup.isContained || slot == null || slotCup != null) return Vector3.one;

        Vector3 originalWorldScale = cup.transform.lossyScale;
        handScale = originalWorldScale;
        slotCup = cup;
        cachedRenderers = null;   // 层级里多了个杯子，重新取一次渲染器

        // 根据杯型移动杯位节点到对应 Y，再把杯子挂上去 (0,0,0)
        float slotY = cup.cupType == Cup.CupType.Large ? largeCupSlotY : mediumCupSlotY;
        slot.localPosition = new Vector3(slotOriginalPosition.x, slotY, slotOriginalPosition.z);

        Transform cupObj = cup.transform;
        cupObj.SetParent(slot, false);
        cupObj.localPosition = Vector3.zero;
        cupObj.localRotation = Quaternion.identity;
        cupObj.localScale = Vector3.one;

        // 放入后只摆杯，不自动注奶茶：等玩家点击交互界面的流体格子再按颜色出液
        return originalWorldScale;
    }

    /// <summary>把机器里的奶茶取走。返回 (Cup, 它原来在手中的 worldScale)；没有就返回 (null, Vector3.one)</summary>
    public (Cup cup, Vector3 handScale) TakeCup(Cup.CupType type)
    {
        Initialize();
        if (slotCup == null || !slotCup.isContained || slotCup.cupType != type)
            return (null, Vector3.one);

        Cup cup = slotCup;
        Vector3 scale = handScale;
        slotCup = null;
        handScale = Vector3.one;
        // 杯位节点恢复原始位置
        if (slot != null) slot.localPosition = slotOriginalPosition;
        cachedRenderers = null;
        return (cup, scale);
    }

    /// <summary>
    /// 按指定颜色出液（奶茶机交互界面点击流体格子时调用）。
    /// 杯位里有杯子就同步把杯内液体染成该颜色并涨液位；没有杯子就只播放水流。
    /// forceOpaque=true 时，液体 Shader _ForceOpaque=1 → alpha 强制=1（牛奶/椰果等需要完全不透明）。
    /// </summary>
    public void PourColored(Color color, string label = "", bool tintCup = true, bool forceOpaque = false)
    {
        Initialize();

        // 往杯里注液要消耗机内流体容量（大杯 500 / 中杯 300）；不足则提示并中止
        if (slotCup != null && !string.IsNullOrEmpty(label))
        {
            float need = ConsumeFor(slotCup.cupType);
            if (!TryConsume(label, need))
            {
                ComingSoonTip.Show($"「{label}」不足：剩 {GetRemaining(label):F0}ml / 需 {need:F0}ml，请先补充");
                return;
            }
        }

        if (nozzle == null)
        {
            if (slotCup == null)
            {
                Debug.LogWarning("[TeaMachine] 既没有出料口，杯位里也没有杯子，无法出液");
                return;
            }
            if (tintCup) slotCup.SetLiquidColor(color, forceOpaque);
            slotCup.SetFillLevel(0.70f);
            if (!string.IsNullOrEmpty(label)) slotCup.teaType = label;
            return;
        }

        PourFluid fluid = PourFluid.Spawn(nozzle, color, forceOpaque);
        if (slotCup != null)
        {
            if (tintCup) slotCup.SetLiquidColor(color, forceOpaque);
            slotCup.AnimateFill(0.70f, fluid.duration);
            if (!string.IsNullOrEmpty(label)) slotCup.teaType = label;
        }
    }

    // ===== 内部 =====


    static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) return t;
        return null;
    }

    static void HideBuiltInByPrefix(Transform root, string prefix)
    {
        if (root == null || string.IsNullOrEmpty(prefix)) return;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name.StartsWith(prefix))
                t.gameObject.SetActive(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectsOfType<TeaMachine>(true).Length > 0) return;

        // 奶茶机 GameObject 名字可能和预期不同（比如用墙壁 Prefab 搭出来没改名）。
        // 用子节点特征反查：拥有 slotName="杯位" **直接**子节点的那个 Transform。
        // 注意必须查直接子节点：递归查找会把组件挂到「前制作台」之类的父级容器（它子树里也含杯位），
        // 机器一旦移出父级就会挂错对象（比如挂到加热机上，找不到出料口 → 不出液）。
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (FindDirectChild(t, "杯位") == null) continue; // 和默认 slotName 保持一致
            if (FindDirectChild(t, "杯位左") != null || FindDirectChild(t, "杯位右") != null) continue; // 那是加热机
            t.gameObject.AddComponent<TeaMachine>();
            return;
        }
    }

    static Transform FindDirectChild(Transform t, string childName)
    {
        foreach (Transform c in t)
            if (c.name == childName) return c;
        return null;
    }
}

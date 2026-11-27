using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用物件等级（商店升级功能的数值基础）：
/// 记录某个游戏物件当前等级；功能脚本用 level / IsAtLeast() 判断解锁
/// （例：IceTray.unlockLevel=2 → 前制作台 2 级才开放冰格交互）。
/// 等级存 gameTime.json（GameTimeStore.machineLevels，与天数/钱包同档），重进游戏保留。
/// 运行时按规范 ID 自动给已知物件补挂（名字或子节点特征，不依赖 GameObject 名）。
/// </summary>
public class MachineLevel : MonoBehaviour
{
    [Tooltip("物件 ID（存档键用），留空自动按规范 ID / 物体名取")]
    public string machineId;
    [Range(1, 10)] public int level = 1;
    public int maxLevel = 5;

    /// <summary>等级变化回调（参数=新等级）</summary>
    public event System.Action<int> OnLevelChanged;

    static readonly Dictionary<string, MachineLevel> registry = new Dictionary<string, MachineLevel>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (CanonicalIdFor(t) == null || t.GetComponent<MachineLevel>() != null) continue;
            t.gameObject.AddComponent<MachineLevel>();   // Awake 里会自动填规范 ID
        }
    }

    /// <summary>
    /// 按特征/名字给已知物件定规范 ID（保证存档键稳定，不依赖 GameObject 名）。
    /// 必须查**直接子节点**（经验 23：递归会挂到父级容器）；UI（RectTransform）不算。
    /// </summary>
    public static string CanonicalIdFor(Transform t)
    {
        if (t == null || t.GetComponent<RectTransform>() != null) return null;

        bool hasSlot = false, hasLeft = false, hasRight = false;
        foreach (Transform c in t)
        {
            if (c.name == "杯位") hasSlot = true;
            else if (c.name == "杯位左") hasLeft = true;
            else if (c.name == "杯位右") hasRight = true;
        }
        if (hasLeft && hasRight) return "加热机";
        if (hasSlot) return "奶茶机";

        string n = t.name;
        if (n.Contains("界面") || n.Contains("按钮")) return null;   // 排除 UI/面板类误匹配
        if (n.Contains("前制作台")) return "前制作台";
        if (n.Contains("制冰机")) return "制冰机";
        if (n.Contains("奶茶杯架")) return "奶茶杯架";
        if (n.Contains("货架")) return "货架";
        if (n.Contains("前台")) return "前台";
        return null;
    }

    void Awake()
    {
        if (string.IsNullOrEmpty(machineId)) machineId = CanonicalIdFor(transform) ?? gameObject.name;
        level = Mathf.Clamp(GameTimeStore.GetMachineLevel(machineId, level), 1, maxLevel);
        registry[machineId] = this;
    }

    void OnDestroy()
    {
        if (machineId != null && registry.TryGetValue(machineId, out MachineLevel m) && m == this)
            registry.Remove(machineId);
    }

    public bool IsAtLeast(int lv) => level >= lv;

    public void SetLevel(int lv)
    {
        lv = Mathf.Clamp(lv, 1, maxLevel);
        if (lv == level) return;
        level = lv;
        GameTimeStore.SetMachineLevel(machineId, level);   // 与天数/钱包同档（gameTime.json）
        OnLevelChanged?.Invoke(level);
        Debug.Log($"[MachineLevel] {machineId} 升到 {level} 级");
    }

    /// <summary>升 1 级；已满级返回 false</summary>
    public bool Upgrade()
    {
        if (level >= maxLevel) return false;
        SetLevel(level + 1);
        return true;
    }

    /// <summary>按 ID 查某个物件的等级组件；找不到返回 null</summary>
    public static MachineLevel Get(string id)
        => id != null && registry.TryGetValue(id, out MachineLevel m) ? m : null;

    [ContextMenu("升 1 级（测试）")]
    void TestUpgrade() => Upgrade();
}

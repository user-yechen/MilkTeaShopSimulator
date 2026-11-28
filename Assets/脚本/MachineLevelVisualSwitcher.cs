using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按 MachineLevel 的等级切换外观：1 级显示 lv1，2 级 lv2，3 级 lv3，其余隐藏。
/// 只 SetActive，不替换预制件、不迁移组件，所以不会丢引用。
/// 场景里没有时自动挂到名字含「前制作台」且带 lv1/lv2/lv3 子物体的根上。
/// </summary>
[DisallowMultipleComponent]
public class MachineLevelVisualSwitcher : MonoBehaviour
{
    [Header("等级来源（留空自动取自身 / 添加 MachineLevel）")]
    public MachineLevel levelSource;

    [Tooltip("留空则自动收集名字含 lv1/lv2/lv3 的直接子物体")]
    public Transform[] levelVisuals = new Transform[0];

    [Tooltip("按外观数量限制最大等级（前制作台=3）")]
    public bool capMaxLevel = true;

    void Awake()
    {
        if (levelSource == null) levelSource = GetComponent<MachineLevel>();
        if (levelSource == null) levelSource = gameObject.AddComponent<MachineLevel>();

        if (levelVisuals == null || levelVisuals.Length == 0) levelVisuals = Collect(gameObject);

        if (capMaxLevel && levelVisuals.Length > 0)
            levelSource.maxLevel = levelVisuals.Length;

        // 先全部隐藏，避免三套模型叠一帧
        foreach (var v in levelVisuals)
            if (v != null) v.gameObject.SetActive(false);
    }

    void Start()
    {
        Apply(levelSource != null ? levelSource.level : 1);
    }

    void OnEnable()
    {
        if (levelSource != null) levelSource.OnLevelChanged += Apply;
    }

    void OnDisable()
    {
        if (levelSource != null) levelSource.OnLevelChanged -= Apply;
    }

    public void Apply(int level)
    {
        if (levelVisuals == null || levelVisuals.Length == 0) return;
        int idx = Mathf.Clamp(level - 1, 0, levelVisuals.Length - 1);
        for (int i = 0; i < levelVisuals.Length; i++)
            if (levelVisuals[i] != null) levelVisuals[i].gameObject.SetActive(i == idx);
    }

    /// <summary>收集直接子物体里名字带 lv1/lv2/lv3 的，按数字排序</summary>
    public static Transform[] Collect(GameObject root)
    {
        var found = new List<KeyValuePair<int, Transform>>();
        foreach (Transform c in root.transform)
        {
            int lv = LevelOf(c.name);
            if (lv > 0) found.Add(new KeyValuePair<int, Transform>(lv, c));
        }
        found.Sort((a, b) => a.Key.CompareTo(b.Key));
        var result = new Transform[found.Count];
        for (int i = 0; i < found.Count; i++) result[i] = found[i].Value;
        return result;
    }

    static int LevelOf(string name)
    {
        string n = name.ToLowerInvariant();
        int i = n.IndexOf("lv");
        if (i < 0 || i + 2 >= n.Length) return 0;
        int lv = 0, j = i + 2;
        while (j < n.Length && char.IsDigit(n[j])) { lv = lv * 10 + (n[j] - '0'); j++; }
        return lv;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t.GetComponent<MachineLevelVisualSwitcher>() != null) continue;
            if (!t.name.Contains("前制作台")) continue;
            if (Collect(t.gameObject).Length < 2) continue;
            t.gameObject.AddComponent<MachineLevelVisualSwitcher>();
        }
    }

    [ContextMenu("按当前等级刷新")]
    void ApplyCurrent()
    {
        if (levelSource == null) levelSource = GetComponent<MachineLevel>();
        Apply(levelSource != null ? levelSource.level : 1);
    }
}

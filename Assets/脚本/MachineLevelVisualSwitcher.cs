using UnityEngine;

/// <summary>
/// 按 MachineLevel 的等级切换外观：1 级显示 levelVisuals[0]，2 级 [1]，3 级 [2]…
/// 只切外观（SetActive），不替换预制件、不迁移组件，所以不会丢引用。
/// 挂在等级根物体（如 前制作台）上，把 lv1/lv2/lv3 三套外观拖进数组即可。
/// </summary>
[DisallowMultipleComponent]
public class MachineLevelVisualSwitcher : MonoBehaviour
{
    [Header("等级来源（留空自动取自身 MachineLevel）")]
    public MachineLevel levelSource;

    [Header("每级外观：0=1级，1=2级，2=3级…")]
    public Transform[] levelVisuals = new Transform[0];

    [Tooltip("等级超过外观数量时，是否继续显示最后一套")]
    public bool clampToLast = true;

    [Tooltip("关闭时，低等级外观也一起隐藏；保留勾选则只显示当前等级")]
    public bool hideOthers = true;

    void Awake()
    {
        if (levelSource == null) levelSource = GetComponent<MachineLevel>();
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

    /// <summary>按等级刷新外观，供升级 / 读档后调用。</summary>
    public void Apply(int level)
    {
        if (levelVisuals == null || levelVisuals.Length == 0) return;

        int idx = Mathf.Clamp(level - 1, 0, levelVisuals.Length - 1);
        for (int i = 0; i < levelVisuals.Length; i++)
        {
            if (levelVisuals[i] == null) continue;
            bool on = i == idx || (!hideOthers && i < idx);
            levelVisuals[i].gameObject.SetActive(on);
        }
    }

    [ContextMenu("按当前等级刷新")]
    void ApplyCurrent()
    {
        if (levelSource == null) levelSource = GetComponent<MachineLevel>();
        Apply(levelSource != null ? levelSource.level : 1);
    }
}

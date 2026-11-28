using System.Collections;
using UnityEngine;

/// <summary>
/// 前制作台冰格交互：只管状态机 + 动画 + 取冰逻辑。
/// 按钮显隐由 CupInteract.Update 统一控制（滚动视图里的三个按钮）。
/// 依赖：Animator 上有 bool 参数 open=true 打开、open=false 关闭。
/// </summary>
public class IceTray : MonoBehaviour
{
    [Header("取冰设置")]
    public Cup.Temperature iceTemperature = Cup.Temperature.NormalIce;
    [Tooltip("最大储量（克）")]
    public int maxIceGrams = 6000;
    [Tooltip("每取一次消耗（克）")]
    public int icePerTake = 200;
    [Tooltip("当前剩余（克），运行时从满仓开始")]
    public int currentIceGrams = 6000;

    /// <summary>是否还有冰</summary>
    public bool HasIce => currentIceGrams > 0;

    /// <summary>
    /// 储量阶段：0=空，1=少（1~1000），2=中（1001~3000），3=多（3001~6000）。
    /// 视觉/动画可订阅 OnIceChanged 后按此切换。
    /// </summary>
    public int IceStage
    {
        get
        {
            if (currentIceGrams <= 0) return 0;
            if (currentIceGrams <= 1000) return 1;
            if (currentIceGrams <= 3000) return 2;
            return 3;
        }
    }

    /// <summary>冰量变化（参数=当前克数）</summary>
    public event System.Action<int> OnIceChanged;

    [Header("解锁条件（物件等级）")]
    [Tooltip("等级来源（如前制作台上的 MachineLevel）；留空自动取自身/父级上的。找不到 MachineLevel 视为始终解锁")]
    public MachineLevel unlockSource;
    [Tooltip("物件达到该等级才开放冰格交互（默认 2 级）")]
    public int unlockLevel = 2;

    /// <summary>冰格是否已解锁（等级够才显示交互按钮）。惰性查找，AutoAttach 晚于 Awake 也能拿到</summary>
    public bool IsUnlocked
    {
        get
        {
            if (unlockSource == null) unlockSource = GetComponentInParent<MachineLevel>();
            return unlockSource == null || unlockSource.level >= unlockLevel;
        }
    }

    public enum IceTrayState { Closed, Opening, Open, Closing }
    public IceTrayState State { get; private set; } = IceTrayState.Closed;

    Animator animator;

    void Awake()
    {
        // 先找自己，再找子物体里的 Animator（Animator 常挂在带 Animation Clip 的子物体上）
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            Debug.LogWarning("[IceTray] 前制作台及其子物体上找不到 Animator 组件");
        else if (animator.runtimeAnimatorController == null)
            Debug.LogWarning("[IceTray] Animator 组件还没绑定 Animator Controller，Inspector 里拖一下");
    }

    /// <summary>给 CupInteract 调用：判断玩家是否靠近</summary>
    public bool IsNear(Vector3 playerPos, float distance)
    {
        return Vector3.Distance(playerPos, transform.position) <= distance;
    }

    // ===== 按钮回调（由 CupInteract 的滚动视图按钮绑定） =====

    public void OnClickOpen()
    {
        if (!IsUnlocked) return;
        if (State != IceTrayState.Closed) return;
        StartCoroutine(PlayOpenAnim());
    }

    public void OnClickClose()
    {
        if (State != IceTrayState.Open) return;
        StartCoroutine(PlayCloseAnim());
    }

    public void OnClickTakeIce()
    {
        if (!IsUnlocked) return;
        if (State != IceTrayState.Open) return;
        if (!HasIce) return;

        CupInteract interact = FindObjectOfType<CupInteract>();
        if (interact != null && interact.HeldCup != null && interact.HeldCup.isContained)
        {
            interact.HeldCup.SetTemperature(iceTemperature);
            currentIceGrams = Mathf.Max(0, currentIceGrams - icePerTake);
            OnIceChanged?.Invoke(currentIceGrams);
            ComingSoonTip.Show($"剩余冰块 {currentIceGrams} g");
        }
    }

    /// <summary>补冰 / 消耗（克），供进货或调试调用</summary>
    public void AddIce(int grams)
    {
        currentIceGrams = Mathf.Clamp(currentIceGrams + grams, 0, maxIceGrams);
        OnIceChanged?.Invoke(currentIceGrams);
    }

    // ===== 动画协程 =====

    IEnumerator PlayOpenAnim()
    {
        State = IceTrayState.Opening;

        if (animator != null)
            animator.SetBool("Open", true);

        yield return WaitForCurrentAnim(animator);

        State = IceTrayState.Open;
    }

    IEnumerator PlayCloseAnim()
    {
        State = IceTrayState.Closing;

        if (animator != null)
            animator.SetBool("Open", false);

        yield return WaitForCurrentAnim(animator);

        State = IceTrayState.Closed;
    }

    // ===== 工具 =====

    static IEnumerator WaitForCurrentAnim(Animator a)
    {
        if (a == null) { yield break; }
        var state = a.GetCurrentAnimatorStateInfo(0);
        float wait = state.length * Mathf.Max(0.01f, state.speed);
        yield return new WaitForSeconds(wait);
    }

    [ContextMenu("重置冰量")]
    void ResetIce()
    {
        currentIceGrams = maxIceGrams;
        OnIceChanged?.Invoke(currentIceGrams);
    }
}

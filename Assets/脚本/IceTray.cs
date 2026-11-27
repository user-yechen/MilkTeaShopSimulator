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
    public int iceCapacity = 8;
    public int iceCount = 8;

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
        if (State != IceTrayState.Open) return;
        if (iceCount <= 0) return;

        CupInteract interact = FindObjectOfType<CupInteract>();
        if (interact != null && interact.HeldCup != null && interact.HeldCup.isContained)
        {
            interact.HeldCup.SetTemperature(iceTemperature);
            iceCount--;
        }
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
        iceCount = iceCapacity;
    }
}

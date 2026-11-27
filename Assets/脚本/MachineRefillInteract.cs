using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 奶茶机补充流体交互（自动挂主摄像机，无需手动设置）：
/// 玩家手持带 FluidProvider 的物品（如牛奶）靠近奶茶机，且机内对应流体未满时，
/// 屏幕下方弹「注入{流体名}」按钮；点击后手上物品消失、容量加至上限为止（多出不返还），
/// 并用提示框显示「流体名 当前容量/总容量」。机内满了不再出按钮。
/// </summary>
public class MachineRefillInteract : MonoBehaviour
{
    [Tooltip("离奶茶机多远内出现注入按钮")]
    public float interactDistance = 3f;

    Camera cam;
    TeaMachine machine;
    Button refillButton;
    Text refillLabel;
    string shownFluid;   // 当前按钮对应哪种流体（变了才重建文字）

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectsOfType<MachineRefillInteract>(true).Length > 0) return;
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[MachineRefillInteract] 场景里没有主摄像机");
            return;
        }
        mainCam.gameObject.AddComponent<MachineRefillInteract>();
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        machine = FindObjectOfType<TeaMachine>();
    }

    void Update()
    {
        if (cam == null) return;
        if (machine == null) machine = FindObjectOfType<TeaMachine>();
        if (machine == null) { SetButton(null); return; }

        // 手上拿着的是流体供给物 + 靠近机器 + 机内该流体有剩余空间 → 出按钮
        FluidProvider provider = ShelfInteract.HeldItem != null
            ? ShelfInteract.HeldItem.GetComponent<FluidProvider>()
            : null;

        bool show = provider != null
            && !string.IsNullOrEmpty(provider.fluidName)
            && machine.HasSpace(provider.fluidName)
            && machine.IsNear(cam.transform.position, interactDistance);

        SetButton(show ? provider : null);
    }

    void SetButton(FluidProvider provider)
    {
        string fluid = provider != null ? provider.fluidName : null;
        if (fluid == shownFluid) return;   // 状态没变不重建/不改文字（每帧重建会吞点击）
        shownFluid = fluid;

        if (fluid == null)
        {
            if (refillButton != null) refillButton.gameObject.SetActive(false);
            return;
        }

        EnsureButton();
        refillLabel.text = "注入" + fluid;
        refillButton.onClick.RemoveAllListeners();
        FluidProvider target = provider;
        refillButton.onClick.AddListener(() => Refill(target));
        refillButton.gameObject.SetActive(true);
    }

    void EnsureButton()
    {
        if (refillButton != null) return;
        if (CupInteract.Instance == null) return;   // 交互滚动视图还没建好，下帧再试
        refillButton = CupInteract.Instance.AddSharedButton("注入");
        refillLabel = refillButton.GetComponentInChildren<Text>(true);
        refillButton.gameObject.SetActive(false);
    }

    void Refill(FluidProvider provider)
    {
        if (provider == null || machine == null) return;

        float added = machine.AddFluid(provider.fluidName, provider.mlPerUnit);   // 超上限自动截断，多出不返还

        // 手上物品消失
        GameObject held = ShelfInteract.HeldItem;
        if (held != null) Destroy(held);
        ShelfInteract.ClearHeldItem();

        // 屏幕提示：流体名 当前容量/总容量
        ComingSoonTip.Show($"{provider.fluidName} {machine.GetRemaining(provider.fluidName):F0}ml / {machine.GetCapacity(provider.fluidName):F0}ml（+{added:F0}ml）");
        Debug.Log($"[MachineRefillInteract] 注入 {provider.fluidName} +{added:F0}ml");
    }
}

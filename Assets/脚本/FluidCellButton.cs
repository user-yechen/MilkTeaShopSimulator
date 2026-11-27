using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在奶茶机交互界面的「流体格子」上：单击后让奶茶机从出料口流出该颜色的液体。
/// 新增流体：复制一个流体格子，在 Inspector 里改 fluidName / fluidColor 即可（machine 留空会自动向上找奶茶机）。
/// </summary>
[RequireComponent(typeof(Button))]
public class FluidCellButton : MonoBehaviour
{
    [Header("流体设置")]
    [Tooltip("流体名称，仅作标识（可和子物体 text 保持一致）")]
    public string fluidName = "牛奶";

    [Tooltip("液体颜色：同时用于出料口的水流和被浇的杯内液体")]
    public Color fluidColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("是否强制完全不透明（牛奶/椰果等选 ✓，茶汤/咖啡等半透明选 ✗）")]
    public bool forceOpaque = true;

    [Tooltip("是否把杯内液体也染成该颜色；关掉则只出水流、不改杯子")]
    public bool tintCup = true;

    [Header("目标奶茶机")]
    [Tooltip("留空则自动向上查找挂有 TeaMachine 的父物体；界面不在机器下面时请手动拖入")]
    public TeaMachine machine;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        if (machine == null) machine = GetComponentInParent<TeaMachine>();

        if (machine == null)
        {
            Debug.LogWarning("[FluidCellButton] 没有指定奶茶机，流体格子「" + fluidName + "」无法出液（请在 Inspector 里拖入 machine）");
            return;
        }

        machine.PourColored(fluidColor, fluidName, tintCup, forceOpaque);
    }
}

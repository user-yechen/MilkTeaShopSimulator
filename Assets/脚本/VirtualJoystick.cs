using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 屏幕左侧虚拟摇杆：手机端拖动小圆点、电脑端用 WASD/方向键，输出一个归一化方向（X=左右，Y=前后）。
/// 挂在摇杆背景 Image 上，需要指定背景和摇杆把手的 RectTransform。
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("摇杆 UI 引用")]
    [Tooltip("摇杆背景区域（大圆）")]
    public RectTransform background;
    [Tooltip("摇杆把手（小圆）")]
    public RectTransform handle;

    [Header("摇杆参数")]
    [Tooltip("把手可移动的最大半径（像素）")]
    public float handleRange = 60f;
    [Tooltip("中心死区半径（像素），此范围内输入视为 0")]
    public float deadZone = 8f;

    [Header("键盘控制（电脑端）")]
    [Tooltip("电脑端用 WASD/方向键操控摇杆")]
    public bool keyboardControl = true;

    /// <summary>归一化的输入方向，范围 [-1,1]</summary>
    public Vector2 InputVector { get; private set; }

    /// <summary>摇杆位置调整模式（JoystickPositionAdjuster 设置）：拖动 = 移动整个摇杆，不输出方向</summary>
    public static bool positionEditMode;

    const string PosXKey = "JoystickPosX";
    const string PosYKey = "JoystickPosY";

    bool isPointerHeld;
    Canvas parentCanvas;        // 调整模式下按 scaleFactor 换算拖动增量
    Vector2 defaultAnchoredPos; // 场景默认位置（Awake 捕获，供"重置默认"用）

    void Reset()
    {
        // 自动从子物体填充引用，方便首次使用
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null && transform.childCount > 0)
            handle = transform.GetChild(0).GetComponent<RectTransform>();
    }

    void Awake()
    {
        // 运行时兜底：没手动连线时，背景取自身、把手取第一个子物体
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null && transform.childCount > 0)
            handle = transform.GetChild(0).GetComponent<RectTransform>();

        parentCanvas = GetComponentInParent<Canvas>();
        if (background != null) defaultAnchoredPos = background.anchoredPosition;
    }

    void Start()
    {
        // 恢复上次自定义的摇杆位置（设置界面 → 自定义摇杆位置 保存的）
        if (background != null && PlayerPrefs.HasKey(PosXKey))
            background.anchoredPosition = new Vector2(
                PlayerPrefs.GetFloat(PosXKey), PlayerPrefs.GetFloat(PosYKey));
    }

    /// <summary>保存当前摇杆位置到 PlayerPrefs（JoystickPositionAdjuster 确定时调用）</summary>
    public void SavePosition()
    {
        if (background == null) return;
        PlayerPrefs.SetFloat(PosXKey, background.anchoredPosition.x);
        PlayerPrefs.SetFloat(PosYKey, background.anchoredPosition.y);
        PlayerPrefs.Save();
    }

    /// <summary>清除已存位置并回到场景默认位置</summary>
    public void ResetPosition()
    {
        PlayerPrefs.DeleteKey(PosXKey);
        PlayerPrefs.DeleteKey(PosYKey);
        PlayerPrefs.Save();
        if (background != null) background.anchoredPosition = defaultAnchoredPos;
    }

    void Update()
    {
        if (positionEditMode) return;   // 调整模式下不响应键盘，避免把手乱动

        // 编辑器与桌面设备启用键盘；安卓/iOS 真机上只走屏幕拖动。
        // 不能用 Application.isMobilePlatform：构建目标切到手机平台时，它在编辑器里也会返回 true。
        bool keyboardPlatform = Application.isEditor || SystemInfo.deviceType == DeviceType.Desktop;
        // 指针按住期间优先拖动输入，避免与键盘互相拉扯
        if (!keyboardControl || !keyboardPlatform || isPointerHeld)
            return;

        // Horizontal/Vertical 默认就是 A/D 与 W/S（含方向键）
        Vector2 dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        InputVector = dir;
        if (handle != null)
            handle.anchoredPosition = dir * handleRange;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (background == null || handle == null) return;   // 引用没连上时保持键盘可用的状态
        isPointerHeld = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null) return;

        // 调整模式：拖动 = 移动整个摇杆（屏幕增量 ÷ Canvas 缩放 = anchoredPosition 增量）
        if (positionEditMode)
        {
            float scale = parentCanvas != null ? parentCanvas.scaleFactor : 1f;
            if (scale <= 0f) scale = 1f;
            background.anchoredPosition += eventData.delta / scale;
            return;
        }

        // 把屏幕坐标转换为背景内的局部坐标
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, eventData.position, eventData.pressEventCamera, out localPoint);

        Vector2 delta = localPoint;
        // 限制把手在半径范围内
        if (delta.magnitude > handleRange)
            delta = delta.normalized * handleRange;

        handle.anchoredPosition = delta;

        // 计算归一化输入（应用死区）
        float normalized = delta.magnitude / handleRange;
        if (normalized <= deadZone / handleRange)
            InputVector = Vector2.zero;
        else
            InputVector = delta.normalized * normalized;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerHeld = false;
        if (positionEditMode) return;   // 调整模式下不清输入、不回把手
        InputVector = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }
}

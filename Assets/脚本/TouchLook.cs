using UnityEngine;

/// <summary>
/// 滑动转视角：在摇杆区域以外的屏幕拖动，即可旋转摄像机朝向（左右转向 + 上下俯仰）。
/// 挂在主摄像机上，并把左侧虚拟摇杆拖到 joystick 字段，用于排除摇杆区域内的滑动。
/// 手机上走 Touch 输入（支持多指，摇杆与转视角可同时操作），编辑器中用鼠标左键拖动。
/// 只改变摄像机旋转，不改变位置，因此 Y 轴高度始终由 PlayerController 锁定。
/// </summary>
public class TouchLook : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("左侧虚拟摇杆，用于排除其区域内的滑动")]
    public VirtualJoystick joystick;

    [Header("灵敏度")]
    [Tooltip("每像素旋转的度数")]
    public float sensitivity = 0.2f;

    [Header("俯仰限制")]
    [Tooltip("最低俯仰角（负值=向下看）")]
    public float minPitch = -60f;
    [Tooltip("最高俯仰角（正值=向上看）")]
    public float maxPitch = 60f;

    float yaw;
    float pitch;
    int lookFingerId = -1;      // 正在用于转视角的手指，-1 表示没有
    bool mouseLooking;
    Vector2 lastPos;

    void Start()
    {
        // 从当前朝向初始化，避免一上来就跳转
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = Mathf.DeltaAngle(0f, e.x);

        if (joystick == null)
            joystick = FindObjectOfType<VirtualJoystick>();
    }

    void Update()
    {
        if (Input.touchCount > 0)
        {
            mouseLooking = false;
            HandleTouches();
        }
        else
        {
            lookFingerId = -1;
            HandleMouse();
        }
    }

    void HandleTouches()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            switch (t.phase)
            {
                case TouchPhase.Began:
                    // 按下点不在摇杆范围内，才接管这根手指
                    if (lookFingerId == -1 && !IsOverJoystick(t.position))
                    {
                        lookFingerId = t.fingerId;
                        lastPos = t.position;
                    }
                    break;

                case TouchPhase.Moved:
                    if (t.fingerId == lookFingerId)
                    {
                        Rotate(t.position - lastPos);
                        lastPos = t.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.fingerId == lookFingerId)
                        lookFingerId = -1;
                    break;
            }
        }
    }

    void HandleMouse()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 从摇杆区域开始拖动时，交给摇杆处理
            mouseLooking = !IsOverJoystick(Input.mousePosition);
            lastPos = Input.mousePosition;
        }

        if (Input.GetMouseButton(0) && mouseLooking)
        {
            Vector2 cur = Input.mousePosition;
            Rotate(cur - lastPos);
            lastPos = cur;
        }

        if (Input.GetMouseButtonUp(0))
            mouseLooking = false;
    }

    void Rotate(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0f) return;

        yaw += delta.x * sensitivity;
        // 手指上滑 = 抬头，下滑 = 低头
        pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>屏幕坐标是否落在摇杆背景区域内</summary>
    bool IsOverJoystick(Vector2 screenPos)
    {
        if (joystick == null) return false;

        RectTransform rect = joystick.background != null
            ? joystick.background
            : joystick.transform as RectTransform;
        if (rect == null) return false;

        // Overlay 画布传 null；Camera 模式要传画布相机，否则坐标换算不准
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }
}

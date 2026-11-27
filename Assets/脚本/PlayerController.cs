using UnityEngine;

/// <summary>
/// 玩家控制器：摄像机即玩家主视角，左侧虚拟摇杆驱动摄像机在水平面（XZ）移动。
/// - 初始位置固定为 (-17.5, 2, 0)
/// - 移动只改变 X、Z，Y 始终锁定为 cameraHeight（2）
/// - 使用 CharacterController 做墙体碰撞，遇墙自动沿墙滑动，不会穿墙
/// 挂在主摄像机上，需要同时添加 CharacterController 组件。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("移动速度（米/秒）")]
    public float moveSpeed = 5f;
    [Tooltip("左侧虚拟摇杆")]
    public VirtualJoystick joystick;

    [Header("位置设置")]
    [Tooltip("初始位置")]
    public Vector3 startPosition = new Vector3(-17.5f, 2f, 0f);
    [Tooltip("摄像机固定高度（Y 轴锁定值）")]
    public float cameraHeight = 2f;

    CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // 初始化到指定位置
        transform.position = startPosition;

        // 让胶囊碰撞体中心与摄像机位置一致，避免偏移
        controller.center = Vector3.zero;

        // 未手动拖引用时自动在场景中查找摇杆，避免忘记连线导致完全不动
        if (joystick == null)
            joystick = FindObjectOfType<VirtualJoystick>();
        if (joystick == null)
            Debug.LogWarning("[PlayerController] 场景中找不到 VirtualJoystick，摄像机不会移动。");

        // 播放模式下添加的组件退出后会被丢弃，这里运行时兜底补上滑动转视角
        if (GetComponent<TouchLook>() == null)
            gameObject.AddComponent<TouchLook>();
    }

    void Update()
    {
        if (joystick == null) return;

        Vector2 input = joystick.InputVector;
        if (input.sqrMagnitude < 0.0001f) return;

        // 相机相对方向：前向投影到水平面，右向也取水平分量
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDir = right * input.x + forward * input.y;
        if (moveDir.sqrMagnitude > 0f)
            moveDir.Normalize();

        // 水平移动量
        Vector3 move = moveDir * moveSpeed * Time.deltaTime;
        // Y 轴拉回固定高度（通过控制器移动，避免穿墙时 Y 漂移）
        move.y = cameraHeight - transform.position.y;

        controller.Move(move);
    }
}

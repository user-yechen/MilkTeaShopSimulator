using UnityEngine;

/// <summary>
/// 摆放挂点：挂在设备/台面/货架上，表示「东西应该放在这里」。
/// 位置 = 物体底面接触的平面；朝向 = 物体正面方向。
/// </summary>
public class Socket : MonoBehaviour
{
    [Tooltip("可接受的类型：cup / item / machine / hand")]
    public string socketType = "cup";

    [Tooltip("可放区域大小（米），仅用于提示与校验")]
    public Vector3 size = new Vector3(0.2f, 0.2f, 0.2f);

    [Tooltip("落位间隙（米），太小会 z-fighting，太大会漏光")]
    public float gap = 0.001f;

    [Tooltip("物体正面方向（本地坐标）")]
    public Vector3 localForward = Vector3.forward;

    public Vector3 WorldForward
    {
        get
        {
            Vector3 f = transform.TransformDirection(localForward);
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.6f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.DrawRay(Vector3.zero, localForward.normalized * Mathf.Max(size.x, size.z) * 0.5f);
    }
}

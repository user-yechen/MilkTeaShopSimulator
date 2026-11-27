using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一摆放：把可摆放物的底面贴到 Socket 平面，朝向对齐 Socket。
/// 所有「放上去」都走这里，不再各自写 localPosition / slotY。
/// </summary>
public static class PlacementService
{
    /// <summary>把 item 放到 socket 上；返回是否成功</summary>
    public static bool Place(GameObject item, Socket socket)
    {
        if (item == null || socket == null) return false;

        Mountable m = item.GetComponent<Mountable>();
        float lift = (m != null ? m.bottomOffset : 0f) + socket.gap;

        Bounds b = GetBounds(item);
        float targetBottom = socket.transform.position.y + lift;
        float deltaY = targetBottom - b.min.y;
        item.transform.position += Vector3.up * deltaY;

        Vector3 fwd = socket.WorldForward;
        item.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        return true;
    }

    /// <summary>在范围内找最近的、类型匹配的 Socket</summary>
    public static Socket FindNearest(Vector3 point, float maxDistance, string type = null)
    {
        Socket best = null;
        float bestDist = maxDistance;

        foreach (Socket s in Object.FindObjectsOfType<Socket>(true))
        {
            if (type != null && !string.IsNullOrEmpty(s.socketType) && s.socketType != type) continue;
            float d = Vector3.Distance(point, s.transform.position);
            if (d <= bestDist)
            {
                bestDist = d;
                best = s;
            }
        }
        return best;
    }

    /// <summary>合并所有 Renderer 的世界包围盒（跳过粒子/Line）</summary>
    public static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);

        foreach (Renderer r in rs)
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!has) b = new Bounds(go.transform.position, Vector3.one * 0.1f);
        return b;
    }
}

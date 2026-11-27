using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 摆放工具：把选中物体吸附到最近的 Socket；批量检查悬空 / 过穿 / 缺挂点。
/// </summary>
public static class PlacementTools
{
    const float SearchRadius = 3f;
    const float FloatTolerance = 0.002f;   // 悬空阈值：底面高出支撑面 2mm
    const float SinkTolerance = 0.005f;    // 过穿阈值：底面低于支撑面 5mm

    [MenuItem("奶茶店/摆放/吸附到最近 Socket", false, 110)]
    public static void SnapSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[摆放] 先选中要吸附的物体");
            return;
        }

        int ok = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            Socket socket = PlacementService.FindNearest(go.transform.position, SearchRadius);
            if (socket == null)
            {
                Debug.LogWarning("[摆放] " + go.name + " 附近 3m 内没有 Socket");
                continue;
            }

            Undo.RecordObject(go.transform, "吸附到 Socket");
            PlacementService.Place(go, socket);
            EditorUtility.SetDirty(go);
            ok++;
            Debug.Log(string.Format("[摆放] {0} → {1}（间隙 {2}mm）", go.name, socket.name, socket.gap * 1000f));
        }

        Debug.Log(string.Format("[摆放] 完成：{0}/{1} 个物体已吸附", ok, Selection.gameObjects.Length));
    }

    [MenuItem("奶茶店/摆放/检查穿模与悬空", false, 111)]
    public static void ValidateScene()
    {
        Mountable[] items = Object.FindObjectsOfType<Mountable>(true);
        int errors = 0;

        foreach (Mountable m in items)
        {
            Socket socket = PlacementService.FindNearest(m.transform.position, SearchRadius, m.mountType);
            if (socket == null)
            {
                socket = PlacementService.FindNearest(m.transform.position, SearchRadius);
                if (socket == null)
                {
                    Debug.LogWarning(string.Format("[摆放] {0}：3m 内没有 Socket", m.name));
                    continue;
                }
            }

            Bounds b = PlacementService.GetBounds(m.gameObject);
            float delta = b.min.y - (socket.transform.position.y + socket.gap);

            if (delta > FloatTolerance)
            {
                errors++;
                Debug.LogError(string.Format("[摆放] {0}：悬空 {1:0.###}m（Socket {2}）",
                    m.name, delta, socket.name));
            }
            else if (delta < -SinkTolerance)
            {
                errors++;
                Debug.LogError(string.Format("[摆放] {0}：过穿 {1:0.###}m（Socket {2}）",
                    m.name, -delta, socket.name));
            }
            else
            {
                Debug.Log(string.Format("[摆放] {0}：OK（底面偏差 {1:0.###}m）", m.name, delta));
            }
        }

        Debug.Log(string.Format("[摆放] 检查完成：{0} 个可摆放物，{1} 个问题", items.Length, errors));
    }
}

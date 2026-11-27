using UnityEditor;
using UnityEngine;

/// <summary>将选中 GameObject（或 Prefab）的 pivot 移到底边几何中心</summary>
public static class PivotToBottomCenter
{
    [MenuItem("奶茶店/杯子/选中物体：Pivot 移到底边中心", false, 22)]
    static void MovePivotToBottomCenter()
    {
        var selection = Selection.gameObjects;
        if (selection.Length == 0) { Debug.LogWarning("[Pivot] 请先选中一个杯子 GameObject 或 Prefab 根节点"); return; }

        int done = 0;
        foreach (var go in selection)
        {
            if (MoveOne(go)) done++;
        }
        Debug.Log($"[Pivot] 完成 {done}/{selection.Length} 个物体");
    }

    [MenuItem("奶茶店/杯子/选中物体：Pivot 移到底边中心", true)]
    static bool Validate() => Selection.gameObjects.Length > 0;

    /// <summary>核心逻辑：计算 bounds，把所有子物体的 localPosition 偏移，使得根节点落在底边中心</summary>
    static bool MoveOne(GameObject root)
    {
        // 计算 bounds（包含所有 MeshRenderer + SkinnedMeshRenderer）
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Debug.LogWarning($"[Pivot] {root.name} 没有 Renderer，跳过"); return false; }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // 底边中心的世界坐标
        Vector3 bottomCenterWorld = new Vector3(b.center.x, b.min.y, b.center.z);
        // 转换为 root 的本地坐标偏移量
        Vector3 localOffset = root.transform.InverseTransformPoint(bottomCenterWorld);
        // 我们要让新 pivot = bottomCenterWorld，即 root.localPosition 不变，但子物体都要相对 root 偏移 -localOffset
        Vector3 shift = -localOffset;

        // Prefab 模式 vs 场景对象
        bool isPrefabRoot = PrefabUtility.IsPartOfPrefabAsset(root);

        if (isPrefabRoot)
        {
            // Prefab 里直接改子物体的 localPosition
            Undo.RegisterCompleteObjectUndo(root, "Pivot to bottom");
            ShiftChildren(root.transform, shift);
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
        }
        else
        {
            // 场景对象：把 root 的 worldPosition 移到底边中心位置，同时偏移子物体，使视觉位置不变
            Undo.RegisterCompleteObjectUndo(root, "Pivot to bottom");
            Vector3 originalWorld = root.transform.position;
            root.transform.position = bottomCenterWorld;
            // 子物体偏移：让它们的世界坐标不变
            ShiftChildren(root.transform, root.transform.TransformVector(originalWorld - bottomCenterWorld));
        }

        // 再计算一次验证
        var r2 = root.GetComponentsInChildren<Renderer>(true);
        Bounds b2 = r2[0].bounds;
        for (int i = 1; i < r2.Length; i++) b2.Encapsulate(r2[i].bounds);
        Vector3 rootWorldPos = root.transform.position;
        Debug.Log($"[Pivot] {root.name} → 新 pivot=({rootWorldPos.x:F3},{rootWorldPos.y:F3},{rootWorldPos.z:F3}), " +
                  $"bounds.min.y={b2.min.y:F3}, bounds.center=({b2.center.x:F3},{b2.center.y:F3},{b2.center.z:F3})");
        return true;
    }

    static void ShiftChildren(Transform parent, Vector3 shift)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Undo.RecordObject(child, "Shift child");
            child.localPosition += shift;
        }
    }
}

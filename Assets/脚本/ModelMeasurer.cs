using UnityEngine;

/// <summary>
/// 模型尺寸测量工具（运行时通用）：
/// 汇总物体及其子层级所有 Renderer / MeshFilter，输出模型的世界包围盒与本地真实尺寸，
/// 供后续「模型升级」功能使用（升级换更大模型时，按旧模型尺寸/占地面积摆放或校验新模型）。
/// 两种尺寸的区别：
///   worldSize = 世界轴对齐包围盒（AABB），物体带旋转时会被放大；
///   localSize = 换算到模型本地空间的尺寸，不受旋转影响，做升级占位判断应以此为准。
/// </summary>
public static class ModelMeasurer
{
    public struct ModelSizeInfo
    {
        public Vector3 worldSize;     // 世界 AABB 尺寸（受旋转影响）
        public Vector3 worldCenter;   // 世界 AABB 中心
        public Vector3 localSize;     // 模型本地尺寸（不受旋转影响）
        public Vector3 localCenter;   // 本地空间中心（相对 root 根节点）
        public int rendererCount;     // 参与计算的 Renderer 数量

        public float Height => localSize.y;      // 模型高度
        public float FootprintX => localSize.x;  // 占地面积 X
        public float FootprintZ => localSize.z;  // 占地面积 Z

        public override string ToString()
        {
            return $"本地尺寸(长X×高Y×深Z) = {localSize.x:F3} × {localSize.y:F3} × {localSize.z:F3} m，" +
                   $"本地中心 = {localCenter}，占地 = {FootprintX:F3} × {FootprintZ:F3} m，" +
                   $"世界AABB = {worldSize}（中心 {worldCenter}），Renderer × {rendererCount}";
        }
    }

    /// <summary>测量 root（含子层级）的模型尺寸；没有任何 Renderer 时返回 false</summary>
    public static bool Measure(GameObject root, out ModelSizeInfo info, bool includeInactive = true)
    {
        info = default;
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
        if (renderers.Length == 0) return false;

        // ① 世界 AABB：直接并集所有 Renderer.bounds
        Bounds world = renderers[0].bounds;
        foreach (Renderer r in renderers)
            world.Encapsulate(r.bounds);

        // ② 本地尺寸：把每个 MeshFilter 的网格本地包围盒 8 角点换算到 root 本地空间
        //    （不能用 Renderer.bounds 反算：世界 AABB 在物体旋转时会取到膨胀后的盒子，见经验 16）
        Matrix4x4 toRootLocal = root.transform.worldToLocalMatrix;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool hasLocal = false;

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(includeInactive))
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null) continue;
            AccumulateCorners(mesh.bounds, toRootLocal * mf.transform.localToWorldMatrix, ref min, ref max);
            hasLocal = true;
        }

        // 没有 MeshFilter（纯粒子/ SkinnedMesh 等）时，退化为把世界 AABB 换算回本地
        if (!hasLocal)
            AccumulateCorners(world, toRootLocal, ref min, ref max);

        info.worldSize = world.size;
        info.worldCenter = world.center;
        info.localSize = max - min;
        info.localCenter = (min + max) * 0.5f;
        info.rendererCount = renderers.Length;
        return true;
    }

    /// <summary>测量并打印到 Console；测量失败打 Warning 并返回 false</summary>
    public static bool LogMeasure(GameObject root, bool includeInactive = true)
    {
        if (root == null) return false;
        if (!Measure(root, out ModelSizeInfo info, includeInactive))
        {
            Debug.LogWarning($"[ModelMeasurer] {root.name} 及其子层级没有任何 Renderer，无法测量");
            return false;
        }
        Debug.Log($"[ModelMeasurer] {root.name}：{info}");
        return true;
    }

    /// <summary>把 Bounds 的 8 个角点经 matrix 变换后并入 min/max</summary>
    static void AccumulateCorners(Bounds b, Matrix4x4 matrix, ref Vector3 min, ref Vector3 max)
    {
        Vector3 c = b.center, e = b.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + Vector3.Scale(e, new Vector3(
                (i & 1) == 0 ? -1f : 1f,
                (i & 2) == 0 ? -1f : 1f,
                (i & 4) == 0 ? -1f : 1f));
            Vector3 p = matrix.MultiplyPoint3x4(corner);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
    }
}

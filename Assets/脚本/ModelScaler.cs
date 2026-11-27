using UnityEngine;

/// <summary>
/// 模型尺寸适配缩放（运行时，配合 ModelMeasurer）：
/// 商店升级换模型时，把新模型缩放到与旧物件一致的尺寸，防止穿模。
/// 只改 transform.localScale（不重烘网格），放置/升级场景足够；
/// 假设物件放在场景根层（父链缩放为 1）、无旋转（机器贴地摆放成立）。
/// </summary>
public static class ModelScaler
{
    public enum FitMode
    {
        UniformFitInside,   // 等比：三轴取最小商，整体收进目标包围盒（默认，任何方向都不穿模）
        UniformByFootprint, // 等比：按 XZ 占地面积贴合，高度随比例（适合台面/落地设备）
        ExactNonUniform,    // 非等比：三轴各自精确等于目标（可能变形）
    }

    public struct FitResult
    {
        public Vector3 targetSize;    // 目标尺寸
        public Vector3 appliedScale;  // 实际写入的 localScale
        public Vector3 finalSize;     // 缩放后的最终尺寸
    }

    /// <summary>测量物体当前实际尺寸（本地尺寸 × lossyScale；仅适用于无旋转物体）</summary>
    public static bool TryGetWorldSize(GameObject obj, out Vector3 size)
    {
        size = Vector3.zero;
        if (obj == null) return false;
        if (!ModelMeasurer.Measure(obj, out ModelMeasurer.ModelSizeInfo info)) return false;
        size = Vector3.Scale(info.localSize, obj.transform.lossyScale);
        return true;
    }

    /// <summary>把 obj 缩放到目标尺寸并应用，返回缩放结果</summary>
    public static FitResult FitTo(GameObject obj, Vector3 targetSize, FitMode mode = FitMode.UniformFitInside, bool log = true)
    {
        FitResult r = new FitResult { targetSize = targetSize };
        if (obj == null) return r;

        if (!ModelMeasurer.Measure(obj, out ModelMeasurer.ModelSizeInfo info))
        {
            Debug.LogWarning($"[ModelScaler] {obj.name} 无 Renderer 无法测量，保持原缩放");
            r.appliedScale = obj.transform.localScale;
            return r;
        }

        // localSize 是 root 本地单位（不含自身 scale）→ 需要的 localScale = 目标 / (localSize × 父链缩放)
        Vector3 cur = info.localSize;
        Vector3 parentScale = obj.transform.parent != null ? obj.transform.parent.lossyScale : Vector3.one;
        Vector3 q = new Vector3(SafeDiv(targetSize.x, cur.x * parentScale.x),
                                SafeDiv(targetSize.y, cur.y * parentScale.y),
                                SafeDiv(targetSize.z, cur.z * parentScale.z));

        Vector3 applied;
        switch (mode)
        {
            case FitMode.UniformByFootprint:
                float f = Mathf.Min(q.x, q.z);
                applied = new Vector3(f, f, f);
                break;
            case FitMode.ExactNonUniform:
                applied = q;
                break;
            default: // UniformFitInside
                float u = Mathf.Min(q.x, Mathf.Min(q.y, q.z));
                applied = new Vector3(u, u, u);
                break;
        }

        obj.transform.localScale = applied;
        r.appliedScale = applied;
        r.finalSize = Vector3.Scale(Vector3.Scale(cur, applied), parentScale);

        if (log)
            Debug.Log($"[ModelScaler] {obj.name}：localScale={applied}，最终 {r.finalSize.x:F3} × {r.finalSize.y:F3} × {r.finalSize.z:F3} m" +
                      $"（目标 {targetSize.x:F3}×{targetSize.y:F3}×{targetSize.z:F3}，{mode}）");
        return r;
    }

    /// <summary>
    /// 商店升级一键换模型：在旧物件的位置/旋转/父节点下实例化新预制件，
    /// 把新模型缩放到旧物件的实际尺寸，然后销毁旧物件。返回新实例（失败返回 null）。
    /// </summary>
    public static GameObject UpgradeReplace(GameObject oldObj, GameObject newPrefab, FitMode mode = FitMode.UniformFitInside)
    {
        if (oldObj == null || newPrefab == null)
        {
            Debug.LogWarning("[ModelScaler] UpgradeReplace 参数为空");
            return null;
        }
        if (!TryGetWorldSize(oldObj, out Vector3 targetSize))
        {
            Debug.LogWarning($"[ModelScaler] 旧物件 {oldObj.name} 无法测量，取消升级替换");
            return null;
        }

        Transform parent = oldObj.transform.parent;
        GameObject inst = Object.Instantiate(newPrefab, oldObj.transform.position, oldObj.transform.rotation, parent);
        inst.name = newPrefab.name;

        FitTo(inst, targetSize, mode);
        Object.Destroy(oldObj);
        return inst;
    }

    static float SafeDiv(float a, float b) => Mathf.Abs(b) < 1e-6f ? 1f : a / b;
}

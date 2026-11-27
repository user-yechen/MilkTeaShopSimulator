using UnityEngine;
using UnityEditor;

/// <summary>
/// 应用缩放/旋转工具：顶部菜单“奶茶店 → 杯子 → 将当前大小设为原生大小”。
/// 把选中物体（含子层级）当前的缩放与旋转烘焙进网格顶点与碰撞体，
/// 视觉大小和朝向保持不变，Transform 的 Scale 全部重置为 1、Rotation 全部重置为 identity。
/// 烘焙出的网格另存为 Assets/烘焙网格/ 下的 .asset，不改动原 FBX。
/// 说明：BoxCollider 无法表示旋转后的盒子，旋转非 90° 倍数时只能近似。
/// </summary>
public static class ApplyScaleTool
{
    [MenuItem("奶茶店/归档/杯子/将当前大小设为原生大小（应用缩放）", false, 21)]
    static void ApplyScale()
    {
        System.IO.Directory.CreateDirectory("Assets/烘焙网格");

        foreach (GameObject go in Selection.gameObjects)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "应用缩放");
            ApplyRecursive(go.transform, Matrix4x4.identity);
            Debug.Log($"[ApplyScaleTool] {go.name}：当前大小与旋转已应用（Scale/Rotation 归 1）");
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("奶茶店/归档/杯子/将当前大小设为原生大小（应用缩放）", true)]
    static bool ApplyScaleValidate() => Selection.activeGameObject != null;

    /// <summary>parentLinear 为父层级累计的旋转+缩放矩阵（不含位移），逐层累计后烘焙</summary>
    static void ApplyRecursive(Transform t, Matrix4x4 parentLinear)
    {
        // SpriteRenderer 的显示大小只在 Transform 上、没有网格可烘，缩放归 1 会直接"变回原先大小"——跳过并保持原样
        bool isSprite = t.GetComponent<SpriteRenderer>() != null
                        && t.GetComponent<MeshFilter>() == null
                        && t.GetComponent<BoxCollider>() == null;
        if (isSprite)
        {
            foreach (Transform child in t)
                ApplyRecursive(child, Matrix4x4.identity);   // 该节点不动，子物体相对坐标系不变
            return;
        }

        Matrix4x4 localLinear = Matrix4x4.TRS(Vector3.zero, t.localRotation, t.localScale);
        Matrix4x4 worldLinear = parentLinear * localLinear;

        BakeMesh(t, worldLinear);
        BakeBoxCollider(t, worldLinear);

        t.localScale = Vector3.one;
        t.localRotation = Quaternion.identity;
        foreach (Transform child in t)
        {
            // 子物体原本处在被父级缩放/旋转过的坐标系里，位置需要做同样的变换
            child.localPosition = worldLinear.MultiplyPoint3x4(child.localPosition);
            ApplyRecursive(child, worldLinear);
        }
    }

    /// <summary>累计旋转+缩放矩阵是否为单位矩阵（没有需要烘焙的变换）</summary>
    static bool IsIdentity(Matrix4x4 m)
    {
        for (int i = 0; i < 16; i++)
        {
            float expected = (i % 5 == 0) ? 1f : 0f;
            if (Mathf.Abs(m[i] - expected) > 0.0001f) return false;
        }
        return true;
    }

    static void BakeMesh(Transform t, Matrix4x4 linear)
    {
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || IsIdentity(linear)) return;

        Mesh src = mf.sharedMesh;
        Mesh baked = Object.Instantiate(src);
        baked.name = src.name + "_原生大小";

        // 顶点按累计的 旋转+缩放 矩阵变换，重置 Transform 后视觉位置与朝向保持不变
        Vector3[] verts = src.vertices;
        for (int i = 0; i < verts.Length; i++)
            verts[i] = linear.MultiplyPoint3x4(verts[i]);
        baked.vertices = verts;

        // 法线按线性变换的逆转置修正（能同时正确处理非均匀缩放与旋转）
        Vector3[] normals = src.normals;
        if (normals.Length > 0 && Mathf.Abs(linear.determinant) > 0.0001f)
        {
            Matrix4x4 normalMatrix = linear.inverse.transpose;
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
            baked.normals = normals;
        }
        baked.RecalculateBounds();

        string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/烘焙网格/{baked.name}.asset");
        AssetDatabase.CreateAsset(baked, path);

        mf.sharedMesh = baked;
        MeshCollider mc = t.GetComponent<MeshCollider>();
        if (mc != null) mc.sharedMesh = baked;
    }

    static void BakeBoxCollider(Transform t, Matrix4x4 linear)
    {
        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box == null || IsIdentity(linear)) return;

        // BoxCollider 无法表示旋转后的盒子，按三个轴的缩放长度换算尺寸（90° 旋转时精确）
        box.size = new Vector3(
            linear.MultiplyVector(new Vector3(box.size.x, 0f, 0f)).magnitude,
            linear.MultiplyVector(new Vector3(0f, box.size.y, 0f)).magnitude,
            linear.MultiplyVector(new Vector3(0f, 0f, box.size.z)).magnitude);
        box.center = linear.MultiplyPoint3x4(box.center);
    }
}

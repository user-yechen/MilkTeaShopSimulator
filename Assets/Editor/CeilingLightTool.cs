using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>把场景里每一块天花板顶板变成光源：加一盏向下的点光 + 换成自发光材质。</summary>
public static class CeilingLightTool
{
    const string CeilingRootName = "天花板";
    const string BaseMaterialPath = "Assets/材料/地板_米黄.mat";
    const string EmissionMaterialPath = "Assets/材料/天花板_发光.mat";
    const float LightOffsetBelow = 0.25f;

    [MenuItem("奶茶店/场景搭建/天花板加光源", false, 7)]
    static void Apply()
    {
        GameObject root = FindCeilingRoot();
        if (root == null)
        {
            Debug.LogError("[CeilingLightTool] 当前场景找不到名为「天花板」的根物体");
            return;
        }

        Material emissionMaterial = EnsureEmissionMaterial();

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int created = 0;
        foreach (Renderer renderer in renderers)
        {
            Light light = renderer.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                GameObject lightGo = new GameObject("天花板光源");
                Undo.RegisterCreatedObjectUndo(lightGo, "天花板加光源");
                lightGo.transform.SetParent(renderer.transform, false);

                light = lightGo.AddComponent<Light>();
                light.type      = LightType.Point;
                light.color     = new Color(1f, 0.96f, 0.88f);
                light.intensity = 1.2f;
                light.range     = 12f;
                light.shadows   = LightShadows.None;
                created++;
            }

            // 顶板带非等比缩放（y 只有 0.1），localPosition 会被压扁，这里按世界坐标放到板底下方
            light.transform.position = renderer.bounds.center
                - Vector3.up * (renderer.bounds.extents.y + LightOffsetBelow);

            if (emissionMaterial != null) renderer.sharedMaterial = emissionMaterial;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[CeilingLightTool] 共 {renderers.Length} 块顶板，新建 {created} 盏点光源，已换自发光材质");
    }

    /// <summary>场景里根节点和每块顶板都叫「天花板」，取子 Renderer 最多的那个当根。</summary>
    static GameObject FindCeilingRoot()
    {
        Transform best = null;
        int bestCount = 0;

        foreach (Transform tf in Object.FindObjectsOfType<Transform>(true))
        {
            if (tf.name != CeilingRootName) continue;

            int count = tf.GetComponentsInChildren<Renderer>(true).Length;
            if (count > bestCount)
            {
                bestCount = count;
                best = tf;
            }
        }

        return best != null ? best.gameObject : null;
    }

    static Material EnsureEmissionMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(EmissionMaterialPath);
        if (material == null)
        {
            Material baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            if (baseMaterial == null)
            {
                Debug.LogError($"[CeilingLightTool] 找不到底材质 {BaseMaterialPath}");
                return null;
            }
            material = new Material(baseMaterial);
            material.name = "天花板_发光";
            AssetDatabase.CreateAsset(material, EmissionMaterialPath);
        }

        material.EnableKeyword("_EMISSION");
        Color emission = new Color(1f, 0.96f, 0.88f) * 0.4f;
        emission.a = 1f;
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }
}

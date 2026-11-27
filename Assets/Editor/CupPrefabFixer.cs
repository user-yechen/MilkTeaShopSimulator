using UnityEngine;
using UnityEditor;

public static class CupPrefabFixer
{
    [MenuItem("奶茶店/归档/材质/修复杯子 Prefab 材质（极简版）", false, 41)]
    static void Fix()
    {
        string[] cups = {
            "Assets/预制件/奶茶杯_中.prefab",
            "Assets/预制件/奶茶杯_大.prefab"
        };

        // 源 GLB 路径（按文件名匹配）
        var glbMap = new System.Collections.Generic.Dictionary<string, string>
        {
            { "奶茶杯_中", "Assets/模型/奶茶杯_中.glb" },
            { "奶茶杯_大", "Assets/模型/奶茶杯_大.glb" }
        };

        foreach (var prefabPath in cups)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            string glbPath = null;
            foreach (var kv in glbMap)
                if (name.StartsWith(kv.Key)) { glbPath = kv.Value; break; }

            if (string.IsNullOrEmpty(glbPath) || !System.IO.File.Exists(glbPath))
            {
                Debug.LogWarning($"[Fix] 找不到 GLB 源: {glbPath}");
                continue;
            }

            // 加载源 GLB，取第一个 Renderer 的材质
            var glbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (glbAsset == null) { Debug.LogError($"[Fix] 加载 GLB 失败: {glbPath}"); continue; }
            var glbR = glbAsset.GetComponentInChildren<Renderer>(true);
            if (glbR == null || glbR.sharedMaterial == null)
            {
                Debug.LogError($"[Fix] GLB 没有材质: {glbPath}");
                continue;
            }
            Debug.Log($"[Fix] 源 GLB 材质 Shader: {glbR.sharedMaterial.shader.name}, 颜色: {glbR.sharedMaterial.color}");

            // 加载 Prefab，给 Renderer 赋材质
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool changed = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r.sharedMaterial == null || r.sharedMaterial != glbR.sharedMaterial)
                {
                    r.sharedMaterial = new Material(glbR.sharedMaterial); // 实例化，不影响源 GLB
                    changed = true;
                    Debug.Log($"  已给 {r.name} 赋材质");
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[Fix] ✅ 保存 {prefabPath}");
            }
            else
            {
                Debug.Log($"[Fix] ⏭️ {prefabPath} 无需修改");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Fix] 全部完成");
    }
}

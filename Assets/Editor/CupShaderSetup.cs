using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 一键配置杯子 Shader 裁剪方案 B。
/// Shader 已放在 Assets/Resources/Shaders/ 下，Cup.cs 用 Resources.Load 加载（Play Mode 和打包都可靠）。
/// </summary>
public static class CupShaderSetup
{
    [MenuItem("奶茶店/归档/材质/配置杯子裁剪 Shader", false, 42)]
    static void Setup()
    {
        // 用 guid 直接加载 shader，比 Shader.Find 更可靠
        string shaderPath = AssetDatabase.GUIDToAssetPath("2f25be5acd9864d41a43984c4666671d");
        Shader cupShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (cupShader == null)
        {
            Debug.LogError("[CupShaderSetup] 找不到 CupWithFill.shader！请确认文件存在。");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab 奶茶杯", new[] { "Assets/预制件" });
        int count = 0;
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            // 只处理单独的杯子 Prefab（名字含"奶茶杯"且不等于"奶茶杯架"）
            string name = prefab.name.Replace("_", "");
            if (name.Contains("奶茶杯架") || !name.Contains("奶茶杯")) continue;

            // 用 PrefabUtility 加载，修改 Renderer 材质后保存
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            // 给根节点 Renderer 赋 CupWithFill Shader
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // 跳过材质已丢失的（被之前的 bug 清空的），让 FixCupMaterials 处理
                if (r.sharedMaterial == null) continue;

                Shader s = r.sharedMaterial.shader;
                if (s == null) continue;
                if (s.name == "Custom/CupWithFill" || s.name == "Custom/MilkTeaLiquid") continue;

                var newMat = new Material(cupShader);
                // 复制原始材质参数
                var srcMat = r.sharedMaterial;
                if (srcMat.HasProperty("_MainTex")) newMat.SetTexture("_MainTex", srcMat.GetTexture("_MainTex"));
                if (srcMat.HasProperty("_Color"))   newMat.SetColor("_Color",   srcMat.GetColor("_Color"));
                if (srcMat.HasProperty("_BaseMap"))   newMat.SetTexture("_MainTex", srcMat.GetTexture("_BaseMap"));
                if (srcMat.HasProperty("_BaseColor")) newMat.SetColor("_Color",     srcMat.GetColor("_BaseColor"));
                r.sharedMaterial = newMat;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
                Debug.Log($"[CupShaderSetup] ✅ 已更新 {path}");
            }
            else
            {
                Debug.Log($"[CupShaderSetup] ⏭️ 跳过 {path}（已是目标 Shader）");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CupShaderSetup] 完成！共更新 {count} 个 Prefab。");
    }
}

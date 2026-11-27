using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 预制件快捷工具：
///   - 选中场景物件 → 一键保存为预制件（自动命名 + 放到 Assets/预制件/）
///   - 选中 PrefabInstance → 一键应用修改到预制件 / 恢复预制件
/// 入口：顶部菜单「奶茶店/预制件/」、右键上下文菜单、快捷键 Ctrl+Shift+S (保存) / Ctrl+Shift+A (应用)
/// </summary>
public static class PrefabQuickTools
{
    const string PrefabFolder = "Assets/预制件";

    // ====== 新建预制件 ======

    [MenuItem("奶茶店/预制件/保存选中为预制件 %#s", true)]
    [MenuItem("CONTEXT/GameObject/保存为预制件", true)]
    static bool ValidateCreatePrefab()
    {
        return Selection.activeGameObject != null && !Application.isPlaying;
    }

    [MenuItem("奶茶店/预制件/保存选中为预制件 %#s", false, 60)]
    [MenuItem("CONTEXT/GameObject/保存为预制件")]
    static void CreatePrefab()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return;

        EnsureFolder(PrefabFolder);

        string fileName = SanitizeFileName(go.name) + ".prefab";
        string path = Path.Combine(PrefabFolder, fileName).Replace("\\", "/");

        // 如果文件已存在，弹框让用户选是否覆盖
        if (File.Exists(path))
        {
            if (!EditorUtility.DisplayDialog("预制件已存在",
                $"{path}\n\n是否覆盖？", "覆盖", "取消"))
                return;
        }

        PrefabUtility.SaveAsPrefabAsset(go, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PrefabQuickTools] ✅ 已保存 {path}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    // ====== 应用修改到预制件 ======

    [MenuItem("奶茶店/预制件/应用选中的预制件实例 %#a", true)]
    [MenuItem("CONTEXT/GameObject/应用到预制件", true)]
    static bool ValidateApplyPrefab()
    {
        if (Selection.activeGameObject == null || Application.isPlaying) return false;
        return PrefabUtility.IsPartOfPrefabInstance(Selection.activeGameObject);
    }

    [MenuItem("奶茶店/预制件/应用选中的预制件实例 %#a", false, 61)]
    [MenuItem("CONTEXT/GameObject/应用到预制件")]
    static void ApplyPrefab()
    {
        GameObject instance = Selection.activeGameObject;
        if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance)) return;

        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (prefab == null)
        {
            Debug.LogWarning("[PrefabQuickTools] 找不到对应的预制件源");
            return;
        }

        PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PrefabQuickTools] ✅ 已应用修改到 {AssetDatabase.GetAssetPath(prefab)}");
    }

    // ====== 恢复预制件 ======

    [MenuItem("奶茶店/预制件/恢复选中的预制件实例", true)]
    [MenuItem("CONTEXT/GameObject/恢复预制件", true)]
    static bool ValidateRevertPrefab()
    {
        if (Selection.activeGameObject == null || Application.isPlaying) return false;
        return PrefabUtility.IsPartOfPrefabInstance(Selection.activeGameObject);
    }

    [MenuItem("奶茶店/预制件/恢复选中的预制件实例", false, 62)]
    [MenuItem("CONTEXT/GameObject/恢复预制件")]
    static void RevertPrefab()
    {
        GameObject instance = Selection.activeGameObject;
        if (instance == null || !PrefabUtility.IsPartOfPrefabInstance(instance)) return;

        if (!EditorUtility.DisplayDialog("恢复预制件",
            $"将丢弃 {instance.name} 的所有本地修改，恢复为预制件当前状态。\n\n继续？",
            "恢复", "取消"))
            return;

        PrefabUtility.RevertPrefabInstance(instance, InteractionMode.UserAction);
        Debug.Log($"[PrefabQuickTools] ✅ 已恢复 {instance.name}");
    }

    // ====== 把选中替换成预制件 ======

    [MenuItem("奶茶店/预制件/用预制件替换选中", true)]
    static bool ValidateReplaceWithPrefab()
    {
        return Selection.activeGameObject != null && !Application.isPlaying;
    }

    [MenuItem("奶茶店/预制件/用预制件替换选中", false, 63)]
    static void ReplaceWithPrefab()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) return;

        string prefabPath = EditorUtility.OpenFilePanel("选择预制件", PrefabFolder, "prefab");
        if (string.IsNullOrEmpty(prefabPath)) return;

        // OpenFilePanel 返回的是系统绝对路径，转成 Assets 相对路径
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
        if (!prefabPath.StartsWith(projectRoot))
        {
            EditorUtility.DisplayDialog("路径错误", "请选择项目内的预制件", "OK");
            return;
        }
        string assetPath = prefabPath.Substring(projectRoot.Length + 1).Replace("\\", "/");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError("[PrefabQuickTools] 加载预制件失败：" + assetPath);
            return;
        }

        // 保存原始位置/旋转/缩放
        Vector3 pos = go.transform.position;
        Quaternion rot = go.transform.rotation;
        Vector3 scale = go.transform.lossyScale;
        Transform parent = go.transform.parent;

        // 实例化预制件
        GameObject newGo = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (newGo != null)
        {
            newGo.transform.position = pos;
            newGo.transform.rotation = rot;
            newGo.transform.localScale = scale;
        }

        // 删除旧物体
        Object.DestroyImmediate(go);

        Selection.activeGameObject = newGo;
        Debug.Log($"[PrefabQuickTools] ✅ 已用 {prefab.name} 替换");
    }

    // ====== 工具方法 ======

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}

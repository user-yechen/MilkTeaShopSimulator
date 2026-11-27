using UnityEditor;
using UnityEngine;

/// <summary>
/// 进入播放模式后，把编辑器的 Scene 视图对齐（绑定）到主摄像机，
/// 方便直接从玩家（摄像机）第一视角观察场景。
/// 菜单：奶茶店 → 工具 → 运行时Scene跟随摄像机（可开关）。
/// </summary>
[InitializeOnLoad]
public static class PlayViewBinder
{
    const string PrefKey = "PlayViewBinder.Enabled";
    const string MenuPath = "奶茶店/归档/工具/运行时Scene跟随摄像机";
    static bool enabled = EditorPrefs.GetBool(PrefKey, true);

    static PlayViewBinder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(MenuPath, false, 102)]
    static void Toggle()
    {
        enabled = !enabled;
        EditorPrefs.SetBool(PrefKey, enabled);
        Menu.SetChecked(MenuPath, enabled);
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, enabled);
        return true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && enabled)
        {
            // 延迟到摄像机 Start 完成后再开始对齐
            EditorApplication.delayCall += () => EditorApplication.update += AlignSceneToCamera;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= AlignSceneToCamera;
        }
    }

    static void AlignSceneToCamera()
    {
        if (!EditorApplication.isPlaying) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;

        view.AlignViewToObject(cam.transform);
    }
}

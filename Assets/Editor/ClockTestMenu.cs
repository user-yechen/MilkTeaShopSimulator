using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>顶部菜单栏里的测试功能</summary>
public static class ClockTestMenu
{
    [MenuItem("奶茶店/工具/调整时间到 9:00", false, 100)]
    static void SetTimeToNine()
    {
        // 运行时：直接把游戏时钟跳到 9:00
        if (Application.isPlaying)
        {
            GameClock clock = Object.FindObjectOfType<GameClock>();
            if (clock != null)
            {
                clock.SetTime(9, 0);
                Debug.Log("[时钟测试] 游戏时间已调整到 09:00");
                return;
            }
            Debug.LogWarning("[时钟测试] 没有找到 GameClock（场景中缺少 Canvas？）");
            return;
        }

        // 编辑模式：修改场景里 GameClock 的起始时间，进入播放后从 9:00 开始
        GameClock[] clocks = Object.FindObjectsOfType<GameClock>(true);
        if (clocks.Length == 0)
        {
            Debug.LogWarning("[时钟测试] 场景中没有 GameClock：直接点 Play 后（会自动挂到 Canvas 上）再使用本菜单。");
            return;
        }

        foreach (GameClock clock in clocks)
        {
            clock.SetStartTime(9, 0);
            EditorUtility.SetDirty(clock);
            Scene scene = clock.gameObject.scene;
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }
        Debug.Log("[时钟测试] 已把起始时间设为 09:00，下次播放时生效。");
    }
}

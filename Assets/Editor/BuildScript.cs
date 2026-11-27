using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 一键出包工具。
/// 菜单：奶茶店/构建/Android APK
/// 命令行：
///   Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt; -buildTarget Android ^
///             -executeMethod BuildScript.BuildAndroid -logFile Build/build.log
/// 产物：Build/Android/milktea_{版本号}_{时间}.apk
/// </summary>
public static class BuildScript
{
    public const string MainScene = "Assets/Scenes/奶茶店.unity";

    [MenuItem("奶茶店/构建/Android APK", false, 200)]
    public static void BuildAndroidMenu()
    {
        BuildAndroid();
    }

    public static void BuildAndroid()
    {
        EnsureSceneInBuildSettings();

        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build", "Android");
        Directory.CreateDirectory(outDir);
        string fileName = string.Format("milktea_{0}_{1:yyyyMMdd_HHmm}.apk", PlayerSettings.bundleVersion, DateTime.Now);
        string outPath = Path.Combine(outDir, fileName);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { MainScene },
            locationPathName = outPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log(string.Format("[Build] 结果={0} 大小={1:F1}MB 耗时={2:F0}s 输出={3}",
            summary.result, summary.totalSize / 1024f / 1024f, summary.totalTime.TotalSeconds, outPath));

        if (summary.result != BuildResult.Succeeded)
            Debug.LogError("[Build] 构建失败，检查上方日志（Android 模块/JDK/SDK/签名）");

        // 批处理模式下用退出码告诉 CI 成功与否
        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>主场景不在 Build Settings 里就补进去（幂等），避免“场景列表为空打不出包”</summary>
    static void EnsureSceneInBuildSettings()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.path == MainScene) return;

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.Insert(0, new EditorBuildSettingsScene(MainScene, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("[Build] 已把主场景加入 Build Settings：" + MainScene);
    }
}

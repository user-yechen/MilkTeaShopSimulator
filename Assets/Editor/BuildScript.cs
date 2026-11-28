using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// 一键出包工具。
/// 菜单：奶茶店/构建/Android APK、WebGL、小游戏宿主（检查/安装）
/// 命令行：
///   Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt; -buildTarget Android ^
///             -executeMethod BuildScript.BuildAndroid -logFile Build/build.log
///   Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt; -buildTarget WebGL ^
///             -executeMethod BuildScript.BuildWebGL -logFile Build/build-webgl.log
/// 产物：Build/Android/*.apk、Build/WebGL/
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

    // ===== WebGL（itch.io / CrazyGames / 自建站点通用） =====

    [MenuItem("奶茶店/构建/WebGL", false, 201)]
    public static void BuildWebGLMenu() => BuildWebGL();

    public static void BuildWebGL()
    {
        EnsureSceneInBuildSettings();
        ConfigureWebGL();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build", "WebGL");
        Directory.CreateDirectory(outDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { MainScene },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Report(BuildPipeline.BuildPlayer(options), outDir);
    }

    static void ConfigureWebGL()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.WebGL, Il2CppCompilerConfiguration.Release);
        PlayerSettings.stripEngineCode = true;

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;   // 任意静态服务器都能直接跑
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.dataCaching = true;

        Debug.Log("[Build] WebGL：IL2CPP/Release、Strip Engine Code、Gzip + decompression fallback");
    }

    // ===== 团结引擎「小游戏宿主」 =====

    const string MiniHostPackage = "cn.tuanjie.minihost";

    [MenuItem("奶茶店/构建/检查小游戏宿主环境", false, 202)]
    public static void CheckMiniHost()
    {
        bool webgl = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        bool pkg = HasPackage(MiniHostPackage);
        Debug.Log($"[Build] WebGL 模块：{(webgl ? "已安装" : "缺失")}；{MiniHostPackage}：{(pkg ? "已安装" : "未安装")}");
        if (!pkg) Debug.Log("[Build] 可用菜单「奶茶店/构建/安装小游戏宿主插件」自动安装");
    }

    [MenuItem("奶茶店/构建/安装小游戏宿主插件", false, 203)]
    public static void InstallMiniHostPackage()
    {
        if (HasPackage(MiniHostPackage))
        {
            Debug.Log("[Build] 已安装 " + MiniHostPackage);
            return;
        }

        AddRequest request = Client.Add(MiniHostPackage);
        while (!request.IsCompleted) System.Threading.Thread.Sleep(200);

        if (request.Status == StatusCode.Success)
            Debug.Log("[Build] 已安装 " + MiniHostPackage + "，重开 Unity 后菜单栏会出现「小游戏宿主」");
        else
            Debug.LogError("[Build] 安装失败：" + request.Error.message);
    }

    /// <summary>打开插件自带的构建窗口（Game Id 等配置在窗口里填）</summary>
    [MenuItem("奶茶店/构建/小游戏宿主（打开构建窗口）", false, 204)]
    public static void OpenMiniHostBuild()
    {
        if (!HasPackage(MiniHostPackage))
        {
            Debug.LogError("[Build] 先安装 " + MiniHostPackage + "（菜单：安装小游戏宿主插件）");
            return;
        }

        if (!EditorApplication.ExecuteMenuItem("小游戏宿主/构建"))
            Debug.LogWarning("[Build] 没找到「小游戏宿主/构建」菜单，确认插件已导入并重开 Unity");
    }

    static bool HasPackage(string packageName)
    {
        foreach (var info in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            if (info.name == packageName) return true;
        return false;
    }

    static void Report(BuildReport report, string outPath)
    {
        BuildSummary summary = report.summary;
        Debug.Log(string.Format("[Build] 结果={0} 大小={1:F1}MB 耗时={2:F0}s 输出={3}",
            summary.result, summary.totalSize / 1024f / 1024f, summary.totalTime.TotalSeconds, outPath));

        if (summary.result != BuildResult.Succeeded)
            Debug.LogError("[Build] 构建失败，检查上方日志");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}

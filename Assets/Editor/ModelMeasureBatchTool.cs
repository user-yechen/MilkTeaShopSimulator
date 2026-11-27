using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 批量测量场景关键物件尺寸，结果写入 Temp/measure_out.txt（不进 Assets，避免触发导入）。
/// 两种用法：
///   ① 编辑器里点菜单「奶茶店/工具/批量测量场景物件尺寸」；
///   ② 命令行无头跑（编辑器不能处于打开状态）：
///      Unity.exe -batchmode -projectPath d:\UnityProject\milktea -executeMethod ModelMeasureBatchTool.RunBatch -quit
/// 物件按特征定位（同 AutoAttach 思路）：奶茶机=有子「杯位」、加热机=有「杯位左」+「杯位右」。
/// 测量用 ModelMeasurer（本地尺寸不吃旋转膨胀），includeInactive=false 只量可见部分。
/// </summary>
public static class ModelMeasureBatchTool
{
    const string OutputPath = "Temp/measure_out.txt";

    [MenuItem("奶茶店/工具/批量测量场景物件尺寸", false, 104)]
    static void RunMenu()
    {
        string report = MeasureAll();
        WriteReport(report);
        Debug.Log(report);
    }

    /// <summary>命令行批处理入口</summary>
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/奶茶店.unity");
        string report = MeasureAll();
        WriteReport(report);
        Debug.Log(report);
        EditorApplication.Exit(0);
    }

    static void WriteReport(string report)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(OutputPath, report);
    }

    static string MeasureAll()
    {
        var sb = new StringBuilder();
        sb.AppendLine("== 场景物件尺寸测量（ModelMeasurer，单位 m，本地尺寸不受旋转影响）==");

        // (显示名, 定位条件)
        var targets = new (string label, System.Func<Transform, bool> match)[]
        {
            ("奶茶机",   t => t.Find("杯位") != null && t.Find("杯位左") == null),
            ("加热机",   t => t.Find("杯位左") != null && t.Find("杯位右") != null),
            ("前台",     t => t.name.Contains("前台") || t.Find("台面") != null),
            ("奶茶杯架", t => t.name.Contains("杯架")),
            ("冰格/前制作台", t => t.GetComponent<IceTray>() != null),
            ("路人",     t => t.name.Contains("路人")),
            ("大奶茶杯", t => t.name == "大奶茶杯"),
            ("中奶茶杯", t => t.name == "中奶茶杯"),
            ("地板",     t => t.name == "地板"),
        };

        var measured = new HashSet<int>();
        foreach ((string label, System.Func<Transform, bool> match) in targets)
        {
            GameObject go = Find(match);
            if (go == null)
            {
                sb.AppendLine($"{label}：未找到");
                continue;
            }
            if (!measured.Add(go.GetInstanceID()))
            {
                sb.AppendLine($"{label}：与上一项同一物体，跳过");
                continue;
            }
            AppendMeasure(sb, label, go);
        }
        return sb.ToString();
    }

    static void AppendMeasure(StringBuilder sb, string label, GameObject go)
    {
        if (!ModelMeasurer.Measure(go, out ModelMeasurer.ModelSizeInfo info, includeInactive: false))
        {
            sb.AppendLine($"{label}（{go.name}）：无可见 Renderer，无法测量");
            return;
        }
        Vector3 s = info.localSize;
        Vector3 w = info.worldSize;
        sb.AppendLine($"{label}（{go.name}）：本地 {s.x:F3} × {s.y:F3} × {s.z:F3}｜占地 {s.x:F3} × {s.z:F3}｜" +
                      $"世界AABB {w.x:F3} × {w.y:F3} × {w.z:F3}｜Renderer×{info.rendererCount}");
    }

    static GameObject Find(System.Func<Transform, bool> match)
    {
        foreach (Transform t in Object.FindObjectsOfType<Transform>(true))
            if (match(t)) return t.gameObject;
        return null;
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单「奶茶店/工具/测量选中模型尺寸」：对选中的每个 GameObject 打印
/// 本地尺寸（长×高×深）、占地面积、世界 AABB，供模型升级功能参考。
/// 测量逻辑在运行时脚本 ModelMeasurer.cs（Editor 与打包都可用同一套 API）。
/// </summary>
public static class ModelMeasureTool
{
    [MenuItem("奶茶店/工具/测量选中模型尺寸", false, 103)]
    static void MeasureSelection()
    {
        foreach (GameObject go in Selection.gameObjects)
            ModelMeasurer.LogMeasure(go);
    }

    [MenuItem("奶茶店/工具/测量选中模型尺寸", true)]
    static bool Validate() => Selection.gameObjects.Length > 0;
}

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一次性工具：给「奶茶机交互界面」预制体里的每个流体格子挂 FluidCellButton，
/// 并按子物体 text 的内容（牛奶 / 茶）填好默认名称和颜色。
/// </summary>
public static class FluidCellSetupTool
{
    const string PrefabPath = "Assets/预制件/奶茶机交互界面.prefab";

    [MenuItem("奶茶店/归档/交互界面/给流体格子加点击脚本", false, 80)]
    static void Setup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("[FluidCellSetupTool] 读不到预制体：" + PrefabPath);
            return;
        }

        int added = 0;
        int configured = 0;
        int closeAdded = 0;
        try
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("流体格子")) continue;

                FluidCellButton cell = t.GetComponent<FluidCellButton>();
                if (cell == null)
                {
                    cell = t.gameObject.AddComponent<FluidCellButton>();
                    added++;
                }

                string label = CellLabel(t);
                if (!string.IsNullOrEmpty(label)) cell.fluidName = label;
                cell.fluidColor = DefaultColor(label);
                cell.tintCup = true;
                configured++;
            }

            // 标题栏的关闭按钮：单击关掉整个窗口（窗口根 = 向上找到的「奶茶机交互界面」）
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.Contains("关闭按钮")) continue;
                if (t.GetComponent<PanelCloseButton>() != null) continue;
                PanelCloseButton close = t.gameObject.AddComponent<PanelCloseButton>();
                close.windowRoot = null;
                closeAdded++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[FluidCellSetupTool] 处理 {configured} 个流体格子（新增脚本 {added} 个），关闭按钮新增 {closeAdded} 个，已保存预制体");
    }

    /// <summary>取格子里第一个有文字的 Text（牛奶 / 茶）</summary>
    static string CellLabel(Transform cell)
    {
        foreach (Transform t in cell.GetComponentsInChildren<Transform>(true))
        {
            Text text = t.GetComponent<Text>();
            if (text != null && !string.IsNullOrEmpty(text.text)) return text.text.Trim();
        }
        return "";
    }

    static Color DefaultColor(string label)
    {
        if (label.Contains("牛奶")) return new Color(1f, 1f, 1f, 0.9f);
        // 茶：黄色半透明（琥珀色茶汤），不要用深棕
        if (label.Contains("茶")) return new Color(1f, 0.85f, 0.3f, 0.6f);
        return new Color(1f, 1f, 1f, 0.85f);
    }
}

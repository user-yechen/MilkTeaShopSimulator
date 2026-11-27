using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 奶茶液材质创建工具：在 Assets/Resources/材料/ 下创建奶茶液.mat（Built-in RP，Particles/Standard Unlit，半透明奶茶色），
/// PourFluid.cs 运行时用 Resources.Load 加载（Editor 和打包都可靠）。
/// </summary>
public static class MilkTeaMaterialSetup
{
    const string MatFolder = "Assets/Resources/材料";
    const string MatPath = "Assets/Resources/材料/奶茶液.mat";

    [MenuItem("奶茶店/材质/创建奶茶液材质并赋值", false, 40)]
    public static void CreateAndAssign()
    {
        // 1. 创建 Particles/Standard Unlit 材质
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            // 回退：Built-in RP 里找不到的话用 Particles/Alpha Blended
            shader = Shader.Find("Particles/Alpha Blended");
        }
        if (shader == null)
        {
            // 最后兜底：任何可用 shader
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            Debug.LogError("[MilkTeaMaterialSetup] 找不到任何可用 Shader！");
            return;
        }

        EnsureFolder(MatFolder);

        Material mat = new Material(shader);
        mat.name = "奶茶液";

        // 奶茶色：浅棕色带半透明
        // r=0.65  g=0.45  b=0.25  a=0.8
        mat.SetColor("_Color", new Color(0.65f, 0.45f, 0.25f, 0.8f));

        // 半透明模式（Particles/Standard Unlit 用 _Mode=3 是 Transparent）
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        // 不让 Tint Color 覆盖主色（Particles/Standard Unlit 默认会乘 _Color）
        // 已经通过 _Color 设置了奶茶色，够用

        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MilkTeaMaterialSetup] ✅ 已创建 {MatPath}，Shader={shader.name}");

        // 2. 选出来让用户能直接看到
        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);
    }

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
}

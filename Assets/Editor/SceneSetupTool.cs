using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

/// <summary>
/// 场景搭建工具：顶部菜单“奶茶店 → 场景搭建 → 创建地板 / 生成半地板 / 生成墙壁 / 生成半长墙 / 墙体高度加倍”。
/// 约定：+Z 为前方，+X 为右方。
/// </summary>
public static class SceneSetupTool
{
    const float FloorSize = 10f;      // 地板边长
    const float FloorThickness = 0.1f; // 地板厚度
    const float FloorTopY = 0f;       // 地板上表面高度
    const float WallLength = 10f;     // 墙壁长度（与地板边长一致）
    const float WallHalfLength = 5f;  // 半长墙长度（与半地板短边一致）
    const float WallHeight = 6f;      // 墙壁高度
    const float WallThickness = 0.2f; // 墙壁厚度

    [MenuItem("奶茶店/场景搭建/创建地板", false, 1)]
    public static void CreateFloor()
    {
        Material floorMat = EnsureMaterial("地板_米黄", new Color(239f / 255f, 228f / 255f, 176f / 255f), 0f, 0.15f);

        // 10×10 地板，生成在场景中心，上表面 y=0，可手动拖动拼接
        CreateFloorPiece("地板", new Vector3(FloorSize, FloorThickness, FloorSize), floorMat);
    }

    [MenuItem("奶茶店/场景搭建/生成半地板（10×5）", false, 2)]
    public static void CreateHalfFloor10x5()
    {
        Material floorMat = EnsureMaterial("地板_米黄", new Color(239f / 255f, 228f / 255f, 176f / 255f), 0f, 0.15f);

        // 10×5 半地板（X 向 10、Z 向 5），其余与整块地板一致
        CreateFloorPiece("半地板（10×5）", new Vector3(FloorSize, FloorThickness, FloorSize / 2f), floorMat);
    }

    [MenuItem("奶茶店/场景搭建/生成半地板（5×10）", false, 3)]
    public static void CreateHalfFloor5x10()
    {
        Material floorMat = EnsureMaterial("地板_米黄", new Color(239f / 255f, 228f / 255f, 176f / 255f), 0f, 0.15f);

        // 5×10 半地板（X 向 5、Z 向 10），其余与整块地板一致
        CreateFloorPiece("半地板（5×10）", new Vector3(FloorSize / 2f, FloorThickness, FloorSize), floorMat);
    }

    /// <summary>生成一块地板：scale 决定长宽高，生成在场景中心、上表面 y=0，可手动拖动拼接</summary>
    static void CreateFloorPiece(string name, Vector3 scale, Material mat)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = name;
        floor.transform.localScale = scale;
        floor.transform.position = new Vector3(0f, FloorTopY - scale.y * 0.5f, 0f);
        floor.GetComponent<Renderer>().sharedMaterial = mat;
        Undo.RegisterCreatedObjectUndo(floor, "生成地板");
        Debug.Log($"[SceneSetupTool] 已生成 {name}（{scale.x}×{scale.z}，上表面 y={FloorTopY}）");
    }

    [MenuItem("奶茶店/场景搭建/生成墙壁", false, 4)]
    public static void CreateWall()
    {
        Material wallMat = EnsureMaterial("墙壁_白", new Color(245f / 255f, 245f / 255f, 240f / 255f), 0f, 0.2f);

        // 10×6×0.2 墙壁，生成在场景中心，底部 y=0，可手动拖动/旋转拼接
        CreateWallPiece("墙壁", WallLength, wallMat);
    }

    [MenuItem("奶茶店/场景搭建/生成半长墙（5×6）", false, 5)]
    public static void CreateHalfWall()
    {
        Material wallMat = EnsureMaterial("墙壁_白", new Color(245f / 255f, 245f / 255f, 240f / 255f), 0f, 0.2f);

        // 5×6×0.2 半长墙，长度适配半地板（10×5 / 5×10）的短边
        CreateWallPiece("半长墙（5×6）", WallHalfLength, wallMat);
    }

    /// <summary>生成一面墙：length 为长度，高度厚度固定；生成在场景中心，底部 y=0，可手动拖动/旋转拼接</summary>
    static void CreateWallPiece(string name, float length, Material mat)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.localScale = new Vector3(length, WallHeight, WallThickness);
        wall.transform.position = new Vector3(0f, WallHeight / 2f, 0f);
        wall.GetComponent<Renderer>().sharedMaterial = mat;
        Undo.RegisterCreatedObjectUndo(wall, "生成墙壁");
        Debug.Log($"[SceneSetupTool] 已生成 {name}（{length}×{WallHeight}×{WallThickness}，底部 y=0）");
    }

    /// <summary>
    /// 把场景中所有墙体（名字以“墙壁”或“半长墙”开头）高度加高一倍：
    /// 缩放 Y 翻倍，并同步调整位置让墙的底边保持在原高度，不会陷入地板。
    /// 支持 Ctrl+Z 撤销。
    /// </summary>
    [MenuItem("奶茶店/场景搭建/墙体高度加倍", false, 6)]
    public static void DoubleWallHeight()
    {
        int count = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (!t.name.StartsWith("墙壁") && !t.name.StartsWith("半长墙")) continue;

                // 底边世界高度 = 中心 - 世界高度的一半（父级缩放为 1 时成立）
                float bottom = t.position.y - t.lossyScale.y * 0.5f;

                Undo.RecordObject(t, "墙体高度加倍");
                Vector3 scale = t.localScale;
                scale.y *= 2f;
                t.localScale = scale;
                t.position = new Vector3(t.position.x, bottom + t.lossyScale.y * 0.5f, t.position.z);

                count++;
            }
        }

        Debug.Log($"[SceneSetupTool] 墙体高度已加倍：共处理 {count} 面墙（底边保持不变）。");
    }

    /// <summary>创建（或更新）一个 Standard 材质，存到 Assets/材料 下</summary>
    static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder("Assets/材料"))
            AssetDatabase.CreateFolder("Assets", "材料");

        string path = $"Assets/材料/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness); // 越低越哑光
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}

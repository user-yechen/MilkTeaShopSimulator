using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

/// <summary>
/// 场景搭建：地板 / 墙壁 / 天花板。
/// 所有生成都带**吸附**：地板拼到已有地板边缘、墙贴地板边且底部下沉 1cm，
/// 生成后不需要再手调位置，也不会出现缝隙或共面闪烁。
/// 约定：+Z 为前方，+X 为右方，网格 0.5m。
/// </summary>
public static class SceneSetupTool
{
    public const float Grid = 0.5f;

    public const float FloorSize = 10f;
    public const float FloorThickness = 0.1f;
    public const float FloorTopY = 0.05f;

    public const float WallLength = 10f;
    public const float WallHalfLength = 5f;
    public const float WallHeight = 6f;
    public const float WallThickness = 0.2f;

    /// <summary>墙底沉入地板的深度：藏在内部，既无缝隙也不会共面闪烁</summary>
    public const float WallSink = 0.01f;

    // ---------------- 地板 ----------------

    [MenuItem("奶茶店/场景搭建/创建地板", false, 1)]
    public static void CreateFloor()
    {
        CreateFloorPiece("地板", FloorSize, FloorSize);
    }

    [MenuItem("奶茶店/场景搭建/生成半地板（10×5）", false, 2)]
    public static void CreateHalfFloor10x5()
    {
        CreateFloorPiece("半地板（10×5）", FloorSize, FloorSize / 2f);
    }

    [MenuItem("奶茶店/场景搭建/生成半地板（5×10）", false, 3)]
    public static void CreateHalfFloor5x10()
    {
        CreateFloorPiece("半地板（5×10）", FloorSize / 2f, FloorSize);
    }

    /// <summary>
    /// 生成一块地板。若当前选中的是地板，则**沿它的 +X 边精确拼接**（共享边，不重叠不留缝）；
    /// 否则放在原点。XZ 位置吸附到 0.5m 网格，上表面固定在 FloorTopY。
    /// </summary>
    public static GameObject CreateFloorPiece(string name, float sizeX, float sizeZ)
    {
        Material floorMat = EnsureMaterial("地板_米黄",
            new Color(239f / 255f, 228f / 255f, 176f / 255f), 0f, 0.15f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = name;
        floor.transform.localScale = new Vector3(sizeX, FloorThickness, sizeZ);

        Vector3 pos = Vector3.zero;
        GameObject anchor = Selection.activeGameObject;
        if (anchor != null && IsFloor(anchor))
        {
            Bounds ab = PlacementService.GetBounds(anchor);
            pos.x = ab.max.x + sizeX * 0.5f;
            pos.z = ab.center.z;
        }

        pos.x = Snap(pos.x);
        pos.z = Snap(pos.z);
        pos.y = FloorTopY - FloorThickness * 0.5f;
        floor.transform.position = pos;

        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        Undo.RegisterCreatedObjectUndo(floor, "生成地板");
        Selection.activeGameObject = floor;

        Debug.Log(string.Format("[SceneSetupTool] 生成 {0}（{1}×{2}），上表面 y={3}，位置 {4}",
            name, sizeX, sizeZ, FloorTopY, pos));
        return floor;
    }

    // ---------------- 墙壁 ----------------

    [MenuItem("奶茶店/场景搭建/生成墙壁（吸附地板边）", false, 4)]
    public static void CreateWall()
    {
        CreateWallAuto(WallLength);
    }

    [MenuItem("奶茶店/场景搭建/生成半长墙（吸附地板边）", false, 5)]
    public static void CreateWallAutoMenu()
    {
        CreateWallAuto(WallHalfLength);
    }

    /// <summary>自动选最近的地板与最近的一条边，生成墙</summary>
    public static GameObject CreateWallAuto(float length)
    {
        GameObject floor = FindFloorForWall();
        if (floor == null)
        {
            Debug.LogWarning("[SceneSetupTool] 场景里没有地板，先创建地板再生成墙");
            return null;
        }

        Vector3 pivot = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : PlacementService.GetBounds(floor).center + Vector3.forward * 5f;

        Vector3 outward;
        float along;
        PickEdge(PlacementService.GetBounds(floor), pivot, out outward, out along);
        return CreateWallOnFloor(floor, length, outward, along);
    }

    /// <summary>
    /// 在指定地板的指定边生成墙。墙的内侧面与地板边平齐，底部下沉 WallSink，
    /// 长度沿边吸附到 0.5m 网格并居中于 alongOffset。
    /// </summary>
    public static GameObject CreateWallOnFloor(GameObject floor, float length, Vector3 outward, float alongOffset = 0f)
    {
        Material wallMat = EnsureMaterial("墙壁_白",
            new Color(245f / 255f, 245f / 255f, 240f / 255f), 0f, 0.2f);

        Bounds fb = PlacementService.GetBounds(floor);
        outward.y = 0f;
        if (outward.sqrMagnitude < 1e-6f) outward = Vector3.forward;
        outward.Normalize();

        Vector3 alongDir = Vector3.Cross(Vector3.up, outward).normalized;
        float halfOut = Mathf.Abs(outward.x) * fb.extents.x + Mathf.Abs(outward.z) * fb.extents.z;
        float halfLen = Mathf.Abs(alongDir.x) * fb.extents.x + Mathf.Abs(alongDir.z) * fb.extents.z;

        length = Mathf.Clamp(length, Grid, halfLen * 2f);
        float maxT = Mathf.Max(0f, halfLen - length * 0.5f);
        float t = Mathf.Clamp(alongOffset, -maxT, maxT);
        t = Snap(t);

        Vector3 edgeCenter = fb.center + outward * halfOut;
        Vector3 pos = edgeCenter + alongDir * t + outward * (WallThickness * 0.5f);

        float bottom = fb.max.y - WallSink;
        pos.y = bottom + WallHeight * 0.5f;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = length >= WallLength - 0.01f ? "墙壁" : "半长墙（5×6）";
        wall.transform.localScale = new Vector3(length, WallHeight, WallThickness);
        wall.transform.position = pos;
        wall.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
        wall.GetComponent<Renderer>().sharedMaterial = wallMat;

        Undo.RegisterCreatedObjectUndo(wall, "生成墙壁");
        Selection.activeGameObject = wall;

        Debug.Log(string.Format("[SceneSetupTool] 生成 {0}，长 {1}，贴在地板边（内侧面 z={2:0.###}，底部 y={3:0.###}，下沉 {4}m）",
            wall.name, length, Vector3.Dot(edgeCenter, outward), bottom, WallSink));
        return wall;
    }

    // ---------------- 墙体高度 ----------------

    [MenuItem("奶茶店/场景搭建/墙体高度加倍", false, 6)]
    public static void DoubleWallHeight()
    {
        int count = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (!t.name.StartsWith("墙壁") && !t.name.StartsWith("半长墙")) continue;

                float bottom = t.position.y - t.lossyScale.y * 0.5f;

                Undo.RecordObject(t, "墙体高度加倍");
                Vector3 scale = t.localScale;
                scale.y *= 2f;
                t.localScale = scale;
                t.position = new Vector3(t.position.x, bottom + t.lossyScale.y * 0.5f, t.position.z);
                count++;
            }
        }

        Debug.Log(string.Format("[SceneSetupTool] 墙体高度已加倍：共处理 {0} 面墙（底边不变）", count));
    }

    // ---------------- 工具方法 ----------------

    public static float Snap(float v)
    {
        return Mathf.Round(v / Grid) * Grid;
    }

    public static bool IsFloor(GameObject go)
    {
        return go != null && (go.name.StartsWith("地板") || go.name.StartsWith("半地板"));
    }

    static GameObject FindFloorForWall()
    {
        if (Selection.activeGameObject != null && IsFloor(Selection.activeGameObject))
            return Selection.activeGameObject;

        Vector3 pivot = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;

        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
            {
                if (!IsFloor(t.gameObject)) continue;
                float d = Vector3.Distance(pivot, t.position);
                if (d < bestDist) { bestDist = d; best = t.gameObject; }
            }
        }
        return best;
    }

    /// <summary>在包围盒四条边里，选离 pivot 最近的一条；输出朝外方向与沿边偏移</summary>
    static void PickEdge(Bounds b, Vector3 p, out Vector3 outward, out float alongOffset)
    {
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        float best = float.MaxValue;
        outward = Vector3.forward;
        alongOffset = 0f;

        foreach (Vector3 dir in dirs)
        {
            Vector3 alongDir = Vector3.Cross(Vector3.up, dir).normalized;
            float halfOut = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
            float halfLen = Mathf.Abs(alongDir.x) * b.extents.x + Mathf.Abs(alongDir.z) * b.extents.z;

            Vector3 edgeCenter = b.center + dir * halfOut;
            float t = Vector3.Dot(p - edgeCenter, alongDir);
            t = Mathf.Clamp(t, -halfLen, halfLen);
            Vector3 nearest = edgeCenter + alongDir * t;
            float d = Vector3.Distance(p, nearest);

            if (d < best)
            {
                best = d;
                outward = dir;
                alongOffset = t;
            }
        }
    }

    /// <summary>创建（或更新）一个 Standard 材质，存到 Assets/材料 下</summary>
    static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder("Assets/材料"))
            AssetDatabase.CreateFolder("Assets", "材料");

        string path = string.Format("Assets/材料/{0}.mat", name);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}

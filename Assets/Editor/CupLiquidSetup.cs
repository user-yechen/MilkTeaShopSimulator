using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 液位子对象设置工具：给奶茶杯添加"液体"Cylinder 子对象，用于 Fill 时 Y 轴缩放模拟液位上升。
/// 入口：奶茶店 → 杯子 → 液位 → 给所有奶茶杯加液体 / 奶茶店 → 杯子 → 液位 → 给选中物体加液体
/// </summary>
public static class CupLiquidSetup
{
    const string LiquidName = "液体";
    const string LiquidMatPath = "Assets/材料/奶茶液.mat";
    const string MediumCupPath = "Assets/预制件/奶茶杯_中.prefab";
    const string LargeCupPath = "Assets/预制件/奶茶杯_大.prefab";

    [Header("液体参数")]
    [Tooltip("液体圆柱体半径（略小于杯内径）")]
    static float LiquidRadiusMedium = 0.04f;
    static float LiquidRadiusLarge = 0.05f;
    [Tooltip("液体圆柱体高度（杯子内部高度）")]
    static float LiquidHeightMedium = 0.08f;
    static float LiquidHeightLarge = 0.10f;

    // ===== 给选中物体加液体 =====

    [MenuItem("奶茶店/归档/杯子/液位/给选中物体加液体", false, 23)]
    static void AddLiquidToSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            Debug.LogWarning("[CupLiquidSetup] 请先选中一个 GameObject");
            return;
        }

        AddLiquidTo(go, LiquidRadiusMedium, LiquidHeightMedium);
    }

    // ===== 给所有奶茶杯 Prefab 加液体 =====

    [MenuItem("奶茶店/归档/杯子/液位/给所有奶茶杯加液体", false, 24)]
    static void AddLiquidToAllCupPrefabs()
    {
        bool anyModified = false;

        if (File.Exists(MediumCupPath))
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(MediumCupPath);
            if (AddLiquidTo(prefab, LiquidRadiusMedium, LiquidHeightMedium))
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, MediumCupPath);
                anyModified = true;
            }
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        else
            Debug.LogWarning("[CupLiquidSetup] 找不到 " + MediumCupPath);

        if (File.Exists(LargeCupPath))
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(LargeCupPath);
            if (AddLiquidTo(prefab, LiquidRadiusLarge, LiquidHeightLarge))
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, LargeCupPath);
                anyModified = true;
            }
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        else
            Debug.LogWarning("[CupLiquidSetup] 找不到 " + LargeCupPath);

        if (anyModified)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CupLiquidSetup] ✅ 已给所有奶茶杯 Prefab 添加液体子对象");
        }
        else
            Debug.Log("[CupLiquidSetup] 两个 Prefab 都已经有液体子对象了，跳过");
    }

    // ===== 核心逻辑 =====

    /// <summary>给指定 GameObject 添加"液体"Cylinder 子对象；已存在则跳过返回 false</summary>
    /// <returns>是否新增了液体子对象</returns>
    static bool AddLiquidTo(GameObject parent, float radius, float height)
    {
        Transform existing = parent.transform.Find(LiquidName);
        if (existing != null)
        {
            Debug.Log("[CupLiquidSetup] " + parent.name + " 已经有液体子对象，跳过");
            return false;
        }

        // 创建子对象
        GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquid.name = LiquidName;

        // 删掉自带的 Collider（液体不需要碰撞）
        Collider col = liquid.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        // 设置父级
        liquid.transform.SetParent(parent.transform, false);

        // Unity Cylinder 默认 mesh 高 2，pivot 在几何中心
        // scale.x = radius / 0.5 = radius * 2
        // scale.y = height / 2（把高 2 的圆柱缩成想要的高度）
        float scaleXZ = radius * 2f;
        float scaleY = height / 2f;

        // 设成完整 scale（Cup.Awake 会记住完整值，再缩到 0.01 开始）
        liquid.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
        // 底面贴杯底：Cylinder pivot 在中心，scale.y = scaleY 后半高为 scaleY
        // 底面在 localPosition.y - scaleY，要让它 = 0，所以 localPosition.y = scaleY
        liquid.transform.localPosition = new Vector3(0f, scaleY, 0f);

        // 赋值材质
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(LiquidMatPath);
        if (mat != null)
        {
            Renderer r = liquid.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }
        else
            Debug.LogWarning("[CupLiquidSetup] 找不到奶茶液材质：" + LiquidMatPath + "，液体将用默认灰色");

        // 关 CastShadows（液体不投影，省性能）
        Renderer renderer = liquid.GetComponent<Renderer>();
        if (renderer != null)
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Debug.Log("[CupLiquidSetup] ✅ 已添加液体到 " + parent.name
            + " (radius=" + radius + ", height=" + height + ")");
        return true;
    }
}

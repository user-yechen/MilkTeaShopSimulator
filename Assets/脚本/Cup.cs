using System.Collections;
using UnityEngine;

/// <summary>
/// 奶茶杯属性组件：杯型、奶茶类型、温度、液位。
/// 液体效果：运行时动态生成 Frustum（截头圆锥/圆台）Mesh，
///           底面放在 liquidLocalY，向上生长，顶面半径 = bottomRadius + slope × height。
/// 杯子材质**永不触碰**，保持原始贴图/透明效果。
/// </summary>
public class Cup : MonoBehaviour
{
    public enum CupType { Medium, Large }
    public enum Temperature { RoomTemp, LessIce, NoIce, NormalIce, Hot }

    [Header("杯子属性")]
    public CupType cupType;
    public string teaType = "奶茶";
    public Temperature temperature = Temperature.RoomTemp;

    [Header("是否装有奶茶")]
    public bool isContained;

    [Header("液位")]
    [Tooltip("当前填充比例 0~1，0=空杯，1=满杯")]
    [Range(0f, 1f)] public float fillLevel;

    [Header("默认液体颜色（Inspector 可调）")]
    [Tooltip("杯子初始化时液体材质的默认颜色；注液后会被 SetLiquidColor 覆盖")]
    public Color defaultLiquidColor = new Color(0.65f, 0.45f, 0.25f, 1f);

    [HideInInspector] public Vector3 baseScale = Vector3.one;
    [HideInInspector] public Quaternion baseRotation = Quaternion.identity;

    // === 运行时缓存 ===
    float cupMinY;              // 杯子模型空间底面 Y
    Transform liquidChild;      // "液体"子对象
    MeshFilter liquidMeshFilter;
    MeshRenderer liquidRenderer;
    bool liquidInitialized;

    // === 液体几何参数 ===
    float liquidBottomRadius;   // 液体下底半径（杯底半径内缩）
    float liquidSlope;          // 半径随高度变化率
    float liquidMaxHeight;      // 满杯时的液体高度
    float liquidLocalY;         // 液体物体相对杯子根节点的本地 y（直接就是 Transform 上的值）

    // === Frustum Mesh 生成常量 ===
    const int Segments = 32;    // 圆周分段数

    public bool IsHot => temperature == Temperature.Hot;

    /// <summary>把杯型枚举转中文：Medium→"中杯"，Large→"大杯"</summary>
    public static string CupTypeName(CupType type) => type == CupType.Large ? "大杯" : "中杯";

    void Awake()
    {
        InitBounds();
        FindLiquidChild();
        ApplyFillLevel();
    }

    /// <summary>
    /// 计算杯子模型空间底面 Y。
    /// 用网格本地包围盒的 8 个角点换算到杯子本地空间，避免世界 AABB 在杯子被旋转
    /// （手持/机器上 X 旋转 180°）时取到杯口，保证液面始终从杯底开始。
    /// </summary>
    void InitBounds()
    {
        float minY = float.MaxValue;
        foreach (var f in GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.gameObject.name == "液体") continue;
            Mesh m = f.sharedMesh;
            if (m == null) continue;

            Bounds b = m.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                float y = transform.InverseTransformPoint(f.transform.TransformPoint(corner)).y;
                if (y < minY) minY = y;
            }
        }
        if (minY < float.MaxValue) cupMinY = minY;
    }

    /// <summary>
    /// 液体几何参数。
    /// 圆台公式：顶面半径 = liquidBottomRadius + liquidSlope × 液体高度
    /// 数值由「手动微调后的液体 Transform」反算而来（2026-11-07）：
    ///   手动微调：liquid.localPosition.y、liquid.localScale
    ///   反算规则：y 缩放 sy → 高度 × sy、斜率 ÷ sy；三轴缩放 s → 底半径 × s、斜率不变。
    /// </summary>
    void SetLiquidBaseParams()
    {
        if (cupType == CupType.Large)
        {
            // 大奶茶杯
            // 手动微调：liquid.localPosition.y = -0.25、liquid.localScale.y = 1.4（X/Z 为 1）
            // -0.25 是相对杯子根节点的本地 y，直接照搬（不再用 cupMinY 换算）
            // → 高 = 0.5043 × 1.4 = 0.70602
            // → 斜率 = 0.119291 ÷ 1.4 = 0.085208（X/Z 未缩放，保持原顶部半径）
            liquidBottomRadius = 0.0950f;
            liquidSlope        = 0.085208f;
            liquidMaxHeight    = 0.70602f;
            liquidLocalY       = -0.25f;
        }
        else
        {
            // 中奶茶杯
            // 手动微调：liquid.localScale = (1.5, 1.5, 1.5)，位置不变（仍贴 cupMinY）
            // → 底半径 = 0.0481 × 1.5 = 0.07215
            // → 高 = 0.3081 × 1.5 = 0.46215
            // → 斜率不变（半径与高度同比例放大）
            liquidBottomRadius = 0.07215f;
            liquidSlope        = 0.229354f;
            liquidMaxHeight    = 0.46215f;
            liquidLocalY       = cupMinY;
        }
    }

    void FindLiquidChild()
    {
        if (liquidInitialized) return;
        liquidChild = transform.Find("液体");

        if (liquidChild == null)
        {
            var go = new GameObject("液体");
            liquidChild = go.transform;
            liquidChild.SetParent(transform, false);

            liquidMeshFilter = go.AddComponent<MeshFilter>();
            liquidRenderer = go.AddComponent<MeshRenderer>();

            // 赋 MilkTeaLiquid Shader
            Shader liqShader = Resources.Load<Shader>("Shaders/MilkTeaLiquid");
            Material liqMat;
            if (liqShader != null)
            {
                liqMat = new Material(liqShader);
                liqMat.SetColor("_Color", defaultLiquidColor);
            }
            else
            {
                liqMat = new Material(Shader.Find("Standard"));
                liqMat.color = defaultLiquidColor;
            }
            liquidRenderer.sharedMaterial = liqMat;
        }
        else
        {
            liquidMeshFilter = liquidChild.GetComponent<MeshFilter>();
            liquidRenderer = liquidChild.GetComponent<MeshRenderer>();
        }

        liquidChild.gameObject.SetActive(isContained);
        liquidInitialized = true;
    }

    public void Empty()
    {
        isContained = false; fillLevel = 0f; ApplyFillLevel();
        teaType = "奶茶"; temperature = Temperature.RoomTemp;
    }

    public void Fill(string contentName = "奶茶")
    {
        isContained = true; teaType = contentName;
        temperature = Temperature.RoomTemp; fillLevel = 1f; ApplyFillLevel();
    }

    public void SetTemperature(Temperature value) { temperature = value; }

    /// <summary>把杯内液体染成指定颜色（取材质实例，不影响其他杯子的共用材质）</summary>
    public void SetLiquidColor(Color color, bool forceOpaque = false)
    {
        FindLiquidChild();
        if (liquidRenderer == null) return;

        Material mat = liquidRenderer.material;
        if (mat == null) return;

        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        else mat.color = color;

        if (mat.HasProperty("_ForceOpaque"))
            mat.SetFloat("_ForceOpaque", forceOpaque ? 1f : 0f);
    }

    public void AnimateFill(float targetLevel, float duration)
    {
        StartCoroutine(FillRoutine(targetLevel, duration));
    }

    IEnumerator FillRoutine(float targetLevel, float duration)
    {
        FindLiquidChild();
        if (liquidChild == null) { Fill(); yield break; }

        float start = fillLevel;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fillLevel = Mathf.Lerp(start, targetLevel, elapsed / duration);
            ApplyFillLevel();
            yield return null;
        }
        fillLevel = targetLevel;
        ApplyFillLevel();
        isContained = true;
    }

    public void SetFillLevel(float level)
    {
        fillLevel = Mathf.Clamp01(level);
        ApplyFillLevel();
        isContained = fillLevel > 0.01f;
    }

    /// <summary>
    /// 核心：重建 Frustum（圆台）Mesh。
    /// 底面半径 = liquidBottomRadius（固定，贴合杯底）
    /// 顶面半径 = liquidBottomRadius + liquidSlope × height（贴合杯壁斜率）
    /// Mesh 底面在本地 y=0，液体物体放在 liquidLocalY → 液面高度 = liquidLocalY + height
    /// </summary>
    void ApplyFillLevel()
    {
        if (liquidMeshFilter == null) return;

        // 空杯隐藏
        if (fillLevel <= 0.01f)
        {
            liquidChild.gameObject.SetActive(false);
            return;
        }
        liquidChild.gameObject.SetActive(true);

        // cupType 往往是在 AddComponent 之后才赋值（CupInteract 取空杯时先挂组件再设类型，
        // 那时 Awake 里 cupType 还是默认的中杯），所以每次应用液位都按当前杯型重新取参数。
        SetLiquidBaseParams();

        float height = liquidMaxHeight * fillLevel;
        float topR   = liquidBottomRadius + liquidSlope * height;
        // 安全：顶面半径不能小于底面（杯子如果是倒锥就 clamp）
        if (topR < liquidBottomRadius) topR = liquidBottomRadius;

        Mesh mesh = BuildFrustumMesh(liquidBottomRadius, topR, height, 0f);
        liquidMeshFilter.mesh = mesh;

        // Mesh 底面在本地 y=0，把液体整体放到 liquidLocalY：液面 = liquidLocalY + height
        liquidChild.localPosition = new Vector3(0f, liquidLocalY, 0f);
        liquidChild.localScale = Vector3.one;
    }

    /// <summary>
    /// 生成 Frustum（截头圆锥/圆台）Mesh。
    /// 底面中心在 (0, baseY, 0)，顶面中心在 (0, baseY + height, 0)
    /// </summary>
    static Mesh BuildFrustumMesh(float bottomR, float topR, float height, float baseY)
    {
        var mesh = new Mesh { name = "LiquidFrustum" };

        int n = Segments;
        int vertCount = n * 2;           // 底圈 n 个 + 顶圈 n 个
        var verts  = new Vector3[vertCount];
        var uvs    = new Vector2[vertCount];
        var tris   = new int[n * 6];     // n 个侧面 quad × 2 triangle × 3 index

        float topY = baseY + height;
        for (int i = 0; i < n; i++)
        {
            float angle = i * Mathf.PI * 2f / n;
            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);

            // 底圈顶点 (0 ~ n-1)
            verts[i]     = new Vector3(ca * bottomR, baseY, sa * bottomR);
            uvs[i]       = new Vector2((float)i / n, 0f);

            // 顶圈顶点 (n ~ 2n-1)
            verts[i + n] = new Vector3(ca * topR,    topY,  sa * topR);
            uvs[i + n]   = new Vector2((float)i / n, 1f);
        }

        // 侧面三角形：每段 [i, i+1, i+n, i+n+1] 构成一个 quad
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int a = i, b = next, c = i + n, d = next + n;
            tris[i * 6 + 0] = a;
            tris[i * 6 + 1] = c;
            tris[i * 6 + 2] = b;
            tris[i * 6 + 3] = b;
            tris[i * 6 + 4] = c;
            tris[i * 6 + 5] = d;
        }

        // 合并底面和顶面（两个 triangle fan）
        // 底面中心顶点 + n 个底圈顶点 = n+1 顶点
        // 顶面中心顶点 + n 个顶圈顶点 = n+1 顶点
        // 总共 vertCount + 2
        var allVerts = new Vector3[vertCount + 2];
        var allUVs   = new Vector2[vertCount + 2];
        verts.CopyTo(allVerts, 0);
        uvs.CopyTo(allUVs, 0);
        allVerts[vertCount]     = new Vector3(0f, baseY, 0f);   // 底面中心
        allVerts[vertCount + 1] = new Vector3(0f, topY, 0f);    // 顶面中心
        allUVs[vertCount]       = new Vector2(0.5f, 0f);
        allUVs[vertCount + 1]   = new Vector2(0.5f, 1f);

        // 底面 triangle fan（绕序：法线朝下）
        int[] bottomTris = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            bottomTris[i * 3 + 0] = vertCount;      // 中心
            bottomTris[i * 3 + 1] = next;           // 外圈下一个
            bottomTris[i * 3 + 2] = i;              // 外圈当前
        }

        // 顶面 triangle fan（绕序：法线朝上）
        int[] topTris = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            topTris[i * 3 + 0] = vertCount + 1;     // 中心
            topTris[i * 3 + 1] = i + n;             // 外圈当前
            topTris[i * 3 + 2] = next + n;          // 外圈下一个
        }

        var allTris = new int[tris.Length + bottomTris.Length + topTris.Length];
        tris.CopyTo(allTris, 0);
        bottomTris.CopyTo(allTris, tris.Length);
        topTris.CopyTo(allTris, tris.Length + bottomTris.Length);

        mesh.vertices  = allVerts;
        mesh.uv        = allUVs;
        mesh.triangles = allTris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}

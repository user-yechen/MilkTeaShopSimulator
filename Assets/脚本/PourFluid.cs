using UnityEngine;

/// <summary>
/// 在奶茶机出料口位置生成一条简单的流体（粒子水柱），用于"注入奶茶"动画。
/// 播放 duration 秒后自动销毁自身。
/// 完全用 Unity 内置组件实现，不需要 Shader/外部资源。
/// </summary>
public class PourFluid : MonoBehaviour
{
    [Header("流体参数")]
    [Tooltip("流体颜色（奶茶默认浅棕色带半透明）")]
    public Color fluidColor = new Color(0.65f, 0.45f, 0.25f, 0.8f);
    [Tooltip("是否强制完全不透明（牛奶水流设 true，茶汤 false）")]
    public bool forceOpaque;
    [Tooltip("总播放时长（秒）")]
    public float duration = 1.2f;
    [Tooltip("水流长度（米）")]
    public float streamLength = 0.8f;
    [Tooltip("水流粗细（米）")]
    public float streamWidth = 0.06f;
    [Tooltip("粒子发射速率（个/秒）")]
    public float emissionRate = 240f;
    [Tooltip("单个粒子生命周期（秒），略长于流体下落时间")]
    public float particleLifetime = 0.6f;
    [Tooltip("下落速度（米/秒），正值 = 向下")]
    public float gravity = 2.5f;

    ParticleSystem ps;
    float startTime;
    static Material cachedFluidMaterial;  // 避免每次播放都 Resources.Load

    /// <summary>
    /// 工厂：在 nozzle 位置实例化一个流体 GameObject，返回 PourFluid 引用。
    /// color 可选：不传则用默认奶茶色；传了就按该颜色出液（奶茶机交互界面的流体格子用）。
    /// forceOpaque：true 时水流完全不透明（牛奶等），false 时按颜色 alpha 半透明（茶汤等）。
    /// </summary>
    public static PourFluid Spawn(Transform nozzle, Color? color = null, bool forceOpaque = false)
    {
        GameObject go = new GameObject("PourFluid");
        if (nozzle != null)
        {
            // 只跟随 nozzle 的位置，不继承它的旋转——让 shape 和 gravity 都稳定对准世界 -Y
            go.transform.position = nozzle.position;
            go.transform.rotation = Quaternion.identity;
        }
        else
        {
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
        }

        var fluid = go.AddComponent<PourFluid>();
        if (color.HasValue) fluid.fluidColor = color.Value;
        fluid.forceOpaque = forceOpaque;
        fluid.Build();
        return fluid;
    }

    void Build()
    {
        // --- ParticleSystem 主模块 ---
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;                 // 初始速度为 0，靠重力往下掉
        main.startSize = streamWidth;
        // 强制不透明时 alpha 强制=1（牛奶水流完全不透明，茶汤按颜色 alpha）
        Color particleColor = forceOpaque ? new Color(fluidColor.r, fluidColor.g, fluidColor.b, 1f) : fluidColor;
        main.startColor = particleColor;
        main.gravityModifier = gravity;       // 正值 × Physics.gravity(0,-9.81,0) = 世界 -Y，向下落
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;

        // --- Emission ---
        ParticleSystem.EmissionModule em = ps.emission;
        em.enabled = true;
        em.rateOverTime = emissionRate;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(emissionRate * duration)) });

        // --- Shape：Cone + angle=0 = 一条竖直水柱 ---
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;                    // angle=0 就是一个圆柱体通道
        shape.radius = streamWidth * 0.5f;
        // Cone 默认沿 shape local +Z 发射；绕 X 轴 -90° 后 local +Z 对准 local -Y（朝下）
        // 配合 gravityModifier=+2.5（世界 -Y），喷射方向和重力方向一致
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // --- 材质：加载 Resources/材料/奶茶液.mat（放在 Resources 下才会被打进 APK） ---
        ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material fluidMat = GetFluidMaterial();
            // 只在拿到材质时才覆盖，避免赋 null 让粒子变成洋红（未赋材质的报错色）
            if (fluidMat != null)
            {
                renderer.sharedMaterial = fluidMat;
                // 每场流用一份材质实例，按 fluidColor 染色（水流颜色 = 流体格子设置的颜色）
                Material tinted = renderer.material;
                if (tinted.HasProperty("_Color"))
                    tinted.SetColor("_Color", fluidColor);
            }
            renderer.sortingOrder = 50;
        }

        startTime = Time.time;
        ps.Play();
    }

    void Update()
    {
        if (Time.time - startTime >= duration)
        {
            // 停止发射，但让现有粒子自然落完（WaitForParticles 会等 Main.startLifetime）
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            StartCoroutine(AutoDestroyCoroutine());
            enabled = false;
        }
    }

    System.Collections.IEnumerator AutoDestroyCoroutine()
    {
        // 等剩余粒子自然消失再销毁
        if (ps != null) yield return new WaitWhile(() => ps.IsAlive(true));
        Destroy(gameObject);
    }

    /// <summary>加载奶茶液材质（运行时）；找不到返回 null（保留粒子系统默认材质）</summary>
    static Material GetFluidMaterial()
    {
        if (cachedFluidMaterial != null) return cachedFluidMaterial;

        cachedFluidMaterial = Resources.Load<Material>("材料/奶茶液");
        if (cachedFluidMaterial == null)
            Debug.LogWarning("[PourFluid] 未在 Resources 中找到 材料/奶茶液，将使用粒子系统默认材质");

        return cachedFluidMaterial;
    }
}

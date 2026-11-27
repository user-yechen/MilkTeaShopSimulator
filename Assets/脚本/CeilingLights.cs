using UnityEngine;

/// <summary>
/// 把「天花板」下的每一块顶板都作为一个点光源，整体提亮室内。
/// 运行时自动挂到天花板根节点上，无需手动挂载。
/// </summary>
public class CeilingLights : MonoBehaviour
{
    [Header("天花板灯光")]
    public Color lightColor         = new Color(1f, 0.96f, 0.88f);  // 暖白
    public float lightIntensity     = 1.2f;
    public float lightRange         = 12f;
    public float lightOffsetBelow   = 0.25f;   // 光源离顶板底面的距离（世界单位）
    public float emissionIntensity  = 0.4f;    // 顶板自发光亮度

    const string CeilingRootName = "天花板";

    bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectOfType<CeilingLights>() != null) return;

        GameObject root = FindCeilingRoot();
        if (root == null) return;

        root.AddComponent<CeilingLights>().Apply();
    }

    /// <summary>
    /// 场景里有多个同名「天花板」（根节点 + 每块顶板都叫这个名字），
    /// GameObject.Find 命中哪个是不确定的，所以这里取子 Renderer 最多的那个当根。
    /// </summary>
    public static GameObject FindCeilingRoot()
    {
        Transform best = null;
        int bestCount = 0;

        foreach (Transform tf in FindObjectsOfType<Transform>())
        {
            if (tf.name != CeilingRootName) continue;

            int count = tf.GetComponentsInChildren<Renderer>(true).Length;
            if (count > bestCount)
            {
                bestCount = count;
                best = tf;
            }
        }

        return best != null ? best.gameObject : null;
    }

    void Awake()
    {
        Apply();
    }

    void Apply()
    {
        if (applied) return;
        applied = true;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Light light = renderer.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                GameObject lightGo = new GameObject("天花板光源");
                lightGo.transform.SetParent(renderer.transform, false);

                light = lightGo.AddComponent<Light>();
                light.type      = LightType.Point;
                light.color     = lightColor;
                light.intensity = lightIntensity;
                light.range     = lightRange;
                light.shadows   = LightShadows.None;
            }

            // 顶板带非等比缩放（y 只有 0.1），直接设 localPosition 会把偏移量压扁，
            // 所以按世界坐标放在顶板底面下方
            light.transform.position = renderer.bounds.center
                - Vector3.up * (renderer.bounds.extents.y + lightOffsetBelow);

            // 顶板自身也发亮，看起来才像光源而不是一块暗板
            Material mat = renderer.material;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(lightColor.r, lightColor.g, lightColor.b) * emissionIntensity);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
        }
    }
}

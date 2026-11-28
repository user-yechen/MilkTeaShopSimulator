using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冰块库存可视化：挂在「前制作台lv2/Cube.001」下的空物体上，
/// 以自身位置为生成原点、按容器包围盒自动计算生成范围，按 fill(0~1) 生成对应数量的冰块。
/// 不修改容器本身，只在自己的子节点里生成 / 回收冰块。
/// </summary>
[DisallowMultipleComponent]
public class IceStockVisual : MonoBehaviour
{
    [Header("容器（留空则用父物体）")]
    public Transform container;

    [Header("冰块")]
    [Tooltip("留空则运行时用内置方块 + iceMaterial 生成")]
    public GameObject iceCubePrefab;
    public Material iceMaterial;
    [Tooltip("留空则自动从容器 Renderer 包围盒推算")]
    public bool autoFitToContainer = true;
    public Vector3 manualSize = new Vector3(2.66f, 0.868f, 3.27f);

    [Header("库存")]
    [Range(0f, 1f)] public float fill = 0.5f;
    public int maxCubes = 160;
    public float cubeSizeMin = 0.34f;
    public float cubeSizeMax = 0.46f;
    [Range(0f, 1f)] public float areaMargin = 0.92f;

    [Header("其它")]
    public int seed = 20261128;
    public bool castShadows = true;
    public bool rebuildOnEnable = true;

    readonly List<GameObject> _spawned = new List<GameObject>();

    void OnEnable()
    {
        if (rebuildOnEnable) Rebuild();
    }

    /// <summary>0=空，1=满。供库存系统调用。</summary>
    public void SetFill(float value01)
    {
        fill = Mathf.Clamp01(value01);
        Rebuild();
    }

    /// <summary>三档：0=低，1=中，2=高。</summary>
    public void SetLevel(int level)
    {
        switch (Mathf.Clamp(level, 0, 2))
        {
            case 0: SetFill(0.22f); break;
            case 1: SetFill(0.55f); break;
            default: SetFill(0.90f); break;
        }
    }

    [ContextMenu("重新生成")]
    public void Rebuild()
    {
        Clear();

        if (!TryGetArea(out Bounds area)) return;
        if (fill <= 0.001f) return;

        var random = new System.Random(seed);
        float fillHeight = area.size.y * fill;
        int count = Mathf.Clamp(
            Mathf.RoundToInt(maxCubes * Mathf.Pow(fill, 1.25f)), 1, maxCubes);

        float halfX = area.size.x * 0.5f * areaMargin;
        float halfZ = area.size.z * 0.5f * areaMargin;

        for (int i = 0; i < count; i++)
        {
            float s = Mathf.Lerp(cubeSizeMin, cubeSizeMax, (float)random.NextDouble());
            // 越靠上越向中心收，形成堆
            float y = area.min.y + 0.02f + fillHeight * Mathf.Pow((float)random.NextDouble(), 0.75f);
            float shrink = Mathf.Clamp01(1f - 0.45f * ((y - area.min.y) / Mathf.Max(0.001f, fillHeight)));
            float limX = Mathf.Max(0.02f, halfX * shrink - s * 0.5f);
            float limZ = Mathf.Max(0.02f, halfZ * shrink - s * 0.5f);

            Vector3 pos = new Vector3(
                area.center.x + ((float)random.NextDouble() * 2f - 1f) * limX,
                y,
                area.center.z + ((float)random.NextDouble() * 2f - 1f) * limZ);

            var go = CreateCube(s);
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(
                ((float)random.NextDouble() * 2f - 1f) * 12f,
                (float)random.NextDouble() * 360f,
                ((float)random.NextDouble() * 2f - 1f) * 12f);
            _spawned.Add(go);
        }
    }

    [ContextMenu("清空")]
    public void Clear()
    {
        for (int i = _spawned.Count - 1; i >= 0; i--)
        {
            if (_spawned[i] == null) continue;
            if (Application.isPlaying) Destroy(_spawned[i]);
            else DestroyImmediate(_spawned[i]);
        }
        _spawned.Clear();
    }

    GameObject CreateCube(float size)
    {
        GameObject go;
        if (iceCubePrefab != null)
        {
            go = Instantiate(iceCubePrefab);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = iceMaterial != null ? iceMaterial : DefaultIceMaterial();
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        go.name = "ice";
        go.transform.localScale = Vector3.one * size;
        return go;
    }

    static Material _fallback;
    static Material DefaultIceMaterial()
    {
        if (_fallback != null) return _fallback;
        var shader = Shader.Find("Standard");
        _fallback = new Material(shader) { name = "IceRuntime" };
        _fallback.color = new Color(0.55f, 0.76f, 0.92f, 1f);
        _fallback.SetFloat("_Glossiness", 0.55f);
        return _fallback;
    }

    bool TryGetArea(out Bounds area)
    {
        area = default;
        var c = container != null ? container : transform.parent;
        if (c == null) return false;

        var renderers = c.GetComponentsInChildren<Renderer>(true);
        bool has = false;
        foreach (var r in renderers)
        {
            if (r.transform.IsChildOf(transform)) continue; // 忽略自己生成的冰块
            if (!has) { area = r.bounds; has = true; }
            else area.Encapsulate(r.bounds);
        }

        if (!has)
        {
            if (!autoFitToContainer)
            {
                area = new Bounds(transform.position, manualSize);
                return true;
            }
            return false;
        }

        if (!autoFitToContainer) area.size = manualSize;
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (!isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Rebuild();
        };
    }
#endif
}

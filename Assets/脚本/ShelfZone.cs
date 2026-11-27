using UnityEngine;

/// <summary>
/// 置物区（货架的存放子对象）识别标识：
/// identifier = 这个区放的是什么（如「牛奶」）；玩家靠近时弹出「获取{identifier}」按钮。
/// itemPrefab = 点击获取时塞到玩家手里的预制件；留空则克隆本区第一个子物体。
/// 通常和 ShelfLayout 挂在同一个物体上（ShelfLayout 管排列，本组件管识别/取货）。
/// </summary>
public class ShelfZone : MonoBehaviour
{
    [Tooltip("识别标识：这区放的是什么（如 牛奶）。留空用物体名")]
    public string identifier;
    [Tooltip("获取时给玩家的预制件；留空克隆本区第一个子物体")]
    public GameObject itemPrefab;

    Renderer[] cachedRenderers;

    /// <summary>展示用标识（空则回退到物体名）</summary>
    public string Identifier => string.IsNullOrEmpty(identifier) ? name : identifier;

    /// <summary>生成一份物品（优先 itemPrefab，否则克隆第一个子物体）；没有可给的返回 null</summary>
    public GameObject SpawnItem()
    {
        GameObject source = itemPrefab;
        if (source == null && transform.childCount > 0)
            source = transform.GetChild(0).gameObject;
        if (source == null)
        {
            Debug.LogWarning($"[ShelfZone] {name} 既没有 itemPrefab 也没有子物体，无法提供「{Identifier}」");
            return null;
        }
        GameObject item = Instantiate(source);
        item.name = source.name;
        item.SetActive(true);
        return item;
    }

    /// <summary>玩家是否靠近本置物区（包围盒最近点距离，与 TeaMachine.IsNear 同一算法）</summary>
    public bool IsNear(Vector3 playerPos, float distance)
    {
        if (cachedRenderers == null) cachedRenderers = GetComponentsInChildren<Renderer>();
        if (cachedRenderers.Length == 0)
            return Vector3.Distance(playerPos, transform.position) <= distance;

        Bounds bounds = cachedRenderers[0].bounds;
        for (int i = 1; i < cachedRenderers.Length; i++) bounds.Encapsulate(cachedRenderers[i].bounds);

        float dx = Mathf.Max(0f, Mathf.Abs(playerPos.x - bounds.center.x) - bounds.extents.x);
        float dy = Mathf.Max(0f, Mathf.Abs(playerPos.y - bounds.center.y) - bounds.extents.y);
        float dz = Mathf.Max(0f, Mathf.Abs(playerPos.z - bounds.center.z) - bounds.extents.z);
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz) <= distance;
    }
}

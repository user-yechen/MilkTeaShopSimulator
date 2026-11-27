using UnityEngine;

/// <summary>
/// 可摆放物：杯子、成品、货架物品等。记录占地/高度/底面偏移，供 PlacementService 自动对齐。
/// 数值用 ModelMeasurer 测量后填，不要手猜。
/// </summary>
public class Mountable : MonoBehaviour
{
    [Tooltip("类型：cup / item / machine")]
    public string mountType = "cup";

    [Tooltip("XZ 占地（米）")]
    public Vector3 footprint = new Vector3(0.1f, 0.1f, 0.1f);

    [Tooltip("高度（米）")]
    public float height = 0.2f;

    [Tooltip("Pivot 到底面的距离；Pivot 在底面中心时填 0")]
    public float bottomOffset = 0f;
}

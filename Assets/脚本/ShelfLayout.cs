using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 货架格子布局（3D 版 LayoutGroup）：挂在货架的「存放物品」子对象上。
/// 拖入子物体后自动按各物品的**实际包围盒尺寸**在 XZ 平面网格排列：
///   · X 轴一行行排，行宽超 maxRowWidth（或数量达 itemsPerRow）就沿 Z 轴换行
///   · 每个物品底面对齐 y=0（底面中心摆放，悬空/下陷都不会有）
///   · 尺寸用 ModelMeasurer 量（本地空间、不吃父链缩放；经验 16）
/// 编辑器拖入即时生效（ExecuteAlways）；运行时增删物品（如商店购买上架）也会自动重排。
/// 物品根节点旋转会被保留，但测量不含根旋转——需要朝向的物品请预先烘好旋转。
/// </summary>
[ExecuteAlways]
public class ShelfLayout : MonoBehaviour
{
    [Header("排列参数（米）")]
    [Tooltip("同一行内物品之间的 X 向间距")]
    public float spacingX = 0.1f;
    [Tooltip("行与行之间的 Z 向间距")]
    public float spacingZ = 0.1f;
    [Tooltip("一行最大宽度（米），超过就换行；0 = 不限宽度（只靠 itemsPerRow 换行）")]
    public float maxRowWidth = 1.5f;
    [Tooltip("一行最多几个物品；0 = 不限数量（只靠 maxRowWidth 换行）")]
    public int itemsPerRow = 0;
    [Tooltip("换行方向：-1 沿 -Z（由后往前），+1 沿 +Z")]
    public int rowDirectionZ = -1;

    void OnTransformChildrenChanged() => Relayout();
    void OnEnable() => Relayout();

    /// <summary>手动触发重排（代码里批量放完物品后调一次即可）</summary>
    public void Relayout()
    {
        // 先量出每个子物体的尺寸，再统一摆放
        List<Transform> items = new List<Transform>();
        List<Vector3> sizes = new List<Vector3>();
        List<Vector3> centers = new List<Vector3>();
        foreach (Transform child in transform)
        {
            items.Add(child);
            if (ModelMeasurer.Measure(child.gameObject, out ModelMeasurer.ModelSizeInfo info))
            {
                sizes.Add(info.localSize);
                centers.Add(info.localCenter);
            }
            else
            {
                sizes.Add(Vector3.zero);   // 没网格的占 0 宽，只留间距
                centers.Add(Vector3.zero);
            }
        }

        float cursorX = 0f;    // 当前行已用到的 X 位置
        float cursorZ = 0f;    // 当前行的 Z 位置
        float rowDepth = 0f;   // 当前行最深物品（换行时累加用）
        int inRow = 0;         // 当前行已有几个

        for (int i = 0; i < items.Count; i++)
        {
            Vector3 size = sizes[i];
            bool needNewRow = inRow > 0 &&
                ((maxRowWidth > 0f && cursorX + size.x > maxRowWidth) ||
                 (itemsPerRow > 0 && inRow >= itemsPerRow));
            if (needNewRow)
            {
                cursorZ += (rowDepth + spacingZ) * rowDirectionZ;
                cursorX = 0f;
                rowDepth = 0f;
                inRow = 0;
            }

            // 目标：包围盒 min 角落在 (cursorX, 0, cursorZ) → 反推根节点 localPosition
            Vector3 center = centers[i];
            items[i].localPosition = new Vector3(
                cursorX - center.x + size.x * 0.5f,
                -(center.y - size.y * 0.5f),
                cursorZ - center.z + size.z * 0.5f);

            cursorX += size.x + spacingX;
            rowDepth = Mathf.Max(rowDepth, size.z);
            inRow++;
        }
    }
}

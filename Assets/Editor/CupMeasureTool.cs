using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 奶茶杯尺寸测量工具：选中杯子后输出
///   上底直径 / 下底直径 / 杯高 / 杯壁斜率
///   以及建议的液体圆台参数
/// </summary>
public class CupMeasureTool
{
    [MenuItem("奶茶店/杯子/测量选中杯子尺寸", false, 20)]
    static void Measure()
    {
        var selections = Selection.gameObjects;
        if (selections.Length == 0)
        {
            Debug.LogWarning("[CupMeasure] 请先选中要测量的杯子 GameObject");
            return;
        }

        foreach (var go in selections)
        {
            MeasureOne(go);
        }
    }

    static void MeasureOne(GameObject go)
    {
        // 1. 合并所有子 MeshFilter 的顶点到"杯子模型空间"
        var filters = go.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0)
        {
            Debug.LogWarning($"[CupMeasure] {go.name} 没有 MeshFilter");
            return;
        }

        var allVerts = new List<Vector3>();
        foreach (var f in filters)
        {
            if (f.sharedMesh == null) continue;
            var mesh = f.sharedMesh;
            var matrix = f.transform.localToWorldMatrix * go.transform.worldToLocalMatrix;
            foreach (var v in mesh.vertices)
                allVerts.Add(matrix.MultiplyPoint3x4(v));
        }

        if (allVerts.Count == 0)
        {
            Debug.LogWarning($"[CupMeasure] {go.name} 没有顶点");
            return;
        }

        // 2. 找 Y 极值（杯子模型空间）
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var v in allVerts)
        {
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }
        float totalH = maxY - minY;

        // 3. 在底部 10% 和顶部 10% 区域分别算半径
        float lowY  = minY + totalH * 0.10f;  // 下 10% 上限
        float highY = maxY - totalH * 0.10f;  // 上 10% 下限

        Vector3 bottomCenter = Vector3.zero, topCenter = Vector3.zero;
        int bCount = 0, tCount = 0;
        foreach (var v in allVerts)
        {
            if (v.y <= lowY)  { bottomCenter += v; bCount++; }
            if (v.y >= highY) { topCenter    += v; tCount++; }
        }
        if (bCount > 0) bottomCenter /= bCount;
        if (tCount > 0) topCenter    /= tCount;

        float bottomR = 0f, topR = 0f;
        foreach (var v in allVerts)
        {
            if (v.y <= lowY)
                bottomR += Vector2.Distance(new Vector2(v.x, v.z), new Vector2(bottomCenter.x, bottomCenter.z));
            if (v.y >= highY)
                topR += Vector2.Distance(new Vector2(v.x, v.z), new Vector2(topCenter.x, topCenter.z));
        }
        if (bCount > 0) bottomR /= bCount;
        if (tCount > 0) topR    /= tCount;

        // 4. 杯壁斜率 = (上底半径 - 下底半径) / 杯高
        //    含义：每上升 1 单位 Y，半径增加 slope
        float slope = totalH > 0.001f ? (topR - bottomR) / totalH : 0f;

        // 5. 输出 + 建议液体参数
        //    液体：下底半径 × 0.5，高度 × 0.8，斜率沿用杯壁
        float liqBottomR = bottomR * 0.5f;
        float liqHeight  = totalH * 0.8f;
        float liqTopR    = liqBottomR + slope * liqHeight;  // 液体液面处的半径（贴合杯壁）

        Debug.Log($"📏 === {go.name} ===");
        Debug.Log($"  杯高     = {totalH:F4}   (Y: {minY:F4} ~ {maxY:F4})");
        Debug.Log($"  下底直径 = {bottomR*2f:F4}   下底半径 r_bot = {bottomR:F4}");
        Debug.Log($"  上底直径 = {topR*2f:F4}   上底半径 r_top = {topR:F4}");
        Debug.Log($"  杯壁斜率 slope = (r_top - r_bot) / H = {slope:F6}");
        Debug.Log($"  ── 建议液体圆台（贴合杯壁）──");
        Debug.Log($"  下底半径 = {liqBottomR:F4}  (r_bot × 0.5)");
        Debug.Log($"  液体高度 = {liqHeight:F4}   (杯高 × 0.8)");
        Debug.Log($"  液面半径 = {liqTopR:F4}    (r_bot + slope × h)");
        Debug.Log($"  cupMinY  = {minY:F4}");
        Debug.Log($"  复制到 Cup.SetLiquidBaseParams():");
        Debug.Log($"    liquidBottomRadius = {liqBottomR:F4}f;");
        Debug.Log($"    liquidSlope        = {slope:F6}f;");
        Debug.Log($"    liquidMaxHeight    = {liqHeight:F4}f;");
    }
}

using UnityEngine;
using UnityEditor;

namespace MilkTea.EditorTools
{
    /// <summary>
    /// 把冰块堆模型对齐到容器（如 前制作台lv2/Cube001）内部：
    /// 1) 父级设为容器
    /// 2) 等比缩放到容器内宽（留 2% 余量）
    /// 3) 底面贴容器内底，XZ 居中
    /// 用法：同时选中「容器」和「冰块模型」两个物体（容器在前），执行菜单。
    /// </summary>
    public static class IcePileAlignTool
    {
        const float MarginXZ = 0.98f;   // 四周留 1%
        const float LiftY = 0.002f;     // 离底 2mm，防 Z-fighting

        [MenuItem("奶茶店/摆放/冰块堆对齐容器", false, 30)]
        public static void AlignSelection()
        {
            var sel = Selection.gameObjects;
            if (sel.Length != 2)
            {
                EditorUtility.DisplayDialog("冰块堆对齐",
                    "请同时选中两个物体：\n1. 容器（如 Cube001）\n2. 冰块堆模型\n\n顺序：容器在前。", "好");
                return;
            }

            var container = sel[0];
            var ice = sel[1];

            if (!TryGetBounds(container, out var cb))
            {
                EditorUtility.DisplayDialog("冰块堆对齐", "容器没有 Renderer/Collider，取不到包围盒。", "好");
                return;
            }
            if (!TryGetBounds(ice, out var ib))
            {
                EditorUtility.DisplayDialog("冰块堆对齐", "冰块模型没有 Renderer，取不到包围盒。", "好");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(ice, "Align Ice Pile");

            // 1) 父级
            ice.transform.SetParent(container.transform, true);

            // 2) 等比缩放：按容器内宽与冰块宽度取小值
            float sx = cb.size.x / Mathf.Max(0.0001f, ib.size.x);
            float sz = cb.size.z / Mathf.Max(0.0001f, ib.size.z);
            float s = Mathf.Min(sx, sz) * MarginXZ;
            ice.transform.localScale *= s;

            // 3) 重新取包围盒，底面贴底、XZ 居中
            if (!TryGetBounds(ice, out ib))
            {
                EditorUtility.DisplayDialog("冰块堆对齐", "缩放后取不到冰块包围盒。", "好");
                return;
            }

            Vector3 delta = new Vector3(
                cb.center.x - ib.center.x,
                cb.min.y + LiftY - ib.min.y,
                cb.center.z - ib.center.z);
            ice.transform.position += delta;

            Selection.activeGameObject = ice;
            EditorGUIUtility.PingObject(ice);
            Debug.Log($"[冰块堆] 已对齐到 {container.name}：缩放 x{s:F3}，冰块尺寸 {ib.size}，容器尺寸 {cb.size}");
        }

        [MenuItem("奶茶店/摆放/打印包围盒尺寸", false, 31)]
        public static void PrintBounds()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (TryGetBounds(go, out var b))
                    Debug.Log($"[包围盒] {go.name}: center={b.center} size={b.size} min={b.min}");
                else
                    Debug.LogWarning($"[包围盒] {go.name}: 没有 Renderer/Collider");
            }
        }

        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!has)
            {
                var colliders = go.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders)
                {
                    if (!has) { bounds = c.bounds; has = true; }
                    else bounds.Encapsulate(c.bounds);
                }
            }

            return has;
        }
    }
}

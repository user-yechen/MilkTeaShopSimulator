using UnityEngine;
using UnityEditor;

namespace MilkTea.EditorTools
{
    /// <summary>
    /// 在选中的冰槽容器（如前制作台lv2/Cube.001）下创建一个空物体「冰块生成点」，
    /// 位置放在容器内底中心。只负责定位置，不挂任何脚本、不生成冰块。
    /// </summary>
    public static class IceSpawnPointTool
    {
        [MenuItem("奶茶店/摆放/创建冰块生成点", false, 32)]
        public static void CreatePoint()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("冰块生成点", "先在 Hierarchy 里选中冰槽容器（如 Cube.001）。", "好");
                return;
            }

            if (!TryGetBounds(sel, out var b))
            {
                EditorUtility.DisplayDialog("冰块生成点", "选中的物体没有 Renderer/Collider，取不到包围盒。", "好");
                return;
            }

            var go = new GameObject("冰块生成点");
            Undo.RegisterCreatedObjectUndo(go, "Create Ice Spawn Point");
            go.transform.SetParent(sel.transform, false);
            go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Debug.Log($"[冰块] 生成点已创建于 {sel.name} 内底中心，容器尺寸 {b.size}");
        }

        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!has)
            {
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    if (!has) { bounds = c.bounds; has = true; }
                    else bounds.Encapsulate(c.bounds);
                }
            }
            return has;
        }
    }
}

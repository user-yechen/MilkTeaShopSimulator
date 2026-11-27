using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 菜单「奶茶店/交互界面/给选中容器加垂直滚动视图」：
/// 把选中的 UI 容器（如「商店容器」）改造成标准垂直滚动视图：
///   容器（加 ScrollRect）
///   ├─ Viewport（RectMask2D 裁剪，右侧留 20px 给滚动条）
///   │   └─ Content（顶对齐竖排：VerticalLayoutGroup + ContentSizeFitter，高度随内容自动撑开）
///   └─ Scrollbar Vertical（右缘竖条，BottomToTop）
/// 容器里原有的直接子物体会被自动移进 Content（保持顺序），实现"套一层滚动视图"。
/// 已带 ScrollRect 的容器直接跳过；全程支持 Ctrl+Z 撤销。
/// </summary>
public static class ScrollViewSetupTool
{
    const float ScrollbarWidth = 16f;
    const float Padding = 4f;

    [MenuItem("奶茶店/交互界面/给选中容器加垂直滚动视图", false, 81)]
    static void Setup()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null) return;

        if (root.GetComponent<ScrollRect>() != null)
        {
            Debug.LogWarning($"[ScrollViewSetupTool] {root.name} 已有 ScrollRect，跳过");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "加垂直滚动视图");

        // ① 记下容器现有的直接子物体（之后要移进 Content）
        int childCount = root.transform.childCount;
        Transform[] existing = new Transform[childCount];
        for (int i = 0; i < childCount; i++) existing[i] = root.transform.GetChild(i);

        // ② Viewport：全铺，右侧让出滚动条宽度，RectMask2D 裁剪（不需要 Image，比 Mask 便宜）
        GameObject viewport = NewUI("Viewport", root.transform);
        RectTransform viewportRT = Stretch(viewport, Padding, Padding, -(ScrollbarWidth + Padding), -Padding);
        viewport.AddComponent<RectMask2D>();

        // ③ Content：顶对齐横铺（pivot 顶），竖排布局 + 高度自适应内容
        GameObject content = NewUI("Content", viewportRT);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
        layout.spacing = Padding;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ④ 把容器原有子物体移进 Content（保持原顺序）
        foreach (Transform child in existing)
            child.SetParent(contentRT, true);

        // ⑤ 竖向滚动条：右缘 16px 竖条 + Handle
        GameObject scrollbarGo = NewUI("Scrollbar Vertical", root.transform);
        Stretch(scrollbarGo, -(ScrollbarWidth), Padding, 0f, -Padding, rightAnchored: true);
        Image scrollbarBg = scrollbarGo.AddComponent<Image>();
        scrollbarBg.color = new Color(0f, 0f, 0f, 0.25f);

        GameObject handle = NewUI("Handle", scrollbarGo.transform);
        Stretch(handle, 0f, 0f, 0f, 0f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(1f, 1f, 1f, 0.6f);

        Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // ⑥ 容器本体挂 ScrollRect 并接线
        ScrollRect scrollRect = root.AddComponent<ScrollRect>();
        scrollRect.content = contentRT;
        scrollRect.viewport = viewportRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        EditorUtility.SetDirty(root);
        Debug.Log($"[ScrollViewSetupTool] {root.name} 已改造成垂直滚动视图：" +
                  $"Viewport + Content(VerticalLayout+ContentSizeFitter) + 右侧滚动条；原有 {existing.Length} 个子物体已移入 Content。" +
                  $"往 Content 里加物品即可自动撑高滚动。记得 Ctrl+S 保存场景。");
    }

    [MenuItem("奶茶店/交互界面/给选中容器加垂直滚动视图", true)]
    static bool Validate()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && go.GetComponent<RectTransform>() != null;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "加垂直滚动视图");
        return go;
    }

    /// <summary>四角拉伸铺平；rightAnchored=true 时锚点贴右缘（offsetMin.x 即宽度取负）</summary>
    static RectTransform Stretch(GameObject go, float left, float bottom, float right, float top, bool rightAnchored = false)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rightAnchored ? new Vector2(1f, 0f) : Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
        return rt;
    }
}

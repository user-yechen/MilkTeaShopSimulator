using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在窗口标题栏的「关闭按钮」上：单击后关掉整个窗口（SetActive(false)）。
/// 窗口根物体默认向上自动查找（名字含「界面」，到 Canvas 为止），也可在 Inspector 里直接拖入。
/// </summary>
[RequireComponent(typeof(Button))]
public class PanelCloseButton : MonoBehaviour
{
    [Tooltip("要关闭的窗口根物体；留空则自动向上查找名字含「界面」的父物体")]
    public GameObject windowRoot;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Close);
    }

    public void Close()
    {
        GameObject target = windowRoot != null ? windowRoot : FindWindowRoot();
        if (target == null && transform.parent != null) target = transform.parent.gameObject;
        if (target == null) target = gameObject;

        target.SetActive(false);
    }

    /// <summary>向上找最外层名字含「界面」的祖先（碰到 Canvas 就停；「奶茶机交互界面」「商店界面」都能命中）</summary>
    GameObject FindWindowRoot()
    {
        GameObject found = null;
        Transform t = transform;
        while (t != null)
        {
            if (t.name.Contains("界面")) found = t.gameObject;
            if (t.GetComponent<Canvas>() != null) break;
            t = t.parent;
        }
        return found;
    }
}

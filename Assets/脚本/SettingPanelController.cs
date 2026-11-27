using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置界面控制器：
/// - 自动查找子物体中的「关闭按钮」和「退出游戏」按钮
/// - 关闭按钮 → 隐藏整个设置界面 (SetActive(false))
/// - 退出游戏按钮 → Application.Quit()
/// 挂在「设置界面」预制件的根物体上。
/// </summary>
public class SettingPanelController : MonoBehaviour
{
    [Header("按钮名字匹配（子物体名包含即可）")]
    [Tooltip("关闭按钮名字关键字，找到后自动绑定关闭界面")]
    public string closeBtnKeyword = "关闭";
    [Tooltip("退出游戏按钮名字关键字，找到后自动绑定退出游戏")]
    public string quitBtnKeyword = "退出";

    [Header("手动引用（留空则自动查找）")]
    public Button closeButton;
    public Button quitButton;

    void Awake()
    {
        if (closeButton == null)
            closeButton = FindButtonByKeyword(closeBtnKeyword);
        if (quitButton == null)
            quitButton = FindButtonByKeyword(quitBtnKeyword);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
        else
            Debug.LogWarning("[SettingPanelController] 没找到关闭按钮（名字含「" + closeBtnKeyword + "」）");

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
        else
            Debug.LogWarning("[SettingPanelController] 没找到退出游戏按钮（名字含「" + quitBtnKeyword + "」）");
    }

    /// <summary>关闭整个设置界面（SetActive(false)）</summary>
    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>退出游戏（编辑器里用 EditorApplication.isPlaying = false 方便调试）</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>在所有子物体（含未激活）中找名字含关键字的 Button</summary>
    Button FindButtonByKeyword(string keyword)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.Contains(keyword)) continue;
            Button btn = t.GetComponent<Button>();
            if (btn != null) return btn;
        }
        return null;
    }
}

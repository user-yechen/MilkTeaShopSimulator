using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景里已有的 Text 统一换成中文动态字体（WebGL/小游戏必需）。
/// 商店、设置等面板是运行时才实例化的，所以持续低频扫描，新面板打开后也能补上。
/// </summary>
public class ChineseFontFixer : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var go = new GameObject("UIFontFixer");
        DontDestroyOnLoad(go);
        go.AddComponent<ChineseFontFixer>();
    }

    void Start()
    {
        StartCoroutine(ApplyLoop());
    }

    IEnumerator ApplyLoop()
    {
        Font font = UIFont.Get();
        Debug.Log("[UIFont] 使用字体：" + font.name);

        while (true)
        {
            UIFont.ApplyAll();
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景里已有的 Text 统一换成中文动态字体（WebGL/小游戏必需）。
/// 运行时新建/实例化的 Text（商店、提示面板等）在前 10 秒内也会被反复补上。
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

        float until = Time.unscaledTime + 10f;
        while (Time.unscaledTime < until)
        {
            Apply(font);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Debug.Log("[UIFont] 已应用到所有 Text：" + font.name);
    }

    static void Apply(Font font)
    {
        foreach (Text text in FindObjectsOfType<Text>(true))
        {
            if (text.font != font) text.font = font;
        }
    }
}

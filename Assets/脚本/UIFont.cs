using UnityEngine;

/// <summary>
/// 运行时 UI 字体：优先用 Resources/Fonts/NotoSansSC（中文），找不到才退回内置 LegacyRuntime。
/// WebGL / 小游戏没有系统字体，必须自带中文字体，否则所有中文都会变成空白。
/// </summary>
public static class UIFont
{
    static Font cached;

    public static Font Get()
    {
        if (cached != null) return cached;

        cached = Resources.Load<Font>("Fonts/NotoSansSC");
        if (cached == null)
        {
            Debug.LogWarning("[UIFont] 找不到 Resources/Fonts/NotoSansSC，中文可能显示不出来");
            cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return cached;
    }
}

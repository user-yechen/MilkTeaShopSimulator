using UnityEditor;
using UnityEngine;

/// <summary>
/// 移动端屏幕方向设置工具。
/// 顶部菜单：奶茶店 → 工具 → 设为横屏（Android）。
/// 一次性把默认方向改为“横屏向左”，并禁止竖屏方向，
/// 之后打包到 Android 时游戏会固定以横屏运行。
/// </summary>
public static class MobileOrientationTool
{
    [MenuItem("奶茶店/工具/设为横屏（Android）", false, 101)]
    public static void SetLandscape()
    {
        // 默认方向：横屏向左（与 Player Settings → 分辨率和显示 → 默认方向 对应）
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // 禁止竖屏；保留左右横向，便于自动旋转时只在这两种之间切换
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        Debug.Log("[MobileOrientationTool] 已设置：默认方向 = 横屏向左，竖屏方向已禁用。");
    }
}

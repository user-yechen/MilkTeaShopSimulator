using System;
using UnityEngine;

/// <summary>
/// 开发期假广告：不弹任何东西，直接回调成功；接入真实 SDK 后换成对应实现即可。
/// </summary>
public class MockAdService : IAdService
{
    public bool IsRewardedReady => true;

    public void ShowRewarded(string placement, Action<bool> onResult)
    {
        Debug.Log($"[Ads] (mock) 激励视频「{placement}」→ 视为看完");
        onResult?.Invoke(true);
    }

    public void ShowInterstitial(string placement)
    {
        Debug.Log($"[Ads] (mock) 插屏「{placement}」");
    }
}

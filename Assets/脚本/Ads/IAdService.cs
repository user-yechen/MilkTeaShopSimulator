using System;

/// <summary>
/// 广告服务抽象：接入哪个平台就实现这个接口，游戏逻辑不依赖具体 SDK。
/// 开发期默认用 MockAdService（直接回调成功）。
/// </summary>
public interface IAdService
{
    bool IsRewardedReady { get; }

    /// <summary>播放激励视频；onResult(true) 表示看完可发奖</summary>
    void ShowRewarded(string placement, Action<bool> onResult);

    /// <summary>播放插屏（可空实现）</summary>
    void ShowInterstitial(string placement);
}

/// <summary>广告服务入口：换实现只改这里 / 启动时 Set。</summary>
public static class AdService
{
    static IAdService instance;

    public static IAdService Instance => instance ?? (instance = new MockAdService());

    public static void Set(IAdService service) => instance = service;
}

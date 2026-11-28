using UnityEngine;

/// <summary>
/// 营业 BGM：8:00–14:00 播 day_loop，14:00–打烊播 night_loop，打烊后停止；新一天重新开始。
/// 音频在 Resources/Audio/BGM/ 下（day_loop / night_loop），场景里没有时自动创建。
/// </summary>
public class BgmController : MonoBehaviour
{
    [Header("音频（留空从 Resources 加载）")]
    public AudioClip dayClip;
    public AudioClip nightClip;
    [Range(0f, 1f)] public float volume = 0.45f;

    [Header("时间段")]
    public int dayStartHour = 8;
    public int nightStartHour = 14;
    public int closeHour = 16;

    AudioSource src;
    int currentPeriod = -1;   // -1 未定，0 停，1 白天，2 夜晚

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectOfType<BgmController>() != null) return;
        var go = new GameObject("BGM");
        go.AddComponent<BgmController>();
    }

    void Awake()
    {
        if (dayClip == null) dayClip = Resources.Load<AudioClip>("Audio/BGM/day_loop");
        if (nightClip == null) nightClip = Resources.Load<AudioClip>("Audio/BGM/night_loop");

        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = volume;
    }

    void Update()
    {
        if (Time.frameCount % 30 != 0) return;
        Evaluate();
    }

    void Evaluate()
    {
        int want = 0;
        var clock = GameClock.Instance;
        if (clock != null)
        {
            int h = clock.Hour;
            if (h >= dayStartHour && h < nightStartHour) want = 1;
            else if (h >= nightStartHour && h < closeHour) want = 2;
        }

        if (want == currentPeriod) return;
        currentPeriod = want;

        if (want == 0) { src.Stop(); return; }

        var clip = want == 1 ? dayClip : nightClip;
        if (clip == null) { src.Stop(); return; }
        if (src.clip != clip) src.clip = clip;
        src.volume = volume;
        src.Play();
    }

    void OnValidate()
    {
        if (src != null) src.volume = volume;
    }
}

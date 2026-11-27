using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏时钟：按 60:1 推进（现实 1 秒 = 游戏 1 分钟），驱动 Canvas 时钟的时针 / 分针（可选秒针）。
/// 日期（第几天）通过 GameTimeStore 以 JSON 存档；钟点每次启动都从开业时间开始。
/// 场景中没有手动挂载时，会在加载后自动挂到 Canvas 上，并按名称查找指针。
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    [Header("起始时间（每次启动都从这里开始）")]
    [Range(0, 23)] public int startHour = 8;
    [Range(0, 59)] public int startMinute = 0;

    [Header("流速")]
    [Tooltip("游戏秒 / 现实秒：60 表示现实 1 秒 = 游戏 1 分钟")]
    public float gameSecondsPerRealSecond = 60f;

    [Header("指针（留空则按名称自动查找）")]
    public Transform hourHand;
    public Transform minuteHand;
    public Transform secondHand;

    [Header("自动查找关键字")]
    public string[] hourHandNames = { "时针", "hourhand", "hour hand", "hour_hand", "hour" };
    public string[] minuteHandNames = { "分针", "minutehand", "minute hand", "minute_hand", "minute" };
    public string[] secondHandNames = { "秒针", "secondhand", "second hand", "second_hand", "second" };

    [Header("存档")]
    [Tooltip("自动写入 JSON 存档的间隔（秒）；切后台 / 退出 / 跨天时也会立即保存")]
    public float saveIntervalSeconds = 5f;

    [Header("天数显示")]
    [Tooltip("天数文字的格式，{0} 会被替换成天数；写在场景中'日期牌'的 text 子对象上")]
    public string dayLabelFormat = "第 {0} 天";
    [Tooltip("'日期牌'在场景中的名字")]
    public string dayBoardName = "日期牌";

    [Header("日期牌动画")]
    [Tooltip("日期牌 Animator 的 bool 参数名：true = 展开，false = 收回")]
    public string dayBoardOpenParameter = "Open";
    [Tooltip("日期牌展开后保持的秒数，之后自动置回 false 收回")]
    public float dayBoardOpenSeconds = 3f;

    Text dayLabel;        // 场景中"日期牌"子对象上的 Text（只改文字，不新建物体）
    Transform dayBoard;        // 日期牌根节点（展开动画挂在它身上）
    Animator dayBoardAnimator; // 日期牌的 Animator
    Coroutine dayBoardRoutine; // 当前的"展开 3 秒后收回"协程

    float totalMinutes;   // 从 0:00 起累计的游戏分钟
    float saveTimer;

    /// <summary>当前是第几天：首次开局为 1，每次"再玩一次"+1，并记录在 JSON 存档里</summary>
    public int Day { get; private set; } = 1;

    /// <summary>钱包余额：每天打烊结算时累加当天营业额，和天数一起存在同一个 JSON 存档里</summary>
    public int Wallet { get; private set; }

    public int Hour => Mathf.FloorToInt(totalMinutes / 60f) % 24;
    public int Minute => Mathf.FloorToInt(totalMinutes) % 60;

    void Awake()
    {
        Instance = this;
        ResolveHands();
        totalMinutes = Mathf.Clamp(startHour, 0, 23) * 60f + Mathf.Clamp(startMinute, 0, 59);
        ApplyHands();
    }

    void Start()
    {
        // 放到 Start：此时 ShopManager 的 Instance 已初始化
        RestoreFromSave();
        EnsureDayLabel();
        ShowDayBoard();   // 重回游戏时也展开一次日期牌
    }

    void Update()
    {
        totalMinutes += gameSecondsPerRealSecond / 60f * Time.deltaTime;
        ApplyHands();

        saveTimer += Time.deltaTime;
        if (saveTimer >= saveIntervalSeconds)
        {
            saveTimer = 0f;
            SaveTime();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveTime();
    }

    void OnApplicationQuit()
    {
        SaveTime();
    }

    void OnDestroy()
    {
        SaveTime();
    }

    /// <summary>读取 JSON 存档：恢复天数和钱包余额，钟点一律从开业时间开始</summary>
    void RestoreFromSave()
    {
        GameTimeData saved = GameTimeStore.Load();
        Day = saved.day;
        Wallet = Mathf.Max(0, saved.wallet);

        // 每次启动都从开业时间开始，不恢复上次钟点
        totalMinutes = Mathf.Clamp(startHour, 0, 23) * 60f + Mathf.Clamp(startMinute, 0, 59);
        ApplyHands();
    }

    /// <summary>把当前天数 + 钱包余额写入 JSON 存档</summary>
    public void SaveTime()
    {
        GameTimeStore.Save(Day, Wallet);
    }

    /// <summary>打烊结算：把当天营业额并入钱包并立即写入 JSON 存档</summary>
    public void AddToWallet(int amount)
    {
        Wallet += Mathf.Max(0, amount);
        SaveTime();
    }

    /// <summary>商店购买扣款：余额足够则扣减并立即写入 JSON 存档；不足返回 false 不动钱包</summary>
    public bool TrySpend(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (Wallet < amount) return false;
        Wallet -= amount;
        SaveTime();
        return true;
    }

    /// <summary>是否已到或超过指定时间（例如顾客 9:00 开始出现）</summary>
    public bool IsAtOrAfter(int hour, int minute)
    {
        return totalMinutes >= hour * 60f + minute;
    }

    /// <summary>直接跳到指定时间（运行时的测试菜单调用）</summary>
    public void SetTime(int hour, int minute)
    {
        totalMinutes = Mathf.Clamp(hour, 0, 23) * 60f + Mathf.Clamp(minute, 0, 59);
        ApplyHands();
    }

    /// <summary>跨天事件：每天开始（NextDay）时触发一次，供奶茶机清空流体等系统订阅</summary>
    public static event System.Action OnNewDay;

    /// <summary>进入下一天：天数 +1，时钟回到指定时间，刷新天数显示并立即写入存档</summary>
    public void NextDay(int hour, int minute)
    {
        Day++;
        SetTime(hour, minute);
        UpdateDayLabel();
        ShowDayBoard();   // 新的一天：日期牌展开
        SaveTime();
        OnNewDay?.Invoke();
    }

    /// <summary>修改起始时间（编辑模式下的测试菜单调用，进入播放后生效）</summary>
    public void SetStartTime(int hour, int minute)
    {
        startHour = Mathf.Clamp(hour, 0, 23);
        startMinute = Mathf.Clamp(minute, 0, 59);
    }

    /// <summary>找到场景中"日期牌"子对象上的 Text，按当前天数刷新；场景里没放日期牌时只警告，不新建物体</summary>
    void EnsureDayLabel()
    {
        if (dayLabel == null)
        {
            Transform board = FindTransform(dayBoardName);
            if (board == null)
            {
                Debug.LogWarning("[GameClock] 场景中没有找到'" + dayBoardName + "'，天数文字不显示。");
                return;
            }

            dayBoard = board;
            dayLabel = board.GetComponentInChildren<Text>(true);
            if (dayLabel == null)
            {
                Debug.LogWarning("[GameClock] '" + dayBoardName + "'下没有 Text 组件，天数文字不显示。");
                return;
            }
        }

        UpdateDayLabel();
    }

    /// <summary>按名字找场景物体：先精确匹配，再退回包含匹配</summary>
    static Transform FindTransform(string targetName)
    {
        Transform contained = null;
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (t.name == targetName) return t;
            if (contained == null && t.name.Contains(targetName)) contained = t;
        }
        return contained;
    }

    /// <summary>按当前天数刷新文字内容</summary>
    void UpdateDayLabel()
    {
        if (dayLabel != null)
            dayLabel.text = string.Format(dayLabelFormat, Day);
    }

    /// <summary>日期牌展开：把 Open 置 true，停留 dayBoardOpenSeconds 秒后置回 false</summary>
    void ShowDayBoard()
    {
        if (dayBoard == null) dayBoard = FindTransform(dayBoardName);
        if (dayBoard == null) return;   // 找不到日期牌：EnsureDayLabel 已警告过
        if (dayBoardAnimator == null) dayBoardAnimator = dayBoard.GetComponentInChildren<Animator>(true);
        if (dayBoardAnimator == null)
        {
            Debug.LogWarning("[GameClock] '" + dayBoardName + "'上没有 Animator，日期牌动画不播放。");
            return;
        }
        if (!HasOpenParameter(dayBoardAnimator))
        {
            Debug.LogWarning("[GameClock] 日期牌 Animator 里没有 bool 参数'" + dayBoardOpenParameter + "'，动画不播放。");
            return;
        }

        // 上一次还没收回就再展开：重来一遍计时
        if (dayBoardRoutine != null) StopCoroutine(dayBoardRoutine);
        dayBoardRoutine = StartCoroutine(DayBoardRoutine());
    }

    IEnumerator DayBoardRoutine()
    {
        dayBoardAnimator.SetBool(dayBoardOpenParameter, true);
        yield return new WaitForSeconds(dayBoardOpenSeconds);
        dayBoardAnimator.SetBool(dayBoardOpenParameter, false);
        dayBoardRoutine = null;
    }

    /// <summary>Animator 里是否存在同名 bool 参数（参数名写错时只警告一次，不反复报错）</summary>
    bool HasOpenParameter(Animator animator)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == dayBoardOpenParameter)
                return true;
        }
        return false;
    }

    void ApplyHands()
    {
        float hourAngle = (totalMinutes % 720f) / 720f * 360f;   // 12 小时转一圈
        float minuteAngle = (totalMinutes % 60f) / 60f * 360f;   // 1 小时转一圈
        float secondAngle = ((totalMinutes * 60f) % 60f) / 60f * 360f;

        SetAngle(hourHand, hourAngle);
        SetAngle(minuteHand, minuteAngle);
        SetAngle(secondHand, secondAngle);
    }

    /// <summary>UI 里顺时针旋转对应负的 Z 角度</summary>
    static void SetAngle(Transform hand, float angle)
    {
        if (hand == null) return;
        hand.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    void ResolveHands()
    {
        if (hourHand == null) hourHand = FindHand(hourHandNames);
        if (minuteHand == null) minuteHand = FindHand(minuteHandNames);
        if (secondHand == null) secondHand = FindHand(secondHandNames);

        if (hourHand == null || minuteHand == null)
            Debug.LogWarning("[GameClock] 没有找到时针/分针，请检查指针名称或手动指定（默认识别含'时针''分针'的子物体）。");
    }

    Transform FindHand(string[] keywords)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            foreach (string key in keywords)
            {
                if (key.Length > 0 && n.Contains(key.ToLowerInvariant()))
                    return t;
            }
        }
        return null;
    }

    /// <summary>场景中没有手动挂载时，自动挂到 Canvas 并接管时钟指针</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectsOfType<GameClock>(true).Length > 0)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        GameObject host = canvas != null ? canvas.gameObject : new GameObject("GameClock");
        host.AddComponent<GameClock>();
    }
}

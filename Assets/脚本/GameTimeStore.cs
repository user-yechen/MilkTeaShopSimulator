using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>单个物件的等级条目（商店购买/升级时写入）</summary>
[Serializable]
public class MachineLevelEntry
{
    public string id;
    public int level = 1;
}

/// <summary>存档内容：第几天 + 钱包余额 + 各物件等级；钟点每次从开业时间开始。</summary>
[Serializable]
public class GameTimeData
{
    public int day = 1;
    public int wallet = 0;   // 钱包余额（每天结算累加）；旧存档没有该字段时按 0 读取
    /// <summary>各物件等级（默认 1 级，购买/升级时提高）；旧存档没有该字段时按空列表读取并补默认</summary>
    public List<MachineLevelEntry> machineLevels = new List<MachineLevelEntry>();
}

/// <summary>
/// 统一存档：天数 + 钱包 + 物件等级。
/// Android / 编辑器：JSON 写到 persistentDataPath/gameTime.json；
/// WebGL / 微信小游戏：写 PlayerPrefs（persistentDataPath 不可写），键名 gameTime.json。
/// 钟点每次启动都从开业时间（startHour:startMinute）开始，不存档。
/// 由 GameClock 调用：启动时 Load 恢复；运行中、切后台、退出、跨天时 Save。
/// 物件等级由 MachineLevel / ShopItem 通过 GetMachineLevel / SetMachineLevel 读写。
/// 首次启动没有存档时从第 1 天开始，已知物件按默认 1 级预写入。
/// </summary>
public static class GameTimeStore
{
    public const string FileName = "gameTime.json";

    /// <summary>WebGL / 小游戏下的 PlayerPrefs 键名</summary>
    public const string PlayerPrefsKey = "gameTime.json";

    /// <summary>存档完整路径（Android / 编辑器用；WebGL 走 PlayerPrefs）</summary>
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>已知可购买/可升级物件：首次读档时预写入，默认 1 级（对应 MachineLevel.CanonicalIdFor 的规范 ID）</summary>
    static readonly string[] DefaultMachineIds =
        { "前制作台", "奶茶机", "加热机", "制冰机", "货架", "奶茶杯架", "前台" };

    static GameTimeData cached;

    /// <summary>读取存档（带内存缓存）；没有存档或读取失败时按第 1 天。兼容旧版带 hour/minute 的存档（多余字段忽略）。</summary>
    public static GameTimeData Load()
    {
        EnsureLoaded();
        return cached;
    }

    static void EnsureLoaded()
    {
        if (cached != null) return;
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string json = PlayerPrefs.GetString(PlayerPrefsKey, null);
            if (!string.IsNullOrEmpty(json))
                cached = JsonUtility.FromJson<GameTimeData>(json);
#else
            if (File.Exists(SavePath))
                cached = JsonUtility.FromJson<GameTimeData>(File.ReadAllText(SavePath));
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameTimeStore] 读取游戏时间存档失败，按第 1 天开始：" + e.Message);
        }

        if (cached == null) cached = new GameTimeData();
        if (cached.day < 1) cached.day = 1;
        if (cached.machineLevels == null) cached.machineLevels = new List<MachineLevelEntry>();

        // 预写入已知物件，默认 1 级
        foreach (string id in DefaultMachineIds)
            if (cached.machineLevels.Find(e => e.id == id) == null)
                cached.machineLevels.Add(new MachineLevelEntry { id = id, level = 1 });
    }

    /// <summary>写入存档（覆盖）：天数 + 钱包；物件等级随缓存一并写入。</summary>
    public static void Save(int day, int wallet)
    {
        EnsureLoaded();
        cached.day = Mathf.Max(1, day);
        cached.wallet = Mathf.Max(0, wallet);
        Write();
    }

    static void Write()
    {
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(cached));
            PlayerPrefs.Save();
#else
            File.WriteAllText(SavePath, JsonUtility.ToJson(cached));
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameTimeStore] 写入游戏时间存档失败：" + e.Message);
        }
    }

    /// <summary>查物件等级；没有记录时按 defaultLevel（默认 1 级）</summary>
    public static int GetMachineLevel(string id, int defaultLevel = 1)
    {
        if (string.IsNullOrEmpty(id)) return Mathf.Max(1, defaultLevel);
        EnsureLoaded();
        MachineLevelEntry e = cached.machineLevels.Find(x => x.id == id);
        return e != null ? Mathf.Max(1, e.level) : Mathf.Max(1, defaultLevel);
    }

    /// <summary>写入物件等级并立即落盘（购买/升级时调用）</summary>
    public static void SetMachineLevel(string id, int level)
    {
        if (string.IsNullOrEmpty(id)) return;
        EnsureLoaded();
        MachineLevelEntry e = cached.machineLevels.Find(x => x.id == id);
        if (e == null)
        {
            e = new MachineLevelEntry { id = id };
            cached.machineLevels.Add(e);
        }
        e.level = Mathf.Max(1, level);

        // 走 GameClock.SaveTime 把天数/钱包一起写入；拿不到时钟（如编辑器测试）就直接写
        if (GameClock.Instance != null) GameClock.Instance.SaveTime();
        else Write();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 顾客刷新与排队管理：
/// - 营业后按 1~20 秒随机波次刷新，每次 1~5 人；场景总人数 ≥10 或队列 &gt;8 时停止刷新；
/// - 队列沿 queueDir 排列、间距 1 米；队首可被接待；
/// - 顾客速度 = baseSpeed × 0.8~1.5，生成时固定。
/// 模型复制自场景里的「路人」（会停用原对象的 PasserbyWalk，避免它自己再跑一轮）。
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    public static CustomerSpawner Instance { get; private set; }

    [Header("模板")]
    public Transform template;
    public float groundY = 1.16f;
    public float modelYawOffset = -90f;

    [Header("入口 / 出口")]
    public float enterMinX = -19.5f;
    public float enterMaxX = -17.3f;
    public float enterZ = 4.5f;
    public Vector3 exitPoint = new Vector3(-18.4f, 1.16f, 4.5f);

    [Header("队列")]
    public Vector3 queueFront = new Vector3(-12f, 1.16f, -16f);
    public Vector3 queueDir = Vector3.left;
    [Tooltip("排队时顾客面朝的方向（默认朝 +X 前台）")]
    public Vector3 queueFaceDir = Vector3.right;
    public float queueSpacing = 1f;
    public Vector3 pickupOffset = new Vector3(0f, 0f, -3f);

    [Header("刷新")]
    public float spawnIntervalMin = 1f;
    public float spawnIntervalMax = 20f;
    public int waveMin = 1;
    public int waveMax = 5;
    public int maxAlive = 10;
    public int maxQueue = 5;
    [Range(0, 23)] public int firstHour = 9;

    [Header("行为")]
    public float baseSpeed = 1.8f;
    public float speedMin = 0.8f;
    public float speedMax = 1.5f;
    public float orderDuration = 4f;
    public Vector3 wanderCenter = new Vector3(-14f, 1.16f, -11f);
    public float wanderRadius = 4f;

    readonly List<CustomerAgent> alive = new List<CustomerAgent>();
    readonly List<CustomerAgent> waiting = new List<CustomerAgent>();
    readonly List<CustomerAgent> pickupQueue = new List<CustomerAgent>();

    public int AliveCount => alive.Count;
    public int WaitingCount => waiting.Count;
    public bool FrontReady => waiting.Count > 0 && waiting[0] != null && waiting[0].IsWaiting;

    void Awake() => Instance = this;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectOfType<CustomerSpawner>() != null) return;
        var go = new GameObject("CustomerSpawner");
        go.AddComponent<CustomerSpawner>();
    }

    void Start()
    {
        if (template == null)
        {
            foreach (Transform t in FindObjectsOfType<Transform>(true))
            {
                if (t.name.Contains("路人")) { template = t; break; }
            }
        }

        if (template == null)
        {
            Debug.LogWarning("[CustomerSpawner] 找不到「路人」模板，顾客不会刷新");
            return;
        }

        // 停用原路人，防止它自己再跑一轮 PasserbyWalk
        template.gameObject.SetActive(false);
        StartCoroutine(SpawnLoop());
    }

    bool CanSpawn
    {
        get
        {
            if (ShopManager.Instance != null && !ShopManager.Instance.IsOpen) return false;
            if (GameClock.Instance != null && !GameClock.Instance.IsAtOrAfter(firstHour, 0)) return false;
            if (alive.Count >= maxAlive) return false;
            if (waiting.Count >= maxQueue) return false;
            return true;
        }
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(Random.Range(2f, 6f));   // 开门后缓一下
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnIntervalMin, spawnIntervalMax));
            if (!CanSpawn) continue;

            int wave = Random.Range(waveMin, waveMax + 1);
            for (int i = 0; i < wave; i++)
            {
                if (!CanSpawn) break;
                SpawnOne();
            }
        }
    }

    void SpawnOne()
    {
        float x = Random.Range(enterMinX, enterMaxX);
        var go = Instantiate(template.gameObject, new Vector3(x, groundY, enterZ),
                             Quaternion.identity);
        go.name = "路人_顾客";

        var old = go.GetComponent<PasserbyWalk>();
        if (old != null) { old.enabled = false; Destroy(old); }

        go.SetActive(true);

        var agent = go.GetComponent<CustomerAgent>();
        if (agent == null) agent = go.AddComponent<CustomerAgent>();
        agent.Init(this, new Vector3(x, groundY, enterZ));

        alive.Add(agent);
        waiting.Add(agent);
        Reslot();
    }

    void Reslot()
    {
        for (int i = 0; i < waiting.Count; i++) waiting[i].AssignSlot(i);
    }

    /// <summary>队首被接待</summary>
    public bool ReceiveFront()
    {
        if (!FrontReady) return false;
        waiting[0].StartOrdering();
        return true;
    }

    public void OnOrdered(CustomerAgent agent)
    {
        waiting.Remove(agent);
        if (!pickupQueue.Contains(agent)) pickupQueue.Add(agent);   // 先来后到
        Reslot();
    }

    public void Despawn(CustomerAgent agent)
    {
        alive.Remove(agent);
        waiting.Remove(agent);
        pickupQueue.Remove(agent);
        Reslot();
        Destroy(agent.gameObject);
    }

    /// <summary>是否轮到该顾客取餐（取餐队列先来后到）</summary>
    public bool IsPickupTurn(CustomerAgent agent) =>
        pickupQueue.Count > 0 && pickupQueue[0] == agent;

    /// <summary>前台是否有该顾客要的饮品</summary>
    public bool HasDrinkFor(CustomerAgent agent) =>
        Counter != null && Counter.HasCup(agent.OrderCupType, agent.OrderFluid);

    /// <summary>取走该顾客的饮品并结算，移出取餐队列</summary>
    public void TakeFor(CustomerAgent agent)
    {
        if (Counter != null) Counter.TakeCup(agent.OrderCupType, agent.OrderFluid);
        if (ShopManager.Instance != null) ShopManager.Instance.CompleteOrder();
        pickupQueue.Remove(agent);
    }

    /// <summary>离开点：刷新点（入口范围）</summary>
    public Vector3 ExitPoint() =>
        new Vector3(Random.Range(enterMinX, enterMaxX), groundY, enterZ);

    /// <summary>进店绕行点（避免穿墙）</summary>
    public Vector3[] EntryPath() => new[]
    {
        new Vector3(-17.5f, groundY, 2.5f),
        new Vector3(-17.5f, groundY, -7f),
        new Vector3(-14f, groundY, -11f),
    };

    /// <summary>离店绕行点（回到刷新点）</summary>
    public Vector3[] ExitPath() => new[]
    {
        new Vector3(-14f, groundY, -11f),
        new Vector3(-17.5f, groundY, -7f),
        new Vector3(-17.5f, groundY, 2.5f),
        ExitPoint(),
    };

    public Vector3 QueuePosition(int index) =>
        queueFront + queueDir.normalized * (Mathf.Clamp(index, 0, maxQueue - 1) * queueSpacing);

    public Vector3 PickupPoint() => queueFront + pickupOffset;

    public Vector3 RandomWanderPoint()
    {
        Vector2 c = Random.insideUnitCircle * wanderRadius;
        return new Vector3(wanderCenter.x + c.x, groundY, wanderCenter.z + c.y);
    }

    CounterCupDisplay counter;
    CounterCupDisplay Counter
    {
        get
        {
            if (counter == null) counter = FindObjectOfType<CounterCupDisplay>();
            return counter;
        }
    }

    public bool CounterHas(Cup.CupType type, string fluid) =>
        Counter != null && Counter.HasCup(type, fluid);

    public void TakeCup(Cup.CupType type, string fluid)
    {
        if (Counter == null) return;
        Counter.TakeCup(type, fluid);
        if (ShopManager.Instance != null) ShopManager.Instance.CompleteOrder();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < maxQueue; i++)
            Gizmos.DrawWireSphere(QueuePosition(i), 0.2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(PickupPoint(), 0.3f);
        Gizmos.DrawWireSphere(exitPoint, 0.3f);
    }
}

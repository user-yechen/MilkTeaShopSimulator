using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路人点餐（可循环接待）：
/// 1) 顾客刷新时间（默认 9:00 起，可在 Inspector 调）到 16:00 打烊之间，从入口范围（x -19.5 ~ -17.3，z=4.5）出现，高度由地面射线检测决定（检测不到地面时保持 fallbackY=1.16）；
/// 2) 沿随机绕行的路线走到前台点餐位（-12, 1.16, -16），面朝 +X（前台方向）；
/// 3) 一直等到前台上摆出奶茶（默认所有类型都接受），取走一杯后结算营收（10 元 + 随机 1~5 元小费），再转身朝出口方向离开；
/// 4) 走回入口范围后隐藏，若仍在可刷新时间内，稍等片刻重新从入口进店接待下一位顾客；未到刷新时间或 16:00 打烊后不再生成。
/// 行走时面朝移动方向；点餐时面朝 +X。X 坐标不会超过 -11.7。
/// 场景里名为“路人”的物体会在加载后自动补上本脚本；也可手动挂载。
/// </summary>
public class PasserbyWalk : MonoBehaviour
{
    enum State { WalkingIn, Ordering, WalkingOut, Finished }

    [Header("路径约束")]
    [Tooltip("出生高度；没有检测到地面时也保持这个高度")]
    public float fallbackY = 1.16f;
    [Tooltip("入口 X 范围起点")]
    public float enterMinX = -19.5f;
    [Tooltip("入口 X 范围终点")]
    public float enterMaxX = -17.3f;
    [Tooltip("入口 / 出口的 Z 坐标")]
    public float enterZ = 4.5f;
    [Tooltip("X 轴上限：任何情况下都不会超过这个值")]
    public float maxX = -11.7f;

    [Header("地面检测")]
    [Tooltip("向下检测地面的射线长度（米）")]
    public float groundProbeDistance = 4f;
    [Tooltip("参与地面检测的碰撞层")]
    public LayerMask groundMask = ~0;

    [Header("随机路径")]
    [Tooltip("绕行走廊的 Z 范围（保持在 z=-5 墙的下方）")]
    public float corridorMinZ = -10f;
    [Tooltip("绕行走廊的 Z 范围（保持在 z=-5 墙的下方）")]
    public float corridorMaxZ = -6.5f;

    [Header("前台点餐")]
    [Tooltip("点餐位的 X 坐标")]
    public float orderX = -12f;
    [Tooltip("点餐位的 Z 坐标")]
    public float orderZ = -16f;

    [Header("行走设置")]
    [Tooltip("移动速度（米/秒）")]
    public float moveSpeed = 1.8f;   // 原 1.2，2026-11-09 改 1.5 倍
    [Tooltip("模型自身正面不是 +Z 时的朝向修正：相对移动方向逆时针 90° 填 -90，顺时针 90° 填 90")]
    public float modelYawOffset = -90f;

    [Header("顾客到店时间")]
    [Tooltip("第一位顾客进店的最早时间（营业 8:00 开门，但顾客要等到这个时间才刷新）")]
    [Range(0, 23)] public int firstHour = 9;
    [Range(0, 59)] public int firstMinute = 0;

    [Header("顾客循环")]
    [Tooltip("上一位顾客离开后，下一位进店前的最短等待时间（秒）")]
    public float nextVisitDelayMin = 0.5f;
    [Tooltip("上一位顾客离开后，下一位进店前的最长等待时间（秒）")]
    public float nextVisitDelayMax = 1.5f;

    /// <summary>走到前台点餐位停下时触发（OrderBubble 用它弹对话框）</summary>
    public System.Action OnArrivedAtCounter;
    /// <summary>取到奶茶转身离开 / RestartDay 时触发（对话框此时应隐藏）</summary>
    public System.Action OnLeft;
    /// <summary>是否正站在前台点餐位（OrderBubble 的 Update 保险用它，状态不是点餐就强制隐藏对话框）</summary>
    public bool IsOrdering => state == State.Ordering;

    State state;
    List<Vector3> path = new List<Vector3>();
    CounterCupDisplay counter;    // 前台成品展示（由 CupInteract 自动创建）
    bool warnedNoCounter;
    bool waitingForOpen;          // 还没到营业时间，先隐藏
    float nextVisitTime = -1f;    // 下一位顾客进店的时间点；< 0 表示不排下一趟
    Renderer[] modelRenderers;
    Collider[] modelColliders;
    float groundOffset;                          // 模型 pivot 到底部（脚底）的高度差
    bool groundOffsetReady;
    readonly RaycastHit[] groundHits = new RaycastHit[8];   // 复用的射线结果缓冲，避免每帧分配

    /// <summary>是否处于营业时间（8:00-16:00 且尚未打烊）</summary>
    bool ShopOpen => ShopManager.Instance == null || ShopManager.Instance.IsOpen;

    /// <summary>现在能否刷新顾客：营业中，且游戏时间已到顾客到店时间（默认 9:00）</summary>
    bool CustomersAllowed
    {
        get
        {
            if (!ShopOpen) return false;
            GameClock clock = GameClock.Instance;
            return clock == null || clock.IsAtOrAfter(firstHour, firstMinute);
        }
    }

    void Start()
    {
        // 未到顾客到店时间 / 营业时间之外先隐藏，到点后再出现
        if (!CustomersAllowed)
        {
            waitingForOpen = true;
            SetModelVisible(false);
            return;
        }
        BeginVisit();
    }

    /// <summary>“再玩一次”：隐藏顾客并等待新一天的营业时间</summary>
    public void RestartDay()
    {
        nextVisitTime = -1f;
        path.Clear();
        state = State.Finished;
        waitingForOpen = true;
        SetModelVisible(false);
        OnLeft?.Invoke();
    }

    void BeginVisit()
    {
        waitingForOpen = false;
        SetModelVisible(true);

        float startX = Random.Range(enterMinX, enterMaxX);
        transform.position = new Vector3(startX, fallbackY, enterZ);
        UpdateGroundOffset();
        SnapToGround();
        BuildInPath(startX);
        state = State.WalkingIn;
    }

    /// <summary>记录模型 pivot 到脚底的高度差，让脚底贴地而不是把 Y 锁死</summary>
    void UpdateGroundOffset()
    {
        if (modelRenderers == null) modelRenderers = GetComponentsInChildren<Renderer>(true);
        if (modelRenderers.Length == 0) return;

        Bounds b = modelRenderers[0].bounds;
        for (int i = 1; i < modelRenderers.Length; i++) b.Encapsulate(modelRenderers[i].bounds);
        groundOffset = transform.position.y - b.min.y;
        groundOffsetReady = true;
    }

    /// <summary>
    /// 向下发射一条射线找地面，把角色放到地面上（一个角色每帧一次 RaycastNonAlloc，开销可忽略）。
    /// 检测不到地面时保持当前高度，避免角色掉进没有碰撞的地面。
    /// </summary>
    void SnapToGround()
    {
        if (!groundOffsetReady) return;

        Vector3 pos = transform.position;
        Vector3 origin = new Vector3(pos.x, pos.y + 0.3f, pos.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
            groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore);

        float floorY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider col = groundHits[i].collider;
            if (col == null || col.transform.IsChildOf(transform)) continue;   // 忽略自己的碰撞箱
            if (groundHits[i].normal.y < 0.5f) continue;                        // 只认朝上的表面（地板）
            floorY = Mathf.Max(floorY, groundHits[i].point.y);                  // 取最近（最高）的地面
        }
        if (floorY == float.NegativeInfinity) return;

        pos.y = floorY + groundOffset;
        transform.position = pos;
    }

    void SetModelVisible(bool visible)
    {
        if (modelRenderers == null) modelRenderers = GetComponentsInChildren<Renderer>(true);
        if (modelColliders == null) modelColliders = GetComponentsInChildren<Collider>(true);

        foreach (Renderer r in modelRenderers) r.enabled = visible;
        foreach (Collider c in modelColliders) c.enabled = visible;
    }

    /// <summary>入口 → 随机绕行 → 点餐位</summary>
    void BuildInPath(float startX)
    {
        path.Clear();
        path.Add(Snap(startX, enterZ));

        // 前厅里先随机拐一下，再穿过 z=-5 墙的缺口（此时 x 一直保持在 -15 墙以西）
        path.Add(Snap(Random.Range(-19.3f, -16.2f), Random.Range(1.5f, 3.5f)));
        float corridorZ = Random.Range(corridorMinZ, corridorMaxZ);
        path.Add(Snap(Random.Range(-19.3f, -16f), corridorZ));

        // 进店后在店内随机绕两个弯
        path.Add(Snap(Random.Range(-15f, -13f), Random.Range(corridorMinZ, corridorMaxZ)));
        path.Add(Snap(Random.Range(-13.5f, -12.2f), Random.Range(-13f, -7f)));

        path.Add(Snap(orderX, orderZ));
    }

    /// <summary>点餐位 → 随机绕行 → 出口</summary>
    void BuildOutPath()
    {
        float exitX = Random.Range(enterMinX, enterMaxX);
        path.Clear();
        path.Add(Snap(Random.Range(-14f, -12.2f), Random.Range(-13f, -9f)));
        path.Add(Snap(Random.Range(-15f, -13.5f), Random.Range(corridorMinZ, corridorMaxZ)));
        path.Add(Snap(Random.Range(-19.3f, -16f), Random.Range(corridorMinZ, corridorMaxZ)));
        path.Add(Snap(Random.Range(-19.3f, -16f), Random.Range(-4.5f, -1.5f)));
        path.Add(Snap(exitX, enterZ));
    }

    void Update()
    {
        // 等到顾客刷新时间（默认 9:00）后再开始这一趟点餐
        if (waitingForOpen)
        {
            if (CustomersAllowed) BeginVisit();
            return;
        }

        switch (state)
        {
            case State.WalkingIn:
                if (FollowPath(Time.deltaTime))
                {
                    state = State.Ordering;
                    // 点餐时面朝 +X（前台方向）
                    FaceDirection(Vector3.right);
                    OnArrivedAtCounter?.Invoke();
                }
                break;

            case State.Ordering:
                // 只取符合自己订单的饮品（杯型+液体都匹配）；没有订单组件时维持"任意一杯都接受"
                if (CounterHasOrderCup())
                {
                    TakeOrderCup();
                    // 顾客拿到奶茶：结算 10 元 + 随机 1~5 元小费
                    if (ShopManager.Instance != null) ShopManager.Instance.CompleteOrder();
                    BuildOutPath();
                    state = State.WalkingOut;
                    OnLeft?.Invoke();
                }
                break;

            case State.WalkingOut:
                if (FollowPath(Time.deltaTime))
                {
                    // 离开后隐藏；营业时间内稍等片刻，让下一位顾客进店
                    SetModelVisible(false);
                    state = State.Finished;
                    nextVisitTime = CustomersAllowed
                        ? Time.time + Random.Range(nextVisitDelayMin, nextVisitDelayMax)
                        : -1f;
                }
                break;

            case State.Finished:
                if (nextVisitTime > 0f && Time.time >= nextVisitTime)
                {
                    nextVisitTime = -1f;
                    if (CustomersAllowed) BeginVisit();
                }
                break;
        }
    }

    /// <summary>沿当前路径在水平面上走一步（高度由地面检测决定）；走到终点返回 true</summary>
    bool FollowPath(float dt)
    {
        if (path.Count == 0) return true;

        Vector3 target = path[0];
        Vector3 pos = transform.position;

        // 只在 XZ 平面上移动，Y 交给地面检测
        Vector3 next = Vector3.MoveTowards(
            new Vector3(pos.x, 0f, pos.z),
            new Vector3(target.x, 0f, target.z),
            moveSpeed * dt);
        next.y = pos.y;
        next.x = Mathf.Clamp(next.x, enterMinX, maxX);   // X 永不超过 -11.7

        Vector3 dir = new Vector3(target.x - next.x, 0f, target.z - next.z);
        if (dir.sqrMagnitude > 0.0001f) FaceDirection(dir);

        transform.position = next;
        SnapToGround();

        if (Mathf.Abs(next.x - target.x) < 0.000001f && Mathf.Abs(next.z - target.z) < 0.000001f)
        {
            path.RemoveAt(0);
            return path.Count == 0;
        }
        return false;
    }

    /// <summary>面朝水平方向 dir（叠加模型自身的朝向修正）</summary>
    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up)
                           * Quaternion.Euler(0f, modelYawOffset, 0f);
    }

    /// <summary>前台上有符合本顾客订单的饮品（杯型+液体都匹配）；没挂 OrderBubble 时退化为"有任意一杯即可"</summary>
    OrderBubble cachedOrder;
    OrderBubble Order => cachedOrder != null ? cachedOrder : (cachedOrder = GetComponent<OrderBubble>());
    Cup.CupType OrderCupType => Order != null && Order.CupType == "大杯" ? Cup.CupType.Large : Cup.CupType.Medium;

    bool CounterHasOrderCup()
    {
        if (counter == null) counter = FindObjectOfType<CounterCupDisplay>();
        if (counter == null)
        {
            if (!warnedNoCounter)
            {
                warnedNoCounter = true;
                Debug.LogWarning("[PasserbyWalk] 场景中找不到前台成品展示（CounterCupDisplay），路人会一直等待。");
            }
            return false;
        }
        return Order != null ? counter.HasCup(OrderCupType, Order.FluidName) : counter.CupCount > 0;
    }

    void TakeOrderCup()
    {
        if (Order != null) counter.TakeCup(OrderCupType, Order.FluidName);
        else counter.TakeOneCup();
    }

    Vector3 Snap(float x, float z)
    {
        return new Vector3(x, fallbackY, z);
    }

    /// <summary>场景中没有挂载时，自动给名为“路人”的物体补上本脚本</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        if (FindObjectOfType<PasserbyWalk>() != null) return;

        Transform target = null;
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            if (!t.name.Contains("路人")) continue;
            // 优先选中根节点，避免把脚本挂到模型内部的身体部件上
            if (t.parent == null) { target = t; break; }
            if (target == null) target = t;
        }

        if (target != null)
            target.gameObject.AddComponent<PasserbyWalk>();
    }
}

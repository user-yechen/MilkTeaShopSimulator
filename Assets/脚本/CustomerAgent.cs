using UnityEngine;

/// <summary>
/// 单个顾客：排队 → 待接待 → 点餐（弹气泡）→ 自由走动 → 取餐 → 离开。
/// 由 CustomerSpawner 统一生成和分配队列槽位；速度在生成时固定（0.8~1.5 倍基础速度）。
/// 模型复制自场景里的「路人」，不再使用 PasserbyWalk。
/// </summary>
public class CustomerAgent : MonoBehaviour
{
    public enum S { ToQueue, Waiting, Ordering, Wander, ToPickup, Leaving }

    public S State { get; private set; } = S.ToQueue;
    public bool Ordered => State == S.Wander || State == S.ToPickup || State == S.Leaving;
    public bool IsWaiting => State == S.Waiting;

    CustomerSpawner spawner;
    OrderBubble order;
    float speed;
    float modelYawOffset = -90f;
    int slot = -1;
    Vector3 target;
    float timer;
    readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>();

    public void Init(CustomerSpawner sp, Vector3 start)
    {
        spawner = sp;
        transform.position = start;
        speed = sp.baseSpeed * Random.Range(sp.speedMin, sp.speedMax);
        modelYawOffset = sp.modelYawOffset;
        order = GetComponent<OrderBubble>();
        if (order == null) order = gameObject.AddComponent<OrderBubble>();
        path.Clear();
        path.AddRange(sp.EntryPath());
    }

    /// <summary>分配队列槽位（0=队首）</summary>
    public void AssignSlot(int index)
    {
        slot = index;
        if (Ordered) return;
        target = spawner.QueuePosition(index);
        if (State != S.Ordering) State = S.ToQueue;
    }

    /// <summary>队首被接待：开始点餐、显示气泡</summary>
    public void StartOrdering()
    {
        State = S.Ordering;
        timer = spawner.orderDuration;
        if (order != null) order.ShowOrder();
    }

    void Update()
    {
        switch (State)
        {
            case S.ToQueue:
                if (path.Count > 0) { FollowPath(); break; }
                if (MoveTo(target)) { State = S.Waiting; FaceDirection(spawner.queueFaceDir); }
                break;

            case S.Waiting:
                if (slot >= 0) MoveTo(spawner.QueuePosition(slot), 0.15f);
                FaceDirection(spawner.queueFaceDir);   // 排队时始终面朝 +X（前台）
                break;

            case S.Ordering:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    if (order != null) order.HideOrder();
                    spawner.OnOrdered(this);          // 进入取餐队列（先来后到）
                    State = S.Wander;
                    target = spawner.RandomWanderPoint();
                }
                break;

            case S.Wander:
                // 排队期间在旁边晃；轮到自己且前台有对应饮品就去取
                if (MoveTo(target)) target = spawner.RandomWanderPoint();
                if (spawner.IsPickupTurn(this) && spawner.HasDrinkFor(this))
                {
                    State = S.ToPickup;
                    target = spawner.PickupPoint();
                }
                break;

            case S.ToPickup:
                if (MoveTo(target))
                {
                    if (spawner.TakeFor(this))
                    {
                        State = S.Leaving;
                        path.Clear();
                        path.AddRange(spawner.ExitPath());
                    }
                    else
                    {
                        // 没取到（杯型/液体不匹配或已被拿），回去等下一轮
                        State = S.Wander;
                        target = spawner.RandomWanderPoint();
                    }
                }
                break;

            case S.Leaving:
                if (path.Count > 0) FollowPath();
                else spawner.Despawn(this);
                break;
        }
    }

    bool FollowPath()
    {
        if (path.Count == 0) return true;
        if (MoveTo(path[0], 0.15f)) path.RemoveAt(0);
        return path.Count == 0;
    }

    public Cup.CupType OrderCupType =>
        order != null && order.CupType == "大杯" ? Cup.CupType.Large : Cup.CupType.Medium;

    public string OrderFluid =>
        order != null && !string.IsNullOrEmpty(order.FluidName) ? order.FluidName : "牛奶";

    bool MoveTo(Vector3 destination, float stopDistance = 0.08f)
    {
        Vector3 pos = transform.position;
        Vector3 next = Vector3.MoveTowards(
            new Vector3(pos.x, 0f, pos.z),
            new Vector3(destination.x, 0f, destination.z),
            speed * Time.deltaTime);
        next.y = spawner != null ? spawner.groundY : pos.y;
        Vector3 dir = new Vector3(destination.x - next.x, 0f, destination.z - next.z);
        if (dir.sqrMagnitude > 0.0001f) FaceDirection(dir);

        transform.position = next;

        Vector2 a = new Vector2(next.x, next.z);
        Vector2 b = new Vector2(destination.x, destination.z);
        return Vector2.Distance(a, b) <= stopDistance;
    }

    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up)
                           * Quaternion.Euler(0f, modelYawOffset, 0f);
    }
}

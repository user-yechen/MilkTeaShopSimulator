using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家交互协调器（挂在主摄像机上）。
/// 自己只管三件事：① 拿杯 ② 近距离判定 + 弹按钮 ③ 按钮点了调用设备组件方法。
/// 杯位/出料口/加热槽等设备逻辑，已全部下沉到 TeaMachine / HeaterMachine 组件里。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CupInteract : MonoBehaviour
{
    [Header("命名（与场景中保持一致）")]
    public string rackFrameName   = "奶茶杯架";
    public string mediumCupName   = "中奶茶杯";   // 模板名前缀
    public string largeCupName    = "大奶茶杯";   // 模板名前缀

    [Header("场景里的冰格按钮名（留空或找不到才在滚动视图里自动创建）")]
    public string iceOpenButtonName  = "打开冰格按钮";
    public string iceCloseButtonName = "关闭冰格按钮";
    public string iceTakeIceButtonName = "取冰按钮";
    [Tooltip("前台有未接待顾客时出现的按钮")]
    public string receptionButtonName = "接待按钮";

    [Header("距离设置")]
    public float interactDistance = 3f;

    [Header("手持设置")]
    public Vector3 holdOffset = new Vector3(0.35f, -0.25f, 0.6f);
    public Vector3 heldCupEuler = new Vector3(180f, 0f, 0f);

    [Header("UI")]
    public Button mediumButton;
    public Button largeButton;
    public Button injectButton;
    [Tooltip("机器里有杯时出现，点击打开奶茶机交互界面选流体")]
    public Button chooseFluidButton;
    public Button machineMediumButton;
    public Button machineLargeButton;
    public Button heatButton;
    public Button heaterMediumButton;
    public Button heaterLargeButton;
    public Button counterPlaceButton;
    public Button iceOpenButton;
    public Button iceCloseButton;
    public Button iceTakeIceButton;
    public Button receptionButton;

    [Header("滚动视图")]
    public Vector2 scrollViewAnchor = new Vector2(0.65f, 0.5f);
    public Vector2 scrollViewSize   = new Vector2(120f, 260f);
    public Vector2 scrollViewOffset = new Vector2(0f, -100f);
    public float scrollFadeRange    = 100f;

    [Header("交互按钮贴图（把 贴图/交互按钮.png 拖到这里）")]
    public Sprite buttonSprite;

    // 交互按钮尺寸：宽度为原先 88 的 2 倍；高度按贴图原生比例（2200:299）等比推算
    const float ButtonWidthScale = 2f;
    const float ButtonWidth  = 88f * ButtonWidthScale;

    float ButtonHeight => buttonSprite != null && buttonSprite.rect.width > 0f
        ? ButtonWidth * (buttonSprite.rect.height / buttonSprite.rect.width)
        : 28f;

    Camera cam;
    Transform mediumContainer;
    Transform largeContainer;
    Transform rackRoot;
    Cup heldCup;

    /// <summary>玩家手里当前拿着的杯子（其他脚本只读访问）</summary>
    public Cup HeldCup => heldCup;

    // —— 设备组件：由 TeaMachine / HeaterMachine 自己在 AfterSceneLoad 自动挂到 GameObject 上 ——
    TeaMachine teaMachine;
    HeaterMachine heaterMachine;

    [Header("奶茶机交互界面")]
    [Tooltip("把 预制件/奶茶机交互界面.prefab 拖进来；留空则在场景里按名字找")]
    public GameObject machinePanelPrefab;

    GameObject machinePanel;
    RectTransform buttonViewport;
    RectTransform buttonContent;
    readonly List<CanvasGroup> scrollGroups = new List<CanvasGroup>();
    readonly Vector3[] corners = new Vector3[4];   // GetWorldCorners 复用缓冲，避免每帧分配
    Renderer[] rackRenderers;                      // 杯架渲染器缓存，避免每帧 GetComponentsInChildren 分配

    CounterCupDisplay counterDisplay;
    IceTray iceTray;

    void Awake() => Instance = this;

    /// <summary>共享入口（静态）：其他交互脚本往「获取大杯」所在的滚动视图加按钮用</summary>
    public static CupInteract Instance { get; private set; }

    /// <summary>在交互按钮滚动视图里加一个同款按钮（滚动视图还没建好返回 null，调用方需自行兜底）</summary>
    public Button AddSharedButton(string label)
    {
        if (buttonContent == null) return null;
        Button btn = CreateButton(buttonContent, label);
        AddButtonToScroll(btn);
        return btn;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        EnsureColliders(mediumCupName);
        EnsureColliders(largeCupName);
        FindContainers();
        EnsureUI();

        // 设备组件自动查找（TeaMachine/HeaterMachine 自己 AutoAttach，这里只取引用）
        teaMachine    = FindObjectOfType<TeaMachine>();
        heaterMachine = FindObjectOfType<HeaterMachine>();

        counterDisplay = FindObjectOfType<CounterCupDisplay>();
        if (counterDisplay == null) counterDisplay = gameObject.AddComponent<CounterCupDisplay>();

        iceTray = FindObjectOfType<IceTray>();
    }

    void Update()
    {
        Vector3 playerPos = cam.transform.position;

        bool nearRack = IsNearRack();
        mediumButton.gameObject.SetActive(nearRack && mediumContainer != null);
        largeButton .gameObject.SetActive(nearRack && largeContainer  != null);

        // —— 奶茶机 ——
        bool nearMachine = teaMachine != null && teaMachine.IsNear(playerPos, interactDistance);

        // 注入按钮：有杯 + 近机器 + 杯子是空的 + 机器有可用杯位
        injectButton.gameObject.SetActive(
            nearMachine && heldCup != null && !heldCup.isContained && teaMachine.HasSlot);

        // 选择液体按钮：机器里有杯（已放入未取走）时出现，点击打开奶茶机交互界面
        chooseFluidButton.gameObject.SetActive(
            nearMachine && teaMachine != null && teaMachine.CurrentCup != null);

        // 机器取杯按钮：显隐 + 动态文字 "取走{杯型}{液体类型}"
        Cup medCup = teaMachine != null && teaMachine.HasTea(Cup.CupType.Medium) ? teaMachine.CurrentCup : null;
        Cup lrgCup = teaMachine != null && teaMachine.HasTea(Cup.CupType.Large)  ? teaMachine.CurrentCup : null;
        machineMediumButton.gameObject.SetActive(nearMachine && medCup != null);
        machineLargeButton .gameObject.SetActive(nearMachine && lrgCup != null);
        if (medCup != null) SetButtonLabel(machineMediumButton, $"取走{Cup.CupTypeName(Cup.CupType.Medium)}{medCup.teaType}");
        if (lrgCup != null) SetButtonLabel(machineLargeButton,  $"取走{Cup.CupTypeName(Cup.CupType.Large)}{lrgCup.teaType}");

        // —— 加热机 ——
        bool nearHeater = heaterMachine != null && heaterMachine.IsNear(playerPos, interactDistance);

        // 加热按钮：有杯 + 杯中有奶茶 + 未加热 + 近加热机 + 有空位
        heatButton.gameObject.SetActive(
            nearHeater && heldCup != null && heldCup.isContained && !heldCup.IsHot
            && heaterMachine.HasFreeSlot);

        // 加热机取杯按钮：显隐 + 动态文字 "取走热{杯型}{液体类型}"
        Cup hMedCup = heaterMachine != null ? heaterMachine.FindHeatedCup(Cup.CupType.Medium) : null;
        Cup hLrgCup = heaterMachine != null ? heaterMachine.FindHeatedCup(Cup.CupType.Large)  : null;
        heaterMediumButton.gameObject.SetActive(nearHeater && hMedCup != null);
        heaterLargeButton .gameObject.SetActive(nearHeater && hLrgCup != null);
        if (hMedCup != null) SetButtonLabel(heaterMediumButton, $"取走热{Cup.CupTypeName(Cup.CupType.Medium)}{hMedCup.teaType}");
        if (hLrgCup != null) SetButtonLabel(heaterLargeButton,  $"取走热{Cup.CupTypeName(Cup.CupType.Large)}{hLrgCup.teaType}");

        // —— 前台 ——
        bool nearCounter = counterDisplay != null && counterDisplay.IsNear(playerPos, interactDistance);
        counterPlaceButton.gameObject.SetActive(nearCounter && heldCup != null && heldCup.isContained);
        receptionButton.gameObject.SetActive(nearCounter && CustomerSpawner.Instance != null
            && CustomerSpawner.Instance.FrontReady);

        // —— 冰格（前制作台，等级不够不开放） ——
        bool nearIce = iceTray != null && iceTray.IsUnlocked && iceTray.IsNear(playerPos, interactDistance);
        bool trayClosed = iceTray == null || iceTray.State == IceTray.IceTrayState.Closed;
        bool trayOpen   = iceTray != null && iceTray.State == IceTray.IceTrayState.Open;
        iceOpenButton   .gameObject.SetActive(nearIce && trayClosed);
        iceCloseButton  .gameObject.SetActive(nearIce && trayOpen);
        iceTakeIceButton.gameObject.SetActive(nearIce && trayOpen && iceTray.HasIce
            && heldCup != null && heldCup.isContained);
    }

    void LateUpdate() => UpdateScrollFade();

    // ===== 近距离判定 =====

    bool IsNearRack()
    {
        if (rackRoot == null) return false;
        if (rackRenderers == null) rackRenderers = rackRoot.GetComponentsInChildren<Renderer>();
        if (rackRenderers.Length == 0)
            return Vector3.Distance(cam.transform.position, rackRoot.position) <= interactDistance;

        Bounds bounds = rackRenderers[0].bounds;
        for (int i = 1; i < rackRenderers.Length; i++) bounds.Encapsulate(rackRenderers[i].bounds);
        return IsNearBounds(bounds, cam.transform.position, interactDistance);
    }

    public static bool IsNearBounds(Bounds bounds, Vector3 point, float distance)
    {
        float dx = Mathf.Max(0f, Mathf.Abs(point.x - bounds.center.x) - bounds.extents.x);
        float dy = Mathf.Max(0f, Mathf.Abs(point.y - bounds.center.y) - bounds.extents.y);
        float dz = Mathf.Max(0f, Mathf.Abs(point.z - bounds.center.z) - bounds.extents.z);
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz) <= distance;
    }

    // ===== 杯架查找 =====

    void FindContainers()
    {
        GameObject rack = GameObject.Find(rackFrameName);
        if (rack == null) return;
        rackRoot = rack.transform;
        mediumContainer = FindCupInChildren(rack.transform, mediumCupName);
        largeContainer  = FindCupInChildren(rack.transform, largeCupName);
        if (mediumContainer == null) Debug.LogWarning("[CupInteract] 架子子集里找不到 " + mediumCupName);
        if (largeContainer  == null) Debug.LogWarning("[CupInteract] 架子子集里找不到 " + largeCupName);
    }

    Transform FindCupInChildren(Transform container, string namePrefix)
    {
        foreach (Transform child in container)
        {
            if (child.name.StartsWith(namePrefix)) return child;
            Transform found = FindCupInChildren(child, namePrefix);
            if (found != null) return found;
        }
        return null;
    }

    // ===== 按钮回调 =====

    public void OnGetMedium() { PickUpFromRack(mediumContainer, Cup.CupType.Medium); }
    public void OnGetLarge()  { PickUpFromRack(largeContainer,  Cup.CupType.Large); }

    void PickUpFromRack(Transform container, Cup.CupType type)
    {
        if (container == null) return;
        if (heldCup != null) Destroy(heldCup.gameObject);

        Transform template = container.childCount > 0 ? container.GetChild(0) : container;
        var cupObj = Instantiate(template, transform);
        cupObj.localPosition = holdOffset;
        cupObj.localRotation = Quaternion.Euler(heldCupEuler);
        Vector3 fullScale = template.lossyScale;
        cupObj.localScale = Vector3.Scale(fullScale, new Vector3(0.4f, 0.4f, 0.4f));

        heldCup = cupObj.GetComponent<Cup>();
        if (heldCup == null) heldCup = cupObj.gameObject.AddComponent<Cup>();
        heldCup.cupType = type;
        heldCup.baseScale = fullScale;
        heldCup.baseRotation = template.rotation;
        heldCup.Empty();

        foreach (Collider c in cupObj.GetComponentsInChildren<Collider>())
            Destroy(c);
    }

    // —— 奶茶机 ——

    public void OnInject()
    {
        if (heldCup == null || heldCup.isContained || teaMachine == null) return;
        teaMachine.PlaceCup(heldCup);
        heldCup = null; // 已经被机器接走了
        // 不再自动打开面板：改由「选择液体」按钮触发
    }

    /// <summary>放入奶茶杯后打开奶茶机交互界面；面板里的流体格子会自动指向这台机器</summary>
    void ShowMachinePanel()
    {
        if (teaMachine == null) return;

        if (machinePanel == null)
        {
            foreach (Transform t in FindObjectsOfType<Transform>(true))
            {
                if (t.name.Contains("奶茶机交互界面")) { machinePanel = t.gameObject; break; }
            }
        }

        if (machinePanel == null && machinePanelPrefab != null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            machinePanel = Instantiate(machinePanelPrefab, canvas != null ? canvas.transform : null, false);
            machinePanel.name = "奶茶机交互界面";
        }

        if (machinePanel == null)
        {
            Debug.LogWarning("[CupInteract] 找不到奶茶机交互界面：请在场景里放一个，或把 预制件/奶茶机交互界面.prefab 拖到 machinePanelPrefab 字段");
            return;
        }

        // 界面不在奶茶机子级时，FluidCellButton 找不到机器，这里统一补上
        foreach (FluidCellButton cell in machinePanel.GetComponentsInChildren<FluidCellButton>(true))
            if (cell.machine == null) cell.machine = teaMachine;

        // 关闭由面板自身的 PanelCloseButton 处理（挂在 顶栏/关闭按钮 上）

        machinePanel.SetActive(true);
    }

    public void OnGetMediumFromMachine() { PickUpFromMachine(Cup.CupType.Medium); }
    public void OnGetLargeFromMachine()  { PickUpFromMachine(Cup.CupType.Large); }

    void PickUpFromMachine(Cup.CupType type)
    {
        if (teaMachine == null) return;
        var (cup, worldScale) = teaMachine.TakeCup(type);
        if (cup == null) return;

        if (heldCup != null) Destroy(heldCup.gameObject);
        Transform cupObj = cup.transform;
        cupObj.SetParent(transform, false);
        cupObj.localPosition = holdOffset;
        cupObj.localRotation = Quaternion.Euler(heldCupEuler);
        cupObj.localScale = ToLocalScale(worldScale, transform);
        heldCup = cup;
    }

    // —— 加热机 ——

    public void OnHeat()
    {
        if (heldCup == null || !heldCup.isContained || heaterMachine == null) return;
        heaterMachine.PlaceCup(heldCup);
        heldCup = null;
    }

    public void OnGetMediumFromHeater() { PickUpFromHeater(Cup.CupType.Medium); }
    public void OnGetLargeFromHeater()  { PickUpFromHeater(Cup.CupType.Large);  }

    void PickUpFromHeater(Cup.CupType type)
    {
        if (heaterMachine == null) return;
        var (cup, worldScale) = heaterMachine.TakeCup(type);
        if (cup == null) return;

        if (heldCup != null) Destroy(heldCup.gameObject);
        Transform cupObj = cup.transform;
        cupObj.SetParent(transform, false);
        cupObj.localPosition = holdOffset;
        cupObj.localRotation = Quaternion.Euler(heldCupEuler);
        cupObj.localScale = ToLocalScale(worldScale, transform);
        heldCup = cup;
    }

    // —— 前台 ——

    public void OnPlaceOnCounter()
    {
        if (heldCup == null || !heldCup.isContained || counterDisplay == null) return;
        if (counterDisplay.TryPlaceCup(heldCup))
        {
            Destroy(heldCup.gameObject);
            heldCup = null;
        }
    }

    // ===== 工具 =====

    public static Vector3 ToLocalScale(Vector3 worldScale, Transform parent)
    {
        Vector3 ps = parent.lossyScale;
        return new Vector3(
            worldScale.x / Mathf.Max(0.0001f, ps.x),
            worldScale.y / Mathf.Max(0.0001f, ps.y),
            worldScale.z / Mathf.Max(0.0001f, ps.z));
    }

    void EnsureColliders(string containerName)
    {
        GameObject container = GameObject.Find(containerName);
        if (container == null) return;
        foreach (MeshFilter mf in container.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null)
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
    }

    // ===== UI（滚动视图 + 按钮） =====

    void EnsureUI()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        RectTransform content = CreateScrollView(canvas);

        // 交互按钮统一贴图（由 Inspector 引用 Assets/贴图/交互按钮.png，场景引用后才会进包）
        if (buttonSprite == null)
            Debug.LogWarning("[CupInteract] 交互按钮贴图未指定：选中 Main Camera，把 贴图/交互按钮.png 拖到 Button Sprite 字段");

        // 场景里已经摆好的冰格按钮优先接管：接管后和运行时创建的按钮一样塞进滚动视图，
        // 统一显隐、点击和布局；改名可在 Inspector 的 iceOpenButtonName 等字段调整
        TakeOverSceneButton(ref iceOpenButton,    iceOpenButtonName);
        TakeOverSceneButton(ref iceCloseButton,   iceCloseButtonName);
        TakeOverSceneButton(ref iceTakeIceButton, iceTakeIceButtonName);
        TakeOverSceneButton(ref receptionButton,  receptionButtonName);

        if (mediumButton       == null) mediumButton        = CreateButton(content, "获取中杯");
        if (largeButton        == null) largeButton         = CreateButton(content, "获取大杯");
        if (injectButton       == null) injectButton        = CreateButton(content, "放入奶茶杯");
        if (chooseFluidButton  == null) chooseFluidButton   = CreateButton(content, "选择液体");
        if (machineMediumButton == null) machineMediumButton = CreateButton(content, "获取中杯奶茶");
        if (machineLargeButton  == null) machineLargeButton  = CreateButton(content, "获取大杯奶茶");
        if (heatButton         == null) heatButton          = CreateButton(content, "加热奶茶");
        if (heaterMediumButton == null) heaterMediumButton  = CreateButton(content, "获取中杯热奶茶");
        if (heaterLargeButton  == null) heaterLargeButton   = CreateButton(content, "获取大杯热奶茶");
        if (counterPlaceButton == null) counterPlaceButton  = CreateButton(content, "放置奶茶");
        if (iceOpenButton      == null) iceOpenButton       = CreateButton(content, "打开冰格");
        if (iceCloseButton     == null) iceCloseButton      = CreateButton(content, "关闭冰格");
        if (iceTakeIceButton   == null) iceTakeIceButton    = CreateButton(content, "取冰");
        if (receptionButton    == null) receptionButton     = CreateButton(content, "接待");

        AddButtonToScroll(mediumButton);
        AddButtonToScroll(largeButton);
        AddButtonToScroll(injectButton);
        AddButtonToScroll(chooseFluidButton);
        AddButtonToScroll(machineMediumButton);
        AddButtonToScroll(machineLargeButton);
        AddButtonToScroll(heatButton);
        AddButtonToScroll(heaterMediumButton);
        AddButtonToScroll(heaterLargeButton);
        AddButtonToScroll(counterPlaceButton);
        AddButtonToScroll(iceOpenButton);
        AddButtonToScroll(iceCloseButton);
        AddButtonToScroll(iceTakeIceButton);
        AddButtonToScroll(receptionButton);

        mediumButton       .onClick.AddListener(OnGetMedium);
        largeButton        .onClick.AddListener(OnGetLarge);
        injectButton       .onClick.AddListener(OnInject);
        chooseFluidButton  .onClick.AddListener(ShowMachinePanel);
        machineMediumButton.onClick.AddListener(OnGetMediumFromMachine);
        machineLargeButton .onClick.AddListener(OnGetLargeFromMachine);
        heatButton         .onClick.AddListener(OnHeat);
        heaterMediumButton .onClick.AddListener(OnGetMediumFromHeater);
        heaterLargeButton  .onClick.AddListener(OnGetLargeFromHeater);
        counterPlaceButton .onClick.AddListener(OnPlaceOnCounter);
        iceOpenButton      .onClick.AddListener(() => { if (iceTray != null) iceTray.OnClickOpen(); });
        iceCloseButton     .onClick.AddListener(() => { if (iceTray != null) iceTray.OnClickClose(); });
        iceTakeIceButton   .onClick.AddListener(() => { if (iceTray != null) iceTray.OnClickTakeIce(); });
        receptionButton    .onClick.AddListener(() => { if (CustomerSpawner.Instance != null) CustomerSpawner.Instance.ReceiveFront(); });

        mediumButton       .gameObject.SetActive(false);
        largeButton        .gameObject.SetActive(false);
        injectButton       .gameObject.SetActive(false);
        chooseFluidButton  .gameObject.SetActive(false);
        machineMediumButton.gameObject.SetActive(false);
        machineLargeButton .gameObject.SetActive(false);
        heatButton         .gameObject.SetActive(false);
        heaterMediumButton .gameObject.SetActive(false);
        heaterLargeButton  .gameObject.SetActive(false);
        counterPlaceButton .gameObject.SetActive(false);
        iceOpenButton      .gameObject.SetActive(false);
        iceCloseButton     .gameObject.SetActive(false);
        iceTakeIceButton   .gameObject.SetActive(false);
        receptionButton    .gameObject.SetActive(false);
    }

    /// <summary>查找按钮子物体上的 Text 组件并设置文字；找不到就跳过</summary>
    static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        Text t = button.GetComponentInChildren<Text>(true);
        if (t != null) t.text = label;
    }

    /// <summary>把场景里已存在的按钮接管给本脚本管理；Inspector 已拖引用或找不到就跳过</summary>
    void TakeOverSceneButton(ref Button slot, string objectName)
    {
        if (slot != null || string.IsNullOrEmpty(objectName)) return;

        // 包含未激活的对象，场景里按钮默认隐藏时也能找到
        foreach (Button b in FindObjectsOfType<Button>(true))
        {
            if (b.gameObject.name != objectName) continue;
            slot = b;
            return;
        }
    }

    RectTransform CreateScrollView(Canvas canvas)
    {
        GameObject scrollGo = new GameObject("交互按钮滚动视图", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(canvas.transform, false);
        RectTransform rt = (RectTransform)scrollGo.transform;
        rt.anchorMin = rt.anchorMax = scrollViewAnchor;
        rt.sizeDelta = new Vector2(scrollViewSize.x * ButtonWidthScale, scrollViewSize.y);
        rt.anchoredPosition = scrollViewOffset;
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = viewportRt.offsetMax = Vector2.zero;
        viewportRt.pivot = new Vector2(0.5f, 1f);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRt = (RectTransform)contentGo.transform;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.content = contentRt;
        scrollRect.viewport = viewportRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        buttonViewport = viewportRt;
        buttonContent = contentRt;
        return contentRt;
    }

    void AddButtonToScroll(Button button)
    {
        if (button == null || buttonContent == null) return;

        // 场景里接管过来的按钮也统一塞进滚动视图，按同一套布局排列
        button.transform.SetParent(buttonContent, false);
        RectTransform rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float height = ButtonHeight;
        rt.sizeDelta = new Vector2(ButtonWidth, height);

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = ButtonWidth;
        layout.preferredHeight = height;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        // 场景按钮原有的 28 号字塞进 176 宽的小按钮会溢出，统一成和运行时按钮一样的字号
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
        }

        // 统一交互按钮贴图，颜色白色（场景按钮原有的颜色/贴图一并覆盖）
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            if (buttonSprite != null) image.sprite = buttonSprite;
            image.color = Color.white;
        }

        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        if (!scrollGroups.Contains(group)) scrollGroups.Add(group);
    }

    Button CreateButton(RectTransform parent, string label)
    {
        GameObject go = new GameObject(label + "按钮", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        RectTransform trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        Text text = textGo.GetComponent<Text>();
        text.font = UIFont.Get();
        text.fontSize = 12;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.text = label;

        return go.GetComponent<Button>();
    }

    void UpdateScrollFade()
    {
        if (buttonViewport == null) return;
        Rect viewRect = buttonViewport.rect;
        float range = Mathf.Max(1f, scrollFadeRange);

        foreach (CanvasGroup group in scrollGroups)
        {
            if (group == null || !group.gameObject.activeSelf) continue;

            RectTransform rt = (RectTransform)group.transform;
            rt.GetWorldCorners(corners);

            float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                float y = buttonViewport.InverseTransformPoint(corners[i]).y;
                if (y > top) top = y;
                if (y < bottom) bottom = y;
            }
            float height = Mathf.Max(1f, top - bottom);
            float overhang = Mathf.Max(0f, top - viewRect.yMax, viewRect.yMin - bottom);
            float alpha = Mathf.Clamp01(1f - overhang / Mathf.Min(height, range));
            group.alpha = alpha;
            group.blocksRaycasts = alpha > 0.1f;
        }
    }
}

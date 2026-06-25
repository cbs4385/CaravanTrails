using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Automatically created at runtime — no scene setup required. Just press Play.
/// Everything — managers, board, camera, UI — is spawned at runtime.
/// Board lives in the XZ plane; camera looks straight down from +Y.
/// </summary>
public class GameBootstrapper : MonoBehaviour
{
    static List<BuildingDefinition> _catalogBuildings;
    static List<UnitDefinition>     _catalogUnits;
    static Sprite                   _roundedSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<GameBootstrapper>() != null) return;
        // Phase 3 scene uses GameController directly — skip the board-game bootstrap.
        if (FindFirstObjectByType<GameController>() != null) return;
        new GameObject("Bootstrapper").AddComponent<GameBootstrapper>();
    }

    void Awake()
    {
        // Phase 3 scene: GameController drives the sim and owns the isometric camera.
        // Bootstrapper auto-created itself but must not override the scene setup.
        if (FindFirstObjectByType<GameController>() != null)
        {
            Destroy(gameObject);
            return;
        }

        var cfg = BuildConfig();

        _catalogBuildings = DefaultBuildings();
        _catalogUnits     = DefaultUnits();

        CreateManagers(cfg);

        var (towns, routes) = CreateBoard();
        RegisterGraph(towns, routes);

        SetupCamera();
        CreateUI();
    }

    // =========================================================================
    //  Config
    // =========================================================================

    static GameConfig BuildConfig()
    {
        var c = ScriptableObject.CreateInstance<GameConfig>();
        c.baseTradeFlowRate      = 10f;
        c.routeBaseCapacity      = 100f;
        c.crimeTradeReduction    = 0.40f;
        c.crimeSpawnPerFlow      = 0.04f;
        c.naturalCrimeGrowthRate = 0.03f;
        c.playerCrimeCompetition = 0.80f;
        c.crimeDecayRate         = 0.02f;
        c.playerTaxCut           = 0.70f;
        c.playerCrimeCut         = 0.60f;
        c.startingBalance        = 500f;
        c.monthLength            = 30;
        c.dayDuration            = 8f;
        return c;
    }

    // =========================================================================
    //  Managers  (all created before the board so singletons are ready)
    // =========================================================================

    static void CreateManagers(GameConfig cfg)
    {
        MakeMono<TradeGraph>      ("TradeGraph");
        MakeMono<SelectionManager>("SelectionManager");

        var gm = MakeMono<GameManager>("GameManager");
        gm.config = cfg;

        var tm = MakeMono<TurnManager>("TurnManager");
        tm.dayDuration = cfg.dayDuration;
        tm.config      = cfg;

        MakeMono<CaravanRouter>("CaravanRouter");

        var pa = MakeMono<PersonalAccount>("PersonalAccount");
        pa.config = cfg;

        var em = MakeMono<EconomyManager>("EconomyManager");
        em.config = cfg;

        var ts = MakeMono<TradeSimulator>("TradeSimulator");
        ts.config = cfg;

        var cm = MakeMono<CrimeManager>("CrimeManager");
        cm.config = cfg;

        MakeMono<BuildingManager> ("BuildingManager");
        MakeMono<UnitManager>    ("UnitManager");
        MakeMono<PlacementManager>("PlacementManager");
        MakeMono<StatsTracker>   ("StatsTracker");
    }

    static T MakeMono<T>(string name) where T : MonoBehaviour
    {
        return new GameObject(name).AddComponent<T>();
    }

    // =========================================================================
    //  Board  (XZ plane, camera looks down from +Y)
    // =========================================================================

    // Town layout (X, Z) — rendered at Y=0
    static readonly (string name, float x, float z, int pop, bool player)[] TownLayout =
    {
        ("Westport",   -4.5f,  2.0f, 220, false),
        ("Millhaven",  -1.0f,  3.5f, 260, false),
        ("Eastgate",    4.0f,  2.0f, 280, false),
        ("Southford",  -3.0f, -2.0f, 180, false),
        ("Crossroads",  0.0f,  0.0f, 150, true ),
    };

    // Off-screen silk road sources/destinations — high population generates trade pressure
    static readonly (string name, float x, float z, int pop, float prodPerPop, float demandPerPop)[] PhantomLayout =
    {
        // Eastern terminus: major exporter of silk and spices
        ("Samarkand",     18f,   1.5f, 700, 1.6f, 0.5f),
        // Western terminus: affluent importer, high demand
        ("Constantinople", -17f,  1.5f, 600, 0.5f, 1.5f),
        // Northern steppe route: balanced nomadic trade
        ("Bukhara",        -1f,  13f,  380, 1.1f, 0.8f),
        // Southern port: Indian Ocean goods, moderate importer
        ("Hormuz",         -3f, -13f,  450, 0.9f, 1.2f),
    };

    static (List<Town>, List<TradeRoute>) CreateBoard()
    {
        var towns = new List<Town>();
        foreach (var (n, x, z, pop, player) in TownLayout)
            towns.Add(SpawnTown(n, x, z, pop, player));

        Town W  = towns[0], M  = towns[1], E  = towns[2],
             S  = towns[3], CR = towns[4];

        // Visible routes
        var routes = new List<TradeRoute>
        {
            SpawnRoute(W,  M,  100f),
            SpawnRoute(M,  E,  100f),
            SpawnRoute(W,  S,   80f),
            SpawnRoute(S,  CR,  90f),
            SpawnRoute(CR, E,  110f),
            SpawnRoute(CR, M,  120f),
            SpawnRoute(CR, W,   70f),
        };

        // Phantom (off-screen) settlements driving silk road traffic
        var phantoms = new List<Town>();
        foreach (var (n, x, z, pop, prod, dem) in PhantomLayout)
            phantoms.Add(SpawnPhantomTown(n, x, z, pop, prod, dem));

        Town Sama = phantoms[0], Cons = phantoms[1],
             Buk  = phantoms[2], Horm = phantoms[3];

        // Silk road entry/exit routes — high capacity, drawn as faded lines off the edge
        routes.Add(SpawnPhantomRoute(Sama, E,   200f));
        routes.Add(SpawnPhantomRoute(Cons, W,   180f));
        routes.Add(SpawnPhantomRoute(Buk,  M,   150f));
        routes.Add(SpawnPhantomRoute(Horm, S,   160f));

        towns.AddRange(phantoms);
        return (towns, routes);
    }

    static Town SpawnTown(string townName, float x, float z, int pop, bool isPlayer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = townName;
        // Flat disc in XZ plane visible from camera above
        go.transform.position   = new Vector3(x, 0f, z);
        go.transform.localScale = new Vector3(0.55f, 0.04f, 0.55f);

        // Unlit so towns are vivid regardless of scene lighting
        var mat = CreateUrpUnlit(isPlayer ? new Color(0.2f, 0.8f, 1.0f) : new Color(0.75f, 0.75f, 0.75f));
        go.GetComponent<Renderer>().material = mat;

        // Replace capsule collider with a sphere for reliable raycasting
        Object.Destroy(go.GetComponent<CapsuleCollider>());
        var sc   = go.AddComponent<SphereCollider>();
        sc.radius = 0.9f;
        sc.center = Vector3.zero;

        var data = ScriptableObject.CreateInstance<TownData>();
        data.townName              = townName;
        data.basePopulation        = pop;
        data.goodsProductionPerPop = 1f;
        data.demandPerPop          = 0.8f;
        data.boardPosition         = new Vector2(x, z);
        data.isPlayerTown          = isPlayer;

        var town      = go.AddComponent<Town>();
        town.data     = data;
        if (isPlayer) town.taxRate = 0f; // player must actively set tax to earn income

        var view      = go.AddComponent<TownView>();
        view.town         = town;
        view.townRenderer = go.GetComponent<Renderer>();

        // Town name above disc (facing up toward camera)
        view.nameLabel  = WorldLabel(go.transform, new Vector3(0, 0.15f, 0),
                                     townName, isPlayer ? Color.cyan : Color.white, 5f);
        view.statsLabel = WorldLabel(go.transform, new Vector3(0, 0.15f, -0.6f),
                                     "", new Color(0.9f, 0.9f, 0.9f), 3.5f);

        if (!isPlayer)
        {
            var ai               = go.AddComponent<TownAI>();
            ai.town              = town;
            ai.buildingCatalogue = _catalogBuildings;
            ai.unitCatalogue     = _catalogUnits;
        }

        return town;
    }

    static TradeRoute SpawnRoute(Town a, Town b, float cap)
    {
        var go  = new GameObject($"Route_{a.data.townName}_{b.data.townName}");
        var mat = CreateUrpUnlit(new Color(0.3f, 0.9f, 0.4f));

        var lr = go.AddComponent<LineRenderer>();
        lr.material = mat;
        lr.useWorldSpace = true;

        var route      = go.AddComponent<TradeRoute>();
        route.townA    = a;
        route.townB    = b;
        route.capacity = cap;

        var view  = go.AddComponent<RouteView>();
        view.route = route;

        return route;
    }

    // Phantom town: invisible settlement off the map edge — no renderer, no collider
    static Town SpawnPhantomTown(string townName, float x, float z, int pop,
                                  float prodPerPop, float demandPerPop)
    {
        var go = new GameObject($"Phantom_{townName}");
        go.transform.position = new Vector3(x, 0f, z);

        var data = ScriptableObject.CreateInstance<TownData>();
        data.townName             = townName;
        data.basePopulation       = pop;
        data.goodsProductionPerPop = prodPerPop;
        data.demandPerPop         = demandPerPop;
        data.boardPosition        = new Vector2(x, z);
        data.isPlayerTown         = false;

        var town  = go.AddComponent<Town>();
        town.data    = data;
        town.taxRate = 0f;
        return town;
    }

    // Phantom route: participates in simulation and draws a faded off-screen line,
    // but has no RouteView / BoxCollider so it is not selectable
    static TradeRoute SpawnPhantomRoute(Town a, Town b, float cap)
    {
        var go  = new GameObject($"PhantomRoute_{a.data.townName}_{b.data.townName}");
        var mat = CreateUrpUnlit(new Color(0.55f, 0.48f, 0.28f)); // dusty caravan-road colour

        var lr = go.AddComponent<LineRenderer>();
        lr.material     = mat;
        lr.useWorldSpace = true;
        lr.startWidth   = 0.025f;
        lr.endWidth     = 0.025f;
        lr.positionCount = 2;
        lr.SetPosition(0, a.transform.position);
        lr.SetPosition(1, b.transform.position);

        var route      = go.AddComponent<TradeRoute>();
        route.townA    = a;
        route.townB    = b;
        route.capacity = cap;

        return route;
    }

    static void RegisterGraph(List<Town> towns, List<TradeRoute> routes)
    {
        foreach (var t in towns)  TradeGraph.Instance.RegisterTown(t);
        foreach (var r in routes) TradeGraph.Instance.RegisterRoute(r);
    }

    // =========================================================================
    //  Camera  — orthographic top-down
    // =========================================================================

    static void SetupCamera()
    {
        var existing = Camera.main;
        if (existing != null)
        {
            // A scene camera is already configured — keep it. Just ensure AudioListener exists.
            if (existing.GetComponent<AudioListener>() == null)
                existing.gameObject.AddComponent<AudioListener>();
            return;
        }

        // No scene camera — create a default top-down board-game camera.
        var go  = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = 6.5f;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.07f, 0.07f, 0.12f);
        cam.farClipPlane     = 50f;

        go.transform.position = new Vector3(0f, 20f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        go.AddComponent<AudioListener>();

        if (FindFirstObjectByType<Light>() == null)
        {
            var lightGO = new GameObject("Directional Light");
            var light   = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    // =========================================================================
    //  UI  — UGUI canvas with HUD, info panels, shop
    // =========================================================================

    void CreateUI()
    {
        _roundedSprite = CreateRoundedSprite(32, 6);

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }

        var canvas = MakeCanvas();

        var hudGO   = BuildHUD(canvas.transform);
        var townGO  = BuildTownPanel(canvas.transform);
        var routeGO = BuildRoutePanel(canvas.transform);
        var shopGO  = BuildShopPanel(canvas.transform);
        BuildStatsPanel(canvas.transform);
        BuildGameOverPanel(canvas.transform); // must be last — renders on top

        // UIManager wires the panels together
        var uim          = canvas.gameObject.AddComponent<UIManager>();
        uim.townPanel    = townGO;
        uim.routePanel   = routeGO;
        uim.shopPanel    = shopGO;
    }

    static Canvas MakeCanvas()
    {
        var go     = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // -------------------------------------------------------------------------
    //  HUD — top bar
    // -------------------------------------------------------------------------

    static GameObject BuildHUD(Transform canvasT)
    {
        var hud = Panel(canvasT, "HUD",
            new Vector2(0,1), new Vector2(1,1),
            new Vector2(0,-60), new Vector2(0,0),
            new Color(0.14f, 0.09f, 0.05f, 0.95f));

        var hl = hud.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment       = TextAnchor.MiddleLeft;
        hl.padding              = new RectOffset(12, 12, 6, 6);
        hl.spacing              = 18;
        hl.childForceExpandHeight = true;

        var hc                = hud.AddComponent<HUDController>();
        hc.dayLabel           = Lbl(hud.transform, "Day 0",       180, 20, color: new Color(0.92f, 0.84f, 0.70f));
        hc.balanceLabel       = Lbl(hud.transform, "Gold: 500",   180, 20, color: new Color(1.00f, 0.85f, 0.28f));
        hc.taxLabel           = Lbl(hud.transform, "Tax:   —",    150, 18, color: new Color(0.80f, 0.70f, 0.54f));
        hc.crimeLabel         = Lbl(hud.transform, "Crime: —",    150, 18, color: new Color(0.80f, 0.70f, 0.54f));
        hc.upkeepLabel        = Lbl(hud.transform, "Upkeep: —",   150, 18, color: new Color(0.80f, 0.70f, 0.54f));
        hc.placementHintLabel = Lbl(hud.transform, "",            340, 18, color: new Color(1.00f, 0.85f, 0.28f));
        hc.shopButton         = Btn(hud.transform, "Shop",         80, 32, new Color(0.52f, 0.34f, 0.08f));
        hc.statsButton        = Btn(hud.transform, "Rankings",    100, 32, new Color(0.36f, 0.22f, 0.08f));
        return hud;
    }

    // -------------------------------------------------------------------------
    //  Town info panel — right side
    // -------------------------------------------------------------------------

    static GameObject BuildTownPanel(Transform canvasT)
    {
        var panel = Panel(canvasT, "TownPanel",
            new Vector2(1,0), new Vector2(1,1),
            new Vector2(-290,60), new Vector2(0,-60),
            new Color(0.14f, 0.09f, 0.05f, 0.95f));

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment       = TextAnchor.UpperLeft;
        vl.padding              = new RectOffset(12,12,12,12);
        vl.spacing              = 6;
        vl.childForceExpandWidth = true;

        var tp            = panel.AddComponent<TownPanel>();
        tp.nameLabel      = Lbl(panel.transform, "Town",           266, 24, bold:true, color: new Color(0.96f, 0.88f, 0.68f));
        tp.populationLabel= Lbl(panel.transform, "Population: —",  266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        tp.tradeLabel     = Lbl(panel.transform, "Trade: —",       266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        tp.taxRevenueLabel= Lbl(panel.transform, "Tax Rev: —",     266, 20, color: new Color(0.82f, 0.72f, 0.56f));

        // Tax slider group (only shown for player town)
        var taxGrp = new GameObject("TaxGroup");
        taxGrp.transform.SetParent(panel.transform, false);
        var taxRT = taxGrp.AddComponent<RectTransform>();
        taxRT.sizeDelta = new Vector2(266, 56);
        var taxVl = taxGrp.AddComponent<VerticalLayoutGroup>();
        taxVl.childAlignment       = TextAnchor.UpperLeft;
        taxVl.spacing              = 2;
        taxVl.childForceExpandWidth = true;
        tp.taxControlGroup = taxGrp;
        tp.taxRateLabel    = Lbl(taxGrp.transform, "Tax Rate: 0%", 266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        tp.taxSlider       = MakeSlider(taxGrp.transform, 266, 28);

        tp.buildingsLabel = Lbl(panel.transform, "Buildings: 0", 266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        tp.openShopButton = Btn(panel.transform, "Open Shop", 266, 38, new Color(0.52f, 0.34f, 0.08f));
        return panel;
    }

    // -------------------------------------------------------------------------
    //  Route info panel — right side (same anchor, toggled by UIManager)
    // -------------------------------------------------------------------------

    static GameObject BuildRoutePanel(Transform canvasT)
    {
        var panel = Panel(canvasT, "RoutePanel",
            new Vector2(1,0), new Vector2(1,1),
            new Vector2(-290,60), new Vector2(0,-60),
            new Color(0.14f, 0.09f, 0.05f, 0.95f));

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment       = TextAnchor.UpperLeft;
        vl.padding              = new RectOffset(12,12,12,12);
        vl.spacing              = 6;
        vl.childForceExpandWidth = true;

        var rp               = panel.AddComponent<RoutePanel>();
        rp.routeNameLabel    = Lbl(panel.transform, "Route",           266, 24, bold:true, color: new Color(0.96f, 0.88f, 0.68f));
        rp.flowLabel         = Lbl(panel.transform, "Trade Flow: —",   266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        rp.naturalCrimeLabel = Lbl(panel.transform, "Natural Crime: —",266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        rp.playerCrimeLabel  = Lbl(panel.transform, "Controlled: —",   266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        rp.unitsLabel        = Lbl(panel.transform, "Units: 0",        266, 20, color: new Color(0.82f, 0.72f, 0.56f));
        rp.openShopButton    = Btn(panel.transform, "Open Shop", 266, 38, new Color(0.52f, 0.34f, 0.08f));
        return panel;
    }

    // -------------------------------------------------------------------------
    //  Shop modal — centred overlay
    // -------------------------------------------------------------------------

    static GameObject BuildShopPanel(Transform canvasT)
    {
        var panel = Panel(canvasT, "ShopPanel",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(-240,-300), new Vector2(240,300),
            new Color(0.12f, 0.07f, 0.03f, 0.97f));
        panel.SetActive(false);

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment       = TextAnchor.UpperCenter;
        vl.padding              = new RectOffset(12,12,12,12);
        vl.spacing              = 8;
        vl.childForceExpandWidth = true;

        var sp          = panel.AddComponent<ShopPanel>();
        sp.titleLabel   = Lbl(panel.transform, "Shop", 456, 26, bold:true,
                              color: new Color(0.96f, 0.88f, 0.68f),
                              align: TextAlignmentOptions.Center);

        sp.itemContainer  = MakeScrollContent(panel.transform, 456, 480);
        sp.shopItemPrefab = BuildShopItemPrefab();

        // Populate catalogue — use the shared static catalog so AI towns reference
        // the exact same definition objects as the shop (avoids duplicates).
        sp.buildingCatalogue = _catalogBuildings;
        sp.unitCatalogue     = _catalogUnits;

        sp.closeButton = Btn(panel.transform, "Close", 456, 38, new Color(0.52f, 0.16f, 0.06f));
        return panel;
    }

    // -------------------------------------------------------------------------
    //  Rankings + Income Graph panel
    // -------------------------------------------------------------------------

    static void BuildStatsPanel(Transform canvasT)
    {
        var panel = Panel(canvasT, "StatsPanel",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(-290,-270), new Vector2(290,270),
            new Color(0.12f, 0.07f, 0.03f, 0.97f));
        panel.SetActive(false);

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment       = TextAnchor.UpperCenter;
        vl.padding              = new RectOffset(14,14,12,12);
        vl.spacing              = 6;
        vl.childForceExpandWidth = true;

        var sp = panel.AddComponent<StatsPanel>();

        Lbl(panel.transform, "Rankings", 548, 24, bold:true, color: new Color(0.96f, 0.88f, 0.68f), align:TextAlignmentOptions.Center);

        // Five rank rows
        sp.rankRows = new TMPro.TextMeshProUGUI[5];
        for (int i = 0; i < 5; i++)
            sp.rankRows[i] = Lbl(panel.transform, "", 548, 22);

        // Separator
        var sep = new GameObject("Sep");
        sep.transform.SetParent(panel.transform, false);
        var sepRt = sep.AddComponent<RectTransform>();
        sepRt.sizeDelta = new Vector2(548, 2);
        sep.AddComponent<Image>().color = new Color(0.42f, 0.30f, 0.12f);

        Lbl(panel.transform, "Wealth Over Time", 548, 18,
            color: new Color(0.72f, 0.62f, 0.44f), align:TextAlignmentOptions.Center);

        // Graph image
        var graphGO = new GameObject("Graph");
        graphGO.transform.SetParent(panel.transform, false);
        var graphRt = graphGO.AddComponent<RectTransform>();
        graphRt.sizeDelta = new Vector2(548, 130);
        var le = graphGO.AddComponent<LayoutElement>();
        le.minHeight = 130; le.preferredHeight = 130;
        sp.graphImage = graphGO.AddComponent<RawImage>();
        sp.graphImage.color = Color.white;

        sp.closeButton = Btn(panel.transform, "Close", 548, 36, new Color(0.52f, 0.16f, 0.06f));
    }

    static void BuildGameOverPanel(Transform canvasT)
    {
        // Full-screen dim overlay — last child of canvas so it draws on top
        var overlay = Panel(canvasT, "GameOverPanel",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.82f));
        overlay.SetActive(false);

        // Centred content box
        var box = new GameObject("Box");
        box.transform.SetParent(overlay.transform, false);
        var boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(540, 290);
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.14f, 0.06f, 0.02f, 1f);
        if (_roundedSprite != null) { boxImg.sprite = _roundedSprite; boxImg.type = Image.Type.Sliced; }

        var vl = box.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment        = TextAnchor.UpperCenter;
        vl.padding               = new RectOffset(20, 20, 24, 20);
        vl.spacing               = 16;
        vl.childForceExpandWidth = true;

        Lbl(box.transform, "BANKRUPT", 500, 56,
            bold: true,
            color: new Color(0.90f, 0.28f, 0.06f),
            align: TextAlignmentOptions.Center);

        var gop           = overlay.AddComponent<GameOverPanel>();
        gop.subtitleLabel = Lbl(box.transform, "", 500, 80,
            color: new Color(0.88f, 0.80f, 0.62f),
            align: TextAlignmentOptions.Center);
        gop.restartButton = Btn(box.transform, "Play Again", 200, 44,
            new Color(0.22f, 0.48f, 0.10f));
    }

    static GameObject BuildShopItemPrefab()
    {
        var item = new GameObject("ShopItem_Template");
        item.SetActive(false);
        var rt  = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(432, 68);
        var itemImg = item.AddComponent<Image>();
        itemImg.color = new Color(0.20f, 0.13f, 0.07f, 1f);
        if (_roundedSprite != null) { itemImg.sprite = _roundedSprite; itemImg.type = Image.Type.Sliced; }
        var le  = item.AddComponent<LayoutElement>();
        le.minHeight = 68f;
        le.preferredHeight = 68f;

        var vl  = item.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(8, 8, 4, 4);
        vl.spacing = 1;
        vl.childForceExpandWidth = true;
        Lbl(item.transform, "Item Name",    432, 22, bold:true, color: new Color(0.96f, 0.88f, 0.68f));
        Lbl(item.transform, "Description", 432, 18, color: new Color(0.68f, 0.58f, 0.42f));
        Btn(item.transform,  "Buy", 80, 26, new Color(0.26f, 0.46f, 0.10f));
        return item;
    }

    // =========================================================================
    //  Default building / unit catalogue
    // =========================================================================

    static List<BuildingDefinition> DefaultBuildings() => new()
    {
        BDef("Customs House",  "Boosts tax efficiency by 25%.",      80f, 5f,
             BuildingEffectType.BoostTaxEfficiency,  0.25f, PlacementType.Town,  BuildingAlignment.Law),
        BDef("Market",         "Boosts town demand by 30%.",          60f, 4f,
             BuildingEffectType.BoostDemand,         0.30f, PlacementType.Town,  BuildingAlignment.Law),
        BDef("Granary",        "Boosts town supply by 20%.",          50f, 3f,
             BuildingEffectType.BoostSupply,         0.20f, PlacementType.Town,  BuildingAlignment.Law),
        BDef("Guard Post",     "Reduces crime on route by 15%/turn.", 70f, 5f,
             BuildingEffectType.ReduceCrime,         0.15f, PlacementType.Route, BuildingAlignment.Law),
        BDef("Safe House",     "Suppresses natural crime growth.",     90f, 6f,
             BuildingEffectType.SuppressNaturalCrime,0.20f, PlacementType.Route, BuildingAlignment.Criminal),
        BDef("Fence Network",  "Generates steady theft income.",      110f, 8f,
             BuildingEffectType.CrimeRevenue,        0.10f, PlacementType.Route, BuildingAlignment.Criminal),
    };

    static List<UnitDefinition> DefaultUnits() => new()
    {
        UDef("Guard",      "Patrols route; reduces crime rate.",              40f, 4f, UnitType.Guard,     0.10f),
        UDef("Inspector",  "Increases effective tax yield on route.",         55f, 5f, UnitType.Inspector, 0.12f),
        UDef("Bandit",     "Steals trade; competes with natural crime.",      35f, 3f, UnitType.Bandit,    0.10f),
        UDef("Crime Boss", "Strong organisation — heavily suppresses wild crime.",
                                                                              80f, 7f, UnitType.CrimeBoss, 0.25f),
    };

    static BuildingDefinition BDef(string n, string desc, float cost, float upk,
        BuildingEffectType eff, float mag, PlacementType pt, BuildingAlignment al,
        float radius = 2f, int unitCap = 2)
    {
        var d = ScriptableObject.CreateInstance<BuildingDefinition>();
        d.buildingName = n; d.description = desc; d.cost = cost; d.upkeepPerTurn = upk;
        d.effectType = eff; d.effectMagnitude = mag; d.placementType = pt; d.alignment = al;
        d.maxPerLocation  = 1;
        d.influenceRadius = radius;
        d.unitCapacity    = unitCap;
        return d;
    }

    static UnitDefinition UDef(string n, string desc, float cost, float upk, UnitType ut, float mag,
        float radius = 1.5f, float speed = 0.5f)
    {
        var d = ScriptableObject.CreateInstance<UnitDefinition>();
        d.unitName = n; d.description = desc; d.cost = cost; d.upkeepPerTurn = upk;
        d.unitType = ut; d.effectMagnitude = mag;
        d.influenceRadius = radius;
        d.moveSpeed       = speed;
        return d;
    }

    // =========================================================================
    //  Material helpers  (URP)
    // =========================================================================

    static Material CreateUrpMat(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        return m;
    }

    static Material CreateUrpUnlit(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.color = c;
        return m;
    }

    // =========================================================================
    //  World-space label
    // =========================================================================

    static TextMeshPro WorldLabel(Transform parent, Vector3 localPos, string text, Color col, float size)
    {
        var go  = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // Rotate so text lies flat, readable from above (camera looking straight down)
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale    = Vector3.one * 0.14f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = col;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.sortingOrder = 5;
        return tmp;
    }

    // =========================================================================
    //  UI helpers
    // =========================================================================

    static GameObject Panel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color bg)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img = go.AddComponent<Image>();
        img.color = bg;
        if (_roundedSprite != null) { img.sprite = _roundedSprite; img.type = Image.Type.Sliced; }
        return go;
    }

    static TextMeshProUGUI Lbl(Transform parent, string text, float w, float h,
        bool bold = false, Color? color = null,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go  = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = Mathf.Round(h * 0.68f);
        tmp.color     = color ?? Color.white;
        tmp.alignment = align;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    static Button Btn(Transform parent, string label, float w, float h, Color bg)
    {
        var go  = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        var bImg = go.AddComponent<Image>();
        bImg.color = bg;
        if (_roundedSprite != null) { bImg.sprite = _roundedSprite; bImg.type = Image.Type.Sliced; }
        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.highlightedColor = bg * 1.3f;
        btn.colors = cb;
        Lbl(go.transform, label, w, h, align: TextAlignmentOptions.Center);
        return btn;
    }

    static Slider MakeSlider(Transform parent, float w, float h)
    {
        var go  = new GameObject("Slider");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        var slider = go.AddComponent<Slider>();

        // Background
        var bg = new GameObject("Bg");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.24f, 0.16f, 0.08f);

        // Fill area
        var fa = new GameObject("FillArea");
        fa.transform.SetParent(go.transform, false);
        var faRt = fa.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(5, 0); faRt.offsetMax = new Vector2(-5, 0);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fa.transform, false);
        var fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0.1f, 1f);
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax  = Vector2.zero;
        fill.AddComponent<Image>().color = new Color(0.78f, 0.55f, 0.15f);
        slider.fillRect = fillRt;

        // Handle
        var ha = new GameObject("HandleArea");
        ha.transform.SetParent(go.transform, false);
        var haRt = ha.AddComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(8, 0); haRt.offsetMax = new Vector2(-8, 0);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(ha.transform, false);
        var hRt = handle.AddComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(18, 0);
        hRt.anchorMin = hRt.anchorMax = new Vector2(0.1f, 0.5f);
        var hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;
        slider.handleRect    = hRt;
        slider.targetGraphic = hImg;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = 0.1f;
        return slider;
    }

    // =========================================================================
    //  Rounded-corner sprite  (9-sliced so it stretches to any panel size)
    // =========================================================================

    static Sprite CreateRoundedSprite(int size, int r)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = RoundedAlpha(x, y, size, size, r);
        tex.SetPixels32(px);
        tex.Apply();
        float b = r;
        return Sprite.Create(tex, new Rect(0, 0, size, size),
                             new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect,
                             new Vector4(b, b, b, b));
    }

    static Color32 RoundedAlpha(int x, int y, int w, int h, int r)
    {
        bool inside;
        if      (x < r     && y < r)     inside = (x-r)*(x-r)     + (y-r)*(y-r)     <= r*r;
        else if (x > w-1-r && y < r)     inside = (x-(w-1-r))*(x-(w-1-r)) + (y-r)*(y-r)     <= r*r;
        else if (x < r     && y > h-1-r) inside = (x-r)*(x-r)     + (y-(h-1-r))*(y-(h-1-r)) <= r*r;
        else if (x > w-1-r && y > h-1-r) inside = (x-(w-1-r))*(x-(w-1-r)) + (y-(h-1-r))*(y-(h-1-r)) <= r*r;
        else inside = true;
        return inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
    }

    static Transform MakeScrollContent(Transform parent, float w, float h)
    {
        var go  = new GameObject("ScrollView");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        var sv = go.AddComponent<ScrollRect>();
        sv.horizontal = false; sv.vertical = true;

        var vp  = new GameObject("Viewport");
        vp.transform.SetParent(go.transform, false);
        var vpRt = vp.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
        var vpImg = vp.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.01f);
        var mask = vp.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        sv.viewport = vpRt;

        var content = new GameObject("Content");
        content.transform.SetParent(vp.transform, false);
        var cRt = content.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0,1); cRt.anchorMax = new Vector2(1,1);
        cRt.pivot     = new Vector2(0.5f,1);
        cRt.offsetMin = cRt.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;
        vlg.padding = new RectOffset(4,4,4,4);
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sv.content = cRt;

        return content.transform;
    }
}

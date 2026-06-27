using System;
using System.IO;
using GameCore.Events;
using GameCore.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// §8.1 Phase 2: minimal interactive shell — function first, no art.
// Drop onto any GameObject in a blank scene; creates its own Canvas at runtime.
[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    [Header("Simulation")]
    public SimConfigAsset ConfigAsset;   // assign in Inspector, or leave null for defaults
    public int Seed;

    // ── Difficulty ────────────────────────────────────────────────────────────

    enum Difficulty { Easy, Normal, Hard }
    Difficulty _difficulty = Difficulty.Normal;

    // ── Sim ──────────────────────────────────────────────────────────────────

    Simulator        _sim;
    int              _pendingOrgDelta;
    EventOption      _pendingEventChoice;
    UpgradePurchase  _pendingUpgrade;
    int              _eventsPaid;
    int              _eventsResisted;
    bool             _autoTick;
    float       _autoTimer;
    const float AutoInterval = 0.75f;

    // ── UI refs ───────────────────────────────────────────────────────────────

    static Sprite _roundedSprite;

    Slider _taxSl, _skimSl, _bribeSl, _unorgSl;
    Text   _taxVal, _skimVal, _bribeVal, _unorgVal;
    Text   _tickTxt, _purseTxt, _coffersTxt, _legitTxt, _trafficShareTxt, _tradeDealTxt, _heatTxt;
    Text   _qualTxt, _safetyTxt, _repTxt, _orgLvTxt, _statusTxt;
    Text   _autoLbl;
    Text   _collUpgTxt, _heatUpgTxt, _connUpgTxt, _townUpgTxt, _routeUpgTxt;

    // ── World map ─────────────────────────────────────────────────────────────
    GameObject _worldMapPanel;
    Image[]    _routeLineImgs;
    Text[]     _townQualTxts;
    Text[]     _townDetailTxts;  // tax + share line per town (rivals) / share line (player)

    // ── Rankings ──────────────────────────────────────────────────────────────
    GameObject _rankingsPanel;
    RawImage   _rankingsGraph;
    Text       _rankStatsTxt;
    Text       _highScoresTxt;
    HighScoreStore _highScores;

    // ── Title screen ──────────────────────────────────────────────────────────

    GameObject _titlePanel;

    // ── Event modal ───────────────────────────────────────────────────────────

    GameObject _eventPanel;
    Text       _evHeadTxt, _evBodyTxt, _evOptALbl, _evOptBLbl;

    // ── Game-over overlay ─────────────────────────────────────────────────────

    GameObject _gameOverPanel;
    Text       _goIconTxt, _goTitleTxt, _goSubTxt, _goStatsTxt;

    // ── Audio ─────────────────────────────────────────────────────────────────

    SoundManager   _sfx;
    bool           _gameOverSoundPlayed;

    // ── Tutorial ──────────────────────────────────────────────────────────────

    TutorialSystem _tutorial;

    // ── Town view ─────────────────────────────────────────────────────────────

    TownPresenter        _townView;
    TradeRouteVisualizer _routeViz;
    CaravanManager       _caravanMgr;

    // ── Programmatic API (§8.9) ───────────────────────────────────────────────

    public static GameController Instance { get; private set; }

    // Sliders
    public void API_SetTaxRate(float v)         { _taxSl.value  = Mathf.Clamp(v, _taxSl.minValue,   _taxSl.maxValue); }
    public void API_SetSkimFraction(float v)    { _skimSl.value = Mathf.Clamp(v, _skimSl.minValue,  _skimSl.maxValue); }
    public void API_SetBribeAmount(float v)     { _bribeSl.value= Mathf.Clamp(v, _bribeSl.minValue, _bribeSl.maxValue); }
    public void API_SetUnorgCrime(float v)      { _unorgSl.value= Mathf.Clamp(v, _unorgSl.minValue, _unorgSl.maxValue); }
    public void API_SetOrgDelta(int d)          { _pendingOrgDelta = d; }

    // Buttons
    public void API_Tick()                      { DoTick(); }
    public void API_ToggleAuto()                { ToggleAuto(); }
    public void API_NewGame()                   { NewGame(); Refresh(); }
    public void API_Begin()                     { _titlePanel?.SetActive(false); }
    public void API_QueueUpgrade(UpgradePurchase up)     { _pendingUpgrade = up; Refresh(); }
    public void API_BuyCollection()   { API_QueueUpgrade(UpgradePurchase.Collection); }
    public void API_BuyHeatDecay()    { API_QueueUpgrade(UpgradePurchase.HeatDecay); }
    public void API_BuyConnections()  { API_QueueUpgrade(UpgradePurchase.Connections); }
    public void API_BuyTownInvest()   { API_QueueUpgrade(UpgradePurchase.TownInvestment); }
    public void API_BuyRoute()        { API_QueueUpgrade(UpgradePurchase.RouteImprovement); }
    public void API_ChooseEvent(EventOption opt){ _pendingEventChoice = opt; _eventPanel?.SetActive(false); }

    // Save / load
    public void API_Save() { SaveGame(); }
    public void API_Load() { LoadGame(); }
    // Difficulty (0=Easy, 1=Normal, 2=Hard)
    public void API_SetDifficulty(int d) { _difficulty = (Difficulty)Mathf.Clamp(d, 0, 2); }

    // State reads
    public WorldState  API_GetState()           => _sim?.State;
    public SimConfig   API_GetConfig()          => _sim?.Config;
    public float       API_GetTaxRate()         => _taxSl != null ? _taxSl.value : 0f;
    public float       API_GetSkimFraction()    => _skimSl != null ? _skimSl.value : 0f;
    public bool        API_IsAutoTick()         => _autoTick;
    public bool        API_IsTitleVisible()     => _titlePanel != null && _titlePanel.activeSelf;
    public bool        API_IsEventPending()     => _eventPanel != null && _eventPanel.activeSelf;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        LoadHighScores();
        NewGame(); BuildUI(); Refresh();
        _sfx = gameObject.AddComponent<SoundManager>();
        _sfx.PlayAmbient();
        _tutorial = gameObject.AddComponent<TutorialSystem>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        _townView   = FindAnyObjectByType<TownPresenter>();
        _routeViz   = FindAnyObjectByType<TradeRouteVisualizer>();
        _caravanMgr = FindAnyObjectByType<CaravanManager>();
        _townView?.ResetVisuals();
        // Seed caravan manager with initial attractiveness so traffic is visible before first tick
        if (_caravanMgr != null)
            _caravanMgr.OnTick(_sim.Config.BaseTrafficVolume * 0.5f);
    }

    void Update()
    {
        if (!_autoTick || _sim.State.IsGameOver) return;
        _autoTimer += Time.deltaTime;
        if (_autoTimer >= AutoInterval) { _autoTimer = 0f; DoTick(); }
    }

    // ── Sim control ───────────────────────────────────────────────────────────

    void NewGame()
    {
        var cfg = BuildDifficultyConfig();
        _sim = new Simulator(cfg, Seed);
        _pendingOrgDelta       = 0;
        _pendingEventChoice    = EventOption.None;
        _pendingUpgrade        = UpgradePurchase.None;
        _eventsPaid            = 0;
        _eventsResisted        = 0;
        _autoTimer             = 0f;
        _autoTick              = false;
        _gameOverSoundPlayed   = false;
        if (_autoLbl != null) _autoLbl.text = "Auto: OFF";
        _gameOverPanel?.SetActive(false);
        _eventPanel?.SetActive(false);
        _townView?.ResetVisuals();
        _tutorial?.ResetForNewGame();
    }

    void DoTick()
    {
        if (_sim.State.IsGameOver) return;
        if (_eventPanel != null && _eventPanel.activeSelf) return; // await player response
        _sim.Tick(new PlayerInput
        {
            TaxRate                   = _taxSl.value,
            SkimFraction              = _skimSl.value,
            BribeAmount               = _bribeSl.value,
            UnorganizedCrimeIntensity = _unorgSl.value,
            OrganizedCrimeLevelDelta  = _pendingOrgDelta,
            Upgrade                   = _pendingUpgrade,
            EventChoice               = _pendingEventChoice,
        });
        _pendingOrgDelta    = 0;
        _pendingUpgrade     = UpgradePurchase.None;
        _pendingEventChoice = EventOption.None;
        _sfx?.PlayTick();
        var tele = _sim.Telemetry;
        if (_caravanMgr != null && tele.Count > 0)
            _caravanMgr.OnTick(tele[tele.Count - 1].TrafficVolume);
        if (_sim.State.PendingEvent != null)
            ShowEventPanel(_sim.State.PendingEvent);
        Refresh();
    }

    void Refresh()
    {
        var s   = _sim.State;
        var cfg = _sim.Config;
        var tele = _sim.Telemetry;

        string diffTag = _difficulty == Difficulty.Easy ? "  <color=#3aaa5a>[Easy]</color>"
                       : _difficulty == Difficulty.Hard ? "  <color=#cc4422>[Hard]</color>"
                       : "";
        _tickTxt.text    = $"Tick  <b>{s.Tick}</b>{diffTag}";
        _purseTxt.text   = $"<color=#907050>Purse</color>    <b>§{s.Purse:N0}</b>";
        _coffersTxt.text = $"<color=#907050>Coffers</color>  <b>§{s.Coffers:N0}</b>";

        float lastContrib = tele.Count > 0 ? tele[tele.Count - 1].CoffersContribution : 0f;
        float legitBuffer = lastContrib * cfg.LegitimacyHeatBufferPerCoffersUnit;
        float legitNorm   = Mathf.Clamp01(lastContrib / (cfg.TributePerTick * 5f));
        _legitTxt.text = $"<color=#3a7a3a>Legit</color>    {Bar(legitNorm)}  −{legitBuffer:F2} heat";

        float lastShare = tele.Count > 0 ? tele[tele.Count - 1].PlayerTrafficShare : 1f / 5f;
        bool rivalsActive = s.RivalTowns != null && s.RivalTowns.Length > 0;
        _trafficShareTxt.gameObject.SetActive(rivalsActive);
        if (rivalsActive)
        {
            _trafficShareTxt.text = $"<color=#4a8abf>Traffic</color>  {Bar(Mathf.Clamp01(lastShare / 0.50f))}  {lastShare:P0}";
            _trafficShareTxt.color = lastShare < 0.18f
                ? new Color(0.85f, 0.50f, 0.25f)
                : lastShare > 0.28f
                    ? new Color(0.42f, 0.85f, 0.72f)
                    : new Color(0.82f, 0.72f, 0.48f);
        }

        if (s.TradeDealTicksRemaining > 0)
        {
            _tradeDealTxt.gameObject.SetActive(true);
            float dealFrac = Mathf.Clamp01((float)s.TradeDealTicksRemaining / cfg.TradeDealDurationTicks);
            _tradeDealTxt.text  = $"<color=#2ad4b0>Trade Deal</color>  {Bar(dealFrac)}  {s.TradeDealTicksRemaining} ticks";
            _tradeDealTxt.color = new Color(0.16f, 0.83f, 0.69f);
        }
        else
        {
            _tradeDealTxt.gameObject.SetActive(false);
        }

        _qualTxt.text    = $"<color=#907050>Town</color>     {Bar(s.TownQuality)}  {s.TownQuality:P0}";
        _safetyTxt.text  = $"<color=#907050>Safety</color>   {Bar(s.Safety)}  {s.Safety:P0}";
        _repTxt.text     = $"<color=#907050>Rep</color>      {Bar(s.Reputation)}  {s.Reputation:P0}";
        _heatTxt.text  = HeatMood(s.Heat);
        _heatTxt.color = HeatColor(s.Heat);
        _orgLvTxt.text   = $"<color=#907050>Org Crime</color>  <b>Lv {s.OrganizedCrimeLevel}</b>";


        float collCost  = cfg.UpgradeCollectionCostBase
            * Mathf.Pow(cfg.UpgradeCollectionCostScalePerLevel, s.CollectionUpgradeLevel);
        float heatCost  = cfg.UpgradeHeatDecayCostBase
            * Mathf.Pow(cfg.UpgradeHeatDecayCostScalePerLevel, s.HeatDecayUpgradeLevel);
        float connCost  = cfg.UpgradeConnectionsCostBase
            * Mathf.Pow(cfg.UpgradeConnectionsCostScalePerLevel, s.ConnectionsLevel);
        float townCost  = cfg.UpgradeTownInvestmentCostBase
            * Mathf.Pow(cfg.UpgradeTownInvestmentCostScalePerLevel, s.TownInvestmentLevel);
        float routeCost = cfg.UpgradeRouteImprovementCostBase
            * Mathf.Pow(cfg.UpgradeRouteImprovementCostScalePerLevel, s.RouteImprovementLevel);

        _collUpgTxt.text  = $"Collect  Lv {s.CollectionUpgradeLevel}  §{collCost:F0}" +
            (_pendingUpgrade == UpgradePurchase.Collection    ? " [q]" : "");
        _heatUpgTxt.text  = $"H.Decay  Lv {s.HeatDecayUpgradeLevel}  §{heatCost:F0}" +
            (_pendingUpgrade == UpgradePurchase.HeatDecay     ? " [q]" : "");
        _connUpgTxt.text  = $"Connect  Lv {s.ConnectionsLevel}  §{connCost:F0}" +
            (_pendingUpgrade == UpgradePurchase.Connections   ? " [q]" : "");
        _townUpgTxt.text  = $"Town Inv  Lv {s.TownInvestmentLevel}  ¢{townCost:F0}" +
            (_pendingUpgrade == UpgradePurchase.TownInvestment? " [q]" : "");
        _routeUpgTxt.text = $"Route Imp  Lv {s.RouteImprovementLevel}  ¢{routeCost:F0}" +
            (_pendingUpgrade == UpgradePurchase.RouteImprovement ? " [q]" : "");

        if (s.IsGameOver)
            ShowGameOverPanel(s);
        else
            _statusTxt.text = string.Empty;

        _townView?.Refresh(s);
        _routeViz?.Refresh(s);
        RefreshWorldMap();
        RefreshRankings();

        if (!s.IsGameOver)
        {
            var lastEvt = tele.Count > 0 ? tele[tele.Count - 1].EventFired : GameCore.Events.EventType.None;
            _tutorial?.Check(s, lastEvt, lastContrib);
        }
    }

    void ToggleAuto()
    {
        _autoTick  = !_autoTick;
        _autoTimer = 0f;
        if (_autoLbl != null)
            _autoLbl.text = _autoTick ? "Auto: ON " : "Auto: OFF";
    }

    // ── Difficulty config ─────────────────────────────────────────────────────

    SimConfig BuildDifficultyConfig()
    {
        var cfg = ConfigAsset != null ? ConfigAsset.Config : new SimConfig();
        switch (_difficulty)
        {
            case Difficulty.Easy:
                cfg.RivalIncursionChance                = 0.05f;
                cfg.RivalIncursionPressurePerSharePoint = 0.12f;
                cfg.RivalIncursionTributeCost           = 30f;
                cfg.WealthWinThreshold                  = 2500f;
                cfg.AuditThreshold                      = 88f;
                cfg.RivalQualityGainRate                = 0.003f;
                cfg.TributePerTick                      = 4f;
                cfg.InspectorVisitChance                = 0.03f;
                break;
            case Difficulty.Hard:
                cfg.RivalIncursionChance                = 0.14f;
                cfg.RivalIncursionPressurePerSharePoint = 0.50f;
                cfg.WealthWinThreshold                  = 5000f;
                cfg.AuditThreshold                      = 60f;
                cfg.RivalQualityGainRate                = 0.012f;
                cfg.TributePerTick                      = 9f;
                cfg.MerchantComplaintChance             = 0.18f;
                cfg.InspectorVisitChance                = 0.08f;
                break;
            // Normal: ship defaults — no changes needed
        }
        return cfg;
    }

    void BeginGame(Difficulty d)
    {
        _difficulty = d;
        _titlePanel?.SetActive(false);
        NewGame();
        Refresh();
    }

    // ── High scores ───────────────────────────────────────────────────────────

    [Serializable] class HighScoreEntry
    {
        public string EndReason;
        public float  FinalPurse;
        public int    FinalTick;
        public string Difficulty;
    }
    [Serializable] class HighScoreStore { public System.Collections.Generic.List<HighScoreEntry> Entries = new System.Collections.Generic.List<HighScoreEntry>(); }

    static string HighScorePath => Path.Combine(Application.persistentDataPath, "highscores.json");

    void LoadHighScores()
    {
        _highScores = File.Exists(HighScorePath)
            ? JsonUtility.FromJson<HighScoreStore>(File.ReadAllText(HighScorePath))
            : new HighScoreStore();
    }

    void TryAddHighScore(WorldState s)
    {
        if (_highScores == null) _highScores = new HighScoreStore();
        _highScores.Entries.Add(new HighScoreEntry
        {
            EndReason  = s.EndReason.ToString(),
            FinalPurse = s.Purse,
            FinalTick  = s.Tick,
            Difficulty = _difficulty.ToString(),
        });
        _highScores.Entries.Sort((a, b) => b.FinalPurse.CompareTo(a.FinalPurse));
        if (_highScores.Entries.Count > 5) _highScores.Entries.RemoveRange(5, _highScores.Entries.Count - 5);
        File.WriteAllText(HighScorePath, JsonUtility.ToJson(_highScores));
        if (_highScoresTxt != null) RefreshRankings();
    }

    // ── Save / load ───────────────────────────────────────────────────────────

    [Serializable]
    class SaveBundle
    {
        public WorldState State;
        public float TaxRate;
        public float SkimFraction;
        public float BribeAmount;
        public float UnorgCrime;
        public int   Seed;
    }

    static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    void SaveGame()
    {
        var bundle = new SaveBundle
        {
            State        = _sim.State.Clone(),
            TaxRate      = _taxSl.value,
            SkimFraction = _skimSl.value,
            BribeAmount  = _bribeSl.value,
            UnorgCrime   = _unorgSl.value,
            Seed         = Seed,
        };
        bundle.State.PendingEvent = null;   // don't persist mid-event state
        File.WriteAllText(SavePath, JsonUtility.ToJson(bundle));
        _statusTxt.text = "Saved.";
    }

    void LoadGame()
    {
        if (!File.Exists(SavePath)) { _statusTxt.text = "No save found."; return; }
        var bundle = JsonUtility.FromJson<SaveBundle>(File.ReadAllText(SavePath));
        // JsonUtility deserializes null class refs as empty objects — sanitize
        if (bundle.State.PendingEvent != null
            && bundle.State.PendingEvent.Type == GameCore.Events.EventType.None)
            bundle.State.PendingEvent = null;
        Seed = bundle.Seed;
        var cfg = ConfigAsset != null ? ConfigAsset.Config : new SimConfig();
        _sim = new Simulator(cfg, Seed, initialState: bundle.State);
        _pendingOrgDelta     = 0;
        _pendingUpgrade      = UpgradePurchase.None;
        _pendingEventChoice  = EventOption.None;
        _eventsPaid          = 0;
        _eventsResisted      = 0;
        _autoTick            = false;
        _gameOverSoundPlayed = false;
        if (_autoLbl != null) _autoLbl.text = "Auto: OFF";
        _gameOverPanel?.SetActive(false);
        _eventPanel?.SetActive(false);
        _taxSl.value   = bundle.TaxRate;
        _skimSl.value  = bundle.SkimFraction;
        _bribeSl.value = bundle.BribeAmount;
        _unorgSl.value = bundle.UnorgCrime;
        Refresh();
        _statusTxt.text = "Loaded.";
    }

    // ── Display helpers ───────────────────────────────────────────────────────

    // Pillar 4: never reveal exact Heat value or audit threshold — ambient mood only
    static string HeatMood(float h)
    {
        if (h < 20) return "The markets are peaceful.";
        if (h < 45) return "Whispers stir in the bazaar.";
        if (h < 70) return "Officials ask pointed questions.";
        return "A shadow falls over the prefecture.";
    }

    static Color HeatColor(float h) =>
        h < 20 ? new Color(0.40f, 1.00f, 0.40f) :
        h < 45 ? new Color(1.00f, 1.00f, 0.40f) :
        h < 70 ? new Color(1.00f, 0.60f, 0.20f) :
                 new Color(1.00f, 0.30f, 0.30f);

    static string Bar(float v)
    {
        int on = Mathf.RoundToInt(Mathf.Clamp01(v) * 8);
        return new string('█', on) + new string('░', 8 - on);
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        _roundedSprite = CreateRoundedSprite(32, 6);

        // Canvas
        var cGO = new GameObject("Canvas");
        var cv  = cGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = cGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cs.matchWidthOrHeight  = 0.5f;
        cGO.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)cGO.transform;

        // EventSystem is required for UI input — create one if absent
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        const float PW = 295f;  // panel width

        // Left panel: anchored to left screen edge, full height
        var lpGO = new GameObject("Controls");
        lpGO.transform.SetParent(root, false);
        var lp       = lpGO.AddComponent<RectTransform>();
        lp.anchorMin = Vector2.zero;
        lp.anchorMax = new Vector2(0f, 1f);
        lp.pivot     = Vector2.zero;
        lp.offsetMin = Vector2.zero;
        lp.offsetMax = new Vector2(PW, 0f);
        BgImg(lp, new Color(0.14f, 0.09f, 0.05f, 0.93f));

        // Title strip pinned to top of left panel
        var titleGO = new GameObject("TitleBg");
        titleGO.transform.SetParent(lp, false);
        var titleRT       = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = Vector2.one;
        titleRT.pivot     = new Vector2(0f, 1f);
        titleRT.offsetMin = new Vector2(0f, -35f);
        titleRT.offsetMax = Vector2.zero;
        BgImg(titleRT, new Color(0.10f, 0.06f, 0.03f, 0.97f));
        MkTxt(titleRT, "The Prefect's Cut", 18, 0, 0, PW, 35, TextAnchor.MiddleCenter, new Color(1.00f, 0.92f, 0.72f));

        // Right panel: anchored to right screen edge, full height
        var rpGO = new GameObject("State");
        rpGO.transform.SetParent(root, false);
        var rp       = rpGO.AddComponent<RectTransform>();
        rp.anchorMin = new Vector2(1f, 0f);
        rp.anchorMax = Vector2.one;
        rp.pivot     = new Vector2(1f, 0f);
        rp.offsetMin = new Vector2(-PW, 0f);
        rp.offsetMax = Vector2.zero;
        BgImg(rp, new Color(0.14f, 0.09f, 0.05f, 0.93f));

        // ── Left panel: controls ───────────────────────────────────────────

        const float xp = 10f, cw = PW - 20f;
        float y = 670f;

        MkTxt(lp, "─ CONTROLS ─", 15, xp, y, cw, 20f, TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
        y -= 26f;

        SliderRow(lp, "Tax Rate",     ref y, xp, cw, 0f, 0.60f, 0.15f, v => _taxVal.text   = $"{v:P0}",  out _taxSl,   out _taxVal);
        SliderRow(lp, "Skim",         ref y, xp, cw, 0f, 1.00f, 0.10f, v => _skimVal.text  = $"{v:P0}",  out _skimSl,  out _skimVal);
        SliderRow(lp, "Bribe / tick", ref y, xp, cw, 0f, 20f,   0.00f, v => _bribeVal.text = $"§{v:F0}", out _bribeSl, out _bribeVal);
        SliderRow(lp, "Street crime", ref y, xp, cw, 0f, 1.00f, 0.00f, v => _unorgVal.text = $"{v:P0}",  out _unorgSl, out _unorgVal);

        y -= 6f;
        MkTxt(lp, "Org Crime", 15, xp, y, 95f, 26f, TextAnchor.MiddleLeft, new Color(0.85f, 0.74f, 0.52f));
        MkBtn(lp, "−", xp + 90f, y, 30f, 26f, () => _pendingOrgDelta--);
        MkBtn(lp, "+", xp + 125f, y, 30f, 26f, () => _pendingOrgDelta++);
        y -= 34f;

        y -= 6f;
        MkTxt(lp, "─ UPGRADES ─", 14, xp, y, cw, 18f, TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
        y -= 22f;

        _collUpgTxt = MkTxt(lp, "Collection  Lv 0  §60", 14, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.85f, 0.76f, 0.58f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => { _pendingUpgrade = UpgradePurchase.Collection; Refresh(); },
            new Color(0.30f, 0.20f, 0.08f), coinSound: true);
        y -= 28f;

        _heatUpgTxt = MkTxt(lp, "H.Decay  Lv 0  §80", 14, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.85f, 0.76f, 0.58f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => { _pendingUpgrade = UpgradePurchase.HeatDecay; Refresh(); },
            new Color(0.30f, 0.20f, 0.08f), coinSound: true);
        y -= 28f;

        _connUpgTxt = MkTxt(lp, "Connect  Lv 0  §75", 14, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.85f, 0.76f, 0.58f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => { _pendingUpgrade = UpgradePurchase.Connections; Refresh(); },
            new Color(0.30f, 0.20f, 0.08f), coinSound: true);
        y -= 28f;

        MkTxt(lp, "─ TOWN / ROUTE ─", 12, xp, y, cw, 16f, TextAnchor.MiddleLeft, new Color(0.80f, 0.68f, 0.38f));
        y -= 20f;

        _townUpgTxt = MkTxt(lp, "Town Inv  Lv 0  §70", 14, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.72f, 0.85f, 0.68f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => { _pendingUpgrade = UpgradePurchase.TownInvestment; Refresh(); },
            new Color(0.14f, 0.32f, 0.12f), coinSound: true);
        y -= 28f;

        _routeUpgTxt = MkTxt(lp, "Route Imp  Lv 0  §100", 14, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.72f, 0.85f, 0.68f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => { _pendingUpgrade = UpgradePurchase.RouteImprovement; Refresh(); },
            new Color(0.14f, 0.32f, 0.12f), coinSound: true);
        y -= 28f;

        y -= 8f;
        MkBtn(lp, "▶  NEXT TICK", xp, y, cw, 40f, DoTick, new Color(0.62f, 0.40f, 0.10f));
        y -= 46f;

        var autoBtn = MkBtn(lp, "Auto: OFF", xp, y, 130f, 26f, ToggleAuto, new Color(0.36f, 0.22f, 0.08f));
        _autoLbl = autoBtn.GetComponentInChildren<Text>();
        MkBtn(lp, "New Game", xp + 140f, y, 130f, 26f, () => { NewGame(); Refresh(); });

        // ── Right panel: state readouts ────────────────────────────────────

        const float rx = 10f, tw = PW - 20f;
        float ry = 680f;

        MkTxt(rp, "─ STATE ─", 15, rx, ry, tw, 20f, TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
        ry -= 26f;

        _tickTxt    = MkTxt(rp, "Tick  <b>0</b>", 17, rx, ry, tw, 24f, TextAnchor.MiddleLeft, new Color(1.00f, 0.88f, 0.58f));
        ry -= 34f;

        _purseTxt   = StatLine(rp, rx, tw, ref ry);
        _purseTxt.color = new Color(1.00f, 0.88f, 0.55f);
        _coffersTxt = StatLine(rp, rx, tw, ref ry);
        _coffersTxt.color = new Color(1.00f, 0.88f, 0.55f);
        _legitTxt   = StatLine(rp, rx, tw, ref ry);
        _legitTxt.color = new Color(0.62f, 0.92f, 0.62f);
        _trafficShareTxt = StatLine(rp, rx, tw, ref ry);
        _tradeDealTxt    = StatLine(rp, rx, tw, ref ry);

        ry -= 6f;
        BgImg(MkRT(rp, "Div1", rx, ry, tw - 24f, 1f), new Color(0.42f, 0.30f, 0.12f));
        ry -= 9f;

        _heatTxt   = StatLine(rp, rx, tw, ref ry);
        _qualTxt   = StatLine(rp, rx, tw, ref ry);
        _safetyTxt = StatLine(rp, rx, tw, ref ry);
        _repTxt    = StatLine(rp, rx, tw, ref ry);

        ry -= 6f;
        BgImg(MkRT(rp, "Div2", rx, ry, tw - 24f, 1f), new Color(0.42f, 0.30f, 0.12f));
        ry -= 9f;

        _orgLvTxt  = StatLine(rp, rx, tw, ref ry);

        ry -= 14f;
        _statusTxt = MkTxt(rp, string.Empty, 15, rx, ry, tw, 30f, TextAnchor.MiddleLeft, Color.white);
        ry -= 38f;
        MkBtn(rp, "WORLD MAP", rx, ry, tw / 2f - 4f, 28f,
            () => { _worldMapPanel.SetActive(true); RefreshWorldMap(); },
            new Color(0.10f, 0.20f, 0.32f));
        MkBtn(rp, "RANKINGS", rx + tw / 2f + 4f, ry, tw / 2f - 4f, 28f,
            () => { _rankingsPanel.SetActive(true); RefreshRankings(); },
            new Color(0.24f, 0.18f, 0.06f));
        ry -= 34f;
        MkBtn(rp, "Save", rx, ry, tw / 2f - 4f, 24f, SaveGame,
            new Color(0.12f, 0.24f, 0.14f));
        MkBtn(rp, "Load", rx + tw / 2f + 4f, ry, tw / 2f - 4f, 24f, LoadGame,
            new Color(0.22f, 0.14f, 0.06f));

        BuildWorldMapPanel(root);
        BuildRankingsPanel(root);
        BuildEventPanel(root);
        BuildGameOverScreen(root);
        BuildTitleScreen(root);   // topmost — covers everything until Begin
    }

    // ── Event modal construction ──────────────────────────────────────────────

    void BuildEventPanel(RectTransform root)
    {
        const float CW = 540f, CH = 290f;

        var go = new GameObject("EventPanel");
        go.transform.SetParent(root, false);
        _eventPanel = go;
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        BgImg(rt, new Color(0.05f, 0.02f, 0.01f, 0.82f));

        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(rt, false);
        var card = cardGO.AddComponent<RectTransform>();
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot     = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CW, CH);
        card.anchoredPosition = Vector2.zero;
        BgImg(card, new Color(0.13f, 0.08f, 0.04f, 0.97f));

        // Headline
        _evHeadTxt = MkTxt(card, "Event", 21, 16f, CH - 52f, CW - 32f, 36f,
            TextAnchor.UpperCenter, new Color(1.00f, 0.88f, 0.55f));

        // Divider
        BgImg(MkRT(card, "Div", 20f, CH - 64f, CW - 40f, 1f),
            new Color(0.42f, 0.30f, 0.12f));

        // Body text (word-wrapped)
        _evBodyTxt = MkTxt(card, "", 13, 20f, CH - 148f, CW - 40f, 78f,
            TextAnchor.UpperLeft, new Color(0.82f, 0.72f, 0.52f));
        _evBodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Option buttons — side by side
        float bw = (CW - 56f) / 2f;
        var btnA = MkBtn(card, "Option A", 18f, 18f, bw, 46f,
            () => { _eventsPaid++; _pendingEventChoice = EventOption.OptionA; _eventPanel.SetActive(false); },
            new Color(0.22f, 0.44f, 0.18f));
        _evOptALbl = btnA.GetComponentInChildren<Text>();
        _evOptALbl.fontSize = 12;

        var btnB = MkBtn(card, "Option B", 20f + bw + 18f, 18f, bw, 46f,
            () => { _eventsResisted++; _pendingEventChoice = EventOption.OptionB; _eventPanel.SetActive(false); },
            new Color(0.48f, 0.20f, 0.08f));
        _evOptBLbl = btnB.GetComponentInChildren<Text>();
        _evOptBLbl.fontSize = 12;

        go.SetActive(false);
    }

    void ShowEventPanel(PendingEvent evt)
    {
        _evHeadTxt.text = evt.Headline;
        _evBodyTxt.text = evt.BodyText;
        _evOptALbl.text = evt.OptionALabel;
        _evOptBLbl.text = evt.OptionBLabel;
        if      (evt.Type == GameCore.Events.EventType.TradeDelegation)    _sfx?.PlayEventTrade();
        else if (evt.Type == GameCore.Events.EventType.SeasonalHarvest)   _sfx?.PlayEventTrade();
        else if (evt.Type == GameCore.Events.EventType.DivertedCaravan)   _sfx?.PlayEventDiverted();
        else if (evt.Type == GameCore.Events.EventType.SeasonalBanditSurge) _sfx?.PlayEventDiverted();
        else                                                               _sfx?.PlayEvent();
        _eventPanel.SetActive(true);
        // Pause auto-tick so player must respond before sim advances
        _autoTick = false;
        if (_autoLbl != null) _autoLbl.text = "Auto: OFF";
    }

    // ── Title screen construction ─────────────────────────────────────────────

    void BuildTitleScreen(RectTransform root)
    {
        const float CW = 520f, CH = 370f;

        // Full-screen dark overlay
        var go = new GameObject("TitleScreen");
        go.transform.SetParent(root, false);
        _titlePanel = go;
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        BgImg(rt, new Color(0.07f, 0.04f, 0.015f, 0.97f));

        // Centre card
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(rt, false);
        var card = cardGO.AddComponent<RectTransform>();
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot     = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CW, CH);
        card.anchoredPosition = Vector2.zero;

        // Title
        MkTxt(card, "THE PREFECT'S CUT", 40, 0f, CH - 68f, CW, 56f,
            TextAnchor.UpperCenter, new Color(1.00f, 0.90f, 0.60f));

        // Ornament divider
        MkTxt(card, "─────  ★  ─────", 13, 0f, CH - 112f, CW, 20f,
            TextAnchor.UpperCenter, new Color(0.60f, 0.44f, 0.20f));

        // Subtitle
        MkTxt(card, "A game of wealth, influence,\nand plausible deniability.", 13,
            40f, CH - 172f, CW - 80f, 52f,
            TextAnchor.UpperCenter, new Color(0.82f, 0.70f, 0.48f));

        // Hint
        MkTxt(card, "Enrich yourself before the Emperor notices.", 11,
            40f, CH - 238f, CW - 80f, 20f,
            TextAnchor.UpperCenter, new Color(0.52f, 0.38f, 0.22f));

        // Lower rule
        BgImg(MkRT(card, "Rule", 40f, 96f, CW - 80f, 1f),
            new Color(0.38f, 0.26f, 0.10f));

        // Difficulty label
        MkTxt(card, "SELECT DIFFICULTY", 11, 0f, 98f, CW, 14f,
            TextAnchor.UpperCenter, new Color(0.52f, 0.40f, 0.24f));

        // Difficulty buttons
        float dbw = (CW - 60f) / 3f;
        MkBtn(card, "EASY",   10f,               24f, dbw, 46f,
            () => BeginGame(Difficulty.Easy),   new Color(0.14f, 0.36f, 0.20f));
        MkBtn(card, "NORMAL", 20f + dbw,         24f, dbw, 46f,
            () => BeginGame(Difficulty.Normal), new Color(0.62f, 0.40f, 0.10f));
        MkBtn(card, "HARD",   30f + dbw * 2f,    24f, dbw, 46f,
            () => BeginGame(Difficulty.Hard),   new Color(0.52f, 0.12f, 0.08f));

        // Strategy hints under each button
        var hc = new Color(0.60f, 0.60f, 0.60f);
        MkTxt(card, "org 1–2 viable",     9, 10f,             74f, dbw, 12f, TextAnchor.UpperCenter, hc);
        MkTxt(card, "org 2 recommended",  9, 20f + dbw,       74f, dbw, 12f, TextAnchor.UpperCenter, hc);
        MkTxt(card, "org 3 required",     9, 30f + dbw * 2f,  74f, dbw, 12f, TextAnchor.UpperCenter, hc);
    }

    // ── Game-over overlay construction ────────────────────────────────────────

    void BuildGameOverScreen(RectTransform root)
    {
        const float CW = 520f, CH = 420f;

        // Full-screen dim
        var overlayGO = new GameObject("GameOver");
        overlayGO.transform.SetParent(root, false);
        _gameOverPanel = overlayGO;
        var overlay    = overlayGO.AddComponent<RectTransform>();
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        BgImg(overlay, new Color(0.05f, 0.02f, 0.01f, 0.88f));

        // Centred card
        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(overlay, false);
        var card       = cardGO.AddComponent<RectTransform>();
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot     = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CW, CH);
        card.anchoredPosition = Vector2.zero;
        BgImg(card, new Color(0.12f, 0.08f, 0.04f, 0.97f));

        // Icon ★ / ✕
        _goIconTxt = MkTxt(card, "★", 44, 0f, CH - 72f, CW, 58f,
            TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.15f));

        // Title
        _goTitleTxt = MkTxt(card, "WEALTH WIN", 26, 0f, CH - 136f, CW, 46f,
            TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.15f));

        // Subtitle
        _goSubTxt = MkTxt(card, "", 13, 20f, CH - 168f, CW - 40f, 24f,
            TextAnchor.UpperCenter, new Color(0.90f, 0.80f, 0.62f));

        // Divider
        BgImg(MkRT(card, "Div", 20f, CH - 182f, CW - 40f, 1f),
            new Color(0.42f, 0.30f, 0.12f));

        // Stats block (up to 8 lines)
        _goStatsTxt = MkTxt(card, "", 13, 28f, 58f, CW - 56f, 172f,
            TextAnchor.UpperLeft, new Color(0.88f, 0.80f, 0.62f));

        // Play Again button
        MkBtn(card, "Play Again", (CW - 220f) * 0.5f, 12f, 220f, 40f,
            () => { NewGame(); Refresh(); },
            new Color(0.52f, 0.34f, 0.08f));

        overlayGO.SetActive(false);
    }

    void ShowGameOverPanel(WorldState s)
    {
        _gameOverPanel.SetActive(true);
        bool win = s.EndReason == EndReason.WealthWin;
        if (!_gameOverSoundPlayed)
        {
            _gameOverSoundPlayed = true;
            if (win) _sfx?.PlayWin(); else _sfx?.PlayLose();
            TryAddHighScore(s);
        }

        _goIconTxt.text  = win ? "★" : "✕";
        _goIconTxt.color = win ? new Color(1.00f, 0.85f, 0.15f) : new Color(1.00f, 0.32f, 0.18f);

        _goTitleTxt.text  = EndReasonTitle(s.EndReason);
        _goTitleTxt.color = _goIconTxt.color;

        _goSubTxt.text = EndReasonFlavour(s);

        int nAudit = 0, nRival = 0, nMerchant = 0, nInspector = 0, nTrade = 0, nDiverted = 0, nSeasonal = 0;
        foreach (var r in _sim.Telemetry)
        {
            if      (r.EventFired == GameCore.Events.EventType.AuditWarning)        nAudit++;
            else if (r.EventFired == GameCore.Events.EventType.RivalIncursion)      nRival++;
            else if (r.EventFired == GameCore.Events.EventType.MerchantComplaint)   nMerchant++;
            else if (r.EventFired == GameCore.Events.EventType.InspectorVisit)      nInspector++;
            else if (r.EventFired == GameCore.Events.EventType.TradeDelegation)     nTrade++;
            else if (r.EventFired == GameCore.Events.EventType.DivertedCaravan)     nDiverted++;
            else if (r.EventFired == GameCore.Events.EventType.SeasonalHarvest     ||
                     r.EventFired == GameCore.Events.EventType.SeasonalGovernorVisit||
                     r.EventFired == GameCore.Events.EventType.SeasonalBanditSurge  ||
                     r.EventFired == GameCore.Events.EventType.SeasonalAuditSeason) nSeasonal++;
        }
        int nTotal = nAudit + nRival + nMerchant + nInspector + nTrade + nDiverted + nSeasonal;
        string eventLines = nTotal == 0 ? "" :
            $"\nEvents faced      {nTotal}  ({_eventsPaid} paid · {_eventsResisted} refused)\n" +
            $"  Audit {nAudit}  Rival {nRival}  Guild {nMerchant}  Inspect {nInspector}\n" +
            $"  Trade deal {nTrade}  Diverted {nDiverted}  Seasonal {nSeasonal}";

        _goStatsTxt.text =
            $"Ticks survived    {s.Tick}   [{_difficulty}]\n" +
            $"Final purse       §{s.Purse:N0}\n" +
            $"Town coffers      §{s.Coffers:N0}\n" +
            $"Town quality      {Bar(s.TownQuality)}  {s.TownQuality:P0}\n" +
            $"Safety            {Bar(s.Safety)}  {s.Safety:P0}\n" +
            $"Reputation        {Bar(s.Reputation)}  {s.Reputation:P0}" +
            eventLines;

        _statusTxt.text = string.Empty;
    }

    static string EndReasonTitle(EndReason r)
    {
        switch (r)
        {
            case EndReason.WealthWin:          return "WEALTH WIN";
            case EndReason.AuditArrest:        return "ARRESTED";
            case EndReason.BankruptcyCollapse: return "BANKRUPTCY";
            case EndReason.RivalOverthrow:     return "OVERTHROWN";
            default:                           return r.ToString().ToUpper();
        }
    }

    static string EndReasonFlavour(WorldState s)
    {
        switch (s.EndReason)
        {
            case EndReason.WealthWin:
            {
                float maxShare = 0f;
                if (s.RivalTowns != null)
                    foreach (var r in s.RivalTowns)
                        if (r.TrafficShare > maxShare) maxShare = r.TrafficShare;
                if (maxShare > 0.30f)
                    return "You enriched yourself despite fierce rival prefects. The Emperor is pleased.";
                if (maxShare > 0.18f)
                    return "You outmaneuvered the silk road's rival prefects. A worthy Prefect indeed.";
                return "The route is yours. A worthy Prefect of the Empire.";
            }
            case EndReason.AuditArrest:        return "The Imperial auditors have come for you.";
            case EndReason.BankruptcyCollapse: return "The treasury is empty. The town falters.";
            case EndReason.RivalOverthrow:
            {
                string rivalName = "A rival prefect";
                if (s.RivalTowns != null)
                {
                    float maxShare = 0f; int maxIdx = -1;
                    for (int i = 0; i < s.RivalTowns.Length; i++)
                        if (s.RivalTowns[i].TrafficShare > maxShare)
                        { maxShare = s.RivalTowns[i].TrafficShare; maxIdx = i; }
                    if (maxIdx >= 0 && maxIdx < TownNames.Length)
                        rivalName = $"The prefect of {TownNames[maxIdx]}";
                }
                return $"{rivalName} has seized control of the route.";
            }
            default:                           return "Your time in office is over.";
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static Font _font;
    static Font GetFont()
    {
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }

    static RectTransform MkRT(RectTransform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static Image BgImg(RectTransform rt, Color c)
    {
        var img = rt.gameObject.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        if (_roundedSprite != null) { img.sprite = _roundedSprite; img.type = Image.Type.Sliced; }
        return img;
    }

    static Text MkTxt(RectTransform parent, string text, int size,
        float x, float y, float w, float h, TextAnchor anchor, Color color)
    {
        var rt = MkRT(parent, "Txt", x, y, w, h);
        var t  = rt.gameObject.AddComponent<Text>();
        t.font = GetFont(); t.fontSize = size; t.color = color;
        t.alignment = anchor; t.text = text;
        t.resizeTextForBestFit = false;
        return t;
    }

    // A blank stat line: pre-positioned, ready for Refresh() to fill in.
    static Text StatLine(RectTransform parent, float x, float w, ref float y)
    {
        const float sh = 20f, sg = 6f;
        var t = MkTxt(parent, string.Empty, 14, x, y, w, sh, TextAnchor.MiddleLeft, Color.white);
        y -= sh + sg;
        return t;
    }

    Button MkBtn(RectTransform parent, string label,
        float x, float y, float w, float h,
        Action onClick, Color? bg = null, bool coinSound = false)
    {
        var rt  = MkRT(parent, "Btn", x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg ?? new Color(0.36f, 0.22f, 0.08f);
        if (_roundedSprite != null) { img.sprite = _roundedSprite; img.type = Image.Type.Sliced; }
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            if (coinSound) _sfx?.PlayCoin(); else _sfx?.PlayClick();
            onClick();
        });

        // Label stretched over button
        var ltRT = MkRT(rt, "Label", 0, 0, w, h);
        ltRT.anchorMin = Vector2.zero;
        ltRT.anchorMax = Vector2.one;
        ltRT.offsetMin = ltRT.offsetMax = Vector2.zero;
        var lt = ltRT.gameObject.AddComponent<Text>();
        lt.font = GetFont(); lt.fontSize = 14; lt.color = Color.white;
        lt.alignment = TextAnchor.MiddleCenter; lt.text = label;
        return btn;
    }

    static Slider MkSlider(RectTransform parent,
        float x, float y, float w, float h, float min, float max, float val)
    {
        var rt = MkRT(parent, "Slider", x, y, w, h);
        BgImg(rt, new Color(0.24f, 0.16f, 0.08f));

        // Fill: Image.Type.Filled so Slider can drive fillAmount directly
        var fillRT  = MkRT(rt, "Fill", 0, 0, 0, 0);
        var fillImg = fillRT.gameObject.AddComponent<Image>();
        fillImg.color      = new Color(0.78f, 0.55f, 0.15f);
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;

        // Handle slide area (inset by handle radius on each side)
        float hr   = h * 0.55f;
        var hsaRT  = MkRT(rt, "HandleArea", hr, 0f, w - 2f * hr, h);

        // Handle
        var handleRT  = MkRT(hsaRT, "Handle", 0, 0, h * 1.1f, h * 1.1f);
        handleRT.anchorMin        = new Vector2(0f, 0.5f);
        handleRT.anchorMax        = new Vector2(0f, 0.5f);
        handleRT.pivot            = new Vector2(0.5f, 0.5f);
        handleRT.anchoredPosition = Vector2.zero;
        handleRT.sizeDelta        = new Vector2(h * 1.1f, h * 0.1f);
        var handleImg = handleRT.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.96f, 0.82f, 0.50f);

        var sl          = rt.gameObject.AddComponent<Slider>();
        sl.fillRect     = fillRT;
        sl.handleRect   = handleRT;
        sl.targetGraphic = handleImg;
        sl.direction    = Slider.Direction.LeftToRight;
        sl.minValue     = min;
        sl.maxValue     = max;
        sl.value        = val;
        return sl;
    }

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

    // ── World-map data ────────────────────────────────────────────────────────

    static readonly string[] TownNames = { "Westport", "Millhaven", "Eastgate", "Southford", "Crossroads" };

    // Canvas positions in 1280×720 reference space; Crossroads is the player town
    static readonly Vector2[] TownPos = {
        new Vector2(330f, 400f),  // Westport  (left of centre, clear of left panel)
        new Vector2(470f, 530f),  // Millhaven
        new Vector2(870f, 500f),  // Eastgate
        new Vector2(730f, 175f),  // Southford
        new Vector2(620f, 340f),  // Crossroads (player)
    };

    // Route connections (indices into TownPos); mirrors GameBootstrapper topology
    static readonly (int a, int b)[] RouteEdges = {
        (0, 1), (0, 4), (1, 2), (1, 4), (2, 4), (2, 3), (4, 3),
    };
    const int PlayerTown = 4;  // Crossroads

    // ── World-map panel ───────────────────────────────────────────────────────

    void BuildWorldMapPanel(RectTransform root)
    {
        var go = new GameObject("WorldMap");
        go.transform.SetParent(root, false);
        _worldMapPanel = go;
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        BgImg(rt, new Color(0.04f, 0.02f, 0.01f, 0.96f));

        MkTxt(rt, "SILK ROAD — TRADE NETWORK", 22, 0f, 678f, 1280f, 34f,
            TextAnchor.MiddleCenter, new Color(1.00f, 0.88f, 0.55f));
        MkTxt(rt, "Click your town to return.", 11, 0f, 654f, 1280f, 18f,
            TextAnchor.MiddleCenter, new Color(0.50f, 0.40f, 0.26f));
        MkBtn(rt, "✕  CLOSE", 20f, 680f, 110f, 26f,
            () => _worldMapPanel.SetActive(false));

        // Route lines drawn first so town nodes sit on top
        _routeLineImgs = new Image[RouteEdges.Length];
        for (int i = 0; i < RouteEdges.Length; i++)
        {
            var (a, b) = RouteEdges[i];
            _routeLineImgs[i] = DrawMapLine(rt, TownPos[a], TownPos[b],
                4f, new Color(0.40f, 0.28f, 0.14f, 0.70f));
        }

        // Town nodes
        const float ND = 52f;
        _townQualTxts  = new Text[TownNames.Length];
        _townDetailTxts = new Text[TownNames.Length];
        for (int i = 0; i < TownNames.Length; i++)
        {
            bool isPlayer = i == PlayerTown;
            var  p        = TownPos[i];

            var nodeRT = MkRT(rt, $"Town_{TownNames[i]}", p.x - ND / 2f, p.y - ND / 2f, ND, ND);
            BgImg(nodeRT, isPlayer
                ? new Color(0.60f, 0.38f, 0.08f, 0.95f)
                : new Color(0.20f, 0.13f, 0.07f, 0.90f));
            MkTxt(nodeRT, TownNames[i].Substring(0, 1), 24, 0f, ND / 2f - 14f, ND, 26f,
                TextAnchor.UpperCenter, Color.white);

            MkTxt(rt, TownNames[i], 11, p.x - 55f, p.y - ND / 2f - 20f, 110f, 16f,
                TextAnchor.MiddleCenter,
                isPlayer ? new Color(1.00f, 0.88f, 0.55f) : new Color(0.68f, 0.58f, 0.42f));

            _townQualTxts[i] = MkTxt(rt, "████████", 9, p.x - 44f, p.y - ND / 2f - 36f, 88f, 14f,
                TextAnchor.MiddleCenter, new Color(0.40f, 0.80f, 0.40f));

            _townDetailTxts[i] = MkTxt(rt, "", 9, p.x - 55f, p.y - ND / 2f - 52f, 110f, 14f,
                TextAnchor.MiddleCenter, new Color(0.55f, 0.45f, 0.30f));

            if (isPlayer)
            {
                MkTxt(rt, "← YOUR TOWN", 10, p.x + ND / 2f + 6f, p.y - 8f, 100f, 18f,
                    TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
                var btn = nodeRT.gameObject.AddComponent<Button>();
                btn.targetGraphic = nodeRT.gameObject.GetComponent<Image>();
                btn.onClick.AddListener(() => _worldMapPanel.SetActive(false));
            }
        }

        go.SetActive(false);
    }

    static Image DrawMapLine(RectTransform parent, Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 diff = to - from;
        float len = diff.magnitude;
        float deg = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        var go = new GameObject("RouteLine");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;
        rt.sizeDelta        = new Vector2(len, thickness);
        rt.localRotation    = Quaternion.Euler(0f, 0f, deg);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    void RefreshWorldMap()
    {
        if (_worldMapPanel == null || !_worldMapPanel.activeSelf) return;
        var s = _sim.State;
        var t = _sim.Telemetry;
        float traffic = t.Count > 0 ? t[t.Count - 1].TrafficVolume : _sim.Config.BaseTrafficVolume * 0.5f;
        float tNorm   = Mathf.Clamp01(traffic / (_sim.Config.BaseTrafficVolume * 1.5f));

        for (int i = 0; i < _routeLineImgs.Length; i++)
        {
            var (a, b) = RouteEdges[i];
            bool playerRoute = a == PlayerTown || b == PlayerTown;
            float br = playerRoute ? Mathf.Lerp(0.30f, 1.00f, tNorm) : 0.35f;
            _routeLineImgs[i].color = new Color(0.72f * br, 0.52f * br, 0.26f * br, 0.85f);
        }

        // Compute player traffic share from rival states
        float rivalShareSum = 0f;
        if (s.RivalTowns != null)
            foreach (var r in s.RivalTowns)
                rivalShareSum += r.TrafficShare;
        float playerShare = Mathf.Clamp01(1f - rivalShareSum);

        for (int i = 0; i < _townQualTxts.Length; i++)
        {
            float q;
            if (i == PlayerTown)
            {
                q = s.TownQuality;
                if (_townDetailTxts[i] != null)
                    _townDetailTxts[i].text = $"{playerShare:P0} of traffic";
            }
            else
            {
                var rival = s.RivalTowns != null && i < s.RivalTowns.Length ? s.RivalTowns[i] : null;
                q = rival?.Quality ?? (0.55f + Mathf.Sin(i * 2.1f) * 0.14f);
                if (_townDetailTxts[i] != null)
                    _townDetailTxts[i].text = rival != null
                        ? $"tax {rival.TaxRate:P0} · {rival.TrafficShare:P0}"
                        : "";
            }

            int on = Mathf.RoundToInt(Mathf.Clamp01(q) * 8);
            _townQualTxts[i].text  = new string('█', on) + new string('░', 8 - on);
            _townQualTxts[i].color = Color.Lerp(
                new Color(0.85f, 0.32f, 0.18f), new Color(0.32f, 0.85f, 0.38f), q);
        }
    }

    // ── Rankings panel ────────────────────────────────────────────────────────

    void BuildRankingsPanel(RectTransform root)
    {
        const float CW = 760f, CH = 500f;

        var go = new GameObject("Rankings");
        go.transform.SetParent(root, false);
        _rankingsPanel = go;
        var overlay = go.AddComponent<RectTransform>();
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        BgImg(overlay, new Color(0.04f, 0.02f, 0.01f, 0.92f));

        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(overlay, false);
        var card = cardGO.AddComponent<RectTransform>();
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot     = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CW, CH);
        card.anchoredPosition = Vector2.zero;
        BgImg(card, new Color(0.10f, 0.07f, 0.03f, 0.97f));

        MkTxt(card, "THE LEDGER", 22, 0f, CH - 48f, CW, 40f,
            TextAnchor.MiddleCenter, new Color(1.00f, 0.88f, 0.55f));
        MkBtn(card, "✕", CW - 38f, CH - 38f, 30f, 30f,
            () => _rankingsPanel.SetActive(false));

        // Legend
        MkTxt(card, "■ Purse   ■ Coffers   ■ Heat", 11, 24f, CH - 68f, CW - 48f, 16f,
            TextAnchor.MiddleRight, new Color(0.68f, 0.62f, 0.40f));

        // Graph background + RawImage on a child (same GO can't hold both Image and RawImage)
        var graphRT = MkRT(card, "Graph", 24f, 90f, CW - 48f, 250f);
        BgImg(graphRT, new Color(0.07f, 0.04f, 0.02f));
        var rawGO = new GameObject("RawGraph");
        rawGO.transform.SetParent(graphRT, false);
        var rawRT = rawGO.AddComponent<RectTransform>();
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.offsetMin = rawRT.offsetMax = Vector2.zero;
        _rankingsGraph = rawGO.AddComponent<RawImage>();

        // Summary text (left half) + high scores (right half) below graph
        float halfW = (CW - 48f) / 2f - 8f;
        _rankStatsTxt = MkTxt(card, "", 13, 24f, 14f, halfW, 68f,
            TextAnchor.UpperLeft, new Color(0.85f, 0.78f, 0.60f));
        _rankStatsTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

        _highScoresTxt = MkTxt(card, "", 12, 24f + halfW + 16f, 14f, halfW, 68f,
            TextAnchor.UpperLeft, new Color(0.88f, 0.80f, 0.45f));
        _highScoresTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

        go.SetActive(false);
    }

    void RefreshRankings()
    {
        if (_rankingsPanel == null || !_rankingsPanel.activeSelf) return;
        var tele = _sim.Telemetry;
        var s    = _sim.State;

        const int GW = 512, GH = 200;
        if (_rankingsGraph.texture != null) Destroy(_rankingsGraph.texture);
        var tex = new Texture2D(GW, GH, TextureFormat.RGBA32, false);
        var px  = new Color32[GW * GH];
        var bgC = new Color32(12, 7, 3, 255);
        for (int k = 0; k < px.Length; k++) px[k] = bgC;

        if (tele.Count > 1)
        {
            float peak = 10f;
            foreach (var r in tele) peak = Mathf.Max(peak, r.Purse, r.Coffers);

            for (int xi = 0; xi < GW; xi++)
            {
                int ti = Mathf.Clamp(Mathf.FloorToInt((float)xi / GW * tele.Count), 0, tele.Count - 1);
                int yP = Mathf.Clamp(Mathf.RoundToInt(tele[ti].Purse    / peak    * (GH - 4)), 0, GH - 1);
                int yC = Mathf.Clamp(Mathf.RoundToInt(tele[ti].Coffers  / peak    * (GH - 4)), 0, GH - 1);
                int yH = Mathf.Clamp(Mathf.RoundToInt(tele[ti].Heat     / 100f    * (GH - 4)), 0, GH - 1);
                for (int dy = -1; dy <= 1; dy++)
                {
                    int pp = (yP + dy) * GW + xi, cp = (yC + dy) * GW + xi, hp = (yH + dy) * GW + xi;
                    if (yP + dy >= 0 && yP + dy < GH) px[pp] = new Color32(210, 168,  62, 255);
                    if (yC + dy >= 0 && yC + dy < GH) px[cp] = new Color32( 78, 192,  92, 255);
                    if (yH + dy >= 0 && yH + dy < GH) px[hp] = new Color32(215,  58,  42, 255);
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        _rankingsGraph.texture = tex;

        float maxP = 0f, maxC = 0f;
        foreach (var r in tele) { maxP = Mathf.Max(maxP, r.Purse); maxC = Mathf.Max(maxC, r.Coffers); }
        _rankStatsTxt.text =
            $"Ticks: {s.Tick}   Peak Purse: §{maxP:N0}   Peak Coffers: §{maxC:N0}\n" +
            $"Town {Bar(s.TownQuality)}   Safety {Bar(s.Safety)}   Rep {Bar(s.Reputation)}";

        if (_highScoresTxt != null)
        {
            if (_highScores == null || _highScores.Entries.Count == 0)
            {
                _highScoresTxt.text = "★ TOP RUNS\n—";
            }
            else
            {
                var sb = new System.Text.StringBuilder("★ TOP RUNS\n");
                for (int i = 0; i < _highScores.Entries.Count; i++)
                {
                    var e = _highScores.Entries[i];
                    string icon = e.EndReason == "WealthWin" ? "★" : "✕";
                    sb.AppendLine($"{i+1}. {icon} §{e.FinalPurse:N0}  t{e.FinalTick}  [{e.Difficulty[0]}]");
                }
                _highScoresTxt.text = sb.ToString().TrimEnd();
            }
        }
    }

    void SliderRow(RectTransform parent, string label, ref float y,
        float x, float panelW, float min, float max, float val,
        Action<float> onChange, out Slider slider, out Text valueText)
    {
        const float lh = 24f, sh = 20f, valW = 60f;
        string initTxt = max > 1f ? $"§{val:F0}" : $"{val:P0}";
        MkTxt(parent, label, 16, x, y, panelW - valW, lh,
            TextAnchor.MiddleLeft, new Color(0.85f, 0.76f, 0.58f));
        valueText = MkTxt(parent, initTxt, 16, x + panelW - valW, y, valW, lh,
            TextAnchor.MiddleRight, Color.white);
        y -= lh + 2f;

        slider = MkSlider(parent, x, y, panelW, sh, min, max, val);
        slider.onValueChanged.AddListener(v => onChange(v));
        y -= sh + 4f;
    }
}

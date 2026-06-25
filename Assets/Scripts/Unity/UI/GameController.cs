using System;
using GameCore.Events;
using GameCore.Sim;
using UnityEngine;
using UnityEngine.UI;

// §8.1 Phase 2: minimal interactive shell — function first, no art.
// Drop onto any GameObject in a blank scene; creates its own Canvas at runtime.
[DisallowMultipleComponent]
public class GameController : MonoBehaviour
{
    [Header("Simulation")]
    public SimConfigAsset ConfigAsset;   // assign in Inspector, or leave null for defaults
    public int Seed;

    // ── Sim ──────────────────────────────────────────────────────────────────

    Simulator        _sim;
    int              _pendingOrgDelta;
    EventOption      _pendingEventChoice;
    UpgradePurchase  _pendingUpgrade;
    bool             _autoTick;
    float       _autoTimer;
    const float AutoInterval = 0.75f;

    // ── UI refs ───────────────────────────────────────────────────────────────

    static Sprite _roundedSprite;

    Slider _taxSl, _skimSl, _bribeSl, _unorgSl;
    Text   _taxVal, _skimVal, _bribeVal, _unorgVal;
    Text   _tickTxt, _purseTxt, _coffersTxt, _heatTxt;
    Text   _qualTxt, _safetyTxt, _repTxt, _orgLvTxt, _statusTxt;
    Text   _autoLbl;
    Text   _collUpgTxt, _heatUpgTxt;

    // ── Title screen ──────────────────────────────────────────────────────────

    GameObject _titlePanel;

    // ── Event modal ───────────────────────────────────────────────────────────

    GameObject _eventPanel;
    Text       _evHeadTxt, _evBodyTxt, _evOptALbl, _evOptBLbl;

    // ── Game-over overlay ─────────────────────────────────────────────────────

    GameObject _gameOverPanel;
    Text       _goIconTxt, _goTitleTxt, _goSubTxt, _goStatsTxt;

    // ── Audio ─────────────────────────────────────────────────────────────────

    SoundManager _sfx;
    bool         _gameOverSoundPlayed;

    // ── Town view ─────────────────────────────────────────────────────────────

    TownPresenter        _townView;
    TradeRouteVisualizer _routeViz;
    CaravanManager       _caravanMgr;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()  { NewGame(); BuildUI(); Refresh(); _sfx = gameObject.AddComponent<SoundManager>(); }

    void Start()
    {
        _townView   = FindObjectOfType<TownPresenter>();
        _routeViz   = FindObjectOfType<TradeRouteVisualizer>();
        _caravanMgr = FindObjectOfType<CaravanManager>();
        _townView?.ResetVisuals();
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
        var cfg = ConfigAsset != null ? ConfigAsset.Config : new SimConfig();
        _sim = new Simulator(cfg, Seed);
        _pendingOrgDelta       = 0;
        _pendingEventChoice    = EventOption.None;
        _pendingUpgrade        = UpgradePurchase.None;
        _autoTimer             = 0f;
        _autoTick              = false;
        _gameOverSoundPlayed   = false;
        if (_autoLbl != null) _autoLbl.text = "Auto: OFF";
        _gameOverPanel?.SetActive(false);
        _eventPanel?.SetActive(false);
        _townView?.ResetVisuals();
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
        var s = _sim.State;
        _tickTxt.text    = $"Tick  <b>{s.Tick}</b>";
        _purseTxt.text   = $"<color=#907050>Purse</color>    <b>§{s.Purse:N0}</b>";
        _coffersTxt.text = $"<color=#907050>Coffers</color>  <b>§{s.Coffers:N0}</b>";
        _qualTxt.text    = $"<color=#907050>Town</color>     {Bar(s.TownQuality)}  {s.TownQuality:P0}";
        _safetyTxt.text  = $"<color=#907050>Safety</color>   {Bar(s.Safety)}  {s.Safety:P0}";
        _repTxt.text     = $"<color=#907050>Rep</color>      {Bar(s.Reputation)}  {s.Reputation:P0}";
        _heatTxt.text  = HeatMood(s.Heat);
        _heatTxt.color = HeatColor(s.Heat);
        _orgLvTxt.text   = $"<color=#907050>Org Crime</color>  <b>Lv {s.OrganizedCrimeLevel}</b>";

        var cfg = _sim.Config;
        float collCost = cfg.UpgradeCollectionCostBase
            * Mathf.Pow(cfg.UpgradeCollectionCostScalePerLevel, s.CollectionUpgradeLevel);
        float heatCost = cfg.UpgradeHeatDecayCostBase
            * Mathf.Pow(cfg.UpgradeHeatDecayCostScalePerLevel, s.HeatDecayUpgradeLevel);
        _collUpgTxt.text = $"Collection  Lv {s.CollectionUpgradeLevel}  §{collCost:F0}";
        _heatUpgTxt.text = $"Heat Decay  Lv {s.HeatDecayUpgradeLevel}  §{heatCost:F0}";

        if (s.IsGameOver)
            ShowGameOverPanel(s);
        else
            _statusTxt.text = string.Empty;

        _townView?.Refresh(s);
        _routeViz?.Refresh(s);
    }

    void ToggleAuto()
    {
        _autoTick  = !_autoTick;
        _autoTimer = 0f;
        if (_autoLbl != null)
            _autoLbl.text = _autoTick ? "Auto: ON " : "Auto: OFF";
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

        MkTxt(lp, "─ CONTROLS ─", 13, xp, y, cw, 18f, TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
        y -= 24f;

        SliderRow(lp, "Tax Rate",     ref y, xp, cw, 0f, 0.60f, 0.15f, v => _taxVal.text   = $"{v:P0}",  out _taxSl,   out _taxVal);
        SliderRow(lp, "Skim",         ref y, xp, cw, 0f, 1.00f, 0.10f, v => _skimVal.text  = $"{v:P0}",  out _skimSl,  out _skimVal);
        SliderRow(lp, "Bribe / tick", ref y, xp, cw, 0f, 20f,   0.00f, v => _bribeVal.text = $"§{v:F0}", out _bribeSl, out _bribeVal);
        SliderRow(lp, "Street crime", ref y, xp, cw, 0f, 1.00f, 0.00f, v => _unorgVal.text = $"{v:P0}",  out _unorgSl, out _unorgVal);

        y -= 6f;
        MkTxt(lp, "Org Crime", 13, xp, y, 85f, 26f, TextAnchor.MiddleLeft, new Color(0.85f, 0.74f, 0.52f));
        MkBtn(lp, "−", xp + 90f, y, 30f, 26f, () => _pendingOrgDelta--);
        MkBtn(lp, "+", xp + 125f, y, 30f, 26f, () => _pendingOrgDelta++);
        y -= 34f;

        y -= 6f;
        MkTxt(lp, "─ UPGRADES ─", 11, xp, y, cw, 16f, TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
        y -= 20f;

        _collUpgTxt = MkTxt(lp, "Collection  Lv 0  §60", 12, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.80f, 0.70f, 0.54f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => _pendingUpgrade = UpgradePurchase.Collection, new Color(0.30f, 0.20f, 0.08f));
        y -= 28f;

        _heatUpgTxt = MkTxt(lp, "Heat Decay  Lv 0  §80", 12, xp, y, cw - 54f, 22f,
            TextAnchor.MiddleLeft, new Color(0.80f, 0.70f, 0.54f));
        MkBtn(lp, "Buy", xp + cw - 48f, y, 44f, 22f,
            () => _pendingUpgrade = UpgradePurchase.HeatDecay, new Color(0.30f, 0.20f, 0.08f));
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

        MkTxt(rp, "─ STATE ─", 13, rx, ry, tw, 18f, TextAnchor.MiddleLeft, new Color(1.00f, 0.85f, 0.45f));
        ry -= 24f;

        _tickTxt    = MkTxt(rp, "Tick  <b>0</b>", 17, rx, ry, tw, 24f, TextAnchor.MiddleLeft, new Color(1.00f, 0.88f, 0.58f));
        ry -= 34f;

        _purseTxt   = StatLine(rp, rx, tw, ref ry);
        _purseTxt.color = new Color(1.00f, 0.88f, 0.55f);
        _coffersTxt = StatLine(rp, rx, tw, ref ry);
        _coffersTxt.color = new Color(1.00f, 0.88f, 0.55f);

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
            () => { _pendingEventChoice = EventOption.OptionA; _eventPanel.SetActive(false); },
            new Color(0.22f, 0.44f, 0.18f));
        _evOptALbl = btnA.GetComponentInChildren<Text>();
        _evOptALbl.fontSize = 12;

        var btnB = MkBtn(card, "Option B", 20f + bw + 18f, 18f, bw, 46f,
            () => { _pendingEventChoice = EventOption.OptionB; _eventPanel.SetActive(false); },
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

        // BEGIN button
        MkBtn(card, "BEGIN", (CW - 190f) * 0.5f, 32f, 190f, 50f,
            () => _titlePanel.SetActive(false),
            new Color(0.62f, 0.40f, 0.10f));
    }

    // ── Game-over overlay construction ────────────────────────────────────────

    void BuildGameOverScreen(RectTransform root)
    {
        const float CW = 520f, CH = 390f;

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

        // Stats block (6 lines of monospaced-ish text)
        _goStatsTxt = MkTxt(card, "", 14, 28f, 58f, CW - 56f, 148f,
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
        }

        _goIconTxt.text  = win ? "★" : "✕";
        _goIconTxt.color = win ? new Color(1.00f, 0.85f, 0.15f) : new Color(1.00f, 0.32f, 0.18f);

        _goTitleTxt.text  = EndReasonTitle(s.EndReason);
        _goTitleTxt.color = _goIconTxt.color;

        _goSubTxt.text = EndReasonFlavour(s.EndReason);

        _goStatsTxt.text =
            $"Ticks survived    {s.Tick}\n" +
            $"Final purse       §{s.Purse:N0}\n" +
            $"Town coffers      §{s.Coffers:N0}\n" +
            $"Town quality      {Bar(s.TownQuality)}  {s.TownQuality:P0}\n" +
            $"Safety            {Bar(s.Safety)}  {s.Safety:P0}\n" +
            $"Reputation        {Bar(s.Reputation)}  {s.Reputation:P0}";

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

    static string EndReasonFlavour(EndReason r)
    {
        switch (r)
        {
            case EndReason.WealthWin:          return "A worthy Prefect of the Empire.";
            case EndReason.AuditArrest:        return "The Imperial auditors have come for you.";
            case EndReason.BankruptcyCollapse: return "The treasury is empty. The town falters.";
            case EndReason.RivalOverthrow:     return "A rival has seized the prefecture.";
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

    static Button MkBtn(RectTransform parent, string label,
        float x, float y, float w, float h,
        Action onClick, Color? bg = null)
    {
        var rt  = MkRT(parent, "Btn", x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bg ?? new Color(0.36f, 0.22f, 0.08f);
        if (_roundedSprite != null) { img.sprite = _roundedSprite; img.type = Image.Type.Sliced; }
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

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
        var fillRT  = MkRT(rt, "Fill", 0, 0, w, h);
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
        handleRT.sizeDelta        = new Vector2(h * 1.1f, h * 1.1f);
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

    void SliderRow(RectTransform parent, string label, ref float y,
        float x, float panelW, float min, float max, float val,
        Action<float> onChange, out Slider slider, out Text valueText)
    {
        const float lh = 18f, sh = 20f, valW = 60f;
        string initTxt = max > 1f ? $"§{val:F0}" : $"{val:P0}";
        MkTxt(parent, label, 13, x, y, panelW - valW, lh,
            TextAnchor.MiddleLeft, new Color(0.80f, 0.70f, 0.54f));
        valueText = MkTxt(parent, initTxt, 13, x + panelW - valW, y, valW, lh,
            TextAnchor.MiddleRight, Color.white);
        y -= lh + 2f;

        slider = MkSlider(parent, x, y, panelW, sh, min, max, val);
        slider.onValueChanged.AddListener(v => onChange(v));
        y -= sh + 10f;
    }
}

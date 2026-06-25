using System;
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

    Simulator _sim;
    int       _pendingOrgDelta;
    bool      _autoTick;
    float     _autoTimer;
    const float AutoInterval = 0.75f;

    // ── UI refs ───────────────────────────────────────────────────────────────

    static Sprite _roundedSprite;

    Slider _taxSl, _skimSl, _bribeSl, _unorgSl;
    Text   _taxVal, _skimVal, _bribeVal, _unorgVal;
    Text   _tickTxt, _purseTxt, _coffersTxt, _heatTxt;
    Text   _qualTxt, _safetyTxt, _repTxt, _orgLvTxt, _statusTxt;
    Text   _autoLbl;

    // ── Game-over overlay ─────────────────────────────────────────────────────

    GameObject _gameOverPanel;
    Text       _goIconTxt, _goTitleTxt, _goSubTxt, _goStatsTxt;

    // ── Town view ─────────────────────────────────────────────────────────────

    TownPresenter        _townView;
    TradeRouteVisualizer _routeViz;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()  { NewGame(); BuildUI(); Refresh(); }

    void Start()
    {
        _townView  = FindObjectOfType<TownPresenter>();
        _routeViz  = FindObjectOfType<TradeRouteVisualizer>();
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
        _pendingOrgDelta = 0;
        _autoTimer = 0f;
        _autoTick  = false;
        if (_autoLbl != null) _autoLbl.text = "Auto: OFF";
        _gameOverPanel?.SetActive(false);
        _townView?.ResetVisuals();
    }

    void DoTick()
    {
        if (_sim.State.IsGameOver) return;
        _sim.Tick(new PlayerInput
        {
            TaxRate                   = _taxSl.value,
            SkimFraction              = _skimSl.value,
            BribeAmount               = _bribeSl.value,
            UnorganizedCrimeIntensity = _unorgSl.value,
            OrganizedCrimeLevelDelta  = _pendingOrgDelta,
        });
        _pendingOrgDelta = 0;
        Refresh();
    }

    void Refresh()
    {
        var s = _sim.State;
        _tickTxt.text    = $"Tick {s.Tick}";
        _purseTxt.text   = $"Purse    §{s.Purse:N0}";
        _coffersTxt.text = $"Coffers  §{s.Coffers:N0}";
        _qualTxt.text    = $"Town     {Bar(s.TownQuality)} {s.TownQuality:P0}";
        _safetyTxt.text  = $"Safety   {Bar(s.Safety)} {s.Safety:P0}";
        _repTxt.text     = $"Rep      {Bar(s.Reputation)} {s.Reputation:P0}";
        _heatTxt.text    = $"Suspicion {HeatBar(s.Heat)}";
        _heatTxt.color   = HeatColor(s.Heat);
        _orgLvTxt.text   = $"Org Crime  Lv {s.OrganizedCrimeLevel}";

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

    // Pillar 4: coarse only — never reveal exact Heat value or audit threshold
    static string HeatBar(float h)
    {
        string lbl = h < 20 ? "Low" : h < 45 ? "Elevated" : h < 70 ? "High" : "CRITICAL";
        int on = Mathf.RoundToInt(Mathf.Clamp01(h / 100f) * 8);
        return new string('█', on) + new string('░', 8 - on) + "  " + lbl;
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
        MkTxt(titleRT, "The Prefect's Cut", 16, 0, 0, PW, 35, TextAnchor.MiddleCenter, new Color(0.96f, 0.88f, 0.68f));

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

        MkTxt(lp, "─ CONTROLS ─", 12, xp, y, cw, 18f, TextAnchor.MiddleLeft, new Color(0.96f, 0.78f, 0.42f));
        y -= 24f;

        SliderRow(lp, "Tax Rate",     ref y, xp, cw, 0f, 0.60f, 0.15f, v => _taxVal.text   = $"{v:P0}",  out _taxSl,   out _taxVal);
        SliderRow(lp, "Skim",         ref y, xp, cw, 0f, 1.00f, 0.10f, v => _skimVal.text  = $"{v:P0}",  out _skimSl,  out _skimVal);
        SliderRow(lp, "Bribe / tick", ref y, xp, cw, 0f, 20f,   0.00f, v => _bribeVal.text = $"§{v:F0}", out _bribeSl, out _bribeVal);
        SliderRow(lp, "Street crime", ref y, xp, cw, 0f, 1.00f, 0.00f, v => _unorgVal.text = $"{v:P0}",  out _unorgSl, out _unorgVal);

        y -= 6f;
        MkTxt(lp, "Org Crime", 12, xp, y, 85f, 26f, TextAnchor.MiddleLeft, Color.white);
        MkBtn(lp, "−", xp + 90f, y, 30f, 26f, () => _pendingOrgDelta--);
        MkBtn(lp, "+", xp + 125f, y, 30f, 26f, () => _pendingOrgDelta++);
        y -= 34f;

        y -= 8f;
        MkBtn(lp, "▶  NEXT TICK", xp, y, cw, 38f, DoTick, new Color(0.52f, 0.34f, 0.08f));
        y -= 46f;

        var autoBtn = MkBtn(lp, "Auto: OFF", xp, y, 130f, 26f, ToggleAuto, new Color(0.36f, 0.22f, 0.08f));
        _autoLbl = autoBtn.GetComponentInChildren<Text>();
        MkBtn(lp, "New Game", xp + 140f, y, 130f, 26f, () => { NewGame(); Refresh(); });

        // ── Right panel: state readouts ────────────────────────────────────

        const float rx = 10f, tw = PW - 20f;
        float ry = 680f;

        MkTxt(rp, "─ STATE ─", 12, rx, ry, tw, 18f, TextAnchor.MiddleLeft, new Color(0.96f, 0.78f, 0.42f));
        ry -= 24f;

        _tickTxt    = MkTxt(rp, "Tick 0", 15, rx, ry, tw, 22f, TextAnchor.MiddleLeft, new Color(0.96f, 0.84f, 0.54f));
        ry -= 32f;

        _purseTxt   = StatLine(rp, rx, tw, ref ry);
        _coffersTxt = StatLine(rp, rx, tw, ref ry);

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

        BuildGameOverScreen(root);
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

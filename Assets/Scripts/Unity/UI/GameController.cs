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

    Slider _taxSl, _skimSl, _bribeSl, _unorgSl;
    Text   _taxVal, _skimVal, _bribeVal, _unorgVal;
    Text   _tickTxt, _purseTxt, _coffersTxt, _heatTxt;
    Text   _qualTxt, _safetyTxt, _repTxt, _orgLvTxt, _statusTxt;
    Text   _autoLbl;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()  { NewGame(); BuildUI(); Refresh(); }

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
        {
            bool win = s.EndReason == EndReason.WealthWin;
            _statusTxt.text  = win ? "★  WEALTH WIN  ★" : $"✕  {s.EndReason}  ✕";
            _statusTxt.color = win ? new Color(1f, 0.85f, 0.15f) : new Color(1f, 0.30f, 0.30f);
        }
        else
        {
            _statusTxt.text = string.Empty;
        }
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
        // Canvas
        var cGO = new GameObject("Canvas");
        var cv  = cGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var cs = cGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280, 720);
        cGO.AddComponent<GraphicRaycaster>();
        var root = (RectTransform)cGO.transform;
        BgImg(root, new Color(0.07f, 0.07f, 0.09f));

        // Title strip
        MkTxt(root, "The Prefect's Cut", 22, 0, 685, 1280, 35, TextAnchor.MiddleCenter, Color.white);
        BgImg(MkRT(root, "Div", 0, 682, 1280, 2), new Color(0.35f, 0.35f, 0.40f));

        // Two panels
        var lp = MkRT(root, "Controls", 10,  10, 570, 662);  BgImg(lp, new Color(0.10f, 0.10f, 0.12f));
        var rp = MkRT(root, "State",   600,  10, 670, 662);  BgImg(rp, new Color(0.10f, 0.10f, 0.12f));

        // ── Left panel: controls ───────────────────────────────────────────

        const float xp = 12f, cw = 546f;
        float y = 630f;

        MkTxt(lp, "─ CONTROLS ─", 13, xp, y, cw, 20f, TextAnchor.MiddleLeft, new Color(0.55f, 0.75f, 0.55f));
        y -= 28f;

        SliderRow(lp, "Tax Rate",     ref y, xp, cw, 0f, 0.60f, 0.15f, v => _taxVal.text   = $"{v:P0}",   out _taxSl,   out _taxVal);
        SliderRow(lp, "Skim",         ref y, xp, cw, 0f, 1.00f, 0.10f, v => _skimVal.text  = $"{v:P0}",   out _skimSl,  out _skimVal);
        SliderRow(lp, "Bribe / tick", ref y, xp, cw, 0f, 20f,   0.00f, v => _bribeVal.text = $"§{v:F0}",  out _bribeSl, out _bribeVal);
        SliderRow(lp, "Street crime", ref y, xp, cw, 0f, 1.00f, 0.00f, v => _unorgVal.text = $"{v:P0}",   out _unorgSl, out _unorgVal);

        y -= 8f;
        MkTxt(lp, "Org Crime", 13, xp, y, 95f, 28f, TextAnchor.MiddleLeft, Color.white);
        MkBtn(lp, "−", xp + 100f, y, 32f, 28f, () => _pendingOrgDelta--);
        MkBtn(lp, "+", xp + 138f, y, 32f, 28f, () => _pendingOrgDelta++);
        y -= 38f;

        y -= 10f;
        MkBtn(lp, "▶  NEXT TICK", xp, y, cw, 42f, DoTick, new Color(0.18f, 0.42f, 0.18f));
        y -= 52f;

        var autoBtn = MkBtn(lp, "Auto: OFF", xp, y, 175f, 28f, ToggleAuto, new Color(0.18f, 0.18f, 0.28f));
        _autoLbl = autoBtn.GetComponentInChildren<Text>();
        MkBtn(lp, "New Game", xp + 185f, y, 150f, 28f, () => { NewGame(); Refresh(); });

        // ── Right panel: state readouts ────────────────────────────────────

        const float rx = 14f, tw = 642f;
        float ry = 630f;

        MkTxt(rp, "─ STATE ─", 13, rx, ry, tw, 20f, TextAnchor.MiddleLeft, new Color(0.55f, 0.75f, 0.55f));
        ry -= 28f;

        _tickTxt    = MkTxt(rp, "Tick 0", 17, rx, ry, tw, 24f, TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 1.00f));
        ry -= 32f;

        _purseTxt   = StatLine(rp, rx, tw, ref ry);
        _coffersTxt = StatLine(rp, rx, tw, ref ry);

        ry -= 6f;
        BgImg(MkRT(rp, "Div1", rx, ry, tw - 24f, 1f), new Color(0.28f, 0.28f, 0.33f));
        ry -= 9f;

        _heatTxt   = StatLine(rp, rx, tw, ref ry);
        _qualTxt   = StatLine(rp, rx, tw, ref ry);
        _safetyTxt = StatLine(rp, rx, tw, ref ry);
        _repTxt    = StatLine(rp, rx, tw, ref ry);

        ry -= 6f;
        BgImg(MkRT(rp, "Div2", rx, ry, tw - 24f, 1f), new Color(0.28f, 0.28f, 0.33f));
        ry -= 9f;

        _orgLvTxt  = StatLine(rp, rx, tw, ref ry);

        ry -= 14f;
        _statusTxt = MkTxt(rp, string.Empty, 20, rx, ry, tw, 36f, TextAnchor.MiddleLeft, Color.white);
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
        img.color = bg ?? new Color(0.20f, 0.20f, 0.26f);
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
        BgImg(rt, new Color(0.18f, 0.18f, 0.20f));

        // Fill: Image.Type.Filled so Slider can drive fillAmount directly
        var fillRT  = MkRT(rt, "Fill", 0, 0, w, h);
        var fillImg = fillRT.gameObject.AddComponent<Image>();
        fillImg.color      = new Color(0.28f, 0.62f, 0.32f);
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
        handleImg.color = new Color(0.72f, 0.90f, 0.72f);

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

    void SliderRow(RectTransform parent, string label, ref float y,
        float x, float panelW, float min, float max, float val,
        Action<float> onChange, out Slider slider, out Text valueText)
    {
        const float lh = 18f, sh = 20f, valW = 60f;
        string initTxt = max > 1f ? $"§{val:F0}" : $"{val:P0}";
        MkTxt(parent, label, 13, x, y, panelW - valW, lh,
            TextAnchor.MiddleLeft, new Color(0.72f, 0.72f, 0.72f));
        valueText = MkTxt(parent, initTxt, 13, x + panelW - valW, y, valW, lh,
            TextAnchor.MiddleRight, Color.white);
        y -= lh + 2f;

        slider = MkSlider(parent, x, y, panelW, sh, min, max, val);
        slider.onValueChanged.AddListener(v => onChange(v));
        y -= sh + 10f;
    }
}

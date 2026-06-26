using System.Collections.Generic;
using GCEvents = GameCore.Events;
using GameCore.Sim;
using UnityEngine;
using UnityEngine.UI;

// Contextual onboarding: Yusuf the Merchant slides up from the bottom the first
// time each mechanic is encountered. One tip at a time, never repeats in a session.
public class TutorialSystem : MonoBehaviour
{
    // ── Tip catalogue ─────────────────────────────────────────────────────────

    enum TipId { GameStart, HeatWarning, FirstEvent, CoffersLow, CrimeReady, WinHalfway, LegitimacyNote }

    readonly struct TipData { public readonly TipId Id; public readonly string Body;
        public TipData(TipId id, string body) { Id = id; Body = body; } }

    static readonly TipData[] AllTips =
    {
        new TipData(TipId.GameStart,
            "Welcome to Crossroads, Tax Collector!\n\nSet your <b>Tax Rate</b> to fill the town coffers, " +
            "and adjust <b>Skim</b> to pocket a share for yourself. Watch the <b>Heat</b> gauge — " +
            "if suspicion climbs past 75, an audit will end your tenure."),

        new TipData(TipId.HeatWarning,
            "Suspicion is rising — officials are watching!\n\nIncrease your <b>Bribe</b> each tick to " +
            "cool things down, or lower your Skim rate. The <b>Connections</b> upgrade permanently " +
            "reduces how quickly heat builds."),

        new TipData(TipId.FirstEvent,
            "An event demands your attention! Events pause the clock until you respond.\n\n" +
            "<b>Option A</b> usually costs coin but keeps suspicion low. " +
            "<b>Option B</b> avoids the cost — but refusal carries consequences."),

        new TipData(TipId.CoffersLow,
            "The town coffers are running dry!\n\nIf tribute goes unpaid, suspicion spikes sharply. " +
            "Raise your <b>Tax Rate</b> or lower your Skim so more revenue flows into the coffers."),

        new TipData(TipId.CrimeReady,
            "You've saved enough to invest in <b>Organised Crime</b>!\n\nThis is the fastest path to " +
            "fortune. Each level costs coin upfront but generates steady income. Start with level 1 " +
            "and pair it with a Bribe to keep heat in check."),

        new TipData(TipId.WinHalfway,
            "Excellent progress — you're halfway to your fortune!\n\nKeep your crime network funded " +
            "and respond to events promptly. Victory is within reach. Don't let rival pressure " +
            "catch you off guard."),

        new TipData(TipId.LegitimacyNote,
            "Every coin that reaches the Coffers speaks well of you!\n\n" +
            "The Empire sees a diligent official funding the town — and <b>suspicion cools</b>. " +
            "Lowering your <b>Skim</b> doesn't just fund the town; it actively reduces how fast " +
            "Heat builds. Watch the green <b>Legit</b> bar on the right."),
    };

    // ── Panel geometry ────────────────────────────────────────────────────────

    const float PW = 620f, PH = 138f;   // panel width / height
    const float AW = 88f;               // avatar column width

    // ── Runtime state ─────────────────────────────────────────────────────────

    readonly HashSet<int>    _shown = new HashSet<int>();
    readonly Queue<TipData>  _queue = new Queue<TipData>();

    RectTransform _panel;
    Text          _bodyTxt;
    float         _slideY;      // current animated Y (panel anchoredPosition.y)
    float         _targetY;     // 0 = fully visible, -(PH+4) = hidden below screen

    // Avatar animation handles
    RectTransform _avatarRoot;
    RectTransform _eyeL, _eyeR;
    Vector2       _avatarBase;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildPanel();
        _slideY  = -(PH + 4f);
        _targetY = -(PH + 4f);
        ApplySlide();
    }

    void Update()
    {
        // Smooth slide
        if (!Mathf.Approximately(_slideY, _targetY))
        {
            _slideY = Mathf.MoveTowards(_slideY, _targetY, Time.deltaTime * 800f);
            ApplySlide();
        }

        // Avatar idle animation (only while panel is at least partly visible)
        if (_avatarRoot != null && _slideY > -(PH + 4f))
        {
            float t   = Time.time;
            float bob = Mathf.Sin(t * 1.8f)  * 2.6f;
            float swx = Mathf.Sin(t * 0.85f) * 1.1f;
            _avatarRoot.anchoredPosition = _avatarBase + new Vector2(swx, bob);

            // Natural blink: brief every ~4.5 s
            bool blink = (t % 4.5f) < 0.13f;
            float eyeH = blink ? 2f : 8f;
            if (_eyeL) _eyeL.sizeDelta = new Vector2(8f, eyeH);
            if (_eyeR) _eyeR.sizeDelta = new Vector2(8f, eyeH);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Check(WorldState state, GCEvents.EventType lastEvent, float coffersContribLastTick = 0f)
    {
        TryQueue(TipId.GameStart,        state.Tick == 1);
        TryQueue(TipId.HeatWarning,      state.Heat  > 38f  && state.Tick > 3);
        TryQueue(TipId.FirstEvent,       lastEvent  != GCEvents.EventType.None);
        TryQueue(TipId.CoffersLow,       state.Coffers < 18f && state.Tick > 5);
        TryQueue(TipId.CrimeReady,       state.Purse >= 38f  && state.OrganizedCrimeLevel == 0 && state.Tick > 5);
        TryQueue(TipId.WinHalfway,       state.Purse >= 1750f);
        TryQueue(TipId.LegitimacyNote,   state.Heat  > 28f  && coffersContribLastTick < 8f && state.Tick > 8);

        if (_targetY < 0f && _queue.Count > 0) ShowNext();
    }

    public void ResetForNewGame()
    {
        _shown.Clear();
        _queue.Clear();
        _targetY = -(PH + 4f);
        _slideY  = -(PH + 4f);
        ApplySlide();
    }

    // ── Internal logic ────────────────────────────────────────────────────────

    void TryQueue(TipId id, bool condition)
    {
        if (!condition || _shown.Contains((int)id)) return;
        _shown.Add((int)id);
        foreach (var t in AllTips) { if (t.Id == id) { _queue.Enqueue(t); break; } }
    }

    void ShowNext()
    {
        if (_queue.Count == 0) return;
        var tip = _queue.Dequeue();
        _bodyTxt.text = tip.Body;
        _targetY = 0f;
    }

    void Dismiss()
    {
        _targetY = -(PH + 4f);
        if (_queue.Count > 0) Invoke(nameof(ShowNextDelayed), 0.35f);
    }

    void ShowNextDelayed() { if (_queue.Count > 0) ShowNext(); }

    void ApplySlide()
    {
        if (_panel != null) _panel.anchoredPosition = new Vector2(0f, _slideY);
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildPanel()
    {
        // Own overlay canvas so it sits above game UI
        var cvGO = new GameObject("TutorialCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;
        var cs = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1280f, 720f);
        cs.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        var root = cvGO.GetComponent<RectTransform>();

        // ── Main panel — bottom-centre, slides up ─────────────────────────────
        var pGO = new GameObject("TutPanel");
        pGO.transform.SetParent(root, false);
        _panel = pGO.AddComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(0.5f, 0f);
        _panel.anchorMax        = new Vector2(0.5f, 0f);
        _panel.pivot            = new Vector2(0.5f, 0f);
        _panel.sizeDelta        = new Vector2(PW, PH);
        _panel.anchoredPosition = new Vector2(0f, -(PH + 4f));

        var bgImg = pGO.AddComponent<Image>();
        bgImg.color = new Color(0.11f, 0.07f, 0.03f, 0.97f);

        // Gold top border
        AddRect(_panel, "Border", 0f, PH - 2f, PW, 2f, new Color(0.82f, 0.64f, 0.22f));

        // ── Avatar column ─────────────────────────────────────────────────────
        float avCX = 10f + AW * 0.5f;
        float avCY = PH * 0.5f;
        BuildAvatar(_panel, avCX, avCY);

        // ── Speech column ─────────────────────────────────────────────────────
        float sx = 10f + AW + 10f;
        float sw = PW - sx - 10f;

        // Speaker name
        var nameGO = Txt(_panel, "YUSUF  ·  SILK ROAD MERCHANT", 13, FontStyle.Bold,
            new Color(0.92f, 0.74f, 0.28f), sx, PH - 12f, sw, 18f, TextAnchor.UpperLeft);

        // Thin divider
        AddRect(_panel, "Div", sx, PH - 33f, sw * 0.55f, 1f, new Color(0.48f, 0.34f, 0.12f));

        // Body text
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(_panel, false);
        var bodyRT       = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin = bodyRT.anchorMax = Vector2.zero;
        bodyRT.pivot     = new Vector2(0f, 1f);
        bodyRT.anchoredPosition = new Vector2(sx, PH - 37f);
        bodyRT.sizeDelta        = new Vector2(sw, 82f);
        _bodyTxt                = bodyGO.AddComponent<Text>();
        _bodyTxt.font           = GetFont();
        _bodyTxt.fontSize       = 12;
        _bodyTxt.color          = new Color(0.90f, 0.84f, 0.70f);
        _bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyTxt.verticalOverflow   = VerticalWrapMode.Overflow;
        _bodyTxt.supportRichText    = true;

        // "Got it" button
        var bRT  = AddRect(_panel, "GotItBtn", PW - 10f - 96f, 10f, 96f, 28f,
                           new Color(0.52f, 0.34f, 0.08f));
        var bImg = bRT.gameObject.GetComponent<Image>();
        var btn  = bRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = bImg;
        btn.onClick.AddListener(Dismiss);
        Txt(bRT, "Got it  ›", 13, FontStyle.Normal, Color.white,
            0f, 0f, 96f, 28f, TextAnchor.MiddleCenter);
    }

    void BuildAvatar(RectTransform parent, float cx, float cy)
    {
        // Container — animation applied to this root
        var cGO = new GameObject("AvatarRoot");
        cGO.transform.SetParent(parent, false);
        _avatarRoot = cGO.AddComponent<RectTransform>();
        _avatarRoot.anchorMin        = _avatarRoot.anchorMax = Vector2.zero;
        _avatarRoot.pivot            = new Vector2(0.5f, 0.5f);
        _avatarRoot.sizeDelta        = new Vector2(AW, AW + 10f);
        _avatarRoot.anchoredPosition = new Vector2(cx, cy);
        _avatarBase                  = new Vector2(cx, cy);

        // Face — warm skin tone
        var face = AddRect(_avatarRoot, "Face",   -30f, -30f, 60f, 60f,
                           new Color(0.88f, 0.69f, 0.46f));

        // Turban — sits on top of face (teal/indigo)
        AddRect(_avatarRoot, "Turban",  -30f, 14f,  60f, 24f, new Color(0.22f, 0.40f, 0.60f));
        AddRect(_avatarRoot, "TKnot",   -38f, 22f,  16f, 10f, new Color(0.30f, 0.52f, 0.74f));

        // Beard
        AddRect(_avatarRoot, "Beard",   -16f, -44f, 32f, 16f, new Color(0.18f, 0.12f, 0.08f));

        // Eyes (animated — keep refs)
        _eyeL = AddRect(_avatarRoot, "EyeL",  -11f, -6f, 8f, 8f,
                        new Color(0.10f, 0.07f, 0.04f));
        _eyeR = AddRect(_avatarRoot, "EyeR",   11f, -6f, 8f, 8f,
                        new Color(0.10f, 0.07f, 0.04f));

        // Mouth
        AddRect(_avatarRoot, "Mouth",     0f, -22f, 22f, 5f,
                new Color(0.58f, 0.32f, 0.18f));

        // Cheek blush dots
        AddRect(_avatarRoot, "BlushL", -24f, -14f, 10f, 6f, new Color(0.88f, 0.50f, 0.42f, 0.55f));
        AddRect(_avatarRoot, "BlushR",  24f, -14f, 10f, 6f, new Color(0.88f, 0.50f, 0.42f, 0.55f));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Pivot = bottom-left for AddRect on panel; centre for AddRect on avatar.
    // For the avatar we use centre pivots so just centre everything at (0,0).
    static RectTransform AddRect(RectTransform parent, string name,
        float x, float y, float w, float h, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = Vector2.zero;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x + w * 0.5f, y + h * 0.5f);
        rt.sizeDelta        = new Vector2(w, h);
        go.AddComponent<Image>().color = color;
        return rt;
    }

    static GameObject Txt(RectTransform parent, string text, int size, FontStyle style,
        Color color, float x, float y, float w, float h, TextAnchor anchor)
    {
        var go  = new GameObject("Txt");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin        = rt.anchorMax = Vector2.zero;
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
        var t               = go.AddComponent<Text>();
        t.font              = GetFont();
        t.fontSize          = size;
        t.fontStyle         = style;
        t.color             = color;
        t.alignment         = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.text              = text;
        return go;
    }

    static Font GetFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}

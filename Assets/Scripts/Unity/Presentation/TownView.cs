using GameCore.Sim;
using UnityEngine;

// §8.1 Phase 3 Step 1 — maps WorldState to town visual blockout.
// Attach to the "Town" parent GameObject. District children are discovered by
// name at runtime, so swapping blockout cubes for final art prefabs requires
// no code changes — just keep the child names consistent.
public class TownPresenter : MonoBehaviour
{
    GameObject _market, _palace, _gate, _guardPost, _shadow, _fountain;

    void Awake()
    {
        _market   = Child("Market");
        _palace   = Child("Palace");
        _gate     = Child("Gate");
        _guardPost = Child("GuardPost");
        _shadow   = Child("ShadowDistrict");
        _fountain = Child("Fountain");
    }

    // Called by GameController each tick and on new game.
    public void Refresh(WorldState state)
    {
        SetColor(_market,    Color.Lerp(MarketWorn,  MarketGold,   state.TownQuality));
        SetColor(_palace,    Color.Lerp(PalaceWorn,  PalaceGold,   state.TownQuality));
        SetColor(_gate,      Color.Lerp(GateWorn,    GateStone,    state.Safety));
        SetColor(_guardPost, Color.Lerp(GuardDim,    GuardBlue,    state.Safety));
        ScaleGuard(state.Safety);
        RefreshShadow(state.OrganizedCrimeLevel);
        if (_fountain != null) _fountain.SetActive(state.Coffers > 15f);
    }

    public void ResetVisuals() => Refresh(WorldState.Default());

    // ── Private helpers ───────────────────────────────────────────────────────

    void ScaleGuard(float safety)
    {
        if (_guardPost == null) return;
        var s = _guardPost.transform.localScale;
        _guardPost.transform.localScale =
            new Vector3(s.x, Mathf.Lerp(0.3f, 1.5f, safety), s.z);
    }

    void RefreshShadow(int orgLevel)
    {
        if (_shadow == null) return;
        _shadow.SetActive(orgLevel > 0);
        if (orgLevel > 0)
            SetColor(_shadow, Color.Lerp(ShadowDim, ShadowBright,
                Mathf.Clamp01(orgLevel / 3f)));
    }

    static void SetColor(GameObject go, Color c)
    {
        if (go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.material.color = c;
    }

    GameObject Child(string n) => transform.Find(n)?.gameObject;

    // ── Blockout palette ──────────────────────────────────────────────────────

    static readonly Color MarketWorn   = new Color(0.45f, 0.40f, 0.35f);
    static readonly Color MarketGold   = new Color(1.00f, 0.78f, 0.32f);
    static readonly Color PalaceWorn   = new Color(0.55f, 0.48f, 0.35f);
    static readonly Color PalaceGold   = new Color(0.95f, 0.82f, 0.45f);
    static readonly Color GateWorn     = new Color(0.50f, 0.44f, 0.36f);
    static readonly Color GateStone    = new Color(0.78f, 0.70f, 0.54f);
    static readonly Color GuardDim     = new Color(0.50f, 0.50f, 0.65f);
    static readonly Color GuardBlue    = new Color(0.22f, 0.35f, 0.78f);
    static readonly Color ShadowDim    = new Color(0.25f, 0.18f, 0.30f);
    static readonly Color ShadowBright = new Color(0.50f, 0.15f, 0.55f);
}

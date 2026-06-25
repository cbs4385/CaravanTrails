using UnityEngine;

public class TradeSimulator : MonoBehaviour
{
    public static TradeSimulator Instance { get; private set; }

    public GameConfig config;

    void Awake() => Instance = this;

    public void SimulateTurn()
    {
        var graph = TradeGraph.Instance;

        foreach (var t in graph.towns)  t.ResetTurnStats();
        foreach (var r in graph.routes) { r.tradeFlowAtoB = 0; r.tradeFlowBtoA = 0; }

        foreach (var route in graph.routes)
            SimulateRoute(route);

        EventBus.TriggerTradeCalculated();
    }

    void SimulateRoute(TradeRoute route)
    {
        Town a = route.townA, b = route.townB;

        // --- Merchant willingness is driven by demand at the destination
        // relative to the destination's own production capacity.
        // A hungry town (demand >> production) motivates traders;
        // a self-sufficient town reduces incentive to make the trip.
        float willingnessAB = MerchantWillingness(b, route);
        float willingnessBA = MerchantWillingness(a, route);

        // --- Physical ceiling: neither side can send more than it can produce
        // or the destination can absorb, and the route has a capacity cap.
        float potAB = Mathf.Min(a.GoodsProduction * 0.5f, b.GoodsDemand * 0.5f, route.capacity);
        float potBA = Mathf.Min(b.GoodsProduction * 0.5f, a.GoodsDemand * 0.5f, route.capacity);

        route.tradeFlowAtoB = Mathf.Max(0f, potAB * willingnessAB);
        route.tradeFlowBtoA = Mathf.Max(0f, potBA * willingnessBA);

        a.tradeVolumeOut += route.tradeFlowAtoB;
        a.tradeVolumeIn  += route.tradeFlowBtoA;
        b.tradeVolumeOut += route.tradeFlowBtoA;
        b.tradeVolumeIn  += route.tradeFlowAtoB;

        ApplyTaxation(route);
    }

    // Merchant willingness to travel this route toward `destination`.
    // Willingness = demand pressure at destination, reduced by cost burden
    // (crime, taxes, bribe extraction). A hungry destination offsets costs.
    float MerchantWillingness(Town destination, TradeRoute route)
    {
        // How hungry is the destination? 1.0 = balanced, >1 = undersupplied (strong pull).
        float hunger = Mathf.Clamp(
            destination.GoodsDemand / Mathf.Max(destination.GoodsProduction, 1f),
            0.3f, 2.0f);

        // Cost burden on the route — each factor drains merchant profit margin.
        float avgTax   = (route.townA.taxRate + route.townB.taxRate) * 0.5f;
        float crime    = route.TotalCrimeLevel   * config.crimeTradeReduction;
        float tax      = avgTax                  * config.taxTradeReduction;
        float bribe    = route.playerCrimeLevel  * config.bribeTradeReduction;
        float burden   = crime + tax + bribe;

        // Profitability: merchants trade when hunger offsets burden.
        // profitFactor of 1.0 = full willingness; 0 = route abandoned.
        float profitFactor = Mathf.Clamp01((hunger - burden) / hunger);

        return profitFactor;
    }

    void ApplyTaxation(TradeRoute route)
    {
        TaxRoute(route.townA, route.tradeFlowBtoA + route.tradeFlowAtoB, route);
        TaxRoute(route.townB, route.tradeFlowAtoB + route.tradeFlowBtoA, route);
    }

    void TaxRoute(Town town, float totalFlow, TradeRoute route)
    {
        if (!town.isPlayerTown) return;
        float inspectorBonus = UnitManager.Instance != null
            ? UnitManager.Instance.GetEffectOnRoute(route, UnitType.Inspector)
            : 0f;
        float revenue    = totalFlow * town.taxRate * (town.GetTaxEfficiencyBonus() + inspectorBonus);
        town.taxRevenueThisTurn += revenue;
        float playerShare = revenue * config.playerTaxCut;
        PersonalAccount.Instance.AddFunds(playerShare, "Tax");
        EconomyManager.Instance.totalTaxRevenue += playerShare;
    }
}

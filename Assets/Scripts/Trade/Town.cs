using System.Collections.Generic;
using UnityEngine;

public class Town : MonoBehaviour
{
    [Header("Config")]
    public TownData data;

    [Header("Runtime")]
    public int   population;
    public float taxRate      = 0.10f;
    public bool  isPlayerTown;

    public List<TradeRoute>    connectedRoutes = new();
    public List<PlacedBuilding> buildings      = new();

    // Stats refreshed each turn
    public float tradeVolumeIn;
    public float tradeVolumeOut;
    public float taxRevenueThisTurn;

    public float GoodsProduction => population * data.goodsProductionPerPop * GetSupplyBonus();
    public float GoodsDemand     => population * data.demandPerPop          * GetDemandBonus();

    void Start()
    {
        if (data == null) return;
        population    = data.basePopulation;
        isPlayerTown  = data.isPlayerTown;
        gameObject.name = data.townName;
    }

    public void ResetTurnStats()
    {
        tradeVolumeIn       = 0;
        tradeVolumeOut      = 0;
        taxRevenueThisTurn  = 0;
    }

    // Called each day after trade is simulated so tradeVolumeIn is current.
    // Growth is driven by how well merchants meet local demand, dampened by
    // crime on connected routes and the local tax burden.
    public void GrowPopulation(GameConfig cfg)
    {
        if (data == null || cfg == null) return;

        // Needs-met ratio: 1.0 = demand exactly satisfied, >1 = surplus, <1 = deficit.
        float demand   = Mathf.Max(GoodsDemand, 1f);
        float needsMet = Mathf.Clamp(tradeVolumeIn / demand, 0f, 2f);

        // Average crime on connected routes deters residents.
        float avgCrime = 0f;
        foreach (var r in connectedRoutes) avgCrime += r.TotalCrimeLevel;
        if (connectedRoutes.Count > 0) avgCrime /= connectedRoutes.Count;

        float tradeGrowth    = (needsMet - 1f) * cfg.popGrowthRate    * population;
        float crimeLeave     =  avgCrime        * cfg.popCrimeDeterrence * population;
        float taxLeave       =  taxRate         * cfg.popTaxDeterrence   * population;

        int delta = Mathf.RoundToInt(tradeGrowth - crimeLeave - taxLeave);
        int floor = Mathf.RoundToInt(data.basePopulation * 0.25f);
        population = Mathf.Max(floor, population + delta);
    }

    public float GetTaxEfficiencyBonus()
        => 1f + (BuildingManager.Instance?.GetEffectOnTown(this, BuildingEffectType.BoostTaxEfficiency) ?? 0f);

    float GetDemandBonus()
        => 1f + (BuildingManager.Instance?.GetEffectOnTown(this, BuildingEffectType.BoostDemand) ?? 0f);

    float GetSupplyBonus()
        => 1f + (BuildingManager.Instance?.GetEffectOnTown(this, BuildingEffectType.BoostSupply) ?? 0f);
}

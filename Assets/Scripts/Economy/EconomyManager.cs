using System.Collections.Generic;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    public GameConfig config;

    public float totalTaxRevenue;
    public float totalCrimeRevenue;
    public float totalUpkeep;
    public float lastCostOfLiving;

    void Awake() => Instance = this;

    public void ResetTurnAccumulators()
    {
        totalTaxRevenue   = 0;
        totalCrimeRevenue = 0;
        totalUpkeep       = 0;
    }

    // Buildings pay first (in purchase order), then units — each from their own purse.
    // Any that cannot cover their upkeep are immediately despawned.
    // Charged once per month; scales with player town population.
    public void DeductCostOfLiving(int playerPopulation, float costPerPerson)
    {
        float cost = playerPopulation * costPerPerson;
        lastCostOfLiving = cost;
        totalUpkeep += cost;
        if (!PersonalAccount.Instance.SpendFunds(cost, "Cost of living"))
            EventBus.TriggerGameOver(TurnManager.Instance.CurrentDay);
    }

    public void DeductUpkeep()
    {
        foreach (var b in new List<PlacedBuilding>(BuildingManager.Instance.All))
        {
            if (b == null) continue;
            if (!b.TryPayUpkeep())
                b.Despawn();
            else if (b.payer == null || b.payer == (IPurse)PersonalAccount.Instance)
                totalUpkeep += b.definition.upkeepPerTurn;
        }

        foreach (var u in new List<PlacedUnit>(UnitManager.Instance.All))
        {
            if (u == null) continue;
            if (!u.TryPayUpkeep())
                u.Despawn();
            else if (u.payer == null || u.payer == (IPurse)PersonalAccount.Instance)
                totalUpkeep += u.definition.upkeepPerTurn;
        }
    }
}

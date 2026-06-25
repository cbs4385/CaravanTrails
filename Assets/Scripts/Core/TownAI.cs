using System.Collections.Generic;
using UnityEngine;

// Attached to every non-player visible town. Earns gold from local trade
// and reactively purchases law-aligned buildings and units to improve its
// situation — competing with the player for route control.
public class TownAI : MonoBehaviour
{
    public Town                    town;
    public List<BuildingDefinition> buildingCatalogue = new();
    public List<UnitDefinition>     unitCatalogue     = new();

    // Simple embedded treasury — AI never shares the player's account.
    class Treasury : IPurse
    {
        public float Balance { get; private set; }
        public Treasury(float start) => Balance = start;
        public bool CanAfford(float amount) => Balance >= amount;
        public bool SpendFunds(float amount, string _)
        {
            if (Balance < amount) return false;
            Balance -= amount;
            return true;
        }
        public void AddFunds(float amount) => Balance += amount;
    }

    Treasury _treasury;

    public float TreasuryBalance => _treasury?.Balance ?? 0f;

    void Start()
    {
        _treasury = new Treasury(350f);
        EventBus.OnTurnEnded += OnDayEnded;
    }

    void OnDestroy() => EventBus.OnTurnEnded -= OnDayEnded;

    void OnDayEnded(int _)
    {
        CollectIncome();
        EvaluateAndBuy();
    }

    void CollectIncome()
    {
        float flow = 0f;
        foreach (var r in town.connectedRoutes) flow += r.TotalTradeFlow;
        _treasury.AddFunds(flow * 0.06f);
    }

    void EvaluateAndBuy()
    {
        if (Random.value > 0.35f) return; // act roughly every 3rd tick

        TradeRoute worstRoute = null;
        float      maxCrime   = 0f;
        foreach (var r in town.connectedRoutes)
            if (r.naturalCrimeLevel > maxCrime) { maxCrime = r.naturalCrimeLevel; worstRoute = r; }

        bool highCrime = maxCrime > 0.25f;
        bool lowFlow   = town.tradeVolumeIn + town.tradeVolumeOut < 50f;

        if (highCrime && worstRoute != null)
        {
            if (TryBuyRouteBuilding(worstRoute, BuildingEffectType.ReduceCrime)) return;
            if (TryBuyUnit(worstRoute, UnitType.Guard))                           return;
        }
        else if (lowFlow)
        {
            if (TryBuyTownBuilding(BuildingEffectType.BoostDemand)) return;
            if (TryBuyTownBuilding(BuildingEffectType.BoostSupply)) return;
        }
        else
        {
            TryBuyAnything();
        }
    }

    bool TryBuyTownBuilding(BuildingEffectType effect)
    {
        foreach (var def in buildingCatalogue)
        {
            if (def.placementType != PlacementType.Town)    continue;
            if (def.effectType    != effect)                continue;
            if (def.alignment     != BuildingAlignment.Law) continue;
            if (!_treasury.CanAfford(def.cost))             continue;
            if (BuildingManager.Instance.PlaceInTown(def, town, _treasury)) return true;
        }
        return false;
    }

    bool TryBuyRouteBuilding(TradeRoute route, BuildingEffectType effect)
    {
        foreach (var def in buildingCatalogue)
        {
            if (def.placementType != PlacementType.Route)   continue;
            if (def.effectType    != effect)                continue;
            if (def.alignment     != BuildingAlignment.Law) continue;
            if (!_treasury.CanAfford(def.cost))             continue;
            if (BuildingManager.Instance.PlaceOnRoute(def, route, _treasury)) return true;
        }
        return false;
    }

    bool TryBuyUnit(TradeRoute route, UnitType unitType)
    {
        // Unit requires an existing building with capacity; buy one first if needed.
        if (BuildingManager.Instance.FindBuildingWithCapacity(route) == null)
        {
            if (!TryBuyRouteBuilding(route, BuildingEffectType.ReduceCrime)) return false;
        }
        foreach (var def in unitCatalogue)
        {
            if (def.unitType != unitType)       continue;
            if (!_treasury.CanAfford(def.cost)) continue;
            if (UnitManager.Instance.PlaceOnRoute(def, route, _treasury)) return true;
        }
        return false;
    }

    void TryBuyAnything()
    {
        foreach (var def in buildingCatalogue)
        {
            if (def.alignment != BuildingAlignment.Law) continue;
            if (!_treasury.CanAfford(def.cost))        continue;

            if (def.placementType == PlacementType.Town)
            {
                if (BuildingManager.Instance.PlaceInTown(def, town, _treasury)) return;
            }
            else
            {
                foreach (var route in town.connectedRoutes)
                    if (BuildingManager.Instance.PlaceOnRoute(def, route, _treasury)) return;
            }
        }
    }
}

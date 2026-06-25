using System.Collections.Generic;
using UnityEngine;

public class TradeRoute : MonoBehaviour
{
    [Header("Endpoints")]
    public Town townA;
    public Town townB;

    [Header("Capacity")]
    public float capacity = 100f;

    // Crime state (0–1)
    public float naturalCrimeLevel;
    public float playerCrimeLevel;
    public float TotalCrimeLevel => Mathf.Clamp01(naturalCrimeLevel + playerCrimeLevel);

    // Flow computed each turn
    public float tradeFlowAtoB;
    public float tradeFlowBtoA;
    public float TotalTradeFlow => tradeFlowAtoB + tradeFlowBtoA;

    public List<PlacedUnit>     units     = new();
    public List<PlacedBuilding> buildings = new();

    public Town GetOtherTown(Town t) => t == townA ? townB : townA;

    public float GetEnforcementStrength()
    {
        if (BuildingManager.Instance == null || UnitManager.Instance == null) return 0f;
        return BuildingManager.Instance.GetEffectOnRoute(this, BuildingEffectType.ReduceCrime)
             + UnitManager.Instance.GetEffectOnRoute(this, UnitType.Guard)
             + UnitManager.Instance.GetEffectOnRoute(this, UnitType.Inspector);
    }

    public float GetPlayerCrimeStrength()
    {
        if (UnitManager.Instance == null) return 0f;
        return UnitManager.Instance.GetEffectOnRoute(this, UnitType.Bandit)
             + UnitManager.Instance.GetEffectOnRoute(this, UnitType.CrimeBoss);
    }

    public float GetNaturalCrimeSuppression()
    {
        if (BuildingManager.Instance == null) return 0f;
        return BuildingManager.Instance.GetEffectOnRoute(this, BuildingEffectType.SuppressNaturalCrime);
    }
}

using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }

    readonly List<PlacedUnit> _all = new();

    void Awake() => Instance = this;

    // building: explicit assignment, or null to auto-find a building with capacity on route.
    // Returns false if no eligible building exists or the purse can't cover the cost.
    public bool PlaceOnRoute(UnitDefinition def, TradeRoute route,
                             IPurse purse = null, PlacedBuilding building = null)
    {
        if (building == null)
            building = BuildingManager.Instance.FindBuildingWithCapacity(route);
        if (building == null || !building.CanAcceptUnit()) return false;

        var payer = purse ?? PersonalAccount.Instance;
        if (!payer.SpendFunds(def.cost, def.unitName)) return false;

        var u            = new GameObject($"Unit_{def.unitName}").AddComponent<PlacedUnit>();
        u.definition     = def;
        u.assignedRoute  = route;
        u.parentBuilding = building;
        u.payer          = payer;
        building.assignedUnits.Add(u);
        route.units.Add(u);
        _all.Add(u);
        u.InitPosition();

        if (def.unitType == UnitType.Bandit || def.unitType == UnitType.CrimeBoss)
            route.playerCrimeLevel = Mathf.Clamp01(route.playerCrimeLevel + def.effectMagnitude * 0.2f);

        return true;
    }

    public void Remove(PlacedUnit u) => _all.Remove(u);

    // Sum of effectMagnitude for units of the given type whose current position
    // is within their influenceRadius of the route segment.
    public float GetEffectOnRoute(TradeRoute route, UnitType type)
    {
        float   v  = 0f;
        Vector3 ra = route.townA.transform.position;
        Vector3 rb = route.townB.transform.position;
        foreach (var u in _all)
        {
            if (u.definition.unitType != type) continue;
            if (DistToSegment(u.transform.position, ra, rb) <= u.definition.influenceRadius)
                v += u.definition.effectMagnitude;
        }
        return v;
    }

    public IReadOnlyList<PlacedUnit> All => _all;

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        if (ab.sqrMagnitude < 0.0001f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return (a + t * ab - p).magnitude;
    }
}

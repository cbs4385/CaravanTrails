using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    readonly List<PlacedBuilding> _all = new();

    void Awake() => Instance = this;

    public bool PlaceInTown(BuildingDefinition def, Town town, IPurse purse = null)
    {
        if (def.placementType != PlacementType.Town) return false;
        if (town.buildings.FindAll(b => b.definition == def).Count >= def.maxPerLocation) return false;
        var payer = purse ?? PersonalAccount.Instance;
        if (!payer.SpendFunds(def.cost, def.buildingName)) return false;

        var b          = new GameObject($"Bldg_{def.buildingName}").AddComponent<PlacedBuilding>();
        b.definition   = def;
        b.placedInTown = town;
        b.payer        = payer;
        town.buildings.Add(b);
        _all.Add(b);
        b.InitPosition();
        return true;
    }

    public bool PlaceOnRoute(BuildingDefinition def, TradeRoute route, IPurse purse = null)
    {
        if (def.placementType != PlacementType.Route) return false;
        if (route.buildings.FindAll(b => b.definition == def).Count >= def.maxPerLocation) return false;
        var payer = purse ?? PersonalAccount.Instance;
        if (!payer.SpendFunds(def.cost, def.buildingName)) return false;

        var b           = new GameObject($"Bldg_{def.buildingName}").AddComponent<PlacedBuilding>();
        b.definition    = def;
        b.placedOnRoute = route;
        b.payer         = payer;
        route.buildings.Add(b);
        _all.Add(b);
        b.InitPosition();
        return true;
    }

    public void Remove(PlacedBuilding b) => _all.Remove(b);

    // Find a building on this route that still has unit capacity.
    public PlacedBuilding FindBuildingWithCapacity(TradeRoute route)
    {
        foreach (var b in _all)
            if (b.placedOnRoute == route && b.CanAcceptUnit()) return b;
        return null;
    }

    // Find a building on the town or any adjacent route that has unit capacity.
    public PlacedBuilding FindBuildingWithCapacityNear(Town town)
    {
        foreach (var b in _all)
            if (b.placedInTown == town && b.CanAcceptUnit()) return b;
        foreach (var r in town.connectedRoutes)
        {
            var b = FindBuildingWithCapacity(r);
            if (b != null) return b;
        }
        return null;
    }

    // Sum of effectMagnitude for all buildings whose influence radius covers this route.
    public float GetEffectOnRoute(TradeRoute route, BuildingEffectType type)
    {
        float    v  = 0f;
        Vector3  ra = route.townA.transform.position;
        Vector3  rb = route.townB.transform.position;
        foreach (var b in _all)
        {
            if (b.definition.effectType != type) continue;
            if (DistToSegment(b.transform.position, ra, rb) <= b.definition.influenceRadius)
                v += b.definition.effectMagnitude;
        }
        return v;
    }

    // Sum of effectMagnitude for all buildings whose influence radius covers this town.
    public float GetEffectOnTown(Town town, BuildingEffectType type)
    {
        float   v  = 0f;
        Vector3 tp = town.transform.position;
        foreach (var b in _all)
        {
            if (b.definition.effectType != type) continue;
            if ((b.transform.position - tp).magnitude <= b.definition.influenceRadius)
                v += b.definition.effectMagnitude;
        }
        return v;
    }

    public IReadOnlyList<PlacedBuilding> All => _all;

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        if (ab.sqrMagnitude < 0.0001f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return (a + t * ab - p).magnitude;
    }
}

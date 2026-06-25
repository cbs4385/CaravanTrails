using System.Collections.Generic;
using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    public BuildingDefinition definition;
    public Town               placedInTown;
    public TradeRoute         placedOnRoute;
    public IPurse             payer;

    public List<PlacedUnit> assignedUnits = new();
    public bool CanAcceptUnit() => assignedUnits.Count < definition.unitCapacity;

    // Called by BuildingManager immediately after field assignment so that
    // transform.position is valid before the next simulation tick.
    public void InitPosition() => transform.position = CalcPosition();

    void Start()
    {
        SpawnMarker();
        SpawnRing(definition.influenceRadius, MarkerColor());
    }

    Vector3 CalcPosition()
    {
        if (placedInTown != null)
        {
            int   idx   = placedInTown.buildings.IndexOf(this);
            float angle = idx * 60f * Mathf.Deg2Rad;
            var   tc    = placedInTown.transform.position;
            return new Vector3(tc.x + 0.5f * Mathf.Cos(angle), 0.08f, tc.z + 0.5f * Mathf.Sin(angle));
        }
        if (placedOnRoute != null)
        {
            int     idx  = placedOnRoute.buildings.IndexOf(this);
            float   t    = 0.25f + idx * 0.15f;
            Vector3 a    = placedOnRoute.townA.transform.position;
            Vector3 b    = placedOnRoute.townB.transform.position;
            Vector3 pt   = Vector3.Lerp(a, b, t);
            Vector3 dir  = (b - a).normalized;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
            return new Vector3(pt.x, 0.08f, pt.z) + perp * (0.22f * (idx % 2 == 0 ? 1f : -1f));
        }
        return Vector3.zero;
    }

    void SpawnMarker()
    {
        var shape = placedOnRoute != null ? PrimitiveType.Cube : PrimitiveType.Cylinder;
        var go    = GameObject.CreatePrimitive(shape);
        go.name   = "Marker";
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = new Vector3(0.14f, 0.06f, 0.14f);
        Object.Destroy(go.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = MarkerColor();
        go.GetComponent<Renderer>().material = mat;
    }

    void SpawnRing(float radius, Color col)
    {
        var go  = new GameObject("Ring");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        var lr  = go.AddComponent<LineRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(col.r, col.g, col.b, 0.4f);
        lr.material      = mat;
        lr.useWorldSpace = false;
        lr.startWidth    = lr.endWidth = 0.025f;
        lr.loop          = true;
        const int seg = 36;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            lr.SetPosition(i, new Vector3(radius * Mathf.Cos(a), 0f, radius * Mathf.Sin(a)));
        }
    }

    public bool TryPayUpkeep()
        => (payer ?? PersonalAccount.Instance)
               .SpendFunds(definition.upkeepPerTurn, $"Upkeep:{definition.buildingName}");

    public void Despawn()
    {
        foreach (var u in new List<PlacedUnit>(assignedUnits)) u.Despawn();
        assignedUnits.Clear();
        if (placedInTown  != null) placedInTown.buildings.Remove(this);
        if (placedOnRoute != null) placedOnRoute.buildings.Remove(this);
        BuildingManager.Instance.Remove(this);
        Destroy(gameObject);
    }

    Color MarkerColor() => definition.effectType switch
    {
        BuildingEffectType.BoostTaxEfficiency   => new Color(1f,    0.85f, 0.1f),
        BuildingEffectType.BoostDemand          => new Color(0.15f, 0.85f, 0.15f),
        BuildingEffectType.BoostSupply          => new Color(1f,    0.55f, 0.1f),
        BuildingEffectType.ReduceCrime          => new Color(0.2f,  0.7f,  1f),
        BuildingEffectType.SuppressNaturalCrime => new Color(0.7f,  0.2f,  0.9f),
        BuildingEffectType.CrimeRevenue         => new Color(0.9f,  0.1f,  0.1f),
        _                                       => Color.white,
    };
}

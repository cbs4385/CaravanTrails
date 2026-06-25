using UnityEngine;

public class PlacedUnit : MonoBehaviour
{
    public UnitDefinition definition;
    public TradeRoute     assignedRoute;
    public PlacedBuilding parentBuilding;
    public IPurse         payer;

    float _t   = 0.5f;
    int   _dir = 1;

    // Called by UnitManager immediately after field assignment.
    public void InitPosition()
    {
        if (assignedRoute != null)
            transform.position = RoutePoint(_t);
    }

    void Start()
    {
        SpawnMarker();
        SpawnRing(definition.influenceRadius, MarkerColor());
    }

    void Update()
    {
        if (assignedRoute == null) return;
        _t += definition.moveSpeed * _dir * Time.deltaTime * 0.08f;
        if (_t >= 0.9f) { _t = 0.9f; _dir = -1; }
        if (_t <= 0.1f) { _t = 0.1f; _dir =  1; }
        transform.position = RoutePoint(_t);
    }

    Vector3 RoutePoint(float t)
    {
        Vector3 a    = assignedRoute.townA.transform.position;
        Vector3 b    = assignedRoute.townB.transform.position;
        Vector3 pt   = Vector3.Lerp(a, b, t);
        Vector3 dir  = (b - a).normalized;
        Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
        int     idx  = assignedRoute.units.IndexOf(this);
        return new Vector3(pt.x, 0.08f, pt.z) + perp * (0.22f * (idx % 2 == 0 ? 1f : -1f));
    }

    void SpawnMarker()
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Marker";
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = new Vector3(0.13f, 0.05f, 0.13f);
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
        mat.color = new Color(col.r, col.g, col.b, 0.35f);
        lr.material      = mat;
        lr.useWorldSpace = false;
        lr.startWidth    = lr.endWidth = 0.02f;
        lr.loop          = true;
        const int seg = 24;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            lr.SetPosition(i, new Vector3(radius * Mathf.Cos(a), 0f, radius * Mathf.Sin(a)));
        }
    }

    public bool TryPayUpkeep()
        => (payer ?? PersonalAccount.Instance)
               .SpendFunds(definition.upkeepPerTurn, $"Upkeep:{definition.unitName}");

    public void Despawn()
    {
        if (parentBuilding != null) parentBuilding.assignedUnits.Remove(this);
        if (assignedRoute  != null) assignedRoute.units.Remove(this);
        UnitManager.Instance.Remove(this);
        Destroy(gameObject);
    }

    Color MarkerColor() => definition.unitType switch
    {
        UnitType.Guard     => new Color(0.1f,  0.9f,  0.55f),
        UnitType.Inspector => new Color(1f,    0.95f, 0.1f),
        UnitType.Bandit    => new Color(1f,    0.4f,  0.15f),
        UnitType.CrimeBoss => new Color(0.85f, 0.1f,  0.55f),
        _                  => Color.white,
    };
}

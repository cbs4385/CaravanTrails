using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RouteView : MonoBehaviour
{
    public TradeRoute route;

    LineRenderer _line;
    GameObject   _colliderGO; // child that holds the BoxCollider, positioned along the segment

    static readonly Color ColorLowCrime  = new(0.2f, 1.0f, 0.4f);
    static readonly Color ColorHighCrime = new(1.0f, 0.2f, 0.2f);
    static readonly Color ColorSelected  = Color.yellow;

    bool _selected;

    void Awake() => _line = GetComponent<LineRenderer>();

    void Start()
    {
        _line.positionCount = 2;
        _line.useWorldSpace = true;

        EventBus.OnRouteSelected   += r => { _selected = r == route; RefreshVisuals(); };
        EventBus.OnTownSelected    += _ => { _selected = false;       RefreshVisuals(); };
        EventBus.OnSelectionCleared += () => { _selected = false;     RefreshVisuals(); };
        EventBus.OnTurnEnded       += _ => RefreshVisuals();
        EventBus.OnTradeCalculated += RefreshVisuals;
        EventBus.OnCrimeCalculated += RefreshVisuals;

        UpdatePositions();
        RefreshVisuals();
    }

    void Update() => UpdatePositions();

    void UpdatePositions()
    {
        if (route?.townA == null || route.townB == null) return;

        Vector3 a = route.townA.transform.position;
        Vector3 b = route.townB.transform.position;

        _line.SetPosition(0, a);
        _line.SetPosition(1, b);

        // Keep collider in sync with current endpoint positions
        RebuildCollider(a, b);
    }

    void RebuildCollider(Vector3 a, Vector3 b)
    {
        if (_colliderGO == null)
        {
            _colliderGO = new GameObject("RouteCollider");
            _colliderGO.transform.SetParent(transform, false);
            var col = _colliderGO.AddComponent<BoxCollider>();
            // Z is the length axis (LookRotation aligns Z to dir).
            // Y is thin but tall enough for a top-down raycast to hit.
            col.size = new Vector3(0.5f, 0.3f, 1f);
            _colliderGO.AddComponent<RouteViewProxy>().owner = this;
        }

        Vector3 mid = (a + b) * 0.5f;
        float   len = Vector3.Distance(a, b);
        Vector3 dir = (b - a).normalized;

        _colliderGO.transform.position = mid;
        _colliderGO.transform.rotation = dir != Vector3.zero
            ? Quaternion.LookRotation(dir)
            : Quaternion.identity;
        // Scale Z to route length; keep X and Y at 1 (col.size handles those)
        _colliderGO.transform.localScale = new Vector3(1f, 1f, len);
    }

    void RefreshVisuals()
    {
        if (route == null) return;
        Color c = _selected
            ? ColorSelected
            : Color.Lerp(ColorLowCrime, ColorHighCrime, route.TotalCrimeLevel);

        _line.startColor = c;
        _line.endColor   = c;

        float w = _selected ? 0.14f : Mathf.Lerp(0.04f, 0.14f, route.TotalTradeFlow / 100f);
        _line.startWidth = w;
        _line.endWidth   = w;
    }
}


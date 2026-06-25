using System.Collections.Generic;
using GameCore.Sim;
using UnityEngine;

// Visualizes the caravan trade route through the Phase 3 isometric town.
// Attach to the Town root; GameController calls Refresh() each tick.
public class TradeRouteVisualizer : MonoBehaviour
{
    // Waypoints in Town local-space (south → north matches caravan flow)
    static readonly Vector3[] Waypoints =
    {
        new Vector3( 0.0f, 0.10f, -12.5f),  // south exit (off-tile)
        new Vector3( 0.4f, 0.10f,  -5.5f),  // Palace gate
        new Vector3( 0.4f, 0.10f,   1.0f),  // Market plaza
        new Vector3( 0.1f, 0.10f,   3.5f),  // main road midpoint
        new Vector3( 0.0f, 0.10f,   7.5f),  // through Gate arch
        new Vector3( 0.0f, 0.10f,  14.0f),  // north approach (off-tile)
    };

    const int   CaravanCount = 3;
    const float CaravanSpeed = 1.8f;  // units / sec

    Material _roadMat;
    Material _caravanMat;
    float    _pathLength;

    readonly List<CaravanState> _caravans = new List<CaravanState>();

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        _pathLength = ComputePathLength();

        _roadMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _roadMat.SetFloat("_Smoothness", 0.02f);
        ApplyRoadTint(0.5f);

        _caravanMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _caravanMat.SetColor("_BaseColor", new Color(0.84f, 0.60f, 0.18f));
        _caravanMat.SetFloat("_Smoothness", 0.12f);

        BuildRoadArms();
        SpawnCaravans();
    }

    void Update()
    {
        float delta = CaravanSpeed * Time.deltaTime;
        foreach (var c in _caravans)
        {
            c.Progress = (c.Progress + delta) % _pathLength;

            Vector3 pos  = SamplePath(c.Progress);
            Vector3 fwd  = SamplePath(Mathf.Min(c.Progress + 0.15f, _pathLength - 0.01f)) - pos;
            c.Root.localPosition = pos;
            if (fwd.sqrMagnitude > 0.0001f)
                c.Root.localRotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }

    void OnDestroy()
    {
        if (_roadMat    != null) Destroy(_roadMat);
        if (_caravanMat != null) Destroy(_caravanMat);
    }

    // ── Public API (called by GameController each tick) ────────────────────────

    public void Refresh(WorldState s) => ApplyRoadTint(s.TownQuality);

    // ── Construction ───────────────────────────────────────────────────────────

    void BuildRoadArms()
    {
        // North arm: Gate (z=7.5) → off-tile (z=15), 7.5 units long, 2 wide
        MakeArm("RouteNorth", new Vector3(0.0f, 0.005f, 11.25f), new Vector3(0.20f, 1f, 0.75f));
        // South arm: south of Palace (z=-7) → off-tile (z=-13.5), 6.5 units long, 2 wide
        MakeArm("RouteSouth", new Vector3(0.3f, 0.005f, -10.25f), new Vector3(0.20f, 1f, 0.65f));
    }

    void MakeArm(string armName, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = armName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        Destroy(go.GetComponent<MeshCollider>());
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial     = _roadMat;
        mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows     = false;
    }

    void SpawnCaravans()
    {
        float interval = _pathLength / CaravanCount;
        for (int i = 0; i < CaravanCount; i++)
        {
            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Caravan" + i;
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.30f, 0.22f, 0.55f);
            Destroy(body.GetComponent<BoxCollider>());
            var mr = body.GetComponent<MeshRenderer>();
            mr.sharedMaterial    = _caravanMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Roof ridge — slightly darker amber on top
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(body.transform, false);
            roof.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            roof.transform.localScale    = new Vector3(0.6f, 0.45f, 0.75f);
            Destroy(roof.GetComponent<BoxCollider>());
            var roofMr = roof.GetComponent<MeshRenderer>();
            var roofMat = new Material(_caravanMat);
            roofMat.SetColor("_BaseColor", new Color(0.60f, 0.35f, 0.10f));
            roofMr.sharedMaterial    = roofMat;
            roofMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _caravans.Add(new CaravanState { Root = body.transform, Progress = i * interval });
        }
    }

    // ── Path helpers ───────────────────────────────────────────────────────────

    void ApplyRoadTint(float quality)
    {
        if (_roadMat == null) return;
        _roadMat.SetColor("_BaseColor",
            Color.Lerp(new Color(0.26f, 0.18f, 0.10f),
                       new Color(0.55f, 0.43f, 0.28f), quality));
    }

    float ComputePathLength()
    {
        float len = 0f;
        for (int i = 1; i < Waypoints.Length; i++)
            len += Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
        return len;
    }

    Vector3 SamplePath(float dist)
    {
        float rem = dist;
        for (int i = 1; i < Waypoints.Length; i++)
        {
            float seg = Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
            if (rem <= seg)
                return Vector3.Lerp(Waypoints[i - 1], Waypoints[i], rem / seg);
            rem -= seg;
        }
        return Waypoints[Waypoints.Length - 1];
    }

    class CaravanState
    {
        public Transform Root;
        public float     Progress;
    }
}

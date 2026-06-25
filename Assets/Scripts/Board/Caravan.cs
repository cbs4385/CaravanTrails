using System.Collections.Generic;
using UnityEngine;

public class Caravan : MonoBehaviour
{
    const float Speed = 0.7f;

    static Material _mat;
    static Material Mat
    {
        get
        {
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _mat.color = new Color(1f, 0.82f, 0.18f);
            }
            return _mat;
        }
    }

    List<Vector3> _waypoints;
    int           _wp;

    // Spawns a caravan that travels through each waypoint in order.
    // Path may include off-screen positions — caravan simply appears when
    // it crosses into the visible area.
    public static void Spawn(List<Vector3> waypoints)
    {
        if (waypoints == null || waypoints.Count < 2) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Caravan";
        var start = waypoints[0];
        go.transform.position   = new Vector3(start.x, 0.1f, start.z);
        go.transform.localScale = new Vector3(0.18f, 0.04f, 0.18f);
        Object.Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material = Mat;

        var c       = go.AddComponent<Caravan>();
        c._waypoints = waypoints.ConvertAll(p => new Vector3(p.x, 0.1f, p.z));
        c._wp        = 1;
    }

    void Update()
    {
        if (_wp >= _waypoints.Count) { Destroy(gameObject); return; }

        transform.position = Vector3.MoveTowards(
            transform.position, _waypoints[_wp], Speed * Time.deltaTime);

        if ((transform.position - _waypoints[_wp]).sqrMagnitude < 0.003f)
            _wp++;
    }
}

using System.Collections.Generic;
using UnityEngine;

public class TradeGraph : MonoBehaviour
{
    public static TradeGraph Instance { get; private set; }

    public List<Town>       towns  = new();
    public List<TradeRoute> routes = new();

    void Awake() => Instance = this;

    public void RegisterTown(Town t)
    {
        if (!towns.Contains(t)) towns.Add(t);
    }

    public void RegisterRoute(TradeRoute r)
    {
        if (routes.Contains(r)) return;
        routes.Add(r);
        if (r.townA != null && !r.townA.connectedRoutes.Contains(r)) r.townA.connectedRoutes.Add(r);
        if (r.townB != null && !r.townB.connectedRoutes.Contains(r)) r.townB.connectedRoutes.Add(r);
    }

    public TradeRoute GetRoute(Town a, Town b)
    {
        foreach (var r in routes)
            if ((r.townA == a && r.townB == b) || (r.townA == b && r.townB == a))
                return r;
        return null;
    }

    // Dijkstra — minimises crime exposure and tax burden.
    // Returns ordered list of towns from start to end, or null if unreachable.
    public List<Town> FindPath(Town start, Town end)
    {
        if (start == null || end == null || start == end) return null;

        var dist      = new Dictionary<Town, float>();
        var prev      = new Dictionary<Town, Town>();
        var remaining = new HashSet<Town>(towns);

        foreach (var t in towns) dist[t] = float.MaxValue;
        dist[start] = 0f;

        while (remaining.Count > 0)
        {
            Town  u    = null;
            float best = float.MaxValue;
            foreach (var t in remaining)
                if (dist.ContainsKey(t) && dist[t] < best) { best = dist[t]; u = t; }

            if (u == null || u == end) break;
            remaining.Remove(u);

            foreach (var route in routes)
            {
                Town neighbor;
                if      (route.townA == u) neighbor = route.townB;
                else if (route.townB == u) neighbor = route.townA;
                else continue;

                if (!remaining.Contains(neighbor)) continue;

                float alt = dist[u] + EdgeCost(route, neighbor);
                if (alt < dist[neighbor])
                {
                    dist[neighbor] = alt;
                    prev[neighbor] = u;
                }
            }
        }

        if (!prev.ContainsKey(end)) return null;

        var path    = new List<Town>();
        var current = end;
        while (current != null)
        {
            path.Insert(0, current);
            prev.TryGetValue(current, out current);
        }
        return path.Count > 0 && path[0] == start ? path : null;
    }

    // crime weighted heavily so caravans strongly avoid dangerous routes;
    // tax matters less but still influences the choice.
    static float EdgeCost(TradeRoute route, Town destination)
        => route.TotalCrimeLevel * 2.5f + destination.taxRate * 3f + 0.1f;
}

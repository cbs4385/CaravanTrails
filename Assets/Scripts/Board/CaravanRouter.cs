using System.Collections.Generic;
using UnityEngine;

// Singleton that spawns silk-road caravans across the full trade network.
// Every trade tick it finds the optimal path (minimising crime + tax) for
// each long-distance journey pair and dispatches one caravan per pair.
// Because the path is re-evaluated each tick, caravans dynamically reroute
// when the player raises taxes or when crime conditions change.
public class CaravanRouter : MonoBehaviour
{
    public static CaravanRouter Instance { get; private set; }

    // Each entry is a (origin, destination) phantom-town name pair.
    static readonly (string from, string to)[] Journeys =
    {
        ("Samarkand",      "Constantinople"),
        ("Constantinople", "Samarkand"),
        ("Bukhara",        "Hormuz"),
        ("Hormuz",         "Bukhara"),
        ("Samarkand",      "Hormuz"),
        ("Constantinople", "Bukhara"),
    };

    (Town from, Town to)[] _pairs;

    void Awake() => Instance = this;

    void Start()
    {
        EventBus.OnTradeCalculated += SpawnWave;
        ResolvePairs();
    }

    void OnDestroy() => EventBus.OnTradeCalculated -= SpawnWave;

    void ResolvePairs()
    {
        _pairs = new (Town, Town)[Journeys.Length];
        for (int i = 0; i < Journeys.Length; i++)
        {
            var (fn, tn) = Journeys[i];
            _pairs[i] = (
                TradeGraph.Instance.towns.Find(t => t.data?.townName == fn),
                TradeGraph.Instance.towns.Find(t => t.data?.townName == tn)
            );
        }
    }

    void SpawnWave()
    {
        foreach (var (from, to) in _pairs)
        {
            if (from == null || to == null) continue;
            var path = TradeGraph.Instance.FindPath(from, to);
            if (path == null || path.Count < 2) continue;
            var waypoints = new List<Vector3>(path.Count);
            foreach (var t in path) waypoints.Add(t.transform.position);
            Caravan.Spawn(waypoints);
        }
    }
}

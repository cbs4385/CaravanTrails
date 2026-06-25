using UnityEngine;

// §6.5: Pool-based visual caravan system. Traffic volume from the sim drives visible agent count.
// Pure presentation layer — economics unchanged, ICaravanSource still owns the numbers.
[DisallowMultipleComponent]
public class CaravanManager : MonoBehaviour
{
    [Header("Path (Gate → Palace, both ends off-screen)")]
    public Vector3[] Waypoints = new Vector3[]
    {
        new Vector3( 0f, 0f,  13f),   // entry — off screen north
        new Vector3( 0f, 0f,   7f),   // Gate threshold
        new Vector3( 0f, 0f,   3.5f), // Road midpoint
        new Vector3( 0f, 0f,   0f),   // Town centre
        new Vector3( 0f, 0f,  -5.5f), // Palace end
        new Vector3( 0f, 0f, -13f),   // exit — off screen south
    };

    [Header("Pool")]
    public int   MaxConcurrent  = 5;
    public float SpawnInterval  = 3.5f;  // seconds between new spawns
    public float TrafficPerSlot = 18f;   // sim traffic units per active caravan slot

    CaravanAgent[] _pool;
    bool[]         _inUse;
    float          _spawnTimer;
    float          _traffic;

    void Awake()
    {
        var traveler = GameObject.Find("Town/Traveler");
        var merchant = GameObject.Find("Town/Merchant");
        if (traveler == null) { Debug.LogWarning("[CaravanManager] Traveler template not found."); return; }

        _pool  = new CaravanAgent[MaxConcurrent];
        _inUse = new bool[MaxConcurrent];

        for (int i = 0; i < MaxConcurrent; i++)
        {
            // Alternate Traveler / Merchant for visual variety
            var template = (i % 2 == 0) ? traveler : (merchant != null ? merchant : traveler);

            // Wrapper owns the CaravanAgent and moves in world space
            var wrapper = new GameObject("Caravan_" + i);
            wrapper.transform.SetParent(transform);
            wrapper.SetActive(false);

            // Clone is a child; CharacterIdleAnim bobs in wrapper's local space
            var clone = Instantiate(template, wrapper.transform);
            clone.name = "Character";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            // Speed up the idle cycle to feel like a walking pace
            var idle = clone.GetComponent<CharacterIdleAnim>();
            if (idle != null)
            {
                idle.bobFreq      *= 2.2f;
                idle.bobAmplitude *= 1.4f;
                idle.armSwingFreq *= 2.2f;
                idle.armSwingDeg  *= 1.8f;
                idle.phase         = Random.Range(0f, Mathf.PI * 2f);
            }

            _pool[i] = wrapper.AddComponent<CaravanAgent>();
        }
    }

    // Called by GameController after each sim tick
    public void OnTick(float trafficVolume) => _traffic = trafficVolume;

    void Update()
    {
        int target = Mathf.Clamp(
            Mathf.RoundToInt(_traffic / TrafficPerSlot), 0, MaxConcurrent);

        if (ActiveCount() >= target) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer < SpawnInterval) return;
        _spawnTimer = 0f;
        TrySpawn();
    }

    void TrySpawn()
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] == null || _inUse[i]) continue;
            int idx = i;
            _inUse[idx] = true;

            // Random lateral offset so caravans don't stack on the same line
            float xOff = Random.Range(-0.6f, 0.6f);
            var wps = new Vector3[Waypoints.Length];
            for (int w = 0; w < Waypoints.Length; w++)
                wps[w] = Waypoints[w] + new Vector3(xOff, 0f, 0f);

            _pool[idx].Speed = Random.Range(1.8f, 2.7f);
            _pool[idx].Launch(wps, () => _inUse[idx] = false);
            return;
        }
    }

    int ActiveCount()
    {
        int n = 0;
        foreach (var u in _inUse) if (u) n++;
        return n;
    }
}

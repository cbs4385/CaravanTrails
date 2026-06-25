using System.Collections;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public int        CurrentDay  { get; private set; }
    public float      dayDuration = 8f;
    public GameConfig config;

    float          _timer;
    bool           _gameOver;
    System.Action<int> _onGameOver;

    void Awake() => Instance = this;

    // Yield one frame so every Start() subscription is registered before the first tick fires.
    IEnumerator Start()
    {
        _onGameOver = _ => _gameOver = true;
        EventBus.OnGameOver += _onGameOver;
        yield return null;
        EconomyManager.Instance.ResetTurnAccumulators();
        TradeSimulator.Instance.SimulateTurn();
        CrimeManager.Instance.SimulateCrime();
    }

    void OnDestroy() { if (_onGameOver != null) EventBus.OnGameOver -= _onGameOver; }

    void Update()
    {
        if (_gameOver) return;
        _timer += Time.deltaTime;
        if (_timer >= dayDuration)
        {
            _timer -= dayDuration;
            Tick();
        }
    }

    void Tick()
    {
        CurrentDay++;
        EventBus.TriggerTurnStarted(CurrentDay);
        EconomyManager.Instance.ResetTurnAccumulators();
        TradeSimulator.Instance.SimulateTurn();
        CrimeManager.Instance.SimulateCrime();
        foreach (var t in TradeGraph.Instance.towns) t.GrowPopulation(config);
        EconomyManager.Instance.DeductUpkeep();

        // Monthly cost of living — scales with player town population.
        if (config != null && CurrentDay % config.monthLength == 0)
        {
            var playerTown = TradeGraph.Instance.towns.Find(t => t.isPlayerTown);
            if (playerTown != null)
                EconomyManager.Instance.DeductCostOfLiving(
                    playerTown.population, config.costOfLivingPerPerson);
        }

        EventBus.TriggerTurnEnded(CurrentDay);
    }
}

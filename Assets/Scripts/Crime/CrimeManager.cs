using UnityEngine;

public class CrimeManager : MonoBehaviour
{
    public static CrimeManager Instance { get; private set; }

    public GameConfig config;

    void Awake() => Instance = this;

    public void SimulateCrime()
    {
        float totalCrimeRevenue = 0f;

        foreach (var route in TradeGraph.Instance.routes)
            totalCrimeRevenue += ProcessRoute(route);

        EconomyManager.Instance.totalCrimeRevenue = totalCrimeRevenue;
        float playerShare = totalCrimeRevenue * config.playerCrimeCut;
        if (playerShare > 0)
            PersonalAccount.Instance.AddFunds(playerShare, "Crime");

        EventBus.TriggerCrimeCalculated();
    }

    float ProcessRoute(TradeRoute route)
    {
        float flow = route.TotalTradeFlow;

        // --- Enforcement and criminal strength (radius-based) ---
        float rawEnforcement = route.GetEnforcementStrength();
        float playerCrime    = route.GetPlayerCrimeStrength();
        float suppression    = route.GetNaturalCrimeSuppression();

        // Criminals bribe guards: each point of criminal presence reduces enforcement
        // by a fraction, representing guards being bought off.
        float bribeReduction  = playerCrime * 0.4f;
        float netEnforcement  = Mathf.Max(0f, rawEnforcement - bribeReduction);

        // --- Natural crime ---
        float naturalGrowth  = flow * config.crimeSpawnPerFlow * config.naturalCrimeGrowthRate;
        // Player criminals compete with natural crime, squeezing it out.
        float competitionSuppression = playerCrime * config.playerCrimeCompetition * naturalGrowth;
        float enforcementReduction   = netEnforcement * 0.08f;

        route.naturalCrimeLevel = Mathf.Clamp01(
            route.naturalCrimeLevel
            + naturalGrowth
            - competitionSuppression
            - suppression
            - enforcementReduction
            - config.crimeDecayRate
        );

        // --- Criminal fighting: player criminals and natural crime clash ---
        // Each side takes losses proportional to the other's strength.
        if (route.playerCrimeLevel > 0 && route.naturalCrimeLevel > 0)
        {
            float playerLoss  = route.naturalCrimeLevel * 0.05f;
            float naturalLoss = route.playerCrimeLevel  * 0.05f;
            route.playerCrimeLevel  = Mathf.Max(0f, route.playerCrimeLevel  - playerLoss);
            route.naturalCrimeLevel = Mathf.Max(0f, route.naturalCrimeLevel - naturalLoss);
        }

        // --- Player crime revenue ---
        // Each criminal unit robs passing merchants; net enforcement partially blocks them.
        float blockRate   = Mathf.Clamp01(netEnforcement * 0.5f);
        float playerTheft = 0f;
        if (route.playerCrimeLevel > 0)
            playerTheft = flow * route.playerCrimeLevel * 0.15f * (1f - blockRate);

        return playerTheft;
    }
}

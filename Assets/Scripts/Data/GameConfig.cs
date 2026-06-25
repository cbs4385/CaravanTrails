using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "CaravanTrails/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Trade")]
    public float baseTradeFlowRate        = 10f;
    public float routeBaseCapacity        = 100f;
    // Crime reduces merchant willingness per unit of crime level
    public float crimeTradeReduction      = 0.6f;
    // Tax burden reduces willingness (penalty = avgTaxRate * this)
    public float taxTradeReduction        = 0.5f;
    // Criminal units demand bribes; penalty = playerCrimeLevel * this
    public float bribeTradeReduction      = 0.15f;

    [Header("Crime")]
    public float crimeSpawnPerFlow        = 0.05f;
    public float naturalCrimeGrowthRate   = 0.03f;
    public float playerCrimeCompetition   = 0.8f;
    public float crimeDecayRate           = 0.02f;

    [Header("Economy")]
    public float playerTaxCut             = 0.70f;
    public float playerCrimeCut           = 0.60f;
    public float startingBalance          = 500f;

    [Header("Population")]
    // How strongly trade surplus/deficit drives daily population change (fraction of pop)
    public float popGrowthRate            = 0.006f;
    // Crime and tax deter residents per unit of crime/tax-rate
    public float popCrimeDeterrence       = 0.003f;
    public float popTaxDeterrence         = 0.002f;

    [Header("Cost of Living")]
    public int   monthLength              = 30;   // simulated days per month
    public float costOfLivingPerPerson    = 0.4f; // gold per resident per month

    [Header("Time")]
    public float dayDuration              = 8f;
}

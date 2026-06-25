using UnityEngine;

public enum BuildingEffectType
{
    ReduceCrime,            // lowers crime level on route/adjacent routes
    BoostTaxEfficiency,     // multiplies tax revenue
    BoostDemand,            // multiplies town demand
    BoostSupply,            // multiplies town production
    CrimeRevenue,           // generates theft income each turn
    SuppressNaturalCrime    // prevents natural crime from spawning (criminal org)
}

public enum PlacementType    { Town, Route }
public enum BuildingAlignment{ Law, Criminal }

[CreateAssetMenu(fileName = "BuildingDef", menuName = "CaravanTrails/BuildingDefinition")]
public class BuildingDefinition : ScriptableObject
{
    public string            buildingName;
    [TextArea] public string description;
    public float             cost;
    public float             upkeepPerTurn;
    public BuildingEffectType effectType;
    public float             effectMagnitude;
    public PlacementType     placementType;
    public BuildingAlignment alignment;
    public int               maxPerLocation  = 1;
    public float             influenceRadius = 2f;
    public int               unitCapacity    = 2;
}

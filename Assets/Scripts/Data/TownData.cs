using UnityEngine;

[CreateAssetMenu(fileName = "TownData", menuName = "CaravanTrails/TownData")]
public class TownData : ScriptableObject
{
    public string   townName;
    public int      basePopulation         = 100;
    public float    goodsProductionPerPop  = 1f;
    public float    demandPerPop           = 0.8f;
    public Vector2  boardPosition;
    public bool     isPlayerTown;
}

using UnityEngine;

public enum UnitType { Guard, Inspector, Bandit, CrimeBoss }

[CreateAssetMenu(fileName = "UnitDef", menuName = "CaravanTrails/UnitDefinition")]
public class UnitDefinition : ScriptableObject
{
    public string           unitName;
    [TextArea] public string description;
    public float            cost;
    public float            upkeepPerTurn;
    public UnitType         unitType;
    public float            effectMagnitude;
    public float            influenceRadius = 1.5f;
    public float            moveSpeed       = 0.5f;
}

using GameCore.Sim;
using UnityEngine;

// §8.3: ScriptableObject adapter. Core consumes SimConfig structs; this is the authoring layer.
// Create via: Assets → Create → Prefect's Cut → SimConfig Asset
[CreateAssetMenu(fileName = "SimConfig", menuName = "Prefect's Cut/SimConfig Asset")]
public class SimConfigAsset : ScriptableObject
{
    public SimConfig Config = new SimConfig();
}

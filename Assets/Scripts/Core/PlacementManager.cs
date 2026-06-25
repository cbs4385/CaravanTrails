using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Tracks a pending build/deploy selection and applies it when the player
// clicks a valid map target. Pressing Escape cancels placement mode.
public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    public BuildingDefinition PendingBuilding { get; private set; }
    public UnitDefinition     PendingUnit     { get; private set; }
    public bool               IsPlacing       => PendingBuilding != null || PendingUnit != null;

    public string PlacementHint
    {
        get
        {
            if (PendingBuilding != null)
                return PendingBuilding.placementType == PlacementType.Town
                    ? $"Click a TOWN to place {PendingBuilding.buildingName}   [Esc] cancel"
                    : $"Click a ROUTE to place {PendingBuilding.buildingName}   [Esc] cancel";
            if (PendingUnit != null)
                return $"Click a ROUTE to deploy {PendingUnit.unitName}   [Esc] cancel";
            return "";
        }
    }

    void Awake() => Instance = this;

    void Update()
    {
        if (!IsPlacing) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
#endif
    }

    public void BeginPlaceBuilding(BuildingDefinition def)
    {
        PendingBuilding = def;
        PendingUnit     = null;
    }

    public void BeginPlaceUnit(UnitDefinition def)
    {
        PendingUnit     = def;
        PendingBuilding = null;
    }

    public void Cancel()
    {
        PendingBuilding = null;
        PendingUnit     = null;
    }

    // Returns true if placement succeeded (caller should not also select the target).
    public bool TryPlaceOnTown(Town town)
    {
        if (PendingBuilding == null || PendingBuilding.placementType != PlacementType.Town)
            return false;
        if (!BuildingManager.Instance.PlaceInTown(PendingBuilding, town))
            return false;
        Cancel();
        return true;
    }

    public bool TryPlaceOnRoute(TradeRoute route)
    {
        if (PendingBuilding != null && PendingBuilding.placementType == PlacementType.Route)
        {
            if (!BuildingManager.Instance.PlaceOnRoute(PendingBuilding, route)) return false;
            Cancel();
            return true;
        }
        if (PendingUnit != null)
        {
            if (!UnitManager.Instance.PlaceOnRoute(PendingUnit, route)) return false;
            Cancel();
            return true;
        }
        return false;
    }
}

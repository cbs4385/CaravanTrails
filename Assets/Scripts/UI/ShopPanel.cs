using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopPanel : MonoBehaviour
{
    [Header("Layout")]
    public TextMeshProUGUI titleLabel;
    public Transform       itemContainer;
    public Button          closeButton;

    [Header("Item Prefab")]
    public GameObject shopItemPrefab;

    [Header("Catalogue")]
    public List<BuildingDefinition> buildingCatalogue = new();
    public List<UnitDefinition>     unitCatalogue     = new();

    Town       _targetTown;
    TradeRoute _targetRoute;
    bool       _isGlobal;

    void Start() => closeButton.onClick.AddListener(() => UIManager.Instance.HideShop());

    // Opened from town panel — only shows town buildings + units.
    public void OpenForTown(Town town)
    {
        _targetTown  = town;
        _targetRoute = null;
        _isGlobal    = false;
        titleLabel.text = $"Shop — {town.data.townName}";
        Populate();
    }

    // Opened from route panel — only shows route buildings + units.
    public void OpenForRoute(TradeRoute route)
    {
        _targetRoute = route;
        _targetTown  = null;
        _isGlobal    = false;
        titleLabel.text = $"Shop — {route.townA.data.townName} ↔ {route.townB.data.townName}";
        Populate();
    }

    // Opened from the global HUD button — shows all items; buying enters placement mode.
    public void OpenGlobal()
    {
        _targetTown  = null;
        _targetRoute = null;
        _isGlobal    = true;
        titleLabel.text = "Shop";
        Populate();
    }

    void Populate()
    {
        foreach (Transform child in itemContainer) Destroy(child.gameObject);

        foreach (var def in buildingCatalogue)
        {
            bool valid = _isGlobal
                      || (_targetTown  != null && def.placementType == PlacementType.Town)
                      || (_targetRoute != null && def.placementType == PlacementType.Route);
            if (!valid) continue;

            bool canAfford = PersonalAccount.Instance.CanAfford(def.cost);
            string tag     = _isGlobal
                ? (def.placementType == PlacementType.Town ? " [town]" : " [route]")
                : "";
            string cap     = $" · {def.unitCapacity} unit slots · r={def.influenceRadius:F1}";
            AddItem($"{def.buildingName}{tag}  [{def.alignment}]",
                    $"{def.cost:F0}g  {def.description}{cap}",
                    canAfford,
                    () => { BuyBuilding(def); Populate(); });
        }

        // Units need a building with capacity. In global mode check entire world; otherwise
        // check the targeted context.
        PlacedBuilding bldgForUnit = _isGlobal
            ? AnyBuildingWithCapacity()
            : _targetRoute != null
                ? BuildingManager.Instance.FindBuildingWithCapacity(_targetRoute)
                : BuildingManager.Instance.FindBuildingWithCapacityNear(_targetTown);

        foreach (var def in unitCatalogue)
        {
            bool hasBldg   = bldgForUnit != null;
            bool canAfford = hasBldg && PersonalAccount.Instance.CanAfford(def.cost);
            string suffix  = !hasBldg     ? "  (build first)"
                           : _isGlobal    ? " [route]"
                           : _targetRoute != null ? ""
                                                  : "  (→ worst route)";
            AddItem($"{def.unitName}  [{def.unitType}]{suffix}",
                    $"{def.cost:F0}g  {def.description}",
                    canAfford,
                    () => { BuyUnit(def); Populate(); });
        }
    }

    void AddItem(string header, string detail, bool canAfford, System.Action onBuy)
    {
        var go = Instantiate(shopItemPrefab, itemContainer);
        go.SetActive(true);
        var labels = go.GetComponentsInChildren<TextMeshProUGUI>();
        if (labels.Length > 0) labels[0].text = header;
        if (labels.Length > 1) labels[1].text = detail;
        var btn = go.GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.interactable = canAfford;
            btn.onClick.AddListener(() => onBuy());
        }
    }

    void BuyBuilding(BuildingDefinition def)
    {
        if (_isGlobal)
        {
            // Enter placement mode; player clicks map to place.
            PlacementManager.Instance.BeginPlaceBuilding(def);
            UIManager.Instance.HideShop();
            return;
        }
        if (_targetTown  != null) BuildingManager.Instance.PlaceInTown(def, _targetTown);
        if (_targetRoute != null) BuildingManager.Instance.PlaceOnRoute(def, _targetRoute);
    }

    void BuyUnit(UnitDefinition def)
    {
        if (_isGlobal)
        {
            PlacementManager.Instance.BeginPlaceUnit(def);
            UIManager.Instance.HideShop();
            return;
        }
        var route = _targetRoute ?? WorstConnectedRoute(_targetTown);
        if (route != null) UnitManager.Instance.PlaceOnRoute(def, route);
    }

    static PlacedBuilding AnyBuildingWithCapacity()
    {
        foreach (var b in BuildingManager.Instance.All)
            if (b.CanAcceptUnit()) return b;
        return null;
    }

    static TradeRoute WorstConnectedRoute(Town town)
    {
        if (town == null) return null;
        TradeRoute worst = null;
        float      max   = -1f;
        foreach (var r in town.connectedRoutes)
            if (r.TotalCrimeLevel > max) { max = r.TotalCrimeLevel; worst = r; }
        return worst;
    }
}

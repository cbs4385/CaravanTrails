using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoutePanel : MonoBehaviour
{
    [Header("Info")]
    public TextMeshProUGUI routeNameLabel;
    public TextMeshProUGUI flowLabel;
    public TextMeshProUGUI naturalCrimeLabel;
    public TextMeshProUGUI playerCrimeLabel;
    public TextMeshProUGUI unitsLabel;

    [Header("Buttons")]
    public Button openShopButton;

    TradeRoute _route;

    void Start()
    {
        EventBus.OnRouteSelected += r => { _route = r; Refresh(); };
        EventBus.OnTurnEnded     += _ => { if (gameObject.activeSelf) Refresh(); };

        openShopButton.onClick.AddListener(() =>
        {
            if (_route == null) return;
            UIManager.Instance.ShowShop();
            FindFirstObjectByType<ShopPanel>()?.OpenForRoute(_route);
        });
    }

    void Refresh()
    {
        if (_route == null) return;

        routeNameLabel.text   = $"{_route.townA.data.townName}  ↔  {_route.townB.data.townName}";
        flowLabel.text        = $"Trade Flow: {_route.TotalTradeFlow:F0}";
        naturalCrimeLabel.text= $"Natural Crime: {_route.naturalCrimeLevel * 100:F0}%";
        playerCrimeLabel.text = $"Controlled Crime: {_route.playerCrimeLevel * 100:F0}%";
        unitsLabel.text       = $"Units: {_route.units.Count}  Buildings: {_route.buildings.Count}";
    }
}

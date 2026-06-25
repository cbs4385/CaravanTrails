using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TownPanel : MonoBehaviour
{
    [Header("Info")]
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI populationLabel;
    public TextMeshProUGUI tradeLabel;
    public TextMeshProUGUI taxRevenueLabel;

    [Header("Tax Controls (player town only)")]
    public GameObject     taxControlGroup;
    public Slider         taxSlider;
    public TextMeshProUGUI taxRateLabel;

    [Header("Buildings")]
    public TextMeshProUGUI buildingsLabel;

    [Header("Buttons")]
    public Button openShopButton;

    Town _town;

    void Start()
    {
        EventBus.OnTownSelected += t => { _town = t; Refresh(); };
        EventBus.OnTurnEnded    += _ => { if (gameObject.activeSelf) Refresh(); };

        taxSlider.minValue = 0f;
        taxSlider.maxValue = 0.50f;
        taxSlider.onValueChanged.AddListener(v =>
        {
            if (_town != null && _town.isPlayerTown)
            {
                _town.taxRate    = v;
                taxRateLabel.text = $"Tax Rate: {v * 100:F0}%";
            }
        });

        openShopButton.onClick.AddListener(() =>
        {
            if (_town == null) return;
            UIManager.Instance.ShowShop();
            FindFirstObjectByType<ShopPanel>()?.OpenForTown(_town);
        });
    }

    void Refresh()
    {
        if (_town == null) return;

        nameLabel.text       = _town.data.townName;
        populationLabel.text = $"Population: {_town.population}";
        tradeLabel.text      = $"In: {_town.tradeVolumeIn:F0}  Out: {_town.tradeVolumeOut:F0}";
        taxRevenueLabel.text = $"Tax Revenue: {_town.taxRevenueThisTurn:F0}g";

        taxControlGroup.SetActive(_town.isPlayerTown);
        if (_town.isPlayerTown)
        {
            taxSlider.SetValueWithoutNotify(_town.taxRate);
            taxRateLabel.text = $"Tax Rate: {_town.taxRate * 100:F0}%";
        }

        buildingsLabel.text = $"Buildings: {_town.buildings.Count}";
    }
}

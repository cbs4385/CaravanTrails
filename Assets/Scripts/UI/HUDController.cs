using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [Header("Labels")]
    public TextMeshProUGUI dayLabel;
    public TextMeshProUGUI balanceLabel;
    public TextMeshProUGUI taxLabel;
    public TextMeshProUGUI crimeLabel;
    public TextMeshProUGUI upkeepLabel;
    public TextMeshProUGUI placementHintLabel;

    [Header("Buttons")]
    public UnityEngine.UI.Button shopButton;
    public UnityEngine.UI.Button statsButton;

    void Start()
    {
        EventBus.OnTurnEnded      += OnDayEnded;
        EventBus.OnAccountChanged += b => balanceLabel.text = $"Gold: {b:F0}";
        shopButton.onClick.AddListener(OpenGlobalShop);
        statsButton.onClick.AddListener(OpenStats);
        RefreshAll();
    }

    void Update()
    {
        if (placementHintLabel != null && PlacementManager.Instance != null)
            placementHintLabel.text = PlacementManager.Instance.PlacementHint;
    }

    static void OpenGlobalShop()
    {
        UIManager.Instance.ShowShop();
        FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include)?.OpenGlobal();
    }

    static void OpenStats()
        => FindFirstObjectByType<StatsPanel>(FindObjectsInactive.Include)?.Open();

    void OnDestroy()
    {
        EventBus.OnTurnEnded -= OnDayEnded;
    }

    void OnDayEnded(int day)
    {
        var eco = EconomyManager.Instance;
        dayLabel.text    = $"Day {day}";
        taxLabel.text    = $"Tax:    +{eco.totalTaxRevenue:F0}";
        crimeLabel.text  = $"Crime:  +{eco.totalCrimeRevenue:F0}";

        string colLabel = eco.lastCostOfLiving > 0
            ? $"Costs: -{eco.totalUpkeep:F0}  (living: -{eco.lastCostOfLiving:F0})"
            : $"Costs: -{eco.totalUpkeep:F0}";
        upkeepLabel.text = colLabel;
    }

    void RefreshAll()
    {
        dayLabel.text     = $"Day {TurnManager.Instance.CurrentDay}";
        balanceLabel.text = $"Gold: {PersonalAccount.Instance.Balance:F0}";
        taxLabel.text     = "Tax:    -";
        crimeLabel.text   = "Crime:  -";
        upkeepLabel.text  = "Upkeep: -";
    }
}

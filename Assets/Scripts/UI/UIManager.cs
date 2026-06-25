using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject townPanel;
    public GameObject routePanel;
    public GameObject shopPanel;

    void Awake() => Instance = this;

    void Start()
    {
        EventBus.OnTownSelected   += _ => { townPanel.SetActive(true); routePanel.SetActive(false); };
        EventBus.OnRouteSelected  += _ => { routePanel.SetActive(true); townPanel.SetActive(false); };
        EventBus.OnSelectionCleared += HideInfoPanels;

        HideInfoPanels();
        shopPanel.SetActive(false);
    }

    public void ShowShop()  => shopPanel.SetActive(true);
    public void HideShop()  => shopPanel.SetActive(false);

    void HideInfoPanels()
    {
        townPanel.SetActive(false);
        routePanel.SetActive(false);
    }
}

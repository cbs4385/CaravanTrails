using TMPro;
using UnityEngine;

public class TownView : MonoBehaviour
{
    public Town town;

    [Header("Visuals")]
    public Renderer townRenderer;
    public TextMeshPro nameLabel;
    public TextMeshPro statsLabel;

    static readonly Color ColorNormal   = new(0.8f, 0.8f, 0.8f);
    static readonly Color ColorPlayer   = new(0.2f, 0.8f, 1.0f);
    static readonly Color ColorSelected = new(1.0f, 0.9f, 0.2f);
    static readonly Color ColorHighCrime= new(1.0f, 0.3f, 0.3f);

    bool _selected;

    void Start()
    {
        EventBus.OnTownSelected   += t => { _selected = t == town; RefreshColor(); };
        EventBus.OnRouteSelected  += _ => { _selected = false;      RefreshColor(); };
        EventBus.OnSelectionCleared += () => { _selected = false;   RefreshColor(); };
        EventBus.OnTurnEnded      += _ => RefreshStats();

        if (nameLabel != null && town?.data != null)
            nameLabel.text = town.data.townName;

        RefreshColor();
        RefreshStats();
    }

    void RefreshColor()
    {
        if (townRenderer == null || town == null) return;
        townRenderer.material.color = _selected    ? ColorSelected
                                    : town.isPlayerTown ? ColorPlayer
                                    : ColorNormal;
    }

    void RefreshStats()
    {
        if (statsLabel == null || town == null) return;
        statsLabel.text = $"Pop {town.population}\n"
                        + $"In {town.tradeVolumeIn:F0}  Out {town.tradeVolumeOut:F0}\n"
                        + $"Tax {town.taxRevenueThisTurn:F0}g";
    }
}

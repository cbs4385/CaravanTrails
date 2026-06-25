using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatsPanel : MonoBehaviour
{
    public TextMeshProUGUI[]  rankRows;   // 5 rows: rank + name + wealth
    public RawImage           graphImage;
    public Button             closeButton;

    Texture2D _tex;

    const int TexW = 400;
    const int TexH = 150;

    void Start()
    {
        _tex = new Texture2D(TexW, TexH, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };
        graphImage.texture = _tex;

        closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        EventBus.OnTurnEnded += _ => { if (gameObject.activeSelf) Refresh(); };
    }

    void OnDestroy() => EventBus.OnTurnEnded -= _ => { if (gameObject.activeSelf) Refresh(); };

    public void Open()
    {
        gameObject.SetActive(true);
        // Force an immediate snapshot so data is current the first time it opens.
        StatsTracker.Instance.Snapshot();
        Refresh();
    }

    void Refresh()
    {
        RefreshLeaderboard();
        RedrawGraph();
    }

    // -------------------------------------------------------------------------
    //  Leaderboard
    // -------------------------------------------------------------------------

    void RefreshLeaderboard()
    {
        var ranked = StatsTracker.Instance.GetRanked();
        for (int i = 0; i < rankRows.Length; i++)
        {
            if (i >= ranked.Count) { rankRows[i].text = ""; continue; }
            var s       = ranked[i];
            bool isYou  = s.label.StartsWith("You");
            string medal = i == 0 ? "#1" : i == 1 ? "#2" : i == 2 ? "#3" : $"#{i + 1}";
            rankRows[i].text  = $"{medal}  {s.label}   {s.Latest:F0} g";
            rankRows[i].color = isYou ? Color.yellow : s.color;
        }
    }

    // -------------------------------------------------------------------------
    //  Graph — Texture2D pixel drawing
    // -------------------------------------------------------------------------

    void RedrawGraph()
    {
        // Dark background
        var pixels = new Color[TexW * TexH];
        var bg     = new Color(0.06f, 0.06f, 0.12f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        // Subtle grid — horizontal lines at 25 %, 50 %, 75 %
        var grid = new Color(0.18f, 0.18f, 0.25f);
        foreach (int frac in new[] { 25, 50, 75 })
        {
            int y = frac * (TexH - 1) / 100;
            for (int x = 0; x < TexW; x++) pixels[y * TexW + x] = grid;
        }

        var series = StatsTracker.Instance.All;
        if (series.Count == 0) { Apply(pixels); return; }

        // Y-axis ceiling = max wealth ever recorded (with a small minimum)
        float maxVal = 100f;
        foreach (var s in series)
            foreach (var v in s.history)
                if (v > maxVal) maxVal = v;

        foreach (var s in series)
        {
            var hist = s.history;
            if (hist.Count < 2) continue;

            for (int x = 0; x < TexW; x++)
            {
                // Map pixel x to a fractional history index
                float t  = (float)x / (TexW - 1) * (hist.Count - 1);
                int   i0 = Mathf.FloorToInt(t);
                int   i1 = Mathf.Min(i0 + 1, hist.Count - 1);
                float v  = Mathf.Lerp(hist[i0], hist[i1], t - i0);

                int cy = Mathf.Clamp(Mathf.RoundToInt(v / maxVal * (TexH - 1)), 0, TexH - 1);

                // Draw 3-pixel-tall line for visibility
                for (int dy = -1; dy <= 1; dy++)
                {
                    int py = Mathf.Clamp(cy + dy, 0, TexH - 1);
                    pixels[py * TexW + x] = s.color;
                }
            }

            // Draw a dot at the latest value
            if (hist.Count > 0)
            {
                float latest = hist[hist.Count - 1];
                int   dotY   = Mathf.Clamp(Mathf.RoundToInt(latest / maxVal * (TexH - 1)), 0, TexH - 1);
                int   dotX   = TexW - 1;
                for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    if (dx * dx + dy * dy > 9) continue;
                    int px = Mathf.Clamp(dotX + dx, 0, TexW - 1);
                    int py = Mathf.Clamp(dotY + dy, 0, TexH - 1);
                    pixels[py * TexW + px] = Color.white;
                }
            }
        }

        Apply(pixels);
    }

    void Apply(Color[] pixels)
    {
        _tex.SetPixels(pixels);
        _tex.Apply();
    }
}

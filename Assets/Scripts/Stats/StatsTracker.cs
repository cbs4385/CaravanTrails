using System.Collections.Generic;
using UnityEngine;

// Records a wealth snapshot each day for every competing entity (player + AI towns).
// StatsPanel reads this for the leaderboard and income graph.
public class StatsTracker : MonoBehaviour
{
    public static StatsTracker Instance { get; private set; }

    public const int MaxHistory = 60; // days of graph history kept

    public class Series
    {
        public string      label;
        public Color       color;
        public List<float> history = new();
        public float       Latest  => history.Count > 0 ? history[history.Count - 1] : 0f;
    }

    public List<Series> All { get; } = new();

    TownAI[] _aiTowns;

    // Fixed palette — index matches TownLayout order in bootstrapper.
    static readonly Color[] AiColors =
    {
        new Color(1f,   0.5f, 0.2f),  // Westport  — orange
        new Color(0.7f, 0.3f, 0.9f),  // Millhaven — purple
        new Color(0.2f, 0.85f,0.2f),  // Eastgate  — green
        new Color(0.9f, 0.8f, 0.1f),  // Southford — yellow
    };

    void Awake() => Instance = this;

    void Start()
    {
        _aiTowns = FindObjectsByType<TownAI>(FindObjectsSortMode.None);
        EventBus.OnTurnEnded += OnDayEnded;
    }

    void OnDestroy() => EventBus.OnTurnEnded -= OnDayEnded;

    void OnDayEnded(int _) => Snapshot();

    public void Snapshot()
    {
        Record("You (Crossroads)", PersonalAccount.Instance.Balance, new Color(0.3f, 0.9f, 1f));

        int colorIdx = 0;
        foreach (var ai in _aiTowns)
        {
            string name  = ai.town?.data?.townName ?? "AI";
            Color  color = colorIdx < AiColors.Length ? AiColors[colorIdx] : Color.white;
            Record(name, ai.TreasuryBalance, color);
            colorIdx++;
        }
    }

    void Record(string label, float value, Color color)
    {
        var s = All.Find(x => x.label == label);
        if (s == null) { s = new Series { label = label, color = color }; All.Add(s); }
        s.history.Add(value);
        if (s.history.Count > MaxHistory) s.history.RemoveAt(0);
    }

    // Returns series sorted best-to-worst by current wealth.
    public List<Series> GetRanked()
    {
        var ranked = new List<Series>(All);
        ranked.Sort((a, b) => b.Latest.CompareTo(a.Latest));
        return ranked;
    }
}

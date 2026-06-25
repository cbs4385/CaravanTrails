using System;

public static class EventBus
{
    public static event Action<int>         OnTurnStarted;
    public static event Action<int>         OnTurnEnded;
    public static event Action<Town>        OnTownSelected;
    public static event Action<TradeRoute>  OnRouteSelected;
    public static event Action              OnSelectionCleared;
    public static event Action<float>       OnAccountChanged;
    public static event Action              OnTradeCalculated;
    public static event Action              OnCrimeCalculated;
    public static event Action<int>         OnGameOver;

    public static void TriggerTurnStarted(int t)       => OnTurnStarted?.Invoke(t);
    public static void TriggerTurnEnded(int t)         => OnTurnEnded?.Invoke(t);
    public static void TriggerTownSelected(Town t)     => OnTownSelected?.Invoke(t);
    public static void TriggerRouteSelected(TradeRoute r) => OnRouteSelected?.Invoke(r);
    public static void TriggerSelectionCleared()       => OnSelectionCleared?.Invoke();
    public static void TriggerAccountChanged(float b)  => OnAccountChanged?.Invoke(b);
    public static void TriggerTradeCalculated()        => OnTradeCalculated?.Invoke();
    public static void TriggerCrimeCalculated()        => OnCrimeCalculated?.Invoke();
    public static void TriggerGameOver(int day)        => OnGameOver?.Invoke(day);
}

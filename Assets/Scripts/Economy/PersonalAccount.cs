using System.Collections.Generic;
using UnityEngine;

public class PersonalAccount : MonoBehaviour, IPurse
{
    public static PersonalAccount Instance { get; private set; }

    public GameConfig config;

    [SerializeField] private float _balance;
    public float Balance => _balance;

    public List<string> log = new();

    void Awake() => Instance = this;

    // Start() runs after all Awake()s so config is guaranteed to be set by the bootstrapper
    void Start()
    {
        _balance = config != null ? config.startingBalance : 500f;
        EventBus.TriggerAccountChanged(_balance);
    }

    public bool CanAfford(float amount) => _balance >= amount;

    public void AddFunds(float amount, string reason)
    {
        _balance += amount;
        Record($"+{amount:F0} {reason}");
        EventBus.TriggerAccountChanged(_balance);
    }

    public bool SpendFunds(float amount, string reason)
    {
        if (!CanAfford(amount)) return false;
        _balance -= amount;
        Record($"-{amount:F0} {reason}");
        EventBus.TriggerAccountChanged(_balance);
        return true;
    }

    void Record(string entry)
    {
        log.Add(entry);
        if (log.Count > 30) log.RemoveAt(0);
    }
}

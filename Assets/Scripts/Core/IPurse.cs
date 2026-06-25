// Spending abstraction used by BuildingManager and UnitManager so both
// the player (PersonalAccount) and AI towns (TownAI.Treasury) can purchase.
public interface IPurse
{
    bool CanAfford(float amount);
    bool SpendFunds(float amount, string reason);
}

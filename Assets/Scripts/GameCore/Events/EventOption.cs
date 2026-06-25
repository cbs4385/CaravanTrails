namespace GameCore.Events
{
    public enum EventOption
    {
        None,    // auto-dismiss with no effects (used by harness / if player skips)
        OptionA, // first choice: typically the costly/safe response
        OptionB, // second choice: typically the risky/cheap response
    }
}

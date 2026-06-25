using System;
using GameCore.Sim;

namespace GameCore.Events
{
    public interface IEventModel
    {
        // Returns a new PendingEvent if conditions are met, or null.
        PendingEvent TryFireEvent(WorldState state, SimConfig config, Random rng);

        // Applies the player's choice and clears state.PendingEvent.
        // EventOption.None = auto-dismiss with no effects.
        void ResolveEvent(WorldState state, EventOption choice, SimConfig config);
    }
}

using System;

namespace GameCore.Events
{
    // Immutable snapshot of an event that awaits a player response.
    // Costs/effects are baked in at creation time from SimConfig so the host
    // can display labels without needing to know config values.
    [Serializable]
    public class PendingEvent
    {
        public EventType Type;
        public string    Headline;
        public string    BodyText;
        public string    OptionALabel;
        public string    OptionBLabel;
        public float     OptionACost;    // purse cost displayed to player
        public float     OptionBEffect;  // penalty magnitude displayed to player
    }
}

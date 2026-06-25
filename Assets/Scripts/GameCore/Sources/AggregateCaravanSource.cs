using System;
using GameCore.Sim;

namespace GameCore.Sources
{
    // §6.5 default: aggregate flow behind ICaravanSource so an agent-based impl can swap in later.
    public class AggregateCaravanSource : ICaravanSource
    {
        public float GetTrafficVolume(float routeAttractiveness, SimConfig config, Random rng)
        {
            float noise = 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * config.TrafficNoiseMagnitude;
            return config.BaseTrafficVolume * routeAttractiveness * noise;
        }
    }
}

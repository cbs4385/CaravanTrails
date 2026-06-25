using System;
using GameCore.Sim;

namespace GameCore.Sources
{
    public interface ICaravanSource
    {
        float GetTrafficVolume(float routeAttractiveness, SimConfig config, Random rng);
    }
}

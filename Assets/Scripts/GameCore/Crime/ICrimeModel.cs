using System;
using GameCore.Sim;

namespace GameCore.Crime
{
    public interface ICrimeModel
    {
        CrimeResult Simulate(WorldState state, PlayerInput input, float trafficVolume, SimConfig config, Random rng);
        float GetOrganizedUpkeep(int level, SimConfig config);
        float GetOrganizedSetupCost(int levelDelta, int currentLevel, SimConfig config);
    }
}

using System;
using GameCore.Sim;

namespace GameCore.Heat
{
    public interface IHeatModel
    {
        float ComputeAccrual(WorldState state, PlayerInput input, TickContext ctx, SimConfig config);
        float ComputeNaturalDecay(WorldState state, SimConfig config);
        AuditResult CheckAudit(WorldState state, SimConfig config, Random rng);
    }
}

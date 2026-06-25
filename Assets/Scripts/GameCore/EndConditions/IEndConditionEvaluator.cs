using GameCore.Sim;

namespace GameCore.EndConditions
{
    public interface IEndConditionEvaluator
    {
        EndResult Evaluate(WorldState state, SimConfig config);
    }
}

namespace Tile.Core.Simulation;

/// <summary>
/// 默认 Behaviour 候选评分器：在行为候选集合中随机选择一个 offset。
/// </summary>
public sealed class RandomBehaviourCandidateScorer : ISimulationCandidateScorer
{
    public static RandomBehaviourCandidateScorer Instance { get; } = new();

    private RandomBehaviourCandidateScorer()
    {
    }

    public SimulationCandidateMode CandidateMode => SimulationCandidateMode.Behaviour;

    public int SelectCandidateOffset(
        SimulationContext context,
        IReadOnlyList<int> candidates)
    {
        throw new NotSupportedException("This scorer only supports Behaviour candidates.");
    }

    public int SelectBehaviourCandidateOffset(
        SimulationContext context,
        IReadOnlyList<Behaviour> candidates)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (candidates is null)
            throw new ArgumentNullException(nameof(candidates));

        if (candidates.Count <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidates));

        return context.Random.Next(candidates.Count);
    }
}

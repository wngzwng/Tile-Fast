namespace Tile.Core.Simulation;

/// <summary>
/// 默认候选评分器：在候选集合中随机选择一个 offset。
/// </summary>
public sealed class RandomCandidateScorer : ISimulationCandidateScorer
{
    public static RandomCandidateScorer Instance { get; } = new();

    private RandomCandidateScorer()
    {
    }

    public SimulationCandidateMode CandidateMode => SimulationCandidateMode.Tile;

    public int SelectCandidateOffset(
        SimulationContext context,
        IReadOnlyList<int> candidates)
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

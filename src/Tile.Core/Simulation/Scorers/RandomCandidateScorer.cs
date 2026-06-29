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
        ReadOnlySpan<int> candidateBuffer,
        int candidateCount)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (candidateCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));

        return context.Random.Next(candidateCount);
    }
}

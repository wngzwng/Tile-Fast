namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 候选评分器，负责从当前候选集合中选择要执行的候选 offset。
/// </summary>
public interface ISimulationCandidateScorer
{
    /// <summary>
    /// 当前 Scorer 支持评分的候选类型。
    /// </summary>
    SimulationCandidateMode CandidateMode { get; }

    /// <summary>
    /// 返回被选候选在 <paramref name="candidateBuffer"/> 中的 offset。
    /// </summary>
    int SelectCandidateOffset(
        SimulationContext context,
        ReadOnlySpan<int> candidateBuffer,
        int candidateCount);
}

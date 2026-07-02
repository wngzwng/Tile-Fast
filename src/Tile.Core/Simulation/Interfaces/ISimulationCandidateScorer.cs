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
    /// 返回被选候选在 <paramref name="candidates"/> 中的 offset。
    /// 候选值的含义由 <see cref="CandidateMode"/> 决定。
    /// </summary>
    int SelectCandidateOffset(
        SimulationContext context,
        IReadOnlyList<int> candidates);

    int SelectBehaviourCandidateOffset(
        SimulationContext context,
        IReadOnlyList<Behaviour> candidates)
    {
        throw new NotSupportedException("This scorer does not support Behaviour candidates.");
    }
}

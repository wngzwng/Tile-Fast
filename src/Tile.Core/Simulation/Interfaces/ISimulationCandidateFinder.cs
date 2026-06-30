namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 候选生成器，负责把当前局面可考虑的候选值写入 <see cref="SimulationContext.Candidates"/>。
/// </summary>
public interface ISimulationCandidateFinder
{
    /// <summary>
    /// 当前 Finder 生成的候选类型。
    /// </summary>
    SimulationCandidateMode CandidateMode { get; }

    /// <summary>
    /// 收集当前局面的候选值，并返回写入 <paramref name="context"/> 当前候选集合的数量。
    /// </summary>
    int FindCandidates(SimulationContext context);
}

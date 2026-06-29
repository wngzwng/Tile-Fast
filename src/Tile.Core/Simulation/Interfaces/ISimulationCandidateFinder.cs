namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 候选生成器，负责把当前 step 可考虑的候选值写入候选缓冲区。
/// </summary>
public interface ISimulationCandidateFinder
{
    /// <summary>
    /// 当前 Finder 生成的候选类型。
    /// </summary>
    SimulationCandidateMode CandidateMode { get; }

    /// <summary>
    /// 收集当前 step 的候选值，并返回写入 <paramref name="candidates"/> 的数量。
    /// </summary>
    int FindCandidates(
        SimulationContext context,
        IList<int> candidates);
}

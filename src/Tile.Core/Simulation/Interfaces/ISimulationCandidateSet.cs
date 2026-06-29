namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 当前候选容器的非泛型公共视图。
/// </summary>
public interface ISimulationCandidateSet
{
    /// <summary>
    /// 当前容器保存的候选类型。
    /// </summary>
    SimulationCandidateMode Mode { get; }

    /// <summary>
    /// 当前 step 收集到的候选数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 当前已选候选的 offset；未选定时为 -1。
    /// </summary>
    int SelectedOffset { get; }

    /// <summary>
    /// 清空候选数量和选中状态。
    /// </summary>
    void Clear();

    /// <summary>
    /// 清空当前 step 的选中状态。
    /// </summary>
    void ClearSelected();
}

namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 当前局面候选快照的非泛型公共视图；不保存历史候选累计。
/// </summary>
public interface ISimulationCandidateSet
{
    /// <summary>
    /// 当前容器保存的候选类型。
    /// </summary>
    SimulationCandidateMode Mode { get; }

    /// <summary>
    /// 当前局面收集到的候选数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 当前已选候选的 offset；未选定时为 -1。
    /// </summary>
    int SelectedOffset { get; }

    /// <summary>
    /// 清空当前局面候选数量和选中状态，用于开始收集新的局面候选。
    /// </summary>
    void Clear();

    /// <summary>
    /// 记录当前 step 选中的候选 offset。
    /// </summary>
    void SetSelectedOffset(int selectedOffset);

    /// <summary>
    /// 清空当前 step 的选中状态。
    /// </summary>
    void ClearSelected();
}

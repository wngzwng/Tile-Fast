namespace Tile.Core.Simulation;

/// <summary>
/// 当前局面的候选快照；只保存本 step 收集到的候选，不保存历史累计。
/// </summary>
public sealed class SimulationCandidateSet<TCandidate> : ISimulationCandidateSet
{
    private readonly List<TCandidate> _items;

    public SimulationCandidateSet(
        SimulationCandidateMode mode,
        int capacity = 0)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Mode = mode;
        _items = new List<TCandidate>(capacity);
    }

    /// <summary>
    /// 当前容器保存的候选类型。
    /// </summary>
    public SimulationCandidateMode Mode { get; }

    /// <summary>
    /// 当前局面收集到的候选项。
    /// </summary>
    public IReadOnlyList<TCandidate> Items => _items;

    /// <summary>
    /// 可写候选列表，供 Finder 在当前 step 中填充候选项。
    /// </summary>
    public IList<TCandidate> MutableItems => _items;

    /// <summary>
    /// 当前局面收集到的候选数量。
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// 当前已选候选在 <see cref="Items"/> 中的 offset；未选定时为 -1。
    /// </summary>
    public int SelectedOffset { get; private set; } = -1;

    /// <summary>
    /// 当前局面选中的候选项。
    /// </summary>
    public TCandidate SelectedItem
    {
        get
        {
            if (SelectedOffset < 0)
                throw new InvalidOperationException("No simulation candidate has been selected.");

            return _items[SelectedOffset];
        }
    }

    /// <summary>
    /// 尝试读取当前局面选中的候选项。
    /// </summary>
    public bool TryGetSelectedItem(out TCandidate? item)
    {
        if (SelectedOffset < 0)
        {
            item = default;
            return false;
        }

        item = _items[SelectedOffset];
        return true;
    }

    /// <summary>
    /// 加入一个当前局面的候选项。
    /// </summary>
    public void Add(TCandidate item)
    {
        _items.Add(item);
    }

    /// <summary>
    /// 记录当前局面选中的候选 offset。
    /// </summary>
    public void SetSelectedOffset(int selectedOffset)
    {
        if ((uint)selectedOffset >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(selectedOffset));

        SelectedOffset = selectedOffset;
    }

    /// <summary>
    /// 清空当前局面候选数量和选中状态，用于开始收集新的局面候选。
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        ClearSelected();
    }

    /// <summary>
    /// 清空当前局面的选中状态。
    /// </summary>
    public void ClearSelected()
    {
        SelectedOffset = -1;
    }
}

namespace Tile.Core.Simulation;

/// <summary>
/// Behaviour 候选收集器；内部持有 BehaviourPool，并在 Clear 时归还本 step 候选。
/// </summary>
public sealed class BehaviourCandidateSet : ISimulationCandidateSet, IDisposable
{
    private readonly BehaviourPool _pool;
    private readonly List<Behaviour> _items;

    public BehaviourCandidateSet(
        int defaultSelectCapacity,
        int capacity = 0)
    {
        if (defaultSelectCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(defaultSelectCapacity));
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _pool = new BehaviourPool(defaultSelectCapacity);
        _items = new List<Behaviour>(capacity);
    }

    public SimulationCandidateMode Mode => SimulationCandidateMode.Behaviour;

    public IReadOnlyList<Behaviour> Items => _items;

    public IList<Behaviour> MutableItems => _items;

    public int Count => _items.Count;

    public int SelectedOffset { get; private set; } = -1;

    public Behaviour SelectedItem
    {
        get
        {
            if (SelectedOffset < 0)
                throw new InvalidOperationException("No simulation candidate has been selected.");

            return _items[SelectedOffset];
        }
    }

    public bool TryGetSelectedItem(out Behaviour? item)
    {
        if (SelectedOffset < 0)
        {
            item = null;
            return false;
        }

        item = _items[SelectedOffset];
        return true;
    }

    public Behaviour Rent(
        BehaviourKind kind,
        int color,
        ReadOnlySpan<int> selectIds)
    {
        return _pool.Rent(
            kind,
            color,
            selectIds);
    }

    public void Add(Behaviour item)
    {
        _items.Add(item);
    }

    public void SetSelectedOffset(int selectedOffset)
    {
        if ((uint)selectedOffset >= (uint)Count)
            throw new ArgumentOutOfRangeException(nameof(selectedOffset));

        SelectedOffset = selectedOffset;
    }

    public void Clear()
    {
        // CandidateSet 是本 step 候选快照 owner；Clear 是唯一统一归还入口。
        foreach (var item in _items)
            _pool.Return(item);

        _items.Clear();
        ClearSelected();
    }

    public void ClearSelected()
    {
        SelectedOffset = -1;
    }

    public void Dispose()
    {
        Clear();
    }
}

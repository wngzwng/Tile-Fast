namespace Tile.Core.Simulation;

/// <summary>
/// Behaviour 对象池；只复用对象，不拥有外部资源，也不追踪借出对象。
/// </summary>
public sealed class BehaviourPool
{
    private readonly Stack<Behaviour> _behaviours = new();
    private readonly int _defaultSelectCapacity;
    private readonly int _maxRetainedBehaviours;

    public BehaviourPool(
        int defaultSelectCapacity,
        int maxRetainedBehaviours = 32)
    {
        if (defaultSelectCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(defaultSelectCapacity));
        if (maxRetainedBehaviours < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetainedBehaviours));

        _defaultSelectCapacity = defaultSelectCapacity;
        _maxRetainedBehaviours = maxRetainedBehaviours;
    }

    /// <summary>
    /// 租借一个 Behaviour，并把 selectIds 复制到 Behaviour 自持 buffer 中。
    /// </summary>
    public Behaviour Rent(
        BehaviourKind kind,
        int color,
        ReadOnlySpan<int> selectIds)
    {
        var behaviour = _behaviours.Count == 0
            ? new Behaviour(_defaultSelectCapacity)
            : _behaviours.Pop();

        behaviour.Initialize(
            kind,
            color,
            selectIds);

        return behaviour;
    }

    internal void Return(Behaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(behaviour);

        behaviour.Reset();

        if (_behaviours.Count < _maxRetainedBehaviours)
            _behaviours.Push(behaviour);
    }
}

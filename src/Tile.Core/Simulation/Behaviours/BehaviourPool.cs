using System.Buffers;

namespace Tile.Core.Simulation;

/// <summary>
/// Behaviour 对象池；内部使用 <see cref="ArrayPool{T}"/> 分配 SelectIds 数组。
/// </summary>
public sealed class BehaviourPool : IDisposable
{
    private readonly ArrayPool<int> _arrayPool;
    private readonly Stack<Behaviour> _behaviours = new();
    private readonly HashSet<Behaviour> _borrowedBehaviours = [];
    private readonly HashSet<Behaviour> _returnedBehaviours = [];
    private readonly int _maxRetainedBehaviours;
    private bool _disposed;

    public BehaviourPool(
        ArrayPool<int>? arrayPool = null,
        int maxRetainedBehaviours = 256)
    {
        if (maxRetainedBehaviours < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetainedBehaviours));

        _arrayPool = arrayPool ?? ArrayPool<int>.Shared;
        _maxRetainedBehaviours = maxRetainedBehaviours;
    }

    /// <summary>
    /// 租借一个 Behaviour，并把 selectIds 复制到池化数组中。
    /// </summary>
    public Behaviour Rent(
        BehaviourKind kind,
        int color,
        ReadOnlySpan<int> selectIds)
    {
        ThrowIfDisposed();

        var buffer = RentSelectBuffer(selectIds);
        var behaviour = _behaviours.Count == 0
            ? new Behaviour()
            : _behaviours.Pop();

        behaviour.Initialize(
            this,
            kind,
            color,
            buffer,
            selectIds.Length);
        _borrowedBehaviours.Add(behaviour);
        _returnedBehaviours.Remove(behaviour);

        return behaviour;
    }

    internal void Return(Behaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(behaviour);

        if (!_borrowedBehaviours.Remove(behaviour))
            return;

        _returnedBehaviours.Add(behaviour);

        var selectIds = behaviour.DetachSelectIds(out var selectCount);
        ReturnSelectBuffer(selectIds, selectCount);

        if (_disposed)
            return;

        if (_behaviours.Count < _maxRetainedBehaviours)
            _behaviours.Push(behaviour);
    }

    public void Dispose()
    {
        if (_borrowedBehaviours.Count > 0)
            throw new InvalidOperationException($"BehaviourPool still has {_borrowedBehaviours.Count} borrowed behaviour(s).");

        _disposed = true;
        _returnedBehaviours.Clear();
        _behaviours.Clear();
    }

    private int[] RentSelectBuffer(ReadOnlySpan<int> selectIds)
    {
        if (selectIds.IsEmpty)
            return [];

        var buffer = _arrayPool.Rent(selectIds.Length);
        selectIds.CopyTo(buffer);

        return buffer;
    }

    private void ReturnSelectBuffer(
        int[] selectIds,
        int selectCount)
    {
        if (selectIds.Length == 0)
            return;

        Array.Clear(selectIds, 0, selectCount);
        _arrayPool.Return(selectIds);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BehaviourPool));
    }
}

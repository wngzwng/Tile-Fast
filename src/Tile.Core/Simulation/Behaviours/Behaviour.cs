using Tile.Core.Core.Moves;

namespace Tile.Core.Simulation;

/// <summary>
/// 行为组，表示一次候选行为需要按顺序选择的 tile 集合。
/// </summary>
public sealed class Behaviour : IDisposable
{
    private BehaviourPool? _owner;
    private int[] _selectIds = [];
    private int _selectCount;
    private bool _isRented;

    public Behaviour()
    {
    }

    /// <summary>
    /// 行为组类型。
    /// </summary>
    public BehaviourKind Kind { get; private set; }

    /// <summary>
    /// 行为组对应的颜色。
    /// </summary>
    public int Color { get; private set; }

    /// <summary>
    /// 行为组需要执行的 Select 数量。
    /// </summary>
    public int Count
    {
        get
        {
            EnsureActive();

            return _selectCount;
        }
    }

    /// <summary>
    /// 需要先执行 Select 的 tile；执行后会进入卡槽。
    /// </summary>
    public ReadOnlySpan<int> SelectIds
    {
        get
        {
            EnsureActive();

            return _selectIds.AsSpan(0, _selectCount);
        }
    }

    /// <summary>
    /// 将行为组展开为可执行 Move 序列。
    /// </summary>
    public IEnumerable<Move> ToMoves()
    {
        EnsureActive();

        for (var i = 0; i < _selectCount; i++)
            yield return new SelectMove(_selectIds[i]);
    }

    /// <summary>
    /// 将行为组和内部数组归还给所属对象池。
    /// </summary>
    public void Dispose()
    {
        if (_owner is null)
        {
            if (_isRented)
                DetachSelectIds(out _);

            return;
        }

        _owner.Return(this);
    }

    public override string ToString()
    {
        EnsureActive();

        var select = _selectCount == 0
            ? "-"
            : string.Join(",", _selectIds.AsSpan(0, _selectCount).ToArray());

        return $"[{Kind}] Color={Color} | Select=[{select}]";
    }

    internal void Initialize(
        BehaviourPool owner,
        BehaviourKind kind,
        int color,
        int[] selectIds,
        int selectCount)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Kind = kind;
        Color = color;
        _selectIds = selectIds ?? throw new ArgumentNullException(nameof(selectIds));
        _selectCount = selectCount;
        _isRented = true;
    }

    internal void Initialize(
        BehaviourKind kind,
        int color,
        int[] selectIds)
    {
        _owner = null;
        Kind = kind;
        Color = color;
        _selectIds = selectIds ?? throw new ArgumentNullException(nameof(selectIds));
        _selectCount = selectIds.Length;
        _isRented = true;
    }

    internal int[] DetachSelectIds(out int selectCount)
    {
        var selectIds = _selectIds;
        selectCount = _selectCount;

        Kind = default;
        Color = default;
        _selectIds = [];
        _selectCount = 0;
        _isRented = false;

        return selectIds;
    }

    private void EnsureActive()
    {
        if (!_isRented)
            throw new ObjectDisposedException(nameof(Behaviour));
    }
}

using Tile.Core.Core.Moves;

namespace Tile.Core.Simulation;

/// <summary>
/// 行为组，表示一次候选行为需要按顺序选择的 tile 集合。
/// Behaviour 是 BehaviourCandidateSet 拥有的 step 候选快照；单个对象不负责归还自己。
/// </summary>
public sealed class Behaviour
{
    private int[] _selectIds;
    private int _selectCount;
    private bool _isRented;

    public Behaviour(int defaultSelectCapacity)
    {
        if (defaultSelectCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(defaultSelectCapacity));

        _selectIds = new int[defaultSelectCapacity];
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

    public override string ToString()
    {
        EnsureActive();

        var select = _selectCount == 0
            ? "-"
            : string.Join(",", _selectIds.AsSpan(0, _selectCount).ToArray());

        return $"[{Kind}] Color={Color} | Select=[{select}]";
    }

    internal void Initialize(
        BehaviourKind kind,
        int color,
        ReadOnlySpan<int> selectIds)
    {
        Kind = kind;
        Color = color;
        EnsureCapacity(selectIds.Length);
        selectIds.CopyTo(_selectIds);
        _selectCount = selectIds.Length;
        _isRented = true;
    }

    internal void Reset()
    {
        // Clear 后旧候选必须失效，避免 scorer/runner 跨 step 持有 Behaviour。
        if (_selectCount > 0)
            Array.Clear(_selectIds, 0, _selectCount);

        Kind = default;
        Color = default;
        _selectCount = 0;
        _isRented = false;
    }

    private void EnsureCapacity(int selectCount)
    {
        if (_selectIds.Length >= selectCount)
            return;

        _selectIds = new int[selectCount];
    }

    private void EnsureActive()
    {
        if (!_isRented)
            throw new ObjectDisposedException(nameof(Behaviour));
    }
}

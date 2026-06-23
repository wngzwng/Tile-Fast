/// <summary>
/// 已消除棋子的回收栈，按后入先出顺序保存 tileIndex。
/// </summary>
public sealed class Corral
{
    private readonly int[] _tiles;
    private int _count;

    /// <summary>
    /// 当前回收栈中的棋子数量。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 当前回收栈是否为空。
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// 创建指定容量的回收栈。
    /// </summary>
    public Corral(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "回收栈容量不能小于 0。");

        _tiles = new int[capacity];
        Array.Fill<int>(_tiles, -1);
    }

    /// <summary>
    /// 将棋子压入回收栈顶。
    /// </summary>
    public void Push(int tileIndex)
    {
        if (_count >= _tiles.Length)
            throw new InvalidOperationException("回收栈已满，无法压入棋子。");

        _tiles[_count++] = tileIndex;
    }

    /// <summary>
    /// 弹出栈顶棋子，并清理原位置。
    /// </summary>
    public int Pop()
    {
        if (_count == 0)
            throw new InvalidOperationException("回收栈为空，无法弹出棋子。");

        var tileIndex = _tiles[--_count];
        _tiles[_count] = -1;

        return tileIndex;
    }

    /// <summary>
    /// 批量弹出棋子，并按后入先出顺序写入 <paramref name="buffer"/>。
    /// </summary>
    /// <returns>实际弹出的棋子数量。</returns>
    public int PopMany(int count, Span<int> buffer)
    {
        ValidatePopCount(count);

        if (buffer.Length < count)
            throw new ArgumentException("弹出缓冲区长度不足。", nameof(buffer));

        for (var i = 0; i < count; i++)
            buffer[i] = Pop();

        return count;
    }

    /// <summary>
    /// 丢弃栈顶指定数量的棋子，不返回被丢弃的内容。
    /// </summary>
    public void DropMany(int count)
    {
        ValidatePopCount(count);

        _count -= count;
        _tiles.AsSpan(_count, count).Fill(-1);
    }

    /// <summary>
    /// 查看栈顶棋子，但不改变回收栈状态。
    /// </summary>
    public int Peek()
    {
        if (_count == 0)
            throw new InvalidOperationException("回收栈为空，无法查看栈顶棋子。");

        return _tiles[_count - 1];
    }

    /// <summary>
    /// 清空回收栈，容量保持不变。
    /// </summary>
    public void Reset()
    {
        _tiles.AsSpan(0, _count).Fill(-1);
        _count = 0;
    }

    /// <summary>
    /// 创建当前回收栈状态的独立副本。
    /// </summary>
    public Corral Clone()
    {
        return new Corral(
            (int[])_tiles.Clone(),
            _count);
    }

    private void ValidatePopCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "弹出数量不能小于 0。");

        if (count > _count)
            throw new InvalidOperationException($"无法弹出 {count} 个棋子，当前只有 {_count} 个。");
    }

    private Corral(
        int[] tiles,
        int count)
    {
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        _count = count;
    }
}

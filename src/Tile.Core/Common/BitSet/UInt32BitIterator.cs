using System.Runtime.CompilerServices;

namespace Tile.Core.Common.BitSet;

/// <summary>
/// 用于遍历 <see cref="uint"/> 位图中所有置位 bit 的零 GC 迭代器。
/// 支持 <c>foreach</c> 模式。
/// </summary>
public ref struct UInt32BitIterator
{
    private readonly ReadOnlySpan<uint> _bits;
    private int _nextStart;

    /// <summary>
    /// 当前遍历到的 bit index。
    /// </summary>
    public int Current { get; private set; }

    /// <summary>
    /// 创建一个新的零 GC 置位迭代器。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UInt32BitIterator(ReadOnlySpan<uint> bits)
    {
        _bits = bits;
        _nextStart = 0;
        Current = -1;
    }

    /// <summary>
    /// foreach pattern 所需方法。
    /// 返回当前迭代器自身。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UInt32BitIterator GetEnumerator()
    {
        return this;
    }

    /// <summary>
    /// foreach pattern 所需方法。
    /// 移动到下一个置位 bit。
    /// 当没有更多置位 bit 时返回 <c>false</c>。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        var bit = BitSetOperations.FindNextSet(_bits, _nextStart);

        if (bit < 0)
            return false;

        Current = bit;
        _nextStart = bit + 1;
        return true;
    }

    /// <summary>
    /// 手动遍历用方法。
    /// 找到时返回 <c>true</c> 并写出 bit index；
    /// 找不到时返回 <c>false</c>。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(out int bit)
    {
        if (!MoveNext())
        {
            bit = -1;
            return false;
        }

        bit = Current;
        return true;
    }
}
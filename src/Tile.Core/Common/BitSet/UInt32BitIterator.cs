using System.Runtime.CompilerServices;

namespace Tile.Core.Common.BitSet;

/// <summary>
/// 用于遍历 <see cref="uint"/> 位图中所有置位 bit 的零 GC 迭代器。
/// 当没有更多置位 bit 时，<see cref="MoveNext"/> 返回 <c>false</c>。
/// </summary>
public ref struct UInt32BitIterator
{
    private readonly ReadOnlySpan<uint> _bits;
    private int _nextStart;

    /// <summary>
    /// 创建一个新的零 GC 置位迭代器。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UInt32BitIterator(ReadOnlySpan<uint> bits)
    {
        _bits = bits;
        _nextStart = 0;
    }

    /// <summary>
    /// 移动到下一个置位 bit。
    /// 找到时返回 <c>true</c> 并写出索引；找不到时返回 <c>false</c>。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(out int bit)
    {
        bit = BitSetOperations.FindNextSet(_bits, _nextStart);

        if (bit < 0)
            return false;

        _nextStart = bit + 1;
        return true;
    }
}

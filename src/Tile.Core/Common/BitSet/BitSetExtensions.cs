namespace Tile.Core.Common.BitSet;

/// <summary>
/// 为位图操作提供链式调用体验。
/// 这里不承载核心逻辑，只是简单转发到 <see cref="BitSetOperations"/>。
/// </summary>
public static class BitSetExtensions
{
    /// <summary>
    /// 原地做并集，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> OrWith(this Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        BitSetOperations.OrWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地做交集，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> AndWith(this Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        BitSetOperations.AndWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地做差集，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> AndNotWith(this Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        BitSetOperations.AndNotWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地做异或，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> XorWith(this Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        BitSetOperations.XorWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地取反，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> NotWith(this Span<ulong> bits)
    {
        BitSetOperations.NotWith(bits);
        return bits;
    }

    /// <summary>
    /// 原地左移，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> LeftShift(this Span<ulong> bits, int count)
    {
        BitSetOperations.LeftShift(bits, count);
        return bits;
    }

    /// <summary>
    /// 原地右移，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<ulong> RightShift(this Span<ulong> bits, int count)
    {
        BitSetOperations.RightShift(bits, count);
        return bits;
    }

    /// <summary>
    /// 返回第一个置位 bit 的索引；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindFirstSet(this Span<ulong> bits)
        => BitSetOperations.FindFirstSet(bits);

    /// <summary>
    /// 返回第一个置位 bit 的索引；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindFirstSet(this ReadOnlySpan<ulong> bits)
        => BitSetOperations.FindFirstSet(bits);

    /// <summary>
    /// 返回从 <paramref name="start"/> 开始找到的下一个置位 bit；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindNextSet(this Span<ulong> bits, int start)
        => BitSetOperations.FindNextSet(bits, start);

    /// <summary>
    /// 返回从 <paramref name="start"/> 开始找到的下一个置位 bit；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindNextSet(this ReadOnlySpan<ulong> bits, int start)
        => BitSetOperations.FindNextSet(bits, start);

    /// <summary>
    /// 返回一个零 GC 的置位 bit 迭代器。
    /// </summary>
    public static UInt64BitIterator EnumerateSetBits(this Span<ulong> bits)
        => BitSetOperations.EnumerateSetBits(bits);

    /// <summary>
    /// 返回一个零 GC 的置位 bit 迭代器。
    /// </summary>
    public static UInt64BitIterator EnumerateSetBits(this ReadOnlySpan<ulong> bits)
        => BitSetOperations.EnumerateSetBits(bits);

    /// <summary>
    /// 原地做并集，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> OrWith(this Span<uint> bits, ReadOnlySpan<uint> other)
    {
        BitSetOperations.OrWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地做交集，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> AndWith(this Span<uint> bits, ReadOnlySpan<uint> other)
    {
        BitSetOperations.AndWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地做差集，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> AndNotWith(this Span<uint> bits, ReadOnlySpan<uint> other)
    {
        BitSetOperations.AndNotWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地做异或，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> XorWith(this Span<uint> bits, ReadOnlySpan<uint> other)
    {
        BitSetOperations.XorWith(bits, other);
        return bits;
    }

    /// <summary>
    /// 原地取反，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> NotWith(this Span<uint> bits)
    {
        BitSetOperations.NotWith(bits);
        return bits;
    }

    /// <summary>
    /// 原地左移，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> LeftShift(this Span<uint> bits, int count)
    {
        BitSetOperations.LeftShift(bits, count);
        return bits;
    }

    /// <summary>
    /// 原地右移，并返回同一个 Span，便于链式调用。
    /// </summary>
    public static Span<uint> RightShift(this Span<uint> bits, int count)
    {
        BitSetOperations.RightShift(bits, count);
        return bits;
    }

    /// <summary>
    /// 返回第一个置位 bit 的索引；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindFirstSet(this Span<uint> bits)
        => BitSetOperations.FindFirstSet(bits);

    /// <summary>
    /// 返回第一个置位 bit 的索引；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindFirstSet(this ReadOnlySpan<uint> bits)
        => BitSetOperations.FindFirstSet(bits);

    /// <summary>
    /// 返回从 <paramref name="start"/> 开始找到的下一个置位 bit；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindNextSet(this Span<uint> bits, int start)
        => BitSetOperations.FindNextSet(bits, start);

    /// <summary>
    /// 返回从 <paramref name="start"/> 开始找到的下一个置位 bit；找不到时返回 <c>-1</c>。
    /// </summary>
    public static int FindNextSet(this ReadOnlySpan<uint> bits, int start)
        => BitSetOperations.FindNextSet(bits, start);

    /// <summary>
    /// 返回一个零 GC 的置位 bit 迭代器。
    /// </summary>
    public static UInt32BitIterator EnumerateSetBits(this Span<uint> bits)
        => BitSetOperations.EnumerateSetBits(bits);

    /// <summary>
    /// 返回一个零 GC 的置位 bit 迭代器。
    /// </summary>
    public static UInt32BitIterator EnumerateSetBits(this ReadOnlySpan<uint> bits)
        => BitSetOperations.EnumerateSetBits(bits);
}

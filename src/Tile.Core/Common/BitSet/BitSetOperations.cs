using System.Numerics;
using System.Runtime.CompilerServices;

namespace Tile.Core.Common.BitSet;

/// <summary>
/// 提供基于 <see cref="Span{T}"/> / <see cref="ReadOnlySpan{T}"/> 的多 word 位图操作。
/// 查找类方法在没有找到时统一返回 <c>-1</c>。
/// </summary>
public static class BitSetOperations
{
    /// <summary>
    /// 将 bit 数量换算成所需的 <see cref="ulong"/> word 数量。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitLenToU64Len(int bitLength)
    {
        if (bitLength < 0)
            throw new ArgumentOutOfRangeException(nameof(bitLength));

        return (int)(((uint)bitLength + 63u) >> 6);
    }

    /// <summary>
    /// 将 bit 数量换算成所需的 <see cref="uint"/> word 数量。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitLenToU32Len(int bitLength)
    {
        if (bitLength < 0)
            throw new ArgumentOutOfRangeException(nameof(bitLength));

        return (int)(((uint)bitLength + 31u) >> 5);
    }

    /// <summary>
    /// 读取指定 bit 是否为 1。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Get(ReadOnlySpan<ulong> bits, int bit)
    {
        ValidateBitIndex(bit);
        return (bits[bit >> 6] & (1UL << (bit & 63))) != 0;
    }

    /// <summary>
    /// 读取指定 bit 是否为 1。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Get(ReadOnlySpan<uint> bits, int bit)
    {
        ValidateBitIndex(bit);
        return (bits[bit >> 5] & (1U << (bit & 31))) != 0;
    }

    /// <summary>
    /// 将指定 bit 置为 1。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(Span<ulong> bits, int bit)
    {
        ValidateBitIndex(bit);
        bits[bit >> 6] |= 1UL << (bit & 63);
    }

    /// <summary>
    /// 将指定 bit 置为 1。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(Span<uint> bits, int bit)
    {
        ValidateBitIndex(bit);
        bits[bit >> 5] |= 1U << (bit & 31);
    }

    /// <summary>
    /// 将指定 bit 清为 0。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(Span<ulong> bits, int bit)
    {
        ValidateBitIndex(bit);
        bits[bit >> 6] &= ~(1UL << (bit & 63));
    }

    /// <summary>
    /// 将指定 bit 清为 0。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(Span<uint> bits, int bit)
    {
        ValidateBitIndex(bit);
        bits[bit >> 5] &= ~(1U << (bit & 31));
    }

    /// <summary>
    /// 将整个位图清空为 0。
    /// </summary>
    public static void ClearAll(Span<ulong> bits) => bits.Clear();

    /// <summary>
    /// 将整个位图清空为 0。
    /// </summary>
    public static void ClearAll(Span<uint> bits) => bits.Clear();

    /// <summary>
    /// 原地做并集：<c>bits |= other</c>。
    /// </summary>
    public static void OrWith(Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] |= other[i];
    }

    /// <summary>
    /// 原地做并集：<c>bits |= other</c>。
    /// </summary>
    public static void OrWith(Span<uint> bits, ReadOnlySpan<uint> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] |= other[i];
    }

    /// <summary>
    /// 原地做交集：<c>bits &amp;= other</c>。
    /// </summary>
    public static void AndWith(Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] &= other[i];
    }

    /// <summary>
    /// 原地做交集：<c>bits &amp;= other</c>。
    /// </summary>
    public static void AndWith(Span<uint> bits, ReadOnlySpan<uint> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] &= other[i];
    }

    /// <summary>
    /// 原地做差集：<c>bits &amp;= ~other</c>。
    /// </summary>
    public static void AndNotWith(Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] &= ~other[i];
    }

    /// <summary>
    /// 原地做差集：<c>bits &amp;= ~other</c>。
    /// </summary>
    public static void AndNotWith(Span<uint> bits, ReadOnlySpan<uint> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] &= ~other[i];
    }

    /// <summary>
    /// 原地做异或：<c>bits ^= other</c>。
    /// </summary>
    public static void XorWith(Span<ulong> bits, ReadOnlySpan<ulong> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] ^= other[i];
    }

    /// <summary>
    /// 原地做异或：<c>bits ^= other</c>。
    /// </summary>
    public static void XorWith(Span<uint> bits, ReadOnlySpan<uint> other)
    {
        ValidateSameLength(bits, other);
        for (var i = 0; i < bits.Length; i++) bits[i] ^= other[i];
    }

    /// <summary>
    /// 原地对整个位图取反。
    /// </summary>
    public static void NotWith(Span<ulong> bits)
    {
        for (var i = 0; i < bits.Length; i++) bits[i] = ~bits[i];
    }

    /// <summary>
    /// 原地对整个位图取反。
    /// </summary>
    public static void NotWith(Span<uint> bits)
    {
        for (var i = 0; i < bits.Length; i++) bits[i] = ~bits[i];
    }

    /// <summary>
    /// 判断位图是否全为 0。
    /// </summary>
    public static bool IsEmpty(ReadOnlySpan<ulong> bits)
    {
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] != 0UL) return false;
        }

        return true;
    }

    /// <summary>
    /// 判断位图是否全为 0。
    /// </summary>
    public static bool IsEmpty(ReadOnlySpan<uint> bits)
    {
        for (var i = 0; i < bits.Length; i++)
        {
            if (bits[i] != 0U) return false;
        }

        return true;
    }

    /// <summary>
    /// 判断位图中是否存在任意一个置位 bit。
    /// </summary>
    public static bool HasAnySet(ReadOnlySpan<ulong> bits) => !IsEmpty(bits);

    /// <summary>
    /// 判断位图中是否存在任意一个置位 bit。
    /// </summary>
    public static bool HasAnySet(ReadOnlySpan<uint> bits) => !IsEmpty(bits);

    /// <summary>
    /// 判断 <paramref name="superset"/> 是否完整包含 <paramref name="subset"/>。
    /// </summary>
    public static bool ContainsAll(ReadOnlySpan<ulong> superset, ReadOnlySpan<ulong> subset)
    {
        ValidateSameLength(superset, subset);
        for (var i = 0; i < superset.Length; i++)
        {
            if ((superset[i] & subset[i]) != subset[i]) return false;
        }

        return true;
    }

    /// <summary>
    /// 判断 <paramref name="superset"/> 是否完整包含 <paramref name="subset"/>。
    /// </summary>
    public static bool ContainsAll(ReadOnlySpan<uint> superset, ReadOnlySpan<uint> subset)
    {
        ValidateSameLength(superset, subset);
        for (var i = 0; i < superset.Length; i++)
        {
            if ((superset[i] & subset[i]) != subset[i]) return false;
        }

        return true;
    }

    /// <summary>
    /// 判断两个位图是否存在交集。
    /// </summary>
    public static bool Overlaps(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
    {
        ValidateSameLength(a, b);
        for (var i = 0; i < a.Length; i++)
        {
            if ((a[i] & b[i]) != 0UL) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断两个位图是否存在交集。
    /// </summary>
    public static bool Overlaps(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        ValidateSameLength(a, b);
        for (var i = 0; i < a.Length; i++)
        {
            if ((a[i] & b[i]) != 0U) return true;
        }

        return false;
    }

    /// <summary>
    /// 统计位图中被置为 1 的 bit 数量。
    /// </summary>
    public static int PopCount(ReadOnlySpan<ulong> bits)
    {
        var count = 0;
        for (var i = 0; i < bits.Length; i++) count += BitOperations.PopCount(bits[i]);
        return count;
    }

    /// <summary>
    /// 统计位图中被置为 1 的 bit 数量。
    /// </summary>
    public static int PopCount(ReadOnlySpan<uint> bits)
    {
        var count = 0;
        for (var i = 0; i < bits.Length; i++) count += BitOperations.PopCount(bits[i]);
        return count;
    }

    /// <summary>
    /// 返回第一个置位 bit 的索引。
    /// 如果没有任何置位 bit，则返回 <c>-1</c>。
    /// </summary>
    public static int FindFirstSet(ReadOnlySpan<ulong> bits) => FindNextSet(bits, 0);

    /// <summary>
    /// 返回第一个置位 bit 的索引。
    /// 如果没有任何置位 bit，则返回 <c>-1</c>。
    /// </summary>
    public static int FindFirstSet(ReadOnlySpan<uint> bits) => FindNextSet(bits, 0);

    /// <summary>
    /// 返回从 <paramref name="start"/> 开始（含当前位）往后找到的第一个置位 bit 索引。
    /// 如果没有找到，则返回 <c>-1</c>。
    /// 当 <paramref name="start"/> 小于 0 时，按 0 处理。
    /// </summary>
    public static int FindNextSet(ReadOnlySpan<ulong> bits, int start)
    {
        if (start < 0)
            start = 0;

        var wi = start >> 6;

        if (wi >= bits.Length)
            return -1;

        var word = bits[wi] & (~0UL << (start & 63));

        while (true)
        {
            if (word != 0UL)
                return (wi << 6) + BitOperations.TrailingZeroCount(word);

            wi++;

            if (wi >= bits.Length)
                return -1;

            word = bits[wi];
        }
    }

    /// <summary>
    /// 返回从 <paramref name="start"/> 开始（含当前位）往后找到的第一个置位 bit 索引。
    /// 如果没有找到，则返回 <c>-1</c>。
    /// 当 <paramref name="start"/> 小于 0 时，按 0 处理。
    /// </summary>
    public static int FindNextSet(ReadOnlySpan<uint> bits, int start)
    {
        if (start < 0)
            start = 0;

        var wi = start >> 5;

        if (wi >= bits.Length)
            return -1;

        var word = bits[wi] & (~0U << (start & 31));

        while (true)
        {
            if (word != 0U)
                return (wi << 5) + BitOperations.TrailingZeroCount(word);

            wi++;

            if (wi >= bits.Length)
                return -1;

            word = bits[wi];
        }
    }

    /// <summary>
    /// 返回一个零 GC 的 <see cref="ulong"/> 位置位迭代器。
    /// </summary>
    public static UInt64BitIterator EnumerateSetBits(ReadOnlySpan<ulong> bits) => new(bits);

    /// <summary>
    /// 返回一个零 GC 的 <see cref="uint"/> 位置位迭代器。
    /// </summary>
    public static UInt32BitIterator EnumerateSetBits(ReadOnlySpan<uint> bits) => new(bits);

    /// <summary>
    /// 将整个位图左移指定 bit 数。
    /// 如果左移量大于等于位图总 word 数覆盖范围，结果会被清零。
    /// </summary>
    public static void LeftShift(Span<ulong> bits, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0 || bits.Length == 0)
            return;

        var wordShift = count >> 6;
        var bitShift = count & 63;

        if (wordShift >= bits.Length)
        {
            bits.Clear();
            return;
        }

        for (var i = bits.Length - 1; i >= 0; i--)
        {
            ulong value = 0;
            var src = i - wordShift;

            if (src >= 0)
            {
                value = bits[src] << bitShift;

                if (bitShift != 0 && src > 0)
                    value |= bits[src - 1] >> (64 - bitShift);
            }

            bits[i] = value;
        }
    }

    /// <summary>
    /// 将整个位图左移指定 bit 数。
    /// 如果左移量大于等于位图总 word 数覆盖范围，结果会被清零。
    /// </summary>
    public static void LeftShift(Span<uint> bits, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0 || bits.Length == 0)
            return;

        var wordShift = count >> 5;
        var bitShift = count & 31;

        if (wordShift >= bits.Length)
        {
            bits.Clear();
            return;
        }

        for (var i = bits.Length - 1; i >= 0; i--)
        {
            uint value = 0;
            var src = i - wordShift;

            if (src >= 0)
            {
                value = bits[src] << bitShift;

                if (bitShift != 0 && src > 0)
                    value |= bits[src - 1] >> (32 - bitShift);
            }

            bits[i] = value;
        }
    }

    /// <summary>
    /// 将整个位图右移指定 bit 数。
    /// 如果右移量大于等于位图总 word 数覆盖范围，结果会被清零。
    /// </summary>
    public static void RightShift(Span<ulong> bits, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0 || bits.Length == 0)
            return;

        var wordShift = count >> 6;
        var bitShift = count & 63;

        if (wordShift >= bits.Length)
        {
            bits.Clear();
            return;
        }

        for (var i = 0; i < bits.Length; i++)
        {
            ulong value = 0;
            var src = i + wordShift;

            if (src < bits.Length)
            {
                value = bits[src] >> bitShift;

                if (bitShift != 0 && src + 1 < bits.Length)
                    value |= bits[src + 1] << (64 - bitShift);
            }

            bits[i] = value;
        }
    }

    /// <summary>
    /// 将整个位图右移指定 bit 数。
    /// 如果右移量大于等于位图总 word 数覆盖范围，结果会被清零。
    /// </summary>
    public static void RightShift(Span<uint> bits, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (count == 0 || bits.Length == 0)
            return;

        var wordShift = count >> 5;
        var bitShift = count & 31;

        if (wordShift >= bits.Length)
        {
            bits.Clear();
            return;
        }

        for (var i = 0; i < bits.Length; i++)
        {
            uint value = 0;
            var src = i + wordShift;

            if (src < bits.Length)
            {
                value = bits[src] >> bitShift;

                if (bitShift != 0 && src + 1 < bits.Length)
                    value |= bits[src + 1] << (32 - bitShift);
            }

            bits[i] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateBitIndex(int bit)
    {
        if (bit < 0)
            throw new ArgumentOutOfRangeException(nameof(bit));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateSameLength<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        where T : unmanaged
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Bitset spans must have the same length.");
    }
}

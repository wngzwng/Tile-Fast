using Tile.Core.Common.BitSet;
using NUnit.Framework;

namespace Tile.Core.Tests.Common.BitSet;

public sealed class BitSetOperationsTests
{
    [Test]
    public void BitLenConverters_ReturnRoundedWordCounts()
    {
        Assert.That(BitSetOperations.BitLenToU64Len(0), Is.EqualTo(0));
        Assert.That(BitSetOperations.BitLenToU64Len(1), Is.EqualTo(1));
        Assert.That(BitSetOperations.BitLenToU64Len(64), Is.EqualTo(1));
        Assert.That(BitSetOperations.BitLenToU64Len(65), Is.EqualTo(2));

        Assert.That(BitSetOperations.BitLenToU32Len(0), Is.EqualTo(0));
        Assert.That(BitSetOperations.BitLenToU32Len(1), Is.EqualTo(1));
        Assert.That(BitSetOperations.BitLenToU32Len(32), Is.EqualTo(1));
        Assert.That(BitSetOperations.BitLenToU32Len(33), Is.EqualTo(2));
    }

    [Test]
    public void BitLenConverters_NegativeInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BitSetOperations.BitLenToU64Len(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitSetOperations.BitLenToU32Len(-1));
    }

    [Test]
    public void GetSetClearAndClearAll_WorkForUlong()
    {
        ulong[] bits = new ulong[2];

        BitSetOperations.Set(bits, 1);
        BitSetOperations.Set(bits, 65);

        Assert.That(BitSetOperations.Get(bits, 1), Is.True);
        Assert.That(BitSetOperations.Get(bits, 65), Is.True);

        BitSetOperations.Clear(bits, 1);

        Assert.That(BitSetOperations.Get(bits, 1), Is.False);
        Assert.That(BitSetOperations.Get(bits, 65), Is.True);

        BitSetOperations.ClearAll(bits);

        Assert.That(BitSetOperations.IsEmpty(bits), Is.True);
    }

    [Test]
    public void GetSetClear_WorkForUint()
    {
        uint[] bits = new uint[2];

        BitSetOperations.Set(bits, 1);
        BitSetOperations.Set(bits, 33);
        BitSetOperations.Clear(bits, 1);

        Assert.That(BitSetOperations.Get(bits, 1), Is.False);
        Assert.That(BitSetOperations.Get(bits, 33), Is.True);
    }

    [Test]
    public void SetOperations_WorkInPlace()
    {
        ulong[] a = [0b1011UL];
        ulong[] b = [0b0110UL];

        BitSetOperations.AndWith(a, b);
        Assert.That(a[0], Is.EqualTo(0b0010UL));

        a[0] = 0b1011UL;
        BitSetOperations.OrWith(a, b);
        Assert.That(a[0], Is.EqualTo(0b1111UL));

        a[0] = 0b1011UL;
        BitSetOperations.AndNotWith(a, b);
        Assert.That(a[0], Is.EqualTo(0b1001UL));

        a[0] = 0b1011UL;
        BitSetOperations.XorWith(a, b);
        Assert.That(a[0], Is.EqualTo(0b1101UL));

        BitSetOperations.NotWith(a);
        Assert.That(a[0], Is.EqualTo(~0b1101UL));
    }

    [Test]
    public void RelationshipOperations_ReturnExpectedValues()
    {
        ulong[] superset = [0b1111UL];
        ulong[] subset = [0b0101UL];
        ulong[] disjoint = [0b10000UL];

        Assert.That(BitSetOperations.ContainsAll(superset, subset), Is.True);
        Assert.That(BitSetOperations.ContainsAll(subset, superset), Is.False);
        Assert.That(BitSetOperations.Overlaps(superset, subset), Is.True);
        Assert.That(BitSetOperations.Overlaps(superset, disjoint), Is.False);
        Assert.That(BitSetOperations.HasAnySet(superset), Is.True);
        Assert.That(BitSetOperations.HasAnySet(Array.Empty<ulong>()), Is.False);
    }

    [Test]
    public void PopCountAndFindMethods_ReturnExpectedValues()
    {
        // 这里重点验证“找不到时返回 -1”这个约定。
        ulong[] bits = [0b10100100UL];

        Assert.That(BitSetOperations.PopCount(bits), Is.EqualTo(3));
        Assert.That(BitSetOperations.FindFirstSet(bits), Is.EqualTo(2));
        Assert.That(BitSetOperations.FindNextSet(bits, 3), Is.EqualTo(5));
        Assert.That(BitSetOperations.FindNextSet(bits, 6), Is.EqualTo(7));
        Assert.That(BitSetOperations.FindNextSet(bits, 8), Is.EqualTo(-1));
        Assert.That(BitSetOperations.FindFirstSet(Array.Empty<ulong>()), Is.EqualTo(-1));
    }

    [Test]
    public void FindNextSet_NegativeStart_TreatedAsZero()
    {
        ulong[] bits = [0b1000UL];

        Assert.That(BitSetOperations.FindNextSet(bits, -10), Is.EqualTo(3));
    }

    [Test]
    public void EnumerateSetBits_IteratesAllSetBitsWithoutAllocation()
    {
        ulong[] bits = [0b10100100UL];
        var iterator = BitSetOperations.EnumerateSetBits(bits);
        var result = new List<int>();

        while (iterator.MoveNext(out var bit))
            result.Add(bit);

        Assert.That(result, Is.EqualTo(new[] { 2, 5, 7 }));
    }

    [Test]
    public void EnumerateSetBits_ForUint_IteratesAllSetBits()
    {
        uint[] bits = [0b1001U];
        var iterator = BitSetOperations.EnumerateSetBits(bits);
        var result = new List<int>();

        while (iterator.MoveNext(out var bit))
            result.Add(bit);

        Assert.That(result, Is.EqualTo(new[] { 0, 3 }));
    }

    [Test]
    public void LeftShiftAndRightShift_WorkAcrossWordBoundaries()
    {
        ulong[] bits = new ulong[2];
        BitSetOperations.Set(bits, 1);
        BitSetOperations.LeftShift(bits, 65);

        Assert.That(BitSetOperations.Get(bits, 66), Is.True);
        Assert.That(BitSetOperations.Get(bits, 1), Is.False);

        BitSetOperations.RightShift(bits, 65);

        Assert.That(BitSetOperations.Get(bits, 1), Is.True);
        Assert.That(BitSetOperations.Get(bits, 66), Is.False);
    }

    [Test]
    public void Shift_ByTooMuch_ClearsBits()
    {
        // 约定：移位量过大时，结果直接清零。
        ulong[] bits = [1UL];

        BitSetOperations.LeftShift(bits, 64);
        Assert.That(BitSetOperations.IsEmpty(bits), Is.True);

        bits[0] = 1UL;
        BitSetOperations.RightShift(bits, 64);
        Assert.That(BitSetOperations.IsEmpty(bits), Is.True);
    }

    [Test]
    public void Shift_NegativeCount_Throws()
    {
        ulong[] bits = [1UL];
        uint[] bits32 = [1U];

        Assert.Throws<ArgumentOutOfRangeException>(() => BitSetOperations.LeftShift(bits, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitSetOperations.RightShift(bits, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitSetOperations.LeftShift(bits32, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitSetOperations.RightShift(bits32, -1));
    }

    [Test]
    public void ExtensionMethods_ForwardToOperations()
    {
        ulong[] bits = [0b0011UL];
        ulong[] other = [0b0101UL];

        bits.AsSpan().OrWith(other);

        Assert.That(bits[0], Is.EqualTo(0b0111UL));
        Assert.That(((ReadOnlySpan<ulong>)bits).FindFirstSet(), Is.EqualTo(0));
    }
}

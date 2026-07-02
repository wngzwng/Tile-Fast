using Tile.Core.Common.BitSet;
using System.Buffers;
using Tile.Core.Common.Math;

namespace Tile.Core.Simulation;

public sealed partial class FseFinder
{
    /// <summary>
    /// 单花色 FSE 搜索状态。
    /// Fixed 是已展开并确认进入行为链的牌，Pick 时全选；
    /// Selectable 是当前上下文下已可直接选择的同花色牌，用于补足消除；
    /// Expanded 是最近一次展开得到的新可选牌，Pick 时至少选 1。
    /// </summary>
    private sealed class FseContext
    {
        public int[] Fixed { get; private set; } = [];

        public int FixedCount { get; private set; }

        public ulong[] FixedBits { get; private set; } = [];

        public int[] Selectable { get; private set; } = [];

        public int SelectableCount { get; private set; }

        public ulong[] SelectableBitsAfterFixed { get; private set; } = [];

        public int[] Expanded { get; private set; } = [];

        public int ExpandedCount { get; private set; }

        public int Suit { get; private set; }

        public void Initialize(
            int suit,
            int[] fixedGroup,
            int fixedCount,
            ulong[] fixedBits,
            int[] selectableGroup,
            int selectableCount,
            ulong[] selectableBitsAfterFixed,
            int[] expandedGroup,
            int expandedCount)
        {
            Suit = suit;
            Fixed = fixedGroup;
            FixedCount = fixedCount;
            FixedBits = fixedBits;
            Selectable = selectableGroup;
            SelectableCount = selectableCount;
            SelectableBitsAfterFixed = selectableBitsAfterFixed;
            Expanded = expandedGroup;
            ExpandedCount = expandedCount;
        }

        public void Reset()
        {
            Suit = default;
            Fixed = [];
            FixedCount = 0;
            FixedBits = [];
            Selectable = [];
            SelectableCount = 0;
            SelectableBitsAfterFixed = [];
            Expanded = [];
            ExpandedCount = 0;
        }

        public bool CanPick(int clearNeedCount)
        {
            // 困难消除必须包含至少一张 Expanded，且 F/S/E 合计要能凑满本次消除需要数。
            if (clearNeedCount <= 0 || ExpandedCount < 1)
                return false;

            return FixedCount + SelectableCount + ExpandedCount >= clearNeedCount;
        }

        public bool CanAdvance(int clearNeedCount)
        {
            // 下一层仍会从 Expanded 至少选 1；只要 Fixed+1 还不足以完成消除，就允许继续展开。
            return clearNeedCount > 0 &&
                   FixedCount + 1 < clearNeedCount;
        }

        public IEnumerable<FseContext> Advance(
            FseContextPool contextPool,
            Func<int, ReadOnlyMemory<ulong>, ReadOnlyMemory<ulong>, int[]> expand)
        {
            var fixedBits = FixedBits.AsMemory(0, contextPool.WordCount);
            var selectableBitsAfterFixed = SelectableBitsAfterFixed.AsMemory(0, contextPool.WordCount);

            for (var i = 0; i < ExpandedCount; i++)
            {
                var expandedTileIndex = Expanded[i];
                var newExpanded = expand(
                    expandedTileIndex,
                    fixedBits,
                    selectableBitsAfterFixed);
                if (newExpanded.Length == 0)
                    continue;

                yield return contextPool.RentAdvanced(
                    this,
                    expandedTileIndex,
                    newExpanded);
            }
        }

        public IEnumerable<FsePick> Pick(int clearNeedCount)
        {
            // Pick 规则：Fixed 全选，Expanded 至少选 1，其余数量由 Selectable 补足。
            var rest = clearNeedCount - FixedCount;
            if (rest <= 0 || ExpandedCount < 1)
                yield break;

            var expandedPickMin = Math.Max(1, rest - SelectableCount);
            var expandedPickMax = Math.Min(ExpandedCount, rest);

            for (var expandedPickCount = expandedPickMin; expandedPickCount <= expandedPickMax; expandedPickCount++)
            {
                var selectablePickCount = rest - expandedPickCount;
                if (selectablePickCount > SelectableCount)
                    continue;

                foreach (var expandedPickMask in MathKit.EnumerateChooseMasks(ExpandedCount, expandedPickCount))
                {
                    foreach (var selectablePickMask in MathKit.EnumerateChooseMasks(SelectableCount, selectablePickCount))
                    {
                        yield return FsePick.Create(
                            FullMask(FixedCount),
                            expandedPickMask,
                            selectablePickMask);
                    }
                }
            }
        }

        public int WriteSelectIds(FsePick pick, Span<int> destination)
        {
            var writeIndex = 0;

            WriteByMask(Fixed, FixedCount, pick.FixedMask, destination, ref writeIndex);
            WriteByMask(Selectable, SelectableCount, pick.SelectableMask, destination, ref writeIndex);
            WriteByMask(Expanded, ExpandedCount, pick.ExpandedMask, destination, ref writeIndex);

            return writeIndex;
        }

        private static void WriteByMask(
            int[] source,
            int count,
            ulong mask,
            Span<int> destination,
            ref int writeIndex)
        {
            if (mask == 0UL)
                return;

            for (var i = 0; i < count; i++)
            {
                if (((mask >> i) & 1UL) != 0)
                    destination[writeIndex++] = source[i];
            }
        }

        private static ulong FullMask(int count)
            => count >= 64 ? ulong.MaxValue : (1UL << count) - 1UL;
    }

    private sealed class FseContextPool
    {
        private readonly ArrayPool<int> _arrayPool;
        private readonly ArrayPool<ulong> _bitArrayPool;
        private readonly Stack<FseContext> _contexts = new();
        private readonly int _wordCount;

        public FseContextPool(
            int wordCount,
            ArrayPool<int>? arrayPool = null,
            ArrayPool<ulong>? bitArrayPool = null)
        {
            if (wordCount < 0)
                throw new ArgumentOutOfRangeException(nameof(wordCount));

            _wordCount = wordCount;
            _arrayPool = arrayPool ?? ArrayPool<int>.Shared;
            _bitArrayPool = bitArrayPool ?? ArrayPool<ulong>.Shared;
        }

        public int WordCount => _wordCount;

        public FseContext Rent(
            int suit,
            ReadOnlySpan<int> fixedGroup,
            ReadOnlySpan<int> selectableGroup,
            ReadOnlySpan<int> expandedGroup)
        {
            var context = _contexts.Count > 0
                ? _contexts.Pop()
                : new FseContext();

            context.Initialize(
                suit,
                RentCopy(fixedGroup),
                fixedGroup.Length,
                RentBits(fixedGroup),
                RentCopy(selectableGroup),
                selectableGroup.Length,
                RentSelectableBits(selectableGroup, expandedGroup),
                RentCopy(expandedGroup),
                expandedGroup.Length);

            return context;
        }

        public FseContext RentAdvanced(FseContext source, int expandedTileIndex, ReadOnlySpan<int> newExpanded)
        {
            var fixedGroup = _arrayPool.Rent(source.FixedCount + 1);
            source.Fixed.AsSpan(0, source.FixedCount).CopyTo(fixedGroup);
            fixedGroup[source.FixedCount] = expandedTileIndex;

            var selectableCount = source.SelectableCount + source.ExpandedCount - 1;
            var selectableGroup = _arrayPool.Rent(selectableCount);
            source.Selectable.AsSpan(0, source.SelectableCount).CopyTo(selectableGroup);

            var writeIndex = source.SelectableCount;
            for (var i = 0; i < source.ExpandedCount; i++)
            {
                var tileIndex = source.Expanded[i];
                if (tileIndex == expandedTileIndex)
                    continue;

                selectableGroup[writeIndex++] = tileIndex;
            }

            var context = _contexts.Count > 0
                ? _contexts.Pop()
                : new FseContext();

            context.Initialize(
                source.Suit,
                fixedGroup,
                source.FixedCount + 1,
                RentFixedBits(source.FixedBits, expandedTileIndex),
                selectableGroup,
                selectableCount,
                RentSelectableBits(
                    selectableGroup.AsSpan(0, selectableCount),
                    newExpanded),
                RentCopy(newExpanded),
                newExpanded.Length);

            return context;
        }

        public void Return(FseContext context)
        {
            ReturnArray(context.Fixed, context.FixedCount);
            ReturnBitArray(context.FixedBits);
            ReturnArray(context.Selectable, context.SelectableCount);
            ReturnBitArray(context.SelectableBitsAfterFixed);
            ReturnArray(context.Expanded, context.ExpandedCount);

            context.Reset();
            _contexts.Push(context);
        }

        private int[] RentCopy(ReadOnlySpan<int> source)
        {
            if (source.IsEmpty)
                return [];

            var destination = _arrayPool.Rent(source.Length);
            source.CopyTo(destination);
            return destination;
        }

        private ulong[] RentSelectableBits(
            ReadOnlySpan<int> selectableGroup,
            ReadOnlySpan<int> expandedGroup)
        {
            if (_wordCount == 0)
                return [];

            var bits = _bitArrayPool.Rent(_wordCount);
            bits.AsSpan(0, _wordCount).Clear();

            foreach (var tileIndex in selectableGroup)
                BitSetOperations.Set(bits.AsSpan(0, _wordCount), tileIndex);

            foreach (var tileIndex in expandedGroup)
                BitSetOperations.Set(bits.AsSpan(0, _wordCount), tileIndex);

            return bits;
        }

        private ulong[] RentBits(ReadOnlySpan<int> tileIndexes)
        {
            if (_wordCount == 0)
                return [];

            var bits = _bitArrayPool.Rent(_wordCount);
            bits.AsSpan(0, _wordCount).Clear();

            foreach (var tileIndex in tileIndexes)
                BitSetOperations.Set(bits.AsSpan(0, _wordCount), tileIndex);

            return bits;
        }

        private ulong[] RentFixedBits(ulong[] sourceFixedBits, int extraTileIndex)
        {
            if (_wordCount == 0)
                return [];

            var bits = _bitArrayPool.Rent(_wordCount);
            sourceFixedBits.AsSpan(0, _wordCount).CopyTo(bits);
            BitSetOperations.Set(bits.AsSpan(0, _wordCount), extraTileIndex);

            return bits;
        }

        private void ReturnArray(int[] array, int count)
        {
            if (array.Length == 0)
                return;

            Array.Clear(array, 0, count);
            _arrayPool.Return(array);
        }

        private void ReturnBitArray(ulong[] array)
        {
            if (array.Length == 0)
                return;

            Array.Clear(array, 0, _wordCount);
            _bitArrayPool.Return(array);
        }
    }
}

using Tile.Core.Common.BitSet;
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
        private readonly int _wordCount;

        public FseContext(int matchRequireCount, int wordCount)
        {
            if (matchRequireCount < 0)
                throw new ArgumentOutOfRangeException(nameof(matchRequireCount));
            if (wordCount < 0)
                throw new ArgumentOutOfRangeException(nameof(wordCount));

            // Context 会在 DFS 中反复复用；这些 buffer 属于状态本身，不再向 ArrayPool 租借。
            _wordCount = wordCount;
            Fixed = new int[matchRequireCount];
            FixedBits = new ulong[wordCount];
            SelectableBits = new ulong[wordCount];
            ExpandedBits = new ulong[wordCount];
            SelectableBitsAfterFixed = new ulong[wordCount];
        }

        // Fixed 需要保留展开链顺序；S/E 只是集合，用 bitset 更贴近后续状态模拟。
        public int[] Fixed { get; }

        public int FixedCount { get; private set; }

        public ulong[] FixedBits { get; }

        public ulong[] SelectableBits { get; }

        public int SelectableCount { get; private set; }

        // 包含当前上下文下已经可直接选择的同色牌，用于过滤“再次展开得到的新增牌”。
        public ulong[] SelectableBitsAfterFixed { get; }

        public ulong[] ExpandedBits { get; }

        public int ExpandedCount { get; private set; }

        public int Suit { get; private set; }

        public void InitializeRoot(
            int suit,
            ReadOnlySpan<ulong> expandedBits,
            int expandedCount)
        {
            Suit = suit;
            FixedCount = 0;
            SelectableCount = 0;
            ExpandedCount = expandedCount;

            ClearState();
            expandedBits[.._wordCount].CopyTo(ExpandedBits);
            // Root 没有 Fixed，原始可选组既是第一层 Expanded，也是 fixed 后已可选集合。
            expandedBits[.._wordCount].CopyTo(SelectableBitsAfterFixed);
        }

        public void InitializeAdvanced(
            FseContext source,
            int expandedTileIndex,
            ReadOnlySpan<ulong> expandedBits,
            int expandedCount)
        {
            Suit = source.Suit;
            FixedCount = source.FixedCount + 1;
            SelectableCount = source.SelectableCount + source.ExpandedCount - 1;
            ExpandedCount = expandedCount;

            ClearState();

            source.Fixed.AsSpan(0, source.FixedCount).CopyTo(Fixed);
            Fixed[source.FixedCount] = expandedTileIndex;

            source.FixedBits.AsSpan(0, _wordCount).CopyTo(FixedBits);
            BitSetOperations.Set(FixedBits, expandedTileIndex);

            source.SelectableBits.AsSpan(0, _wordCount).CopyTo(SelectableBits);
            BitSetOperations.OrWith(SelectableBits, source.ExpandedBits.AsSpan(0, _wordCount));
            BitSetOperations.Clear(SelectableBits, expandedTileIndex);

            expandedBits[.._wordCount].CopyTo(ExpandedBits);

            // 下一层展开只应该关注“新露出的牌”，所以要把旧 S/E 和本轮新 E 都记入已可选集合。
            SelectableBits.AsSpan(0, _wordCount).CopyTo(SelectableBitsAfterFixed);
            BitSetOperations.OrWith(SelectableBitsAfterFixed, ExpandedBits.AsSpan(0, _wordCount));
        }

        public void Reset()
        {
            Suit = default;
            FixedCount = 0;
            SelectableCount = 0;
            ExpandedCount = 0;
            ClearState();
        }

        private void ClearState()
        {
            Array.Clear(Fixed, 0, Fixed.Length);
            FixedBits.AsSpan(0, _wordCount).Clear();
            SelectableBits.AsSpan(0, _wordCount).Clear();
            ExpandedBits.AsSpan(0, _wordCount).Clear();
            SelectableBitsAfterFixed.AsSpan(0, _wordCount).Clear();
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

            WriteByMask(Fixed.AsSpan(0, FixedCount), pick.FixedMask, destination, ref writeIndex);

            // S/E 平时只保存 bitset；只有输出具体行为时才临时展开成按 mask 可寻址的 index span。
            Span<int> selectable = stackalloc int[SelectableCount];
            TileIndexSet.Wrap(SelectableBits.AsSpan(0, _wordCount)).CopyTo(selectable);
            WriteByMask(selectable, pick.SelectableMask, destination, ref writeIndex);

            Span<int> expanded = stackalloc int[ExpandedCount];
            TileIndexSet.Wrap(ExpandedBits.AsSpan(0, _wordCount)).CopyTo(expanded);
            WriteByMask(expanded, pick.ExpandedMask, destination, ref writeIndex);

            return writeIndex;
        }

        private static void WriteByMask(
            ReadOnlySpan<int> source,
            ulong mask,
            Span<int> destination,
            ref int writeIndex)
        {
            if (mask == 0UL)
                return;

            for (var i = 0; i < source.Length; i++)
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
        // Pool 只负责 Context 生命周期；展开 scratch 由搜索过程持有，避免资源管理边界扩散。
        private readonly Stack<FseContext> _contexts = new();
        private readonly int _matchRequireCount;
        private readonly int _wordCount;

        public FseContextPool(
            int matchRequireCount,
            int wordCount)
        {
            if (matchRequireCount < 0)
                throw new ArgumentOutOfRangeException(nameof(matchRequireCount));
            if (wordCount < 0)
                throw new ArgumentOutOfRangeException(nameof(wordCount));

            _matchRequireCount = matchRequireCount;
            _wordCount = wordCount;
        }

        public int WordCount => _wordCount;

        public FseContext Rent(
            int suit,
            ReadOnlySpan<ulong> expandedBits,
            int expandedCount)
        {
            var context = _contexts.Count > 0
                ? _contexts.Pop()
                : new FseContext(_matchRequireCount, _wordCount);

            context.InitializeRoot(
                suit,
                expandedBits,
                expandedCount);

            return context;
        }

        public FseContext RentAdvanced(
            FseContext source,
            int expandedTileIndex,
            ReadOnlySpan<ulong> expandedBits,
            int expandedCount)
        {
            var context = _contexts.Count > 0
                ? _contexts.Pop()
                : new FseContext(_matchRequireCount, _wordCount);

            context.InitializeAdvanced(
                source,
                expandedTileIndex,
                expandedBits,
                expandedCount);

            return context;
        }

        public void Return(FseContext context)
        {
            context.Reset();
            _contexts.Push(context);
        }
    }
}

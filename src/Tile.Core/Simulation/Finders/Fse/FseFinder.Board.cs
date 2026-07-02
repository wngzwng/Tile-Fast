using Tile.Core.Common.BitSet;
using Tile.Core.Core;

namespace Tile.Core.Simulation;

public sealed partial class FseFinder
{
    /// <summary>
    /// FSE 对当前盘面的窄视图。
    /// 不缓存单色 tile 集合，只保存进入搜索需要的花色入口事实。
    /// </summary>
    private sealed class FseBoard
    {
        private FseBoard(
            LevelCore level,
            ulong selectableSuitBits)
        {
            Level = level;
            SelectableSuitBits = selectableSuitBits;
            AvailableCapacity = level.StagingArea.AvailableCapacity;
            MatchRequireCount = level.RuleSpec.MatchRequireCount;
            WordCount = level.Mapping.WordCount;
        }

        public static FseBoard Create(LevelCore level)
        {
            var selectableSuitBits = 0UL;

            foreach (var tileIndex in level.Pasture.SelectableTiles)
            {
                var suit = level.Mapping.GetSuit(tileIndex);
                selectableSuitBits |= 1UL << suit;
            }

            return new FseBoard(
                level,
                selectableSuitBits);
        }

        public LevelCore Level { get; }

        public ulong SelectableSuitBits { get; }

        public int AvailableCapacity { get; }

        public int MatchRequireCount { get; }

        public int WordCount { get; }

        public ReadOnlySpan<ulong> OriginSelectableBits => Level.Pasture.SelectableTiles.Bits;

        /// <summary>
        /// 当前花色还需要多少张牌才能凑满一次消除。
        /// </summary>
        public int GetClearNeedCount(int suit)
        {
            return MatchRequireCount - Level.StagingArea.SuitCounter[suit];
        }

        /// <summary>
        /// 只搜索能在当前剩余卡槽容量内完成的消除行为。
        /// </summary>
        public bool CanBuildClear(int clearNeedCount)
        {
            return clearNeedCount > 0 &&
                   clearNeedCount <= AvailableCapacity;
        }

        /// <summary>
        /// 写出指定花色在当前原始可选集合中的 tile bitset。
        /// </summary>
        public int GetSuitBitSet(int suit, Span<ulong> buffer)
        {
            if (buffer.Length < WordCount)
                throw new ArgumentException("Suit bitset buffer length is too small.", nameof(buffer));

            var target = buffer[..WordCount];
            target.Clear();

            var suitBits = Level.Mapping.GetTileBitsBySuit(suit);
            if (suitBits.IsEmpty)
                return 0;

            OriginSelectableBits.CopyTo(target);
            BitSetOperations.AndWith(target, suitBits);

            return BitSetOperations.PopCount(target);
        }

        /// <summary>
        /// 收集 source 被移除后才变为可选的同花色候选。
        /// </summary>
        public int CollectExpandedTileBits(
            int sourceTileIndex,
            ReadOnlySpan<ulong> fixedBits,
            ReadOnlySpan<ulong> selectableBitsAfterFixed,
            Span<ulong> fixedAndSourceBits,
            Span<ulong> candidateBits)
        {
            if (candidateBits.Length < WordCount)
                throw new ArgumentException("Candidate bitset buffer length is too small.", nameof(candidateBits));
            if (fixedBits.Length < WordCount)
                throw new ArgumentException("Fixed bitset length is too small.", nameof(fixedBits));
            if (selectableBitsAfterFixed.Length < WordCount)
                throw new ArgumentException("Selectable bitset length is too small.", nameof(selectableBitsAfterFixed));
            if (fixedAndSourceBits.Length < WordCount)
                throw new ArgumentException("Fixed and source bitset buffer length is too small.", nameof(fixedAndSourceBits));

            var ignored = fixedAndSourceBits[..WordCount];
            fixedBits[..WordCount].CopyTo(ignored);
            BitSetOperations.Set(ignored, sourceTileIndex);

            var candidates = candidateBits[..WordCount];
            Level.Pasture.SimulateAffectedTileBits(
                sourceTileIndex,
                ignored,
                candidates);

            var suitBits = Level.Mapping.GetTileBitsBySuit(Level.Mapping.GetSuit(sourceTileIndex));
            if (suitBits.IsEmpty)
            {
                candidates.Clear();
                return 0;
            }

            // 只保留同花色、且不是当前上下文中已经可选的新增候选。
            BitSetOperations.AndWith(candidates, suitBits);
            BitSetOperations.AndNotWith(candidates, selectableBitsAfterFixed[..WordCount]);
            BitSetOperations.Clear(candidates, sourceTileIndex);

            foreach (var candidateIndex in TileIndexSet.Wrap(candidates))
            {
                // SimulateState 比 bitset 运算贵，放在最后做精确状态过滤。
                if (!IsSelectable(candidateIndex, ignored) ||
                    !IsVisibleInOrigin(candidateIndex))
                {
                    BitSetOperations.Clear(candidates, candidateIndex);
                }
            }

            return BitSetOperations.PopCount(candidates);
        }

        public bool IsVisibleInOrigin(int tileIndex)
            => IsVisible(tileIndex, ReadOnlySpan<ulong>.Empty);

        public bool IsSelectable(int tileIndex, ReadOnlySpan<ulong> ignoredTileBits)
        {
            Level.Pasture.SimulateState(
                tileIndex,
                ignoredTileBits,
                out _,
                out var selectable);

            return selectable;
        }

        private bool IsVisible(int tileIndex, ReadOnlySpan<ulong> ignoredTileBits)
        {
            Level.Pasture.SimulateState(
                tileIndex,
                ignoredTileBits,
                out var visible,
                out _);

            return visible;
        }
    }
}

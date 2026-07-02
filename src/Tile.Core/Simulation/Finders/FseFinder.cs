using System.Numerics;
using Tile.Core.Common.BitSet;
using Tile.Core.Core;

namespace Tile.Core.Simulation;

/// <summary>
/// FSE 行为搜索器：只依赖 LevelCore，通过回调输出 Behaviour。
/// </summary>
public sealed partial class FseFinder
{
    public static FseFinder Instance { get; } = new();

    public FseFinder()
    {
    }

    public int FindCandidates(
        LevelCore level,
        BehaviourFactory behaviourFactory,
        Action<Behaviour> behaviourCollect)
    {
        if (level is null)
            throw new ArgumentNullException(nameof(level));
        if (behaviourFactory is null)
            throw new ArgumentNullException(nameof(behaviourFactory));
        if (behaviourCollect is null)
            throw new ArgumentNullException(nameof(behaviourCollect));

        var board = FseBoard.Create(level);
        var contextPool = new FseContextPool(board.MatchRequireCount, board.WordCount);
        // 已参与任意消除候选的牌，不再作为 flip 候选输出。
        var coveredByClearBits = new ulong[board.WordCount];
        var candidateCount = 0;

        FindClearCandidates(
            board,
            contextPool,
            behaviourFactory,
            behaviourCollect,
            coveredByClearBits,
            ref candidateCount);
        FindFlipCandidates(
            board,
            behaviourFactory,
            behaviourCollect,
            coveredByClearBits,
            ref candidateCount);

        return candidateCount;
    }

    private static void FindClearCandidates(
        FseBoard board,
        FseContextPool contextPool,
        BehaviourFactory behaviourFactory,
        Action<Behaviour> behaviourCollect,
        Span<ulong> coveredByClearBits,
        ref int candidateCount)
    {
        foreach (var suit in EnumerateSuitBits(board.SelectableSuitBits))
        {
            var clearNeedCount = board.GetClearNeedCount(suit);

            if (!board.CanBuildClear(clearNeedCount))
                continue;

            var originSelectableBits = new ulong[board.WordCount];
            var originSelectableCount = board.GetSuitBitSet(suit, originSelectableBits);
            if (originSelectableCount == 0)
                continue;

            var root = contextPool.Rent(
                suit,
                originSelectableBits,
                originSelectableCount);

            SearchClearCandidates(
                board,
                contextPool,
                root,
                clearNeedCount,
                BehaviourKind.EasyClear,
                behaviourFactory,
                behaviourCollect,
                coveredByClearBits,
                ref candidateCount);
        }
    }

    private static void SearchClearCandidates(
        FseBoard board,
        FseContextPool contextPool,
        FseContext root,
        int clearNeedCount,
        BehaviourKind rootKind,
        BehaviourFactory behaviourFactory,
        Action<Behaviour> behaviourCollect,
        Span<ulong> coveredByClearBits,
        ref int candidateCount)
    {
        var stack = new Stack<SearchFrame>();
        // Scratch 属于一次单色搜索过程；Context 只保存可回溯状态，Pool 也不再代管临时资源。
        var fixedAndSourceScratchBits = new ulong[board.WordCount];
        var expandedScratchBits = new ulong[board.WordCount];
        stack.Push(new SearchFrame(root, rootKind));
        FseContext? activeContext = null;

        try
        {
            while (stack.Count > 0)
            {
                var frame = stack.Pop();
                var context = frame.Context;
                activeContext = context;

                PickClearCandidates(
                    board,
                    context,
                    clearNeedCount,
                    frame.Kind,
                    behaviourFactory,
                    behaviourCollect,
                    coveredByClearBits,
                    ref candidateCount);

                if (!context.CanAdvance(clearNeedCount))
                {
                    activeContext = null;
                    contextPool.Return(context);
                    continue;
                }

                foreach (var sourceTileIndex in TileIndexSet.Wrap(context.ExpandedBits.AsSpan(0, board.WordCount)))
                {
                    // 这里用当前层的已可选集合过滤新增 E，避免同一张牌在不同组里重复参与组合。
                    var expandedCount = FindExpandedTiles(
                        board,
                        sourceTileIndex,
                        context.FixedBits.AsSpan(0, board.WordCount),
                        context.SelectableBitsAfterFixed.AsSpan(0, board.WordCount),
                        fixedAndSourceScratchBits,
                        expandedScratchBits);

                    if (expandedCount == 0)
                        continue;

                    var next = contextPool.RentAdvanced(
                        context,
                        sourceTileIndex,
                        expandedScratchBits,
                        expandedCount);

                    stack.Push(new SearchFrame(next, BehaviourKind.HardClear));
                }

                activeContext = null;
                contextPool.Return(context);
            }
        }
        finally
        {
            if (activeContext is not null)
                contextPool.Return(activeContext);

            while (stack.Count > 0)
                contextPool.Return(stack.Pop().Context);
        }
    }

    private static void PickClearCandidates(
        FseBoard board,
        FseContext context,
        int clearNeedCount,
        BehaviourKind kind,
        BehaviourFactory behaviourFactory,
        Action<Behaviour> behaviourCollect,
        Span<ulong> coveredByClearBits,
        ref int candidateCount)
    {
        if (!context.CanPick(clearNeedCount))
            return;

        // clearNeedCount 来自匹配规则，预期很小；这里用栈上临时 buffer 避免候选构建分配。
        Span<int> selectIds = stackalloc int[clearNeedCount];
        foreach (var pick in context.Pick(clearNeedCount))
        {
            var writtenCount = context.WriteSelectIds(pick, selectIds);
            if (writtenCount != clearNeedCount)
                throw new InvalidOperationException("FSE pick wrote an unexpected number of select ids.");

            var behaviour = behaviourFactory(
                kind,
                context.Suit,
                selectIds);

            behaviourCollect(behaviour);
            candidateCount++;

            foreach (var tileIndex in selectIds)
                BitSetOperations.Set(coveredByClearBits, tileIndex);
        }
    }

    private static void FindFlipCandidates(
        FseBoard board,
        BehaviourFactory behaviourFactory,
        Action<Behaviour> behaviourCollect,
        ReadOnlySpan<ulong> coveredByClearBits,
        ref int candidateCount)
    {
        if (board.AvailableCapacity <= 0)
            return;

        Span<int> selectIds = stackalloc int[1];
        foreach (var tileIndex in TileIndexSet.Wrap(board.OriginSelectableBits))
        {
            if (BitSetOperations.Get(coveredByClearBits, tileIndex))
                continue;

            selectIds[0] = tileIndex;
            var behaviour = behaviourFactory(
                BehaviourKind.Flip,
                board.Level.Mapping.GetSuit(tileIndex),
                selectIds);

            behaviourCollect(behaviour);
            candidateCount++;
        }
    }

    private static int FindExpandedTiles(
        FseBoard board,
        int sourceTileIndex,
        ReadOnlySpan<ulong> fixedBits,
        ReadOnlySpan<ulong> selectableBitsAfterFixed,
        Span<ulong> fixedAndSourceScratchBits,
        Span<ulong> expandedScratchBits)
    {
        fixedAndSourceScratchBits[..board.WordCount].Clear();
        expandedScratchBits[..board.WordCount].Clear();

        return board.CollectExpandedTileBits(
            sourceTileIndex,
            fixedBits,
            selectableBitsAfterFixed,
            fixedAndSourceScratchBits,
            expandedScratchBits);
    }

    private static IEnumerable<int> EnumerateSuitBits(ulong suitBits)
    {
        while (suitBits != 0UL)
        {
            var suit = BitOperations.TrailingZeroCount(suitBits);
            yield return suit;

            suitBits &= suitBits - 1UL;
        }
    }
}

using System.Buffers;
using System.Numerics;
using Tile.Core.Common.BitSet;
using Tile.Core.Core;

namespace Tile.Core.Simulation;

/// <summary>
/// PiKa 规则下的行为评分器。
/// </summary>
public sealed class BehaviourScorerForPiKa
{
    private static readonly BehaviourScoreConfigPiKa s_defaultConfig = new();

    private static readonly Dictionary<int, BehaviourScoreConfigPiKa> s_configByMatchCount = new()
    {
        [2] = new BehaviourScoreConfigPiKa
        {
            EasyFullMatchBaseScoreAtStorage = 4d,
            EasyFullMatchBaseScoreAtNormal = 6d,
            EasyPartMatchBaseScore = 8d,
            HardBaseScore = 6d,
            FlipBaseScore = 0d,
            FreedSlotScoreFactor = 8d,
            ClearColorBonusScore = 4d,
            NewSelectableScoreFactor = 1d,
            NewVisibleScoreFactor = 0.5d,
            NewMatchChanceScoreFactor = 3d,
            SlotRelatedColorScoreFactor = 3d
        },

        [3] = new BehaviourScoreConfigPiKa
        {
            EasyFullMatchBaseScoreAtStorage = 4d,
            EasyFullMatchBaseScoreAtNormal = 6d,
            EasyPartMatchBaseScore = 8d,
            HardBaseScore = 6d,
            FlipBaseScore = 0d,
            FreedSlotScoreFactor = 8d,
            ClearColorBonusScore = 4d,
            NewSelectableScoreFactor = 1.6d,
            NewVisibleScoreFactor = 0.5d,
            NewMatchChanceScoreFactor = 3d,
            SlotRelatedColorScoreFactor = 3d
        }
    };

    private readonly BehaviourScoreConfigPiKa? _overrideConfig;

    public static BehaviourScorerForPiKa Instance { get; } = new();

    public BehaviourScorerForPiKa()
        : this(null)
    {
    }

    public BehaviourScorerForPiKa(BehaviourScoreConfigPiKa? overrideConfig)
    {
        _overrideConfig = overrideConfig;
    }

    public static BehaviourScoreConfigPiKa DefaultConfig => s_defaultConfig;

    public static IReadOnlyDictionary<int, BehaviourScoreConfigPiKa> DefaultConfigByMatchCount
        => s_configByMatchCount;

    public static BehaviourScoreConfigPiKa GetDefaultConfig(int matchCount)
        => GetConfig(matchCount);

    public double EvaluateScore(LevelCore level, Behaviour behaviour)
    {
        if (level is null)
            throw new ArgumentNullException(nameof(level));
        if (behaviour is null)
            throw new ArgumentNullException(nameof(behaviour));

        var config = _overrideConfig ?? GetConfig(level.RuleSpec.MatchRequireCount);
        var context = BuildBehaviourScoreContext(level, behaviour, config);

        // 总分公式：
        // total = base
        //       + freedSlotCount * freedSlotFactor
        //       + canClearWholeColor ? clearColorBonus : 0
        //       + newSelectableCount * newSelectableFactor
        //       + newVisibleCount * newVisibleFactor
        //       + slotRelatedColorScore
        //       + newMatchChanceCount * newMatchChanceFactor
        double totalScore = 0d;
        totalScore += ComputeBaseScore(context);
        totalScore += ComputeFreedSlotScore(context);
        totalScore += ComputeClearWholeColorScore(context);
        totalScore += ComputeNewSelectableScore(context);
        totalScore += ComputeNewVisibleScore(context);
        totalScore += ComputeSlotRelatedColorScore(context);
        totalScore += ComputeNewMatchChanceScore(context);

        return totalScore;
    }

    private static BehaviourScoreContext BuildBehaviourScoreContext(
        LevelCore level,
        Behaviour behaviour,
        BehaviourScoreConfigPiKa config)
    {
        int wordCount = level.Mapping.WordCount;
        var buffer = ArrayPool<ulong>.Shared.Rent(wordCount * 4);

        try
        {
            var rented = buffer.AsSpan(0, wordCount * 4);
            rented.Clear();

            var originSelectableBits = rented[..wordCount];
            var newlyAddedSelectableBits = rented.Slice(wordCount, wordCount);
            var newlyAddedVisibleBits = rented.Slice(wordCount * 2, wordCount);
            var afterSelectableBits = rented.Slice(wordCount * 3, wordCount);

            level.Pasture.SelectableTiles.Bits.CopyTo(originSelectableBits);

            ulong newlyAddedSelectableSuitBits = 0UL;

            CollectNewlyAddedExposureBits(
                level,
                behaviour,
                originSelectableBits,
                newlyAddedSelectableBits,
                ref newlyAddedSelectableSuitBits,
                newlyAddedVisibleBits,
                afterSelectableBits);

            int newlyAddedSelectableCount = BitSetOperations.PopCount(newlyAddedSelectableBits);
            int newlyAddedVisibleCount = BitSetOperations.PopCount(newlyAddedVisibleBits);

            return new BehaviourScoreContext
            {
                Config = config,
                BaseScore = EvaluateBaseScore(level, behaviour, config),
                FreedSlotCount = EvaluateFreedSlotCount(level, behaviour),
                CanClearWholeColor = EvaluateCanClearWholeColor(
                    level,
                    behaviour,
                    afterSelectableBits),
                NewSelectableCount = newlyAddedSelectableCount,
                NewVisibleCount = newlyAddedVisibleCount,
                SlotRelatedColorScore = EvaluateSlotRelatedColorScore(
                    level,
                    behaviour,
                    newlyAddedSelectableCount,
                    newlyAddedSelectableSuitBits,
                    afterSelectableBits,
                    config),
                NewMatchChanceCount = EvaluateNewMatchChanceCount(
                    level,
                    behaviour,
                    newlyAddedSelectableCount,
                    newlyAddedSelectableSuitBits,
                    originSelectableBits,
                    afterSelectableBits),
            };
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 基础分：直接使用行为类型对应的基础分。
    /// </summary>
    private static double ComputeBaseScore(in BehaviourScoreContext context)
        => context.BaseScore;

    /// <summary>
    /// 腾槽收益公式：
    /// score = freedSlotCount * FreedSlotScoreFactor。
    /// </summary>
    private static double ComputeFreedSlotScore(in BehaviourScoreContext context)
        => context.FreedSlotCount * context.Config.FreedSlotScoreFactor;

    /// <summary>
    /// 清色收益公式：
    /// score = CanClearWholeColor ? ClearColorBonusScore : 0。
    /// </summary>
    private static double ComputeClearWholeColorScore(in BehaviourScoreContext context)
        => context.CanClearWholeColor ? context.Config.ClearColorBonusScore : 0d;

    /// <summary>
    /// 新增可选收益公式：
    /// score = NewSelectableCount * NewSelectableScoreFactor。
    /// </summary>
    private static double ComputeNewSelectableScore(in BehaviourScoreContext context)
        => context.NewSelectableCount * context.Config.NewSelectableScoreFactor;

    /// <summary>
    /// 新增可见收益公式：
    /// score = NewVisibleCount * NewVisibleScoreFactor。
    /// </summary>
    private static double ComputeNewVisibleScore(in BehaviourScoreContext context)
        => context.NewVisibleCount * context.Config.NewVisibleScoreFactor;

    /// <summary>
    /// 槽相关花色收益已经在指标层完成组合数/线性兜底计算，
    /// 公式层直接使用该指标值。
    /// </summary>
    private static double ComputeSlotRelatedColorScore(in BehaviourScoreContext context)
        => context.SlotRelatedColorScore;

    /// <summary>
    /// 新增可配机会收益公式：
    /// score = NewMatchChanceCount * NewMatchChanceScoreFactor。
    /// </summary>
    private static double ComputeNewMatchChanceScore(in BehaviourScoreContext context)
        => context.NewMatchChanceCount * context.Config.NewMatchChanceScoreFactor;

    /// <summary>
    /// 行为基础分公式：
    /// - EasyClear 且行为选择数等于 MatchRequireCount：EasyFullMatchBaseScoreAtNormal
    /// - EasyClear 但未完整匹配：EasyPartMatchBaseScore
    /// - HardClear：HardBaseScore
    /// - Flip：FlipBaseScore
    /// </summary>
    private static double EvaluateBaseScore(
        LevelCore level,
        Behaviour behaviour,
        BehaviourScoreConfigPiKa config)
    {
        bool isFullEasyClear = behaviour.Count == level.RuleSpec.MatchRequireCount;

        return behaviour.Kind switch
        {
            BehaviourKind.EasyClear => isFullEasyClear
                ? config.EasyFullMatchBaseScoreAtNormal
                : config.EasyPartMatchBaseScore,

            BehaviourKind.HardClear => config.HardBaseScore,

            BehaviourKind.Flip => config.FlipBaseScore,

            _ => throw new ArgumentOutOfRangeException(
                nameof(behaviour),
                behaviour.Kind,
                "Unknown behaviour kind.")
        };
    }

    /// <summary>
    /// 腾槽数公式：
    /// - Flip 不消除，不产生腾槽收益
    /// - 其他行为按 MatchRequireCount - behaviour.Count 估算本次补用了多少槽内牌
    /// - 小于 0 时归 0
    /// </summary>
    private static int EvaluateFreedSlotCount(LevelCore level, Behaviour behaviour)
    {
        if (behaviour.Kind == BehaviourKind.Flip)
            return 0;

        int freedSlotCount = level.RuleSpec.MatchRequireCount - behaviour.Count;
        return Math.Max(0, freedSlotCount);
    }

    /// <summary>
    /// 是否能清空整个花色。
    ///
    /// old:
    /// 当前口径：
    /// - 槽内该花色数量
    /// - + 场上可见牌中该花色数量
    /// 若总数恰好等于 MatchRequireCount，则本次消除后会清空这一花色。
    ///
    /// new:
    /// 1. 这个行为组是消除行为
    /// 2. 行为组消除后的盘面，没有这个花色可选
    /// </summary>
    private static bool EvaluateCanClearWholeColor(
        LevelCore level,
        Behaviour behaviour,
        ReadOnlySpan<ulong> afterSelectableBits)
    {
        if (!IsClearBehaviour(behaviour.Kind))
            return false;

        return CountTilesBySuit(level, afterSelectableBits, behaviour.Color) == 0;
    }

    private static bool IsClearBehaviour(BehaviourKind kind)
    {
        return kind is BehaviourKind.EasyClear
            or BehaviourKind.HardClear
            or BehaviourKind.GeneralClear;
    }

    /// <summary>
    /// 槽相关花色收益公式：
    /// 只统计“新增可选牌颜色”和“行为后槽内颜色”的交集。
    /// 对每个命中颜色：
    /// - p = 行为后场上该颜色可选牌数
    /// - q = 行为后槽内该颜色数量
    /// - n = MatchRequireCount
    /// 若 p + q >= n：
    ///     score += C(p, n - q) * SlotRelatedColorScoreFactor
    /// 否则：
    ///     score += (SlotRelatedColorScoreFactor / n) * p
    /// </summary>
    private static double EvaluateSlotRelatedColorScore(
        LevelCore level,
        Behaviour behaviour,
        int newlyAddedSelectableCount,
        ulong newlyAddedSelectableSuitBits,
        ReadOnlySpan<ulong> afterSelectableBits,
        BehaviourScoreConfigPiKa config)
    {
        if (newlyAddedSelectableCount == 0)
            return 0d;

        double score = 0d;
        int n = level.RuleSpec.MatchRequireCount;
        var suitBits = newlyAddedSelectableSuitBits;

        while (suitBits != 0)
        {
            int suit = BitOperations.TrailingZeroCount(suitBits);
            int q = GetAfterSlotCount(level, behaviour, suit);

            if (q == 0)
            {
                suitBits &= suitBits - 1;
                continue;
            }

            int p = CountTilesBySuit(level, afterSelectableBits, suit);

            if (p + q >= n)
            {
                int needFromBoard = n - q;
                score += Combination(p, needFromBoard) * config.SlotRelatedColorScoreFactor;
            }
            else
            {
                score += (config.SlotRelatedColorScoreFactor / n) * p;
            }

            suitBits &= suitBits - 1;
        }

        return score;
    }

    /// <summary>
    /// 新增可配机会数公式：
    /// 只对本次新增可选牌涉及的颜色做 before/after 比较。
    /// 对每个颜色：
    ///     delta = afterMatchChanceCount - beforeMatchChanceCount
    ///     total += max(delta, 0)
    /// 最终加分由 ComputeNewMatchChanceScore 乘权重。
    /// </summary>
    private static int EvaluateNewMatchChanceCount(
        LevelCore level,
        Behaviour behaviour,
        int newlyAddedSelectableCount,
        ulong newlyAddedSelectableSuitBits,
        ReadOnlySpan<ulong> originSelectableBits,
        ReadOnlySpan<ulong> afterSelectableBits)
    {
        if (newlyAddedSelectableCount == 0)
            return 0;

        int totalNewMatchChanceCount = 0;
        int n = level.RuleSpec.MatchRequireCount;
        var suitBits = newlyAddedSelectableSuitBits;

        while (suitBits != 0)
        {
            int suit = BitOperations.TrailingZeroCount(suitBits);
            int beforeSelectableCount = CountTilesBySuit(
                level,
                originSelectableBits,
                suit);
            int beforeSlotCount = level.StagingArea.GetSuitCount(suit);

            int afterSelectableCount = CountTilesBySuit(level, afterSelectableBits, suit);
            int afterSlotCount = GetAfterSlotCount(level, behaviour, suit);

            int beforeMatchChanceCount = GetMatchChanceCount(
                beforeSelectableCount,
                beforeSlotCount,
                n);

            int afterMatchChanceCount = GetMatchChanceCount(
                afterSelectableCount,
                afterSlotCount,
                n);

            int delta = afterMatchChanceCount - beforeMatchChanceCount;
            if (delta > 0)
                totalNewMatchChanceCount += delta;

            suitBits &= suitBits - 1;
        }

        return totalNewMatchChanceCount;
    }

    private static void BuildSelectedTileBits(Behaviour behaviour, Span<ulong> tileBits)
    {
        foreach (int tileIndex in behaviour.SelectIds)
            BitSetOperations.Set(tileBits, tileIndex);
    }

    private static void BuildAfterSelectableBits(
        ReadOnlySpan<ulong> originSelectableBits,
        ReadOnlySpan<ulong> selectedTileBits,
        ReadOnlySpan<ulong> newlyAddedSelectableBits,
        Span<ulong> afterSelectableBits)
    {
        originSelectableBits.CopyTo(afterSelectableBits);
        BitSetOperations.AndNotWith(afterSelectableBits, selectedTileBits);
        BitSetOperations.OrWith(afterSelectableBits, newlyAddedSelectableBits);
    }

    private static void CollectNewlyAddedExposureBits(
        LevelCore level,
        Behaviour behaviour,
        ReadOnlySpan<ulong> originSelectableBits,
        Span<ulong> newlyAddedSelectableBits,
        ref ulong newlyAddedSelectableSuitBits,
        Span<ulong> newlyAddedVisibleBits,
        Span<ulong> afterSelectableBits)
    {
        int wordCount = level.Mapping.WordCount;

        Span<ulong> selectedTileBits = stackalloc ulong[wordCount];
        BuildSelectedTileBits(behaviour, selectedTileBits);

        Span<ulong> ignoredTileBits = stackalloc ulong[wordCount];
        selectedTileBits.CopyTo(ignoredTileBits);

        Span<ulong> affectedTileBits = stackalloc ulong[wordCount];
        Span<ulong> singleAffectedTileBits = stackalloc ulong[wordCount];

        foreach (int tileIndex in behaviour.SelectIds)
        {
            level.Pasture.SimulateAffectedTileBits(
                tileIndex,
                ignoredTileBits,
                singleAffectedTileBits);

            BitSetOperations.OrWith(affectedTileBits, singleAffectedTileBits);
        }

        foreach (int tileIndex in TileIndexSet.Wrap(affectedTileBits))
        {
            if (BitSetOperations.Get(selectedTileBits, tileIndex))
                continue;

            level.Pasture.SimulateState(
                tileIndex,
                ignoredTileBits,
                out bool visible,
                out bool selectable);

            if (selectable && !BitSetOperations.Get(originSelectableBits, tileIndex))
            {
                int suit = level.Mapping.GetSuit(tileIndex);
                BitSetOperations.Set(newlyAddedSelectableBits, tileIndex);
                newlyAddedSelectableSuitBits |= 1UL << suit;
                continue;
            }

            if (visible && !level.Pasture.IsVisible(tileIndex))
            {
                BitSetOperations.Set(newlyAddedVisibleBits, tileIndex);
            }
        }

        BuildAfterSelectableBits(
            originSelectableBits,
            selectedTileBits,
            newlyAddedSelectableBits,
            afterSelectableBits);
    }

    private static ulong BuildSuitBits(LevelCore level, ReadOnlySpan<ulong> tileBits)
    {
        ulong suitBits = 0UL;

        foreach (int tileIndex in TileIndexSet.Wrap(tileBits))
        {
            int suit = level.Mapping.GetSuit(tileIndex);
            suitBits |= 1UL << suit;
        }

        return suitBits;
    }

    private static int GetAfterSlotCount(LevelCore level, Behaviour behaviour, int suit)
    {
        int originSlotCount = level.StagingArea.GetSuitCount(suit);

        if (behaviour.Kind == BehaviourKind.Flip)
            return suit == behaviour.Color
                ? originSlotCount + 1
                : originSlotCount;

        return suit == behaviour.Color
            ? 0
            : originSlotCount;
    }

    private static int CountTilesBySuit(
        LevelCore level,
        ReadOnlySpan<ulong> tileBits,
        int suit)
    {
        var suitTileBits = level.Mapping.GetTileBitsBySuit(suit);
        if (suitTileBits.IsEmpty)
            return 0;

        Span<ulong> intersectionBits = stackalloc ulong[level.Mapping.WordCount];
        tileBits.CopyTo(intersectionBits);
        BitSetOperations.AndWith(intersectionBits, suitTileBits);

        return BitSetOperations.PopCount(intersectionBits);
    }

    /// <summary>
    /// 单色可配机会数公式：
    /// needFromBoard = matchRequireCount - slotCount
    /// remainingSelectableCount = selectableCount - needFromBoard
    /// chanceCount = max(remainingSelectableCount, 0) / matchRequireCount
    ///
    /// 这里统计的是补足槽内已有牌之后，场上剩余可选牌还能额外形成几组。
    /// </summary>
    private static int GetMatchChanceCount(
        int selectableCount,
        int slotCount,
        int matchRequireCount)
    {
        int needFromBoard = matchRequireCount - slotCount;
        int remainingSelectableCount = selectableCount - needFromBoard;

        if (remainingSelectableCount < 0)
            return 0;

        return remainingSelectableCount / matchRequireCount;
    }

    /// <summary>
    /// 组合数公式：
    /// C(n, k) = n! / (k! * (n - k)!)。
    /// 实现时使用递推乘除，避免直接计算阶乘。
    /// </summary>
    private static int Combination(int n, int k)
    {
        if (k < 0 || k > n)
            return 0;
        if (k == 0 || k == n)
            return 1;

        k = Math.Min(k, n - k);

        long result = 1;
        for (int i = 1; i <= k; i++)
            result = result * (n - k + i) / i;

        return (int)result;
    }

    private static BehaviourScoreConfigPiKa GetConfig(int matchCount)
    {
        return s_configByMatchCount.TryGetValue(matchCount, out var config)
            ? config
            : s_defaultConfig;
    }

    /// <summary>
    /// 评分配置。
    /// 这里只放“权重”和“基础分参数”，不放盘面逻辑。
    /// </summary>
    public sealed class BehaviourScoreConfigPiKa
    {
        /// <summary>
        /// Easy 完整消除，在暂存槽玩法下的基础分。
        /// </summary>
        public double EasyFullMatchBaseScoreAtStorage { get; init; } = 4d;

        /// <summary>
        /// Easy 完整消除，在普通槽玩法下的基础分。
        /// </summary>
        public double EasyFullMatchBaseScoreAtNormal { get; init; } = 3d;

        /// <summary>
        /// Easy 部分消除 / 部分匹配 的基础分。
        /// </summary>
        public double EasyPartMatchBaseScore { get; init; } = 3d;

        /// <summary>
        /// Hard 行为基础分。
        /// </summary>
        public double HardBaseScore { get; init; } = 2d;

        /// <summary>
        /// Flip 行为基础分。
        /// </summary>
        public double FlipBaseScore { get; init; } = 0d;

        /// <summary>
        /// 每腾出 1 个槽位的分数倍率。
        /// </summary>
        public double FreedSlotScoreFactor { get; init; } = 5d;

        /// <summary>
        /// 清空某一花色时的额外加分。
        /// </summary>
        public double ClearColorBonusScore { get; init; } = 4d;

        /// <summary>
        /// 每新增 1 张可选牌的分数倍率。
        /// </summary>
        public double NewSelectableScoreFactor { get; init; } = 1d;

        /// <summary>
        /// 每新增 1 张可见牌的分数倍率。
        /// </summary>
        public double NewVisibleScoreFactor { get; init; } = 0.5d;

        /// <summary>
        /// 每新增 1 个可配机会的分数倍率。
        /// </summary>
        public double NewMatchChanceScoreFactor { get; init; } = 3d;

        /// <summary>
        /// 槽相关花色收益的分数倍率。
        /// </summary>
        public double SlotRelatedColorScoreFactor { get; init; } = 3d;
    }

    private readonly struct BehaviourScoreContext
    {
        public required BehaviourScoreConfigPiKa Config { get; init; }
        public double BaseScore { get; init; }
        public int FreedSlotCount { get; init; }
        public bool CanClearWholeColor { get; init; }
        public int NewSelectableCount { get; init; }
        public int NewVisibleCount { get; init; }
        public double SlotRelatedColorScore { get; init; }
        public int NewMatchChanceCount { get; init; }
    }

}

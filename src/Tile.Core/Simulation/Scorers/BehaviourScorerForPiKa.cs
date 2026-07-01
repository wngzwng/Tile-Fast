using Tile.Core.Common.BitSet;
using Tile.Core.Common.Math;
using Tile.Core.Core;

namespace Tile.Core.Simulation;

/// <summary>
/// PiKa 规则下的行为评分器。
/// </summary>
public sealed class BehaviourScorerForPiKa : ISimulationCandidateScorer
{
    private double _softMaxTemperature = 0.5d;
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

    public BehaviourScorerForPiKa SetSoftMaxTemperature(double softMaxTemperature)
    {
        _softMaxTemperature = softMaxTemperature;
        return this;
    }

    public static BehaviourScoreConfigPiKa DefaultConfig => s_defaultConfig;

    public static IReadOnlyDictionary<int, BehaviourScoreConfigPiKa> DefaultConfigByMatchCount
        => s_configByMatchCount;

    public static BehaviourScoreConfigPiKa GetDefaultConfig(int matchCount)
        => GetConfig(matchCount);

    public SimulationCandidateMode CandidateMode => SimulationCandidateMode.Behaviour;

    public int SelectCandidateOffset(
        SimulationContext context,
        IReadOnlyList<int> candidates)
    {
        throw new NotSupportedException("This scorer only supports Behaviour candidates.");
    }

    public int SelectBehaviourCandidateOffset(
        SimulationContext context,
        IReadOnlyList<Behaviour> candidates)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (candidates is null)
            throw new ArgumentNullException(nameof(candidates));
        if (candidates.Count <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidates));

        var scores = new double[candidates.Count];
        var weights = new double[candidates.Count];
        var level = context.SourceLevel;

        for (var offset = 0; offset < candidates.Count; offset++)
            scores[offset] = EvaluateScore(level, candidates[offset]);

        MathKit.Softmax(scores, weights, temperature: _softMaxTemperature);

        var selectedOffset = MathKit.WeightedChoice(weights, context.Random);
        return selectedOffset >= 0
            ? selectedOffset
            : context.Random.Next(candidates.Count);
    }

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
        CollectBehaviourScoreData(
            level,
            behaviour,
            out int newSelectableCount,
            out int newVisibleCount,
            out ulong newSelectableColorBits,
            out int[] afterSelectableColorCounts,
            out int[] afterSlotColorCounts);

        return new BehaviourScoreContext
        {
            Config = config,
            BaseScore = EvaluateBaseScore(level, behaviour, config),
            FreedSlotCount = EvaluateFreedSlotCount(level, behaviour),
            CanClearWholeColor = EvaluateCanClearWholeColor(
                behaviour,
                afterSelectableColorCounts),
            NewSelectableCount = newSelectableCount,
            NewVisibleCount = newVisibleCount,
            SlotRelatedColorScore = EvaluateSlotRelatedColorScore(
                level.RuleSpec.MatchRequireCount,
                newSelectableCount,
                newSelectableColorBits,
                afterSelectableColorCounts,
                afterSlotColorCounts,
                config),
            NewMatchChanceCount = EvaluateNewMatchChanceCount(
                level,
                newSelectableCount,
                newSelectableColorBits,
                afterSelectableColorCounts,
                afterSlotColorCounts),
        };
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
        Behaviour behaviour,
        ReadOnlySpan<int> afterSelectableColorCounts)
    {
        if (!IsClearBehaviour(behaviour.Kind))
            return false;

        return GetColorCount(afterSelectableColorCounts, behaviour.Color) == 0;
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
        int matchRequireCount,
        int newSelectableCount,
        ulong newSelectableColorBits,
        ReadOnlySpan<int> afterSelectableColorCounts,
        ReadOnlySpan<int> afterSlotColorCounts,
        BehaviourScoreConfigPiKa config)
    {
        if (newSelectableCount == 0)
            return 0d;

        double score = 0d;
        int n = matchRequireCount;

        foreach (int color in EnumerateColorBits(newSelectableColorBits))
        {
            if (GetColorCount(afterSlotColorCounts, color) == 0)
                continue;

            int p = GetColorCount(afterSelectableColorCounts, color);
            int q = GetColorCount(afterSlotColorCounts, color);

            if (p + q >= n)
            {
                int needFromBoard = n - q;
                score += Combination(p, needFromBoard) * config.SlotRelatedColorScoreFactor;
            }
            else
            {
                score += (config.SlotRelatedColorScoreFactor / n) * p;
            }
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
        int newSelectableCount,
        ulong newSelectableColorBits,
        ReadOnlySpan<int> afterSelectableColorCounts,
        ReadOnlySpan<int> afterSlotColorCounts)
    {
        if (newSelectableCount == 0)
            return 0;

        var beforeSelectableColorCounts = BuildBeforeSelectableColorCounts(
            level,
            newSelectableColorBits);
        var beforeSlotColorCounts = BuildBeforeSlotColorCounts(level);

        int totalNewMatchChanceCount = 0;
        int n = level.RuleSpec.MatchRequireCount;

        foreach (int color in EnumerateColorBits(newSelectableColorBits))
        {
            int beforeSelectableCount = GetColorCount(beforeSelectableColorCounts, color);
            int beforeSlotCount = GetColorCount(beforeSlotColorCounts, color);

            int afterSelectableCount = GetColorCount(afterSelectableColorCounts, color);
            int afterSlotCount = GetColorCount(afterSlotColorCounts, color);

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
        }

        return totalNewMatchChanceCount;
    }

    /// <summary>
    /// 收集公式所需事实：
    /// - 行为后新增 selectable / visible
    /// - 新增 selectable 涉及的颜色集合
    /// - 行为后 selectable 颜色分布
    /// - 行为后 slot 颜色分布
    /// </summary>
    private static void CollectBehaviourScoreData(
        LevelCore level,
        Behaviour behaviour,
        out int newSelectableCount,
        out int newVisibleCount,
        out ulong newSelectableColorBits,
        out int[] afterSelectableColorCounts,
        out int[] afterSlotColorCounts)
    {
        int wordCount = level.Mapping.WordCount;
        var behaviourTileBits = BuildBehaviourTileBits(level, behaviour);
        var newSelectableTileBits = new ulong[wordCount];
        var newVisibleTileBits = new ulong[wordCount];
        newSelectableCount = 0;
        newVisibleCount = 0;

        Span<ulong> ignoredTileBits = stackalloc ulong[wordCount];
        foreach (int tileIndex in behaviour.SelectIds)
            BitSetOperations.Set(ignoredTileBits, tileIndex);

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
            if (BitSetOperations.Get(behaviourTileBits, tileIndex))
                continue;

            level.Pasture.SimulateState(
                tileIndex,
                ignoredTileBits,
                out bool visible,
                out bool selectable);

            if (selectable && !level.Pasture.IsSelectable(tileIndex))
            {
                BitSetOperations.Set(newSelectableTileBits, tileIndex);
                newSelectableCount++;
                continue;
            }

            if (visible && !level.Pasture.IsVisible(tileIndex))
            {
                BitSetOperations.Set(newVisibleTileBits, tileIndex);
                newVisibleCount++;
            }
        }

        var newSelectableColorCounts = BuildColorCounts(
            level,
            newSelectableTileBits,
            out newSelectableColorBits);
        afterSelectableColorCounts = BuildAfterSelectableColorCounts(
            level,
            behaviourTileBits,
            newSelectableColorCounts);
        afterSlotColorCounts = BuildAfterSlotColorCounts(level, behaviour);
    }

    private static ulong[] BuildBehaviourTileBits(LevelCore level, Behaviour behaviour)
    {
        var tileBits = new ulong[level.Mapping.WordCount];

        foreach (int tileIndex in behaviour.SelectIds)
            BitSetOperations.Set(tileBits, tileIndex);

        return tileBits;
    }

    private static int[] BuildColorCounts(
        LevelCore level,
        ReadOnlySpan<ulong> tileBits,
        out ulong colorBits)
    {
        var counts = new int[Tile.MaxSuitCount];
        colorBits = 0UL;

        foreach (int tileIndex in TileIndexSet.Wrap(tileBits))
        {
            int color = level.Mapping.GetSuit(tileIndex);
            AddToColorCounts(counts, ref colorBits, color, 1);
        }

        return counts;
    }

    private static int[] BuildBeforeSelectableColorCounts(
        LevelCore level,
        ulong targetColorBits)
    {
        var counts = new int[Tile.MaxSuitCount];

        foreach (int tileIndex in level.Pasture.SelectableTiles)
        {
            int color = level.Mapping.GetSuit(tileIndex);
            if (!ContainsColor(targetColorBits, color))
                continue;

            counts[color]++;
        }

        return counts;
    }

    private static int[] BuildAfterSelectableColorCounts(
        LevelCore level,
        ReadOnlySpan<ulong> behaviourTileBits,
        ReadOnlySpan<int> newSelectableColorCounts)
    {
        var counts = new int[Tile.MaxSuitCount];

        foreach (int tileIndex in level.Pasture.SelectableTiles)
        {
            if (BitSetOperations.Get(behaviourTileBits, tileIndex))
                continue;

            int color = level.Mapping.GetSuit(tileIndex);
            counts[color]++;
        }

        AddColorCounts(counts, newSelectableColorCounts);

        return counts;
    }

    private static int[] BuildBeforeSlotColorCounts(LevelCore level)
    {
        var counts = new int[Tile.MaxSuitCount];
        var counters = level.StagingArea.SuitCounter;

        for (var color = 0; color < counters.Length; color++)
            counts[color] = counters[color];

        return counts;
    }

    private static int[] BuildAfterSlotColorCounts(LevelCore level, Behaviour behaviour)
    {
        var afterSlotColorCounts = BuildBeforeSlotColorCounts(level);

        if (behaviour.Kind == BehaviourKind.Flip)
            afterSlotColorCounts[behaviour.Color]++;
        else
            afterSlotColorCounts[behaviour.Color] = 0;

        return afterSlotColorCounts;
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

    private static void AddColorCounts(Span<int> target, ReadOnlySpan<int> source)
    {
        for (var color = 0; color < source.Length; color++)
            target[color] += source[color];
    }

    private static void AddToColorCounts(
        Span<int> counts,
        ref ulong colorBits,
        int color,
        int delta)
    {
        counts[color] += delta;
        colorBits |= 1UL << color;
    }

    private static int GetColorCount(ReadOnlySpan<int> counts, int color)
    {
        return counts[color];
    }

    private static bool ContainsColor(ulong colorBits, int color)
    {
        return (colorBits & (1UL << color)) != 0;
    }

    private static IEnumerable<int> EnumerateColorBits(ulong colorBits)
    {
        for (var color = 0; color < Tile.MaxSuitCount; color++)
        {
            if (ContainsColor(colorBits, color))
                yield return color;
        }
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

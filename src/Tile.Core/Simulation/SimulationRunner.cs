using Tile.Core.Core;
using Tile.Core.Core.Moves;
using Tile.Core.Metrices;

namespace Tile.Core.Simulation;

public sealed class SimulationRunner
{
    private readonly ISimulationCandidateFinder _candidateFinder;
    private readonly ISimulationCandidateScorer _candidateScorer;

    public SimulationRunner(
        ISimulationCandidateFinder? candidateFinder = null,
        ISimulationCandidateScorer? candidateScorer = null)
    {
        _candidateFinder = candidateFinder ?? SelectableTileCandidateFinder.Instance;
        _candidateScorer = candidateScorer ?? CreateDefaultScorer(_candidateFinder.CandidateMode);

        if (_candidateFinder.CandidateMode != _candidateScorer.CandidateMode)
            throw new ArgumentException("Simulation candidate finder and scorer must use the same candidate mode.");
    }

    private SimulationRunMetrics SimulateOne(
        SimulationContext context,
        IReadOnlyList<ISimulationHook> hooks,
        ref MetricBag runBag)
    {
        var level = context.SourceLevel;
        while (true)
        {
            if (level.IsFinish())
            {
                context.MarkSuccess();

                return new SimulationRunMetrics(
                    IsFailed: false,
                    FailPosition: -1);
            }

            if (level.StagingArea.IsFull)
            {
                context.MarkFailure();

                return new SimulationRunMetrics(
                    IsFailed: true,
                    FailPosition: context.MoveCount);
            }

            if (!TryExecuteStep(context, hooks, ref runBag))
            {
                context.MarkFailure();

                return new SimulationRunMetrics(
                    IsFailed: true,
                    FailPosition: context.MoveCount);
            }
        }
    }

    private bool TryExecuteStep(
        SimulationContext context,
        IReadOnlyList<ISimulationHook> hooks,
        ref MetricBag runBag)
    {
        switch (context.CandidateMode)
        {
            case SimulationCandidateMode.Tile:
                return TryExecuteTileStep(
                    context,
                    hooks,
                    ref runBag);
            case SimulationCandidateMode.Behaviour:
                return TryExecuteBehaviourStep(
                    context,
                    hooks,
                    ref runBag);
            default:
                throw new InvalidOperationException("Unsupported simulation candidate mode.");
        }
    }

    private bool TryExecuteTileStep(
        SimulationContext context,
        IReadOnlyList<ISimulationHook> hooks,
        ref MetricBag runBag)
    {
        var level = context.SourceLevel;
        var candidates = context.TileCandidates;
        candidates.Clear();

        var candidateCount = _candidateFinder.FindCandidates(context);

        if (candidateCount == 0)
            return false;

        InvokeStepBeforeHooks(hooks, context, ref runBag);

        var selectedCandidateOffset = _candidateScorer.SelectCandidateOffset(
            context,
            candidates.Items);
        if ((uint)selectedCandidateOffset >= (uint)candidateCount)
            throw new InvalidOperationException("Simulation candidate scorer returned an invalid candidate offset.");

        context.SetSelectedCandidateOffset(selectedCandidateOffset);
        var selectedTileIndex = candidates.SelectedItem;

        InvokeBehaviourBeforeHooks(hooks, context, ref runBag);
        level.DoMove(new SelectMove(selectedTileIndex));
        context.IncreaseMoveCount();
        InvokeBehaviourAfterHooks(hooks, context, ref runBag);

        InvokeStepAfterHooks(hooks, context, ref runBag);

        return true;
    }

    private bool TryExecuteBehaviourStep(
        SimulationContext context,
        IReadOnlyList<ISimulationHook> hooks,
        ref MetricBag runBag)
    {
        var level = context.SourceLevel;
        var candidates = context.BehaviourCandidates;
        candidates.Clear();

        var candidateCount = _candidateFinder.FindCandidates(context);

        if (candidateCount == 0)
            return false;

        InvokeStepBeforeHooks(hooks, context, ref runBag);

        var selectedCandidateOffset = _candidateScorer.SelectBehaviourCandidateOffset(
            context,
            candidates.Items);
        if ((uint)selectedCandidateOffset >= (uint)candidateCount)
            throw new InvalidOperationException("Simulation candidate scorer returned an invalid candidate offset.");

        context.SetSelectedCandidateOffset(selectedCandidateOffset);
        var selectedBehaviour = candidates.SelectedItem;

        InvokeBehaviourBeforeHooks(hooks, context, ref runBag);
        foreach (var move in selectedBehaviour.ToMoves())
        {
            level.DoMove(move);
            context.IncreaseMoveCount();
        }
        InvokeBehaviourAfterHooks(hooks, context, ref runBag);

        InvokeStepAfterHooks(hooks, context, ref runBag);

        return true;
    }

    /// <summary>
    /// 对指定关卡执行多次随机模拟，并在生命周期节点调用可选 hooks。
    /// </summary>
    public SimulationBatchMetrics SimulateMany(
        LevelCore level,
        int simulationCount,
        Random? random = null,
        IReadOnlyList<ISimulationHook>? hooks = null)
    {
        if (level is null)
            throw new ArgumentNullException(nameof(level));

        if (simulationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(simulationCount), "Simulation count must be greater than 0.");

        var context = new SimulationContext(
            level,
            simulationCount,
            random ?? Random.Shared,
            _candidateFinder.CandidateMode);
        var runBag = new MetricBag();
        var aggBag = new MetricBag();

        return SimulateMany(
            context,
            runBag,
            aggBag,
            hooks);
    }

    /// <summary>
    /// 使用外部提供的可复用容器执行当前 batch。调用方必须先调用
    /// <see cref="SimulationContext.ResetBatch(LevelCore,int,Random,SimulationCandidateMode)"/>
    /// 完成 context 初始化。
    /// </summary>
    public SimulationBatchMetrics SimulateMany(
        SimulationContext context,
        MetricBag runBag,
        MetricBag aggBag,
        IReadOnlyList<ISimulationHook>? hooks = null)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (runBag is null)
            throw new ArgumentNullException(nameof(runBag));

        if (aggBag is null)
            throw new ArgumentNullException(nameof(aggBag));

        hooks ??= Array.Empty<ISimulationHook>();

        context.EnsureInitialized();
        aggBag.ResetValues();

        var failPositionSum = 0;

        InvokeBatchStartHooks(hooks, context);

        for (var i = 0; i < context.SimulationCount; i++)
        {
            runBag.ResetValues();
            context.StartRun(i);

            InvokeRunStartHooks(hooks, context, ref runBag);

            var runMetrics = SimulateOne(context, hooks, ref runBag);
            runMetrics.WriteTo(runBag);

            context.CompleteRun(runMetrics);
            InvokeRunEndHooks(hooks, context, ref runBag);
            InvokeRunAggregateHooks(hooks, context, ref runBag, ref aggBag);

            if (runMetrics.IsFailed)
            {
                failPositionSum += runMetrics.FailPosition;
            }
        }

        var failureRate = (double)context.FailureCount / context.SimulationCount;
        var averageFailPosition = context.FailureCount == 0
            ? -1.0
            : (double)failPositionSum / context.FailureCount;

        var batchMetrics = new SimulationBatchMetrics(
            TotalCount: context.SimulationCount,
            SuccessCount: context.SuccessCount,
            FailureCount: context.FailureCount,
            FailureRate: failureRate,
            AverageFailPosition: averageFailPosition);
        batchMetrics.WriteTo(aggBag);

        InvokeBatchEndHooks(hooks, context, ref aggBag);

        return batchMetrics;
    }

    private static ISimulationCandidateScorer CreateDefaultScorer(SimulationCandidateMode candidateMode)
    {
        return candidateMode switch
        {
            SimulationCandidateMode.Tile => RandomCandidateScorer.Instance,
            SimulationCandidateMode.Behaviour => PiKaBehaviourCandidateScorer.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(candidateMode)),
        };
    }

    private static void InvokeBatchStartHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context)
    {
        foreach (var hook in hooks)
            hook.OnBatchStart(context);
    }

    private static void InvokeBatchEndHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag aggBag)
    {
        foreach (var hook in hooks)
            hook.OnBatchEnd(context, ref aggBag);
    }

    private static void InvokeRunStartHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag)
    {
        foreach (var hook in hooks)
            hook.OnRunStart(context, ref runBag);
    }

    private static void InvokeStepBeforeHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag)
    {
        foreach (var hook in hooks)
            hook.OnStepBefore(context, ref runBag);
    }

    private static void InvokeBehaviourBeforeHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag)
    {
        foreach (var hook in hooks)
            hook.OnBehaviourBefore(context, ref runBag);
    }

    private static void InvokeBehaviourAfterHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag)
    {
        foreach (var hook in hooks)
            hook.OnBehaviourAfter(context, ref runBag);
    }

    private static void InvokeStepAfterHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag)
    {
        foreach (var hook in hooks)
            hook.OnStepAfter(context, ref runBag);
    }

    private static void InvokeRunEndHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag)
    {
        foreach (var hook in hooks)
            hook.OnRunEnd(context, ref runBag);
    }

    private static void InvokeRunAggregateHooks(
        IReadOnlyList<ISimulationHook> hooks,
        SimulationContext context,
        ref MetricBag runBag,
        ref MetricBag aggBag)
    {
        foreach (var hook in hooks)
            hook.OnRunAggregate(context, ref runBag, ref aggBag);
    }
}

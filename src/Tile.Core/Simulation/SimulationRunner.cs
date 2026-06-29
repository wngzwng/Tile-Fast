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
        _candidateScorer = candidateScorer ?? RandomCandidateScorer.Instance;

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
            if (level.Pasture.IsEmpty)
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

            var candidates = context.TileCandidates;
            var candidateCount = _candidateFinder.FindCandidates(
                context,
                candidates.MutableItems);

            if (candidateCount == 0)
            {
                context.MarkFailure();

                return new SimulationRunMetrics(
                    IsFailed: true,
                    FailPosition: context.MoveCount);
            }

            InvokeStepBeforeHooks(hooks, context, ref runBag);

            var selectedCandidateOffset = _candidateScorer.SelectCandidateOffset(
                context,
                candidates.Items);
            if ((uint)selectedCandidateOffset >= (uint)candidateCount)
                throw new InvalidOperationException("Simulation candidate scorer returned an invalid candidate offset.");

            context.SetSelectedCandidateOffset(selectedCandidateOffset);
            var selectedCandidateValue = candidates.SelectedItem;

            InvokeBehaviourBeforeHooks(hooks, context, ref runBag);
            level.DoMove(new SelectMove(selectedCandidateValue));
            context.IncreaseMoveCount();
            InvokeBehaviourAfterHooks(hooks, context, ref runBag);

            InvokeStepAfterHooks(hooks, context, ref runBag);
        }
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

        random ??= Random.Shared;
        hooks ??= Array.Empty<ISimulationHook>();

        var context = new SimulationContext(
            level.Clone(),
            simulationCount,
            random,
            _candidateFinder.CandidateMode);

        var failPositionSum = 0;
        var aggBag = new MetricBag();

        InvokeBatchStartHooks(hooks, context);

        for (var i = 0; i < simulationCount; i++)
        {
            context.StartRun(i);

            var runBag = new MetricBag();
            InvokeRunStartHooks(hooks, context, ref runBag);

            var runMetrics = SimulateOne(context, hooks, ref runBag);
            runMetrics.WriteTo(runBag);

            context.CompleteRun(runMetrics);
            InvokeRunEndHooks(hooks, context, ref runBag);

            if (runMetrics.IsFailed)
            {
                failPositionSum += runMetrics.FailPosition;
            }
        }

        var failureRate = (double)context.FailureCount / simulationCount;
        var averageFailPosition = context.FailureCount == 0
            ? -1.0
            : (double)failPositionSum / context.FailureCount;

        var batchMetrics = new SimulationBatchMetrics(
            TotalCount: simulationCount,
            SuccessCount: context.SuccessCount,
            FailureCount: context.FailureCount,
            FailureRate: failureRate,
            AverageFailPosition: averageFailPosition);
        batchMetrics.WriteTo(aggBag);

        InvokeBatchEndHooks(hooks, context, ref aggBag);

        return batchMetrics;
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
}

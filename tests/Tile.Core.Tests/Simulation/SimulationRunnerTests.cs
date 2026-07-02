using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Metrices;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class SimulationRunnerTests
{
    [Test]
    public void SimulateMany_WithSingleRun_WhenLevelCanBeCleared_ReturnsSuccess()
    {
        var level = CreateSingleMatchLevel();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateMany(level, simulationCount: 1, new Random(123));

        Assert.That(metrics.TotalCount, Is.EqualTo(1));
        Assert.That(metrics.SuccessCount, Is.EqualTo(1));
        Assert.That(metrics.FailureCount, Is.Zero);
        Assert.That(metrics.FailureRate, Is.EqualTo(0.0));
        Assert.That(metrics.AverageFailPosition, Is.EqualTo(-1.0));
    }

    [Test]
    public void SimulateMany_DoesNotMutateOriginalLevel()
    {
        var level = CreateSingleMatchLevel();
        var before = level.Serialize();
        var runner = new SimulationRunner();

        _ = runner.SimulateMany(level, simulationCount: 1, new Random(123));

        Assert.That(level.Serialize(), Is.EqualTo(before));
        Assert.That(level.Pasture.PresentTiles.Count(), Is.EqualTo(3));
        Assert.That(level.StagingArea.IsEmpty, Is.True);
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void SimulateMany_WithSingleRun_WhenStagingAreaIsFull_ReturnsFailureAtCurrentMoveCount()
    {
        var level = CreateFullStagingFailureLevel();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateMany(level, simulationCount: 1, new Random(123));

        Assert.That(metrics.TotalCount, Is.EqualTo(1));
        Assert.That(metrics.SuccessCount, Is.Zero);
        Assert.That(metrics.FailureCount, Is.EqualTo(1));
        Assert.That(metrics.FailureRate, Is.EqualTo(1.0));
        Assert.That(metrics.AverageFailPosition, Is.EqualTo(0.0));
    }

    [Test]
    public void SimulateMany_AggregatesSuccessMetrics()
    {
        var level = CreateSingleMatchLevel();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateMany(level, simulationCount: 5, new Random(123));

        Assert.That(metrics.TotalCount, Is.EqualTo(5));
        Assert.That(metrics.SuccessCount, Is.EqualTo(5));
        Assert.That(metrics.FailureCount, Is.Zero);
        Assert.That(metrics.FailureRate, Is.EqualTo(0.0));
        Assert.That(metrics.AverageFailPosition, Is.EqualTo(-1.0));
    }

    [Test]
    public void SimulateMany_AggregatesFailureMetrics()
    {
        var level = CreateFullStagingFailureLevel();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateMany(level, simulationCount: 4, new Random(123));

        Assert.That(metrics.TotalCount, Is.EqualTo(4));
        Assert.That(metrics.SuccessCount, Is.Zero);
        Assert.That(metrics.FailureCount, Is.EqualTo(4));
        Assert.That(metrics.FailureRate, Is.EqualTo(1.0));
        Assert.That(metrics.AverageFailPosition, Is.EqualTo(0.0));
    }

    [Test]
    public void SimulateMany_WhenSimulationCountIsInvalid_Throws()
    {
        var level = CreateSingleMatchLevel();
        var runner = new SimulationRunner();

        Assert.Throws<ArgumentOutOfRangeException>(() => runner.SimulateMany(level, simulationCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.SimulateMany(level, simulationCount: -1));
    }

    [Test]
    public void SimulateMany_WithHooks_InvokesLifecycleAndExposesMetricBags()
    {
        var level = CreateSingleMatchLevel();
        var runner = new SimulationRunner();
        var hook = new RecordingSimulationHook();

        var metrics = runner.SimulateMany(
            level,
            simulationCount: 1,
            new Random(123),
            [hook]);

        Assert.That(metrics.SuccessCount, Is.EqualTo(1));
        Assert.That(
            hook.Events,
            Is.EqualTo(new[]
            {
                "batch-start:-1",
                "run-start:0",
                "step-before:0:0:3",
                "behaviour-before:0:0:3:2",
                "behaviour-after:0:1:3:2",
                "step-after:0:1:3",
                "step-before:0:1:2",
                "behaviour-before:0:1:2:1",
                "behaviour-after:0:2:2:1",
                "step-after:0:2:2",
                "step-before:0:2:1",
                "behaviour-before:0:2:1:0",
                "behaviour-after:0:3:1:0",
                "step-after:0:3:1",
                "run-end:0:False",
                "batch-end:1:1"
            }));
    }

    [Test]
    public void SimulateMany_UsesInjectedFinderAndScorer()
    {
        var level = CreateSingleMatchLevel();
        var finder = new RecordingCandidateFinder();
        var scorer = new FirstCandidateScorer();
        var hook = new RecordingSimulationHook();
        var runner = new SimulationRunner(finder, scorer);

        var metrics = runner.SimulateMany(
            level,
            simulationCount: 1,
            new Random(123),
            [hook]);

        Assert.That(metrics.SuccessCount, Is.EqualTo(1));
        Assert.That(finder.CallCount, Is.EqualTo(3));
        Assert.That(scorer.CallCount, Is.EqualTo(3));
        Assert.That(scorer.CandidateCounts, Is.EqualTo(new[] { 3, 2, 1 }));
        Assert.That(hook.CandidateModes, Is.EqualTo(new[] { SimulationCandidateMode.Tile }));
    }

    [Test]
    public void SimulateMany_WithRunAggregateHook_CanReadRunBagAndWriteAggBag()
    {
        var level = CreateSingleMatchLevel();
        var hook = new RecordingAggregateHook();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateMany(
            level,
            simulationCount: 1,
            new Random(123),
            [hook]);

        Assert.That(metrics.SuccessCount, Is.EqualTo(1));
        Assert.That(
            hook.Events,
            Is.EqualTo(new[]
            {
                "run-end:False",
                "run-aggregate:False",
                "batch-end:1",
            }));
    }

    [Test]
    public void SimulateMany_WithMetricAdapter_ComputesRunMetricsAndAggregatesIntoAggBag()
    {
        var level = CreateSingleMatchLevel();
        var context = new SimulationContext();
        context.ResetBatch(
            level,
            simulationCount: 2,
            new Random(123));
        var runBag = new MetricBag();
        var aggBag = new MetricBag();
        var adapter = new MoveCountMetricAdapter();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateMany(
            context,
            runBag,
            aggBag,
            [adapter]);

        Assert.That(metrics.SuccessCount, Is.EqualTo(2));
        Assert.That(aggBag.GetOrDefault(MoveCountMetricAdapter.TotalMoveCount), Is.EqualTo(6));
        Assert.That(adapter.ComputeCount, Is.EqualTo(2));
        Assert.That(adapter.AggregateCount, Is.EqualTo(2));
    }

    [Test]
    public void SimulateMany_WithReusableContainers_ResetsContextAndMetricBagsPerBatch()
    {
        var level = CreateSingleMatchLevel();
        var context = new SimulationContext();
        context.ResetBatch(
            level,
            simulationCount: 2,
            new Random(123));
        var runBag = new MetricBag();
        var aggBag = new MetricBag();
        var adapter = new MoveCountMetricAdapter();
        var runner = new SimulationRunner();

        _ = runner.SimulateMany(
            context,
            runBag,
            aggBag,
            [adapter]);
        Assert.That(aggBag.GetOrDefault(MoveCountMetricAdapter.TotalMoveCount), Is.EqualTo(6));

        context.ResetBatch(
            level,
            simulationCount: 1,
            new Random(123));

        var metrics = runner.SimulateMany(
            context,
            runBag,
            aggBag,
            [adapter]);

        Assert.That(metrics.SuccessCount, Is.EqualTo(1));
        Assert.That(context.SimulationCount, Is.EqualTo(1));
        Assert.That(context.SuccessCount, Is.EqualTo(1));
        Assert.That(context.FailureCount, Is.Zero);
        Assert.That(aggBag.GetOrDefault(MoveCountMetricAdapter.TotalMoveCount), Is.EqualTo(3));
        Assert.That(runBag.GetOrDefault(MoveCountMetricAdapter.RunMoveCount), Is.EqualTo(3));
    }

    [Test]
    public void SimulateMany_WithReusableContainers_WhenContextIsNotInitialized_Throws()
    {
        var runner = new SimulationRunner();
        var context = new SimulationContext();
        var runBag = new MetricBag();
        var aggBag = new MetricBag();

        Assert.Throws<InvalidOperationException>(() =>
            runner.SimulateMany(
                context,
                runBag,
                aggBag));
    }

    [Test]
    public void Constructor_WhenFinderAndScorerCandidateModesDiffer_Throws()
    {
        var finder = new RecordingCandidateFinder(SimulationCandidateMode.Tile);
        var scorer = new FirstCandidateScorer(SimulationCandidateMode.Behaviour);

        Assert.Throws<ArgumentException>(() => new SimulationRunner(finder, scorer));
    }

    [Test]
    public void SimulateMany_WhenCandidateModeIsBehaviour_ExecutesBehaviourCandidates()
    {
        var level = CreateSingleMatchLevel();
        var finder = BehaviourCandidateFinder.Instance;
        var scorer = FirstCandidateScorer.Behaviour();
        var runner = new SimulationRunner(finder, scorer);

        var metrics = runner.SimulateMany(
            level,
            simulationCount: 1,
            new Random(123));

        Assert.That(metrics.SuccessCount, Is.EqualTo(1));
        Assert.That(scorer.CallCount, Is.GreaterThan(0));
    }

    [Test]
    public void SimulationMetrics_WriteToMetricBag()
    {
        var runMetrics = new SimulationRunMetrics(
            IsFailed: true,
            FailPosition: 7);

        var batchMetrics = new SimulationBatchMetrics(
            TotalCount: 10,
            SuccessCount: 6,
            FailureCount: 4,
            FailureRate: 0.4,
            AverageFailPosition: 3.5);

        var bag = new MetricBag();

        runMetrics.WriteTo(bag);
        batchMetrics.WriteTo(bag);

        Assert.That(bag.GetOrDefault(SimulationRunMetricKeys.IsFailed), Is.True);
        Assert.That(bag.GetOrDefault(SimulationRunMetricKeys.FailPosition), Is.EqualTo(7));
        Assert.That(bag.GetOrDefault(SimulationBatchMetricKeys.TotalCount), Is.EqualTo(10));
        Assert.That(bag.GetOrDefault(SimulationBatchMetricKeys.SuccessCount), Is.EqualTo(6));
        Assert.That(bag.GetOrDefault(SimulationBatchMetricKeys.FailureCount), Is.EqualTo(4));
        Assert.That(bag.GetOrDefault(SimulationBatchMetricKeys.FailureRate), Is.EqualTo(0.4));
        Assert.That(bag.GetOrDefault(SimulationBatchMetricKeys.AverageFailPosition), Is.EqualTo(3.5));
    }

    [Test]
    public void SimulationMetrics_ToString_ReturnsStableSummary()
    {
        var runMetrics = new SimulationRunMetrics(
            IsFailed: true,
            FailPosition: 7);

        var batchMetrics = new SimulationBatchMetrics(
            TotalCount: 10,
            SuccessCount: 6,
            FailureCount: 4,
            FailureRate: 0.4,
            AverageFailPosition: 3.5);

        Assert.That(
            runMetrics.ToString(),
            Is.EqualTo("SimulationRunMetrics(IsFailed=True, FailPosition=7)"));

        Assert.That(
            batchMetrics.ToString(),
            Is.EqualTo("SimulationBatchMetrics(Total=10, Success=6, Failure=4, FailureRate=0.4, AverageFailPosition=3.5)"));
    }

    [Test]
    public void SimulationMetricKeys_AllContainsDeclaredKeys()
    {
        Assert.That(
            SimulationRunMetricKeys.All.Select(key => key.Name),
            Is.EqualTo(new[]
            {
                "run.simulation.is_failed",
                "run.simulation.fail_position",
            }));

        Assert.That(
            SimulationBatchMetricKeys.All.Select(key => key.Name),
            Is.EqualTo(new[]
            {
                "batch.simulation.total_count",
                "batch.simulation.success_count",
                "batch.simulation.failure_count",
                "batch.simulation.failure_rate",
                "batch.simulation.avg_fail_position",
            }));
    }

    private static LevelCore CreateSingleMatchLevel()
    {
        int[] positions =
        [
            (0, 0, 0).PackXyz(),
            (2, 0, 0).PackXyz(),
            (4, 0, 0).PackXyz()
        ];

        int[] suits = [1, 1, 1];

        return new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.TripleTile,
            suits.AsSpan());
    }

    private static LevelCore CreateFullStagingFailureLevel()
    {
        int[] positions =
        [
            (0, 0, 0).PackXyz(),
            (2, 0, 0).PackXyz(),
            (4, 0, 0).PackXyz(),
            (6, 0, 0).PackXyz(),
            (8, 0, 0).PackXyz()
        ];

        int[] suits = [1, 2, 3, 4, 5];

        var level = new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.PairClassic,
            suits.AsSpan());

        FillStagingWithoutMatching(level, tileIndex: 0);
        FillStagingWithoutMatching(level, tileIndex: 1);
        FillStagingWithoutMatching(level, tileIndex: 2);
        FillStagingWithoutMatching(level, tileIndex: 3);

        return level;
    }

    private static void FillStagingWithoutMatching(
        LevelCore level,
        int tileIndex)
    {
        level.Pasture.Lift(tileIndex);
        level.StagingArea.Enter(tileIndex);
    }

    private sealed class RecordingSimulationHook : ISimulationHook
    {
        public List<string> Events { get; } = [];

        public List<SimulationCandidateMode> CandidateModes { get; } = [];

        public void OnBatchStart(SimulationContext context)
        {
            Events.Add($"batch-start:{context.SimulationIndex}");
        }

        public void OnBatchEnd(SimulationContext context, ref MetricBag aggBag)
        {
            Events.Add(
                $"batch-end:{context.SimulationNumber}:{aggBag.GetOrDefault(SimulationBatchMetricKeys.SuccessCount)}");
        }

        public void OnRunStart(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add($"run-start:{context.SimulationIndex}");
            CandidateModes.Add(context.CandidateMode);
        }

        public void OnStepBefore(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add(
                $"step-before:{context.SimulationIndex}:{context.MoveCount}:{context.CandidateCount}");
        }

        public void OnBehaviourBefore(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add(
                $"behaviour-before:{context.SimulationIndex}:{context.MoveCount}:{context.CandidateCount}:{context.SelectedTileIndex}");
        }

        public void OnBehaviourAfter(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add(
                $"behaviour-after:{context.SimulationIndex}:{context.MoveCount}:{context.CandidateCount}:{context.SelectedTileIndex}");
        }

        public void OnStepAfter(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add(
                $"step-after:{context.SimulationIndex}:{context.MoveCount}:{context.CandidateCount}");
        }

        public void OnRunEnd(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add(
                $"run-end:{context.SimulationIndex}:{runBag.GetOrDefault(SimulationRunMetricKeys.IsFailed)}");
        }
    }

    private sealed class RecordingAggregateHook : ISimulationHook
    {
        private static readonly MetricKey<int> AggregateSeenCount =
            new("test.aggregate_seen_count");

        public List<string> Events { get; } = [];

        public void OnRunEnd(SimulationContext context, ref MetricBag runBag)
        {
            Events.Add(
                $"run-end:{runBag.GetOrDefault(SimulationRunMetricKeys.IsFailed)}");
        }

        public void OnRunAggregate(
            SimulationContext context,
            ref MetricBag runBag,
            ref MetricBag aggBag)
        {
            Events.Add(
                $"run-aggregate:{runBag.GetOrDefault(SimulationRunMetricKeys.IsFailed)}");
            aggBag.Add(AggregateSeenCount, 1);
        }

        public void OnBatchEnd(SimulationContext context, ref MetricBag aggBag)
        {
            Events.Add($"batch-end:{aggBag.GetOrDefault(AggregateSeenCount)}");
        }
    }

    private sealed class MoveCountMetricAdapter : MetricAdapterBase
    {
        public static readonly MetricKey<int> RunMoveCount =
            new("test.run_move_count");

        public static readonly MetricKey<int> TotalMoveCount =
            new("test.total_move_count");

        public int ComputeCount { get; private set; }

        public int AggregateCount { get; private set; }

        public override void Compute(
            SimulationContext context,
            MetricBag runBag)
        {
            ComputeCount++;
            runBag.Set(RunMoveCount, context.MoveCount);
        }

        public override void Aggregate(
            SimulationContext context,
            MetricBag runBag,
            MetricBag aggBag)
        {
            AggregateCount++;
            aggBag.Add(TotalMoveCount, runBag.GetOrDefault(RunMoveCount));
        }
    }

    private sealed class RecordingCandidateFinder : ISimulationCandidateFinder
    {
        private readonly SimulationCandidateMode _candidateMode;

        public RecordingCandidateFinder(
            SimulationCandidateMode candidateMode = SimulationCandidateMode.Tile)
        {
            _candidateMode = candidateMode;
        }

        public int CallCount { get; private set; }

        public SimulationCandidateMode CandidateMode => _candidateMode;

        public int FindCandidates(SimulationContext context)
        {
            CallCount++;

            var candidates = context.TileCandidates;

            foreach (var tileIndex in context.SourceLevel.Pasture.SelectableTiles)
                candidates.MutableItems.Add(tileIndex);

            return candidates.Count;
        }
    }

    private sealed class FirstCandidateScorer : ISimulationCandidateScorer
    {
        private readonly SimulationCandidateMode _candidateMode;

        public FirstCandidateScorer(
            SimulationCandidateMode candidateMode = SimulationCandidateMode.Tile)
        {
            _candidateMode = candidateMode;
        }

        public static FirstCandidateScorer Behaviour()
            => new(SimulationCandidateMode.Behaviour);

        public int CallCount { get; private set; }

        public SimulationCandidateMode CandidateMode => _candidateMode;

        public List<int> CandidateCounts { get; } = [];

        public int SelectCandidateOffset(
            SimulationContext context,
            IReadOnlyList<int> candidates)
        {
            CallCount++;
            CandidateCounts.Add(candidates.Count);

            return 0;
        }

        public int SelectBehaviourCandidateOffset(
            SimulationContext context,
            IReadOnlyList<Behaviour> candidates)
        {
            CallCount++;
            CandidateCounts.Add(candidates.Count);

            return 0;
        }
    }

}

using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Metrices;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class SimulationRunnerTests
{
    [Test]
    public void SimulateOne_WhenLevelCanBeCleared_ReturnsSuccess()
    {
        var level = CreateSingleMatchLevel();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateOne(level, new Random(123));

        Assert.That(metrics.IsFailed, Is.False);
        Assert.That(metrics.FailPosition, Is.EqualTo(-1));
    }

    [Test]
    public void SimulateOne_DoesNotMutateOriginalLevel()
    {
        var level = CreateSingleMatchLevel();
        var before = level.Serialize();
        var runner = new SimulationRunner();

        _ = runner.SimulateOne(level, new Random(123));

        Assert.That(level.Serialize(), Is.EqualTo(before));
        Assert.That(level.Pasture.PresentTiles.Count(), Is.EqualTo(3));
        Assert.That(level.StagingArea.IsEmpty, Is.True);
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void SimulateOne_WhenStagingAreaIsFull_ReturnsFailureAtCurrentMoveCount()
    {
        var level = CreateFullStagingFailureLevel();
        var runner = new SimulationRunner();

        var metrics = runner.SimulateOne(level, new Random(123));

        Assert.That(metrics.IsFailed, Is.True);
        Assert.That(metrics.FailPosition, Is.Zero);
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
}

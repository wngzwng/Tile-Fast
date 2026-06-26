using Tile.Core.Metrices;

namespace Tile.Core.Simulation;

public static class SimulationRunMetricKeys
{
    public static readonly MetricKey<bool> IsFailed =
        new("run.simulation.is_failed");

    public static readonly MetricKey<int> FailPosition =
        new("run.simulation.fail_position");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        IsFailed,
        FailPosition,
    ];
}

public readonly record struct SimulationRunMetrics(
    bool IsFailed,
    int FailPosition)
{
    public override string ToString()
    {
        return $"SimulationRunMetrics(" +
               $"IsFailed={IsFailed}, " +
               $"FailPosition={FailPosition})";
    }

    public void WriteTo(MetricBag bag)
    {
        ArgumentNullException.ThrowIfNull(bag);

        bag.Set(SimulationRunMetricKeys.IsFailed, IsFailed);
        bag.Set(SimulationRunMetricKeys.FailPosition, FailPosition);
    }
}

public static class SimulationBatchMetricKeys
{
    public static readonly MetricKey<int> TotalCount =
        new("batch.simulation.total_count");

    public static readonly MetricKey<int> SuccessCount =
        new("batch.simulation.success_count");

    public static readonly MetricKey<int> FailureCount =
        new("batch.simulation.failure_count");

    public static readonly MetricKey<double> FailureRate =
        new("batch.simulation.failure_rate");

    public static readonly MetricKey<double> AverageFailPosition =
        new("batch.simulation.avg_fail_position");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        TotalCount,
        SuccessCount,
        FailureCount,
        FailureRate,
        AverageFailPosition,
    ];
}

public readonly record struct SimulationBatchMetrics(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    double FailureRate,
    double AverageFailPosition)
{
    public override string ToString()
    {
        return $"SimulationBatchMetrics(" +
               $"Total={TotalCount}, " +
               $"Success={SuccessCount}, " +
               $"Failure={FailureCount}, " +
               $"FailureRate={FailureRate}, " +
               $"AverageFailPosition={AverageFailPosition})";
    }

    public void WriteTo(MetricBag bag)
    {
        ArgumentNullException.ThrowIfNull(bag);

        bag.Set(SimulationBatchMetricKeys.TotalCount, TotalCount);
        bag.Set(SimulationBatchMetricKeys.SuccessCount, SuccessCount);
        bag.Set(SimulationBatchMetricKeys.FailureCount, FailureCount);
        bag.Set(SimulationBatchMetricKeys.FailureRate, FailureRate);
        bag.Set(SimulationBatchMetricKeys.AverageFailPosition, AverageFailPosition);
    }
}

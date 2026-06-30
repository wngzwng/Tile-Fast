using Tile.Core.Simulation;

namespace Tile.Core.Metrices;

/// <summary>
/// Simulation 指标收集器，负责从上下文计算 run 指标，并把 run 指标聚合到 batch 指标。
/// </summary>
public interface IMetricCollector
{
    /// <summary>
    /// 计算单次 run 指标，并写入 <paramref name="runBag"/>。
    /// </summary>
    void Compute(
        SimulationContext context,
        MetricBag runBag);

    /// <summary>
    /// 读取 <paramref name="runBag"/>，把本次 run 的结果聚合进 <paramref name="aggBag"/>。
    /// </summary>
    void Aggregate(
        SimulationContext context,
        MetricBag runBag,
        MetricBag aggBag);
}

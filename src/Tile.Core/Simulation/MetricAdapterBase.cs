using Tile.Core.Metrices;

namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 指标适配器基类：接入生命周期 hook，负责 run 指标计算、batch 聚合和按需序列化。
/// </summary>
public abstract class MetricAdapterBase :
    ISimulationHook,
    IMetricCollector,
    IMetricSerializer
{
    /// <summary>
    /// 计算单次 run 指标；默认由 <see cref="ISimulationHook.OnRunEnd"/> 调用。
    /// </summary>
    public abstract void Compute(
        SimulationContext context,
        MetricBag runBag);

    /// <summary>
    /// 把单次 run 指标聚合进 batch 指标；默认由 <see cref="ISimulationHook.OnRunAggregate"/> 调用。
    /// </summary>
    public abstract void Aggregate(
        SimulationContext context,
        MetricBag runBag,
        MetricBag aggBag);

    /// <summary>
    /// 把适配器关心的指标输出到字典。默认不输出任何字段。
    /// </summary>
    public virtual void Serialize(
        MetricBag bag,
        Dictionary<string, object?> output)
    {
    }

    public virtual void OnRunEnd(
        SimulationContext context,
        ref MetricBag runBag)
    {
        Compute(context, runBag);
    }

    public virtual void OnRunAggregate(
        SimulationContext context,
        ref MetricBag runBag,
        ref MetricBag aggBag)
    {
        Aggregate(context, runBag, aggBag);
    }
}

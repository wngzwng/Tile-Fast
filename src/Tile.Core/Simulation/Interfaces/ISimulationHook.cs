using Tile.Core.Metrices;

namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 生命周期扩展点，用于在固定时机观察状态并写入指标。
/// </summary>
public interface ISimulationHook
{
    /// <summary>
    /// Batch 开始前调用；此时尚未进入任何 run。
    /// </summary>
    void OnBatchStart(SimulationContext context);

    /// <summary>
    /// Batch 完成后调用；批量指标已写入 <paramref name="aggBag"/>。
    /// </summary>
    void OnBatchEnd(SimulationContext context, ref MetricBag aggBag);

    /// <summary>
    /// 单次 run 开始后调用；run 级指标应写入 <paramref name="runBag"/>。
    /// </summary>
    void OnRunStart(SimulationContext context, ref MetricBag runBag);

    /// <summary>
    /// 每个 step 的候选集合收集完成后、行为选择前调用。
    /// </summary>
    void OnStepBefore(SimulationContext context, ref MetricBag runBag);

    /// <summary>
    /// 当前行为已选定、尚未执行前调用。
    /// </summary>
    void OnBehaviourBefore(SimulationContext context, ref MetricBag runBag);

    /// <summary>
    /// 当前行为执行完成后调用。
    /// </summary>
    void OnBehaviourAfter(SimulationContext context, ref MetricBag runBag);

    /// <summary>
    /// 每个 step 完成后调用。
    /// </summary>
    void OnStepAfter(SimulationContext context, ref MetricBag runBag);

    /// <summary>
    /// 单次 run 完成后调用；run 结果指标已写入 <paramref name="runBag"/>。
    /// </summary>
    void OnRunEnd(SimulationContext context, ref MetricBag runBag);
}

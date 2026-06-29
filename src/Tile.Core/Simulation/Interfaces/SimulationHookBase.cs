using Tile.Core.Metrices;

namespace Tile.Core.Simulation;

/// <summary>
/// 空实现的 Simulation hook 基类；派生类只覆写需要的生命周期方法。
/// </summary>
public abstract class SimulationHookBase : ISimulationHook
{
    public virtual void OnBatchStart(SimulationContext context)
    {
    }

    public virtual void OnBatchEnd(SimulationContext context, ref MetricBag aggBag)
    {
    }

    public virtual void OnRunStart(SimulationContext context, ref MetricBag runBag)
    {
    }

    public virtual void OnStepBefore(SimulationContext context, ref MetricBag runBag)
    {
    }

    public virtual void OnBehaviourBefore(SimulationContext context, ref MetricBag runBag)
    {
    }

    public virtual void OnBehaviourAfter(SimulationContext context, ref MetricBag runBag)
    {
    }

    public virtual void OnStepAfter(SimulationContext context, ref MetricBag runBag)
    {
    }

    public virtual void OnRunEnd(SimulationContext context, ref MetricBag runBag)
    {
    }
}

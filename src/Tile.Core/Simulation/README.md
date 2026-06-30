# Simulation 容器复用与指标适配器设计

## 目标

Simulation 需要支持大批量运行，因此核心对象应尽量复用：

* `SimulationContext` 是运行上下文容器，可复用。
* `MetricBag` 是指标容器，可复用。
* `SimulationRunner` 负责生命周期推进。
* 指标适配器接入 hook，负责计算和聚合。
* 序列化在模拟结束后由外部决定是否执行。

核心原则：

```text
Runner 控制生命周期
Context / MetricBag 由外部复用
Adapter 只计算和聚合
Serializer 只负责输出
```

---

## 对象职责

### SimulationContext

`SimulationContext` 表示当前 batch / run / step 的运行状态。

它应该保存：

* 当前关卡副本
* 当前 run index
* batch 总次数
* 成功 / 失败数量
* 当前候选模式
* 当前局面候选快照
* 当前 run 状态
* 当前 move count

它不应该保存：

* `runBag`
* `aggBag`
* 输出字典
* CSV / JSON 写入器

原因：

```text
Context 是运行状态容器，不是指标容器，也不是输出容器。
```

建议后续提供复用入口：

```csharp
context.ResetBatch(
    level,
    simulationCount,
    random,
    candidateMode);

context.StartRun(simulationIndex);
```

每个 step 开始时：

```csharp
context.Candidates.Clear();
```

`SimulationCandidateSet<TCandidate>` 只表示当前局面的候选快照，不保存历史累计。

---

## MetricBag

`MetricBag` 是唯一指标数据载体。

它应该被外部创建并复用：

```csharp
var runBag = new MetricBag();
var aggBag = new MetricBag();
```

每个 batch 开始：

```csharp
aggBag.ResetValues();
```

每个 run 开始：

```csharp
runBag.ResetValues();
```

`ResetValues()` 与 `Clear()` 的语义：

```text
ResetValues()
    清空当前值
    保留内部 List 容器和容量
    适合 simulation 热路径

Clear()
    清空所有值和内部容器
    适合 schema 变化或彻底释放状态
```

---

## Runner 生命周期

推荐 Runner 使用外部初始化好的容器：

```csharp
public SimulationBatchMetrics SimulateMany(
    SimulationContext context,
    MetricBag runBag,
    MetricBag aggBag,
    IReadOnlyList<ISimulationHook>? hooks = null);
```

旧的便捷入口可以保留：

```csharp
public SimulationBatchMetrics SimulateMany(...);
```

便捷入口内部可以临时创建并初始化容器；高频批处理应使用可复用入口。

可复用入口不装配、不初始化 `SimulationContext`。调用方需要先完成 batch 初始化：

```csharp
context.ResetBatch(
    level,
    simulationCount,
    random,
    candidateMode);

runner.SimulateMany(
    context,
    runBag,
    aggBag,
    hooks);
```

原因：

```text
Context 是可复用状态容器。
Runner 只推进已初始化的 context，不替调用方决定本轮 batch 的 level / count / random。
```

推荐生命周期：

```text
外部:
    context.ResetBatch(...)

Runner:
aggBag.ResetValues()
OnBatchStart

for each run:
    runBag.ResetValues()
    context.StartRun(i)
    OnRunStart

    while running:
        context.Candidates.Clear()
        Finder 收集当前局面候选
        Scorer 选择候选
        OnStepBefore
        OnBehaviourBefore
        执行动作
        OnBehaviourAfter
        OnStepAfter

    内置 run metrics 写入 runBag
    OnRunEnd
    OnRunAggregate(runBag -> aggBag)

内置 batch metrics 写入 aggBag
OnBatchEnd
return result
```

---

## 指标适配器

指标适配器负责把指标计算接入 Simulation 生命周期。

建议基类：

```csharp
public abstract class MetricAdapterBase :
    ISimulationHook,
    IMetricCollector,
    IMetricSerializer
{
    public abstract void Compute(
        SimulationContext context,
        MetricBag runBag);

    public abstract void Aggregate(
        SimulationContext context,
        MetricBag runBag,
        MetricBag aggBag);

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
```

说明：

* `Compute` 只写 `runBag`。
* `Aggregate` 只读 `runBag`，写 `aggBag`。
* `Serialize` 在模拟结束后由外部按需调用。
* Adapter 不创建 bag。
* Adapter 不保存 bag。

---

## Hook 补充

为了让聚合不依赖缓存 runBag，建议增加一个生命周期 hook：

```csharp
void OnRunAggregate(
    SimulationContext context,
    ref MetricBag runBag,
    ref MetricBag aggBag);
```

位置：

```text
runMetrics.WriteTo(runBag)
context.CompleteRun(runMetrics)
OnRunEnd(context, runBag)
OnRunAggregate(context, runBag, aggBag)
```

这样 adapter 可以在每个 run 结束后立即聚合。

不建议让 adapter 私下保存所有 `runBag`，因为这会破坏容器复用目标。

---

## 序列化

序列化不属于 hook 自动流程。

模拟结束后，外部决定：

* 是否序列化
* 调用哪些 adapter 的序列化逻辑
* 输出到 Dictionary / CSV / JSON

推荐调用：

```csharp
var output = new Dictionary<string, object?>();

adapter.Serialize(
    aggBag,
    output);
```

Serializer 不依赖 `SimulationContext`；adapter 序列化自己关心的指标。

---

## 推荐实现顺序

1. 增加 `OnRunAggregate` hook。
2. 新增 `MetricAdapterBase`，同时实现 hook、collector、serializer。
3. 新增可复用 Runner 入口，接收外部 `SimulationContext / runBag / aggBag`。
4. 调整旧 Runner 入口为便捷 wrapper。
5. 外部在模拟结束后按需调用 adapter 的 `Serialize`。

---

## 不做的事

不要把 `runBag / aggBag` 放进 `SimulationContext`。

不要在 `OnBatchEnd` 自动序列化。

不要让 adapter 缓存所有 runBag 来做聚合。

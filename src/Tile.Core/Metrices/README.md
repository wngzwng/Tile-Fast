1. 指标计算器： 多个指标 外部指定： 我要哪些指标 -> 注入对应的指标计算器 收集，聚合，序列化 （bag, 指标计算器组） -> Dict<string, object> 2. 要哪些具体的指标，以及指标的顺序 + Dict<string, object> => csv



# Metrics System 设计文档（最终版）

---

# 1. 系统目标

构建一个支持以下能力的指标系统：

* Simulation 生命周期指标采集（主流程）
* Event 驱动指标采集（旁路流程）
* 指标计算（Compute）
* 指标聚合（Aggregate）
* 指标序列化（Serialize）
* CSV / Dictionary 输出

---

# 2. 系统结构

系统由两条数据通道组成：

## 2.1 Simulation Metrics（主流程）

* 生命周期驱动
* Run / Batch 结构明确

数据流：

```
Compute → Aggregate → Serialize
```

---

## 2.2 Event Metrics（旁路流程）

* 策略驱动
* 非生命周期绑定

数据流：

```
Event → EventCollector → MetricBag
```

---

## 2.3 汇合点

所有指标最终统一进入：

```
MetricBag
```

---

# 3. 核心对象

---

## 3.1 SimulationContext

SimulationContext 是 simulation 的运行状态容器，可被外部创建并复用。

它保存 batch / run / step 过程中会变化的状态：

* 当前 run 使用的关卡副本
* 当前 run index 与 batch 总次数
* 成功 / 失败 run 计数
* 当前候选模式与当前局面的候选快照
* 当前 run 状态与已执行行为数量

它不保存 `runBag`、`aggBag`，也不负责输出。

高频批处理时，调用方应先初始化 context：

```csharp
context.ResetBatch(
    level,
    simulationCount,
    random,
    candidateMode);
```

Runner 的可复用入口只推进已初始化的 context，不负责装配本轮 batch 的
level / count / random。

---

## 3.2 MetricBag

指标容器（唯一数据载体）

```csharp
public sealed class MetricBag
{
    void Set<T>(MetricKey<T> key, T value);

    void Add(MetricKey<int> key, int value);
    void Add(MetricKey<double> key, double value);

    void Append<T>(MetricKey<List<T>> key, T value);

    bool TryRead<T>(MetricKey<T> key, out T? value);
}
```

---

## 3.3 MetricKey

强类型指标键

```csharp
public readonly struct MetricKey<T>
{
    public string Name { get; }

    public Type ValueType => typeof(T);

    public MetricKey(string name)
    {
        Name = name;
    }
}
```

---

# 4. Simulation Metrics（主流程接口）

## 4.1 Metric Collector

负责 Compute + Aggregate

```csharp
public interface IMetricCollector
{
    void Compute(
        SimulationContext context,
        MetricBag runBag);

    void Aggregate(
        SimulationContext context,
        MetricBag runBag,
        MetricBag aggBag);
}
```

---

## 职责说明

### Compute

* 单次运行指标生成
* 写入 runBag

### Aggregate

* run → batch 聚合
* 写入 aggBag（streaming）

---

# 5. Event Metrics（旁路流程接口）

## 5.1 Event 定义

```csharp
public readonly struct MetricEvent
{
    public string Name { get; }

    public SimulationContext Context { get; }

    public object? Payload { get; }
}
```

---

## 5.2 Event Collector

```csharp
public interface IMetricEventCollector
{
    void Handle(MetricEvent evt, MetricBag bag);
}
```

---

## 职责说明

* 接收事件
* 根据事件写入 MetricBag
* 不参与生命周期

---

## 5.3 Event Bus（逻辑层）

```
Strategy → Event → Collector → MetricBag
```

---

# 6. Simulation 生命周期控制（Hook）

Simulation 相关代码按目录分组：

```text
Simulation/
├── Interfaces/  生命周期 hook、候选生成与候选评分接口
├── Finders/     候选生成器默认实现
├── Scorers/     候选评分器默认实现
├── Types/       上下文、模式、状态等类型
├── Metrics/     Simulation 指标键与结果结构
└── SimulationRunner.cs
```

```csharp
public interface ISimulationHook
{
    void OnBatchStart(SimulationContext context);

    void OnBatchEnd(SimulationContext context, ref MetricBag aggBag);

    void OnRunStart(SimulationContext context, ref MetricBag runBag);

    void OnStepBefore(SimulationContext context, ref MetricBag runBag);

    void OnBehaviourBefore(SimulationContext context, ref MetricBag runBag);

    void OnBehaviourAfter(SimulationContext context, ref MetricBag runBag);

    void OnStepAfter(SimulationContext context, ref MetricBag runBag);

    void OnRunEnd(SimulationContext context, ref MetricBag runBag);

    void OnRunAggregate(
        SimulationContext context,
        ref MetricBag runBag,
        ref MetricBag aggBag);
}
```

`ISimulationHook` 为各生命周期方法提供默认空实现，常用实现只需要实现关心的方法。

Runner 的候选流程拆成两个可替换接口：

```csharp
public interface ISimulationCandidateFinder
{
    SimulationCandidateMode CandidateMode { get; }

    int FindCandidates(SimulationContext context);
}

public interface ISimulationCandidateScorer
{
    SimulationCandidateMode CandidateMode { get; }

    int SelectCandidateOffset(
        SimulationContext context,
        IReadOnlyList<int> candidates);
}

```

默认实现使用 `SelectableTileCandidateFinder` 收集当前可选 tile，
再由 `RandomCandidateScorer` 在候选集合中随机选择一个候选 offset。
候选项统一由 `SimulationContext.Candidates` 表示当前容器，内部使用 `List<TCandidate>` 存储：

* `SimulationCandidateMode.Tile` 下，候选值是 tileIndex。
* `SimulationCandidateMode.Behaviour` 下，候选值是 `Behaviour`。

---

## 职责说明

* 控制执行时机
* 不参与指标计算
* 不直接处理 Event
* `OnBehaviourBefore` / `OnBehaviourAfter` 可从 `SimulationContext.Candidates.SelectedOffset` 读取本步选中的候选 offset。
* Tile 候选模式下，可从 `SimulationContext.TileCandidates.SelectedItem` 或 `SimulationContext.SelectedTileIndex` 读取本步选中的 tileIndex。
* Behaviour 候选模式下，可从 `SimulationContext.BehaviourCandidates.SelectedItem` 或 `SimulationContext.SelectedBehaviour` 读取本步选中的行为组。

---

# 7. 指标序列化接口

```csharp
public interface IMetricSerializer
{
    void Serialize(
        MetricBag bag,
        Dictionary<string, object?> output);
}
```

---

## 职责说明

* MetricBag → Dictionary<string, object?>
* 输出哪些指标由实现方决定
* 用于 CSV / JSON

---

# 8. 执行流程

---

## 8.1 Simulation 主流程

```
OnBatchStart
    ↓
for each Run:
    OnRunStart
    Compute(runBag)
    OnStep (optional)
    Aggregate(runBag → aggBag)
    OnRunEnd
OnBatchEnd
Serialize(aggBag)
```

---

## 8.2 Event 流程（并行）

```
Strategy Execution
    ↓
MetricEvent
    ↓
IMetricEventCollector
    ↓
MetricBag（写入 runBag / aggBag）
```

---

# 9. 数据流汇总

```
                 SimulationContext
                         ↓
        ┌────────────────────────────────┐
        │        Compute Layer           │
        │   runBag (MetricBag)          │
        └─────────────┬──────────────────┘
                      ↓
        ┌────────────────────────────────┐
        │      Aggregate Layer           │
        │   aggBag (MetricBag)          │
        └─────────────┬──────────────────┘
                      ↓
        ┌────────────────────────────────┐
        │     Event Metrics Layer        │
        │ MetricEvent → Collectors       │
        └─────────────┬──────────────────┘
                      ↓
                MetricBag（统一）
                      ↓
        ┌────────────────────────────────┐
        │       Serialize Layer          │
        │ Dictionary<string, object?>   │
        └─────────────┬──────────────────┘
                      ↓
                    CSV
```

---

# 10. 核心设计原则

## 10.1 单一数据载体

* 所有指标最终进入 MetricBag

---

## 10.2 双通道输入

* Simulation（结构化）
* Event（行为驱动）

---

## 10.3 生命周期隔离

* Hook 控制生命周期
* Collector 不控制时序
* Event 不参与生命周期

---

## 10.4 分层职责

| 层             | 职责     |
| -------------- | -------- |
| Hook           | 调度     |
| Collector      | 计算     |
| EventCollector | 行为采集 |
| Serializer     | 输出     |

---

# 11. 系统本质

该系统是：

> 双通道（Simulation + Event）驱动的 streaming metrics pipeline，通过 MetricBag 统一承载数据，并通过 Aggregate + Serialize 完成批处理输出。

---

如果你下一步要继续，我可以帮你做：

* 结构图（可视化架构）
* 或 API 最小实现版本（可直接开工）

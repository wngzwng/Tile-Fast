# 指标文件三件套设计文档

## 1. 背景

在 ThreeTile 的模拟、求解、批处理和分析系统中，指标会不断增长。

例如：

```text
level.success_count
level.fail_count
level.fail_rate
run.policy.avg_candidate_count
run.policy.max_candidate_count
batch.policy.avg_candidate_count
slot.slot_usage_after_move_list
smooth.max_smooth_tile_steps
```

如果所有指标都放进一个巨大的全局表，例如：

```csharp
public static class MetricKeys
{
    public static readonly MetricKey<int> SuccessCount = ...
    public static readonly MetricKey<double> FailRate = ...
    public static readonly MetricKey<int> CandidateCount = ...
    public static readonly MetricKey<double> AvgCandidateScore = ...
    // 几百个指标...
}
```

后期会出现几个问题：

```text
1. 全局指标表越来越大，难以维护。
2. 不知道某个指标是谁计算出来的。
3. 指标名、指标结果结构、指标计算逻辑分散在不同地方。
4. 删除或修改某组指标时，很难确认影响范围。
5. Output 层容易和 Calculator 层发生职责混乱。
```

因此推荐使用“指标文件三件套”设计。

---

## 2. 核心原则

核心原则是：

```text
每组指标一个文件。
谁计算，谁声明自己能产出哪些指标。
不要建立一个巨大的全局指标名总表。
```

每个指标文件内部包含三件套：

```text
1. MetricKeys
   这一组指标能产出的强类型 Key。

2. Metrics
   这一组指标的结果结构体。

3. MetricCalculator
   这一组指标的计算器。
```

例如：

```text
LevelResultMetrics.cs
    ├── LevelResultMetricKeys
    ├── LevelResultMetrics
    └── LevelResultMetricCalculator
```

或者：

```text
PolicyCandidateMetrics.cs
    ├── PolicyCandidateRunMetricKeys
    ├── PolicyCandidateBatchMetricKeys
    ├── PolicyCandidateRunMetrics
    ├── PolicyCandidateBatchMetrics
    └── PolicyCandidateMetricCalculator
```

---

## 3. 三件套职责划分

### 3.1 MetricKeys

`MetricKeys` 负责声明这一组指标可以产出哪些指标列。

它不负责计算，也不负责输出。

例如：

```csharp
public static class LevelResultMetricKeys
{
    public static readonly MetricKey<int> SuccessCount =
        new("level.success_count");

    public static readonly MetricKey<int> FailCount =
        new("level.fail_count");

    public static readonly MetricKey<double> FailRate =
        new("level.fail_rate");

    public static readonly MetricKey<double> AvgScore =
        new("level.avg_score");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        SuccessCount,
        FailCount,
        FailRate,
        AvgScore,
    ];
}
```

它的职责是：

```text
声明指标名。
声明指标类型。
提供这一组指标的 All 列表。
```

其中 `All` 的作用是：

```text
告诉 Output 层：这一组指标理论上可以产出哪些列。
```

但 `All` 不一定等于最终 CSV 输出顺序。

最终输出顺序仍由 CLI / Output 层决定。

---

### 3.2 Metrics 结果结构体

`Metrics` 结构体负责承载一组指标的计算结果。

例如：

```csharp
public readonly record struct LevelResultMetrics(
    int SuccessCount,
    int FailCount,
    double FailRate,
    double AvgScore)
{
    public void WriteTo(MetricBag bag)
    {
        bag.Set(LevelResultMetricKeys.SuccessCount, SuccessCount);
        bag.Set(LevelResultMetricKeys.FailCount, FailCount);
        bag.Set(LevelResultMetricKeys.FailRate, FailRate);
        bag.Set(LevelResultMetricKeys.AvgScore, AvgScore);
    }
}
```

它的职责是：

```text
1. 承载一组强类型指标结果。
2. 把结果写入 MetricBag。
```

它不负责：

```text
1. 决定 CSV 输出顺序。
2. 决定是否输出某个指标。
3. 决定输出文件路径。
4. 决定 CSV 格式化细节。
```

这些属于 Output 层。

---

### 3.3 MetricCalculator

`MetricCalculator` 负责从原始运行结果、Collector 数据或聚合数据中计算指标。

例如：

```csharp
public static class LevelResultMetricCalculator
{
    public static LevelResultMetrics Calculate(
        int successCount,
        int failCount,
        double totalScore)
    {
        int totalCount = successCount + failCount;

        double failRate = totalCount == 0
            ? 0
            : (double)failCount / totalCount;

        double avgScore = totalCount == 0
            ? 0
            : totalScore / totalCount;

        return new LevelResultMetrics(
            SuccessCount: successCount,
            FailCount: failCount,
            FailRate: failRate,
            AvgScore: avgScore);
    }
}
```

它的职责是：

```text
1. 接收原始数据。
2. 计算指标。
3. 返回 Metrics 结果结构体。
```

它不负责：

```text
1. 写 CSV。
2. 控制输出列顺序。
3. 控制最终输出路径。
4. 读取 CLI 输出配置。
```

---

## 4. 为什么使用 MetricKey<T>，而不是 const string

旧写法可能是：

```csharp
public static class LevelResultMetricNames
{
    public const string SuccessCount = "level.successCount";
    public const string FailCount = "level.failCount";
    public const string FailRate = "level.failRate";
    public const string AvgScore = "level.avgScore";

    public static readonly string[] All =
    {
        SuccessCount,
        FailCount,
        FailRate,
        AvgScore,
    };
}
```

这种方式的问题是：

```text
1. 指标名和指标类型没有绑定。
2. SuccessCount 是 int 这件事丢了。
3. FailRate 是 double 这件事丢了。
4. 写入 MetricBag 时仍然可能写错类型。
```

例如：

```csharp
bag.SetDouble(LevelResultMetricNames.SuccessCount, 1.5);
```

如果 MetricBag 保留了 `SetDouble(string name, double value)` 这种接口，这段代码可以编译通过。

使用 `MetricKey<TValue>` 后：

```csharp
public static readonly MetricKey<int> SuccessCount =
    new("level.success_count");

public static readonly MetricKey<double> FailRate =
    new("level.fail_rate");
```

此时：

```csharp
bag.Set(LevelResultMetricKeys.SuccessCount, 10);
```

正确。

但：

```csharp
bag.Set(LevelResultMetricKeys.SuccessCount, 0.25);
```

会直接编译失败。

因为 `SuccessCount` 是：

```csharp
MetricKey<int>
```

只能写入 `int`。

---

## 5. MetricKey<T> 与 IMetricKey

由于不同指标类型不同：

```csharp
MetricKey<int>
MetricKey<double>
MetricKey<bool>
MetricKey<string>
MetricKey<List<int>>
MetricKey<List<double>>
```

它们不能直接放进一个 `MetricKey<T>[]`。

因此需要一个非泛型接口：

```csharp
public interface IMetricKey
{
    string Name { get; }

    Type ValueType { get; }
}
```

`MetricKey<TValue>` 实现这个接口：

```csharp
public readonly struct MetricKey<TValue> : IMetricKey
{
    public MetricKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Metric key name cannot be empty.", nameof(name));

        Name = name;
    }

    public string Name { get; }

    public Type ValueType => typeof(TValue);

    public override string ToString()
    {
        return Name;
    }
}
```

这样每组指标就可以提供：

```csharp
public static readonly IReadOnlyList<IMetricKey> All =
[
    SuccessCount,
    FailCount,
    FailRate,
    AvgScore,
];
```

`MetricKey<T>` 用于强类型读写。

`IMetricKey` 用于 Output 层统一遍历列。

---

## 6. 指标命名约定

推荐指标名使用：

```text
scope.domain.metric_name
```

例如：

```text
level.success_count
level.fail_count
level.fail_rate

run.policy.avg_candidate_count
run.policy.max_candidate_count
run.policy.no_candidate_count

batch.policy.avg_candidate_count
batch.policy.max_candidate_count
batch.policy.no_candidate_count
```

命名层级建议：

```text
level.*
    关卡级聚合指标。

run.*
    单次运行指标。

batch.*
    批量聚合指标。

debug.*
    调试指标。

error.*
    错误或失败原因指标。
```

风格上推荐：

```text
snake_case
```

例如：

```text
level.success_count
run.policy.avg_candidate_count
batch.slot.avg_slot_usage
```

不推荐：

```text
level.successCount
level.SuccessCount
LEVEL.SUCCESS_COUNT
```

原因：

```text
1. snake_case 更适合作为 CSV 列名。
2. 和 TOML / 配置字段风格一致。
3. 跨语言处理更自然。
4. 输出到 shell、Python、pandas 时更稳定。
```

---

## 7. 单次指标与批量指标

同一个领域的指标，通常需要区分：

```text
run.*
    单次运行指标。

batch.*
    批量聚合指标。
```

例如，候选数量相关指标可以拆成：

```csharp
public static class PolicyCandidateRunMetricKeys
{
    public static readonly MetricKey<double> AvgCandidateCount =
        new("run.policy.avg_candidate_count");

    public static readonly MetricKey<int> MaxCandidateCount =
        new("run.policy.max_candidate_count");

    public static readonly MetricKey<int> NoCandidateCount =
        new("run.policy.no_candidate_count");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    ];
}
```

批量指标：

```csharp
public static class PolicyCandidateBatchMetricKeys
{
    public static readonly MetricKey<double> AvgCandidateCount =
        new("batch.policy.avg_candidate_count");

    public static readonly MetricKey<int> MaxCandidateCount =
        new("batch.policy.max_candidate_count");

    public static readonly MetricKey<int> NoCandidateCount =
        new("batch.policy.no_candidate_count");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    ];
}
```

注意：

```text
AvgCandidateCount 是 double。
MaxCandidateCount 是 int。
NoCandidateCount 是 int。
```

不要因为它们同属候选指标，就全部用 double。

类型应该表达指标本身的含义。

---

## 8. LevelResultMetrics 完整示例

```csharp
using System.Collections.Generic;

namespace ThreeTile.Core.Simulation.Metrics;

public static class LevelResultMetricKeys
{
    public static readonly MetricKey<int> SuccessCount =
        new("level.success_count");

    public static readonly MetricKey<int> FailCount =
        new("level.fail_count");

    public static readonly MetricKey<double> FailRate =
        new("level.fail_rate");

    public static readonly MetricKey<double> AvgScore =
        new("level.avg_score");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        SuccessCount,
        FailCount,
        FailRate,
        AvgScore,
    ];
}

public readonly record struct LevelResultMetrics(
    int SuccessCount,
    int FailCount,
    double FailRate,
    double AvgScore)
{
    public void WriteTo(MetricBag bag)
    {
        bag.Set(LevelResultMetricKeys.SuccessCount, SuccessCount);
        bag.Set(LevelResultMetricKeys.FailCount, FailCount);
        bag.Set(LevelResultMetricKeys.FailRate, FailRate);
        bag.Set(LevelResultMetricKeys.AvgScore, AvgScore);
    }
}

public static class LevelResultMetricCalculator
{
    public static LevelResultMetrics Calculate(
        int successCount,
        int failCount,
        double totalScore)
    {
        int totalCount = successCount + failCount;

        double failRate = totalCount == 0
            ? 0
            : (double)failCount / totalCount;

        double avgScore = totalCount == 0
            ? 0
            : totalScore / totalCount;

        return new LevelResultMetrics(
            SuccessCount: successCount,
            FailCount: failCount,
            FailRate: failRate,
            AvgScore: avgScore);
    }
}
```

---

## 9. PolicyCandidateMetrics 完整示例

```csharp
using System.Collections.Generic;

namespace ThreeTile.Core.Simulation.Metrics;

public static class PolicyCandidateRunMetricKeys
{
    public static readonly MetricKey<double> AvgCandidateCount =
        new("run.policy.avg_candidate_count");

    public static readonly MetricKey<int> MaxCandidateCount =
        new("run.policy.max_candidate_count");

    public static readonly MetricKey<int> NoCandidateCount =
        new("run.policy.no_candidate_count");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    ];
}

public readonly record struct PolicyCandidateRunMetrics(
    double AvgCandidateCount,
    int MaxCandidateCount,
    int NoCandidateCount)
{
    public void WriteTo(MetricBag bag)
    {
        bag.Set(PolicyCandidateRunMetricKeys.AvgCandidateCount, AvgCandidateCount);
        bag.Set(PolicyCandidateRunMetricKeys.MaxCandidateCount, MaxCandidateCount);
        bag.Set(PolicyCandidateRunMetricKeys.NoCandidateCount, NoCandidateCount);
    }
}

public static class PolicyCandidateBatchMetricKeys
{
    public static readonly MetricKey<double> AvgCandidateCount =
        new("batch.policy.avg_candidate_count");

    public static readonly MetricKey<int> MaxCandidateCount =
        new("batch.policy.max_candidate_count");

    public static readonly MetricKey<int> NoCandidateCount =
        new("batch.policy.no_candidate_count");

    public static readonly IReadOnlyList<IMetricKey> All =
    [
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    ];
}

public readonly record struct PolicyCandidateBatchMetrics(
    double AvgCandidateCount,
    int MaxCandidateCount,
    int NoCandidateCount)
{
    public void WriteTo(MetricBag bag)
    {
        bag.Set(PolicyCandidateBatchMetricKeys.AvgCandidateCount, AvgCandidateCount);
        bag.Set(PolicyCandidateBatchMetricKeys.MaxCandidateCount, MaxCandidateCount);
        bag.Set(PolicyCandidateBatchMetricKeys.NoCandidateCount, NoCandidateCount);
    }
}

public static class PolicyCandidateMetricCalculator
{
    public static PolicyCandidateRunMetrics CalculateRun(
        IReadOnlyList<int> candidateCounts)
    {
        if (candidateCounts.Count == 0)
        {
            return new PolicyCandidateRunMetrics(
                AvgCandidateCount: 0,
                MaxCandidateCount: 0,
                NoCandidateCount: 0);
        }

        int sum = 0;
        int max = 0;
        int noCandidateCount = 0;

        for (int i = 0; i < candidateCounts.Count; i++)
        {
            int count = candidateCounts[i];

            sum += count;

            if (count > max)
                max = count;

            if (count == 0)
                noCandidateCount++;
        }

        return new PolicyCandidateRunMetrics(
            AvgCandidateCount: (double)sum / candidateCounts.Count,
            MaxCandidateCount: max,
            NoCandidateCount: noCandidateCount);
    }

    public static PolicyCandidateBatchMetrics CalculateBatch(
        IReadOnlyList<PolicyCandidateRunMetrics> runs)
    {
        if (runs.Count == 0)
        {
            return new PolicyCandidateBatchMetrics(
                AvgCandidateCount: 0,
                MaxCandidateCount: 0,
                NoCandidateCount: 0);
        }

        double avgCandidateCountSum = 0;
        int maxCandidateCount = 0;
        int noCandidateCount = 0;

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];

            avgCandidateCountSum += run.AvgCandidateCount;

            if (run.MaxCandidateCount > maxCandidateCount)
                maxCandidateCount = run.MaxCandidateCount;

            noCandidateCount += run.NoCandidateCount;
        }

        return new PolicyCandidateBatchMetrics(
            AvgCandidateCount: avgCandidateCountSum / runs.Count,
            MaxCandidateCount: maxCandidateCount,
            NoCandidateCount: noCandidateCount);
    }
}
```

---

## 10. Output 层如何消费 MetricBag

指标结果结构体不直接决定 CSV 顺序。

推荐流程是：

```text
MetricCalculator
    计算 Metrics 结果

Metrics.WriteTo(MetricBag)
    写入 MetricBag

Output 层
    根据配置好的 outputColumns 从 MetricBag 读取值
    按 outputColumns 顺序写出 CSV
```

示例：

```csharp
IReadOnlyList<IMetricKey> outputColumns =
[
    LevelResultMetricKeys.SuccessCount,
    LevelResultMetricKeys.FailCount,
    LevelResultMetricKeys.FailRate,
    LevelResultMetricKeys.AvgScore,
];
```

输出时：

```csharp
foreach (var key in outputColumns)
{
    bag.TryFormat(key, out var value);
    writer.WriteField(value);
}
```

这里：

```text
列顺序由 outputColumns 决定。
不是由 Metrics 结构体决定。
不是由 MetricKeys.All 决定。
```

---

## 11. MetricBag.TryFormat

为了让 Output 层可以根据 `IMetricKey` 统一读取值，MetricBag 可以提供一个格式化方法。

```csharp
public bool TryFormat(IMetricKey key, out string value)
{
    if (key.ValueType == typeof(int))
    {
        if (_ints.TryGetValue(key.Name, out int v))
        {
            value = v.ToString();
            return true;
        }
    }
    else if (key.ValueType == typeof(double))
    {
        if (_doubles.TryGetValue(key.Name, out double v))
        {
            value = v.ToString("G17");
            return true;
        }
    }
    else if (key.ValueType == typeof(bool))
    {
        if (_bools.TryGetValue(key.Name, out bool v))
        {
            value = v ? "true" : "false";
            return true;
        }
    }
    else if (key.ValueType == typeof(string))
    {
        if (_strings.TryGetValue(key.Name, out string? v))
        {
            value = v;
            return true;
        }
    }
    else if (key.ValueType == typeof(List<int>))
    {
        if (_intLists.TryGetValue(key.Name, out var list))
        {
            value = string.Join(";", list);
            return true;
        }
    }
    else if (key.ValueType == typeof(List<double>))
    {
        if (_doubleLists.TryGetValue(key.Name, out var list))
        {
            value = string.Join(";", list.Select(x => x.ToString("G17")));
            return true;
        }
    }

    value = string.Empty;
    return false;
}
```

注意：如果不想在 Core 层依赖 LINQ，可以把 `List<double>` 格式化挪到 Output 层，或者用手写循环。

高性能版本可以避免 LINQ：

```csharp
private static string FormatDoubleList(IReadOnlyList<double> values)
{
    if (values.Count == 0)
        return string.Empty;

    var builder = new System.Text.StringBuilder();

    builder.Append(values[0].ToString("G17"));

    for (int i = 1; i < values.Count; i++)
    {
        builder.Append(';');
        builder.Append(values[i].ToString("G17"));
    }

    return builder.ToString();
}
```

---

## 12. 是否保留 ToDict

不建议把 `ToDict()` 作为主路径。

例如：

```csharp
public Dictionary<string, object?> ToDict()
{
    return new Dictionary<string, object?>
    {
        [LevelResultMetricNames.SuccessCount] = SuccessCount,
        [LevelResultMetricNames.FailCount] = FailCount,
        [LevelResultMetricNames.FailRate] = FailRate,
        [LevelResultMetricNames.AvgScore] = AvgScore,
    };
}
```

问题是：

```text
1. int / double 写入 object 会装箱。
2. 类型信息重新丢失。
3. 又退回 Dictionary<string, object?> 模式。
4. 容易绕开 MetricBag 的强类型设计。
```

如果确实需要调试，可以命名为：

```csharp
ToDebugDictionary()
```

但不要作为正常输出链路。

---

## 13. 是否保留 ToCsvValues

不建议在 Metrics 结构体中保留 `ToCsvValues()` 作为主路径。

例如：

```csharp
public string[] ToCsvValues()
{
    return
    [
        SuccessCount.ToString(),
        FailCount.ToString(),
        FailRate.ToString("G17"),
        AvgScore.ToString("G17"),
    ];
}
```

这个方法的问题是：

```text
1. 它隐含了一套列顺序。
2. 它容易和 Output 层的列顺序配置重复。
3. 后续调整输出列时，需要同时改 Output 和 Metrics。
4. 它把格式化职责放进了结果结构体。
```

更推荐：

```text
Metrics 只提供 WriteTo(MetricBag)。
Output 层统一决定列顺序和格式化。
```

因此主路径应该是：

```csharp
metrics.WriteTo(bag);
```

然后：

```csharp
foreach (var key in outputColumns)
{
    bag.TryFormat(key, out var value);
    writer.WriteField(value);
}
```

---

## 14. All 的定位

每组 `MetricKeys.All` 只表示：

```text
这一组指标理论上能产出哪些指标。
```

它可以用于：

```text
1. 默认输出列。
2. 文档生成。
3. 校验配置中指定的指标名是否存在。
4. Debug 打印。
```

但它不应该强制决定最终 CSV 输出顺序。

最终 CSV 输出顺序应该来自：

```text
1. CLI 默认输出配置。
2. TOML 配置。
3. 用户命令行覆写。
4. Output 层组装后的 outputColumns。
```

也就是说：

```text
MetricKeys.All 是候选列。
outputColumns 是最终列。
```

---

## 15. 文件组织建议

建议按指标领域拆分文件。

例如：

```text
ThreeTile.Core/
└── Simulation/
    └── Metrics/
        ├── MetricKey.cs
        ├── MetricBag.cs
        ├── LevelResultMetrics.cs
        ├── PolicyCandidateMetrics.cs
        ├── SlotUsageMetrics.cs
        ├── SuitSeqMetrics.cs
        ├── SmoothMetrics.cs
        └── FailReasonMetrics.cs
```

每个 `*Metrics.cs` 文件内部保持三件套：

```text
XXXMetricKeys
XXXMetrics
XXXMetricCalculator
```

如果同时存在 run 和 batch 指标，可以在同一个文件里拆两组：

```text
XXXRunMetricKeys
XXXRunMetrics

XXXBatchMetricKeys
XXXBatchMetrics

XXXMetricCalculator
```

---

## 16. 依赖方向

推荐依赖方向：

```text
MetricCalculator
    依赖原始数据、Collector、RunResult。

Metrics
    只依赖 MetricBag 和 MetricKeys。

MetricKeys
    只依赖 MetricKey<T>。

Output 层
    依赖 IMetricKey 和 MetricBag。
```

不推荐：

```text
MetricCalculator 依赖 CsvWriter。
MetricCalculator 读取 CLI 配置。
Metrics 结构体决定 CSV 列顺序。
Output 层反向调用 Calculator。
所有 Calculator 依赖一个巨大的 GlobalMetricKeys。
```

---

## 17. 推荐调用流程

以一次 Level 统计为例：

```csharp
var bag = new MetricBag();

var levelMetrics = LevelResultMetricCalculator.Calculate(
    successCount,
    failCount,
    totalScore);

levelMetrics.WriteTo(bag);

foreach (var key in outputColumns)
{
    bag.TryFormat(key, out var value);
    csvWriter.WriteField(value);
}
```

完整流程：

```text
原始运行数据
    ↓
MetricCalculator.Calculate(...)
    ↓
Metrics 结果结构体
    ↓
Metrics.WriteTo(MetricBag)
    ↓
Output 层按 outputColumns 读取
    ↓
CSV
```

---

## 18. 设计边界

### MetricKeys 不做计算

错误：

```csharp
public static class LevelResultMetricKeys
{
    public static double CalculateFailRate(...)
    {
        ...
    }
}
```

MetricKeys 只声明 key。

计算逻辑放到 Calculator。

---

### Metrics 不决定输出顺序

错误：

```csharp
public string[] ToCsvValues()
{
    ...
}
```

主路径中不推荐让 Metrics 结构体生成 CSV 值。

输出顺序由 Output 层控制。

---

### Calculator 不写 CSV

错误：

```csharp
public static void CalculateAndWriteCsv(...)
{
    ...
}
```

Calculator 只计算指标，不负责最终输出。

---

### Output 不计算指标

错误：

```csharp
if (key.Name == "level.fail_rate")
{
    var failRate = ...
}
```

Output 层只格式化和写出，不重新计算指标。

---

## 19. 最终设计原则

可以总结成几句话：

```text
MetricKey<T> 绑定指标名和指标类型。
MetricBag 按类型分仓存储指标值。
每组指标一个文件。
每个指标文件包含 MetricKeys、Metrics、MetricCalculator 三件套。
谁计算，谁声明自己能产出的指标 Key。
Metrics 只负责承载结果和 WriteTo(MetricBag)。
Output 层负责列选择、列顺序和格式化。
不要建立巨大的全局指标名总表。
不要把 Metrics 结构体变成 CSV 输出器。
不要把 Calculator 变成 Output 层。
```

一句话总结：

```text
指标三件套解决的是“指标归属和职责边界”的问题；
MetricKey<T> 解决的是“指标名和指标类型绑定”的问题；
MetricBag 解决的是“指标统一存储和输出读取”的问题。
```

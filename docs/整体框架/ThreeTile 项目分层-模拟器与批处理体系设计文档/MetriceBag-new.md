# MetricBag 与 MetricKey 设计文档

## 1. 背景

在模拟、求解、批量分析和指标导出过程中，系统需要收集大量指标。

这些指标有几个特点：

```text
数量多。
类型杂。
来源分散。
最终需要统一读取、聚合和导出 CSV。
```

例如：

```text
success_count
failure_count
fail_rate
is_success
level_id
candidate_count_list
selected_score_list
```

如果每个模块都自己维护字段，后续会出现几个问题：

```text
指标分散，难以统一输出。
新增指标需要修改大量结构。
聚合逻辑和输出逻辑容易重复。
不同指标类型容易混乱。
```

因此需要一个统一的指标容器：

```text
MetricBag
```

它的目标不是成为一个万能对象字典，而是成为一个类型边界明确、读取稳定、输出方便的指标容器。

---

## 2. 设计目标

MetricBag 的第一版目标如下：

```text
1. 支持常用指标类型。
2. 保持指标名和指标类型绑定。
3. 避免 Dictionary<string, object> 带来的装箱和拆箱。
4. 支持标量指标和序列指标。
5. 支持重复运行时复用 List 容量。
6. 方便后续 CSV 输出和指标聚合。
```

不追求：

```text
1. 支持任意类型。
2. 支持复杂嵌套结构。
3. 支持动态 schema 推断。
4. 支持运行时类型转换。
```

第一版保持简单、明确、可控。

---

## 3. 核心设计

MetricBag 分成三个部分：

```text
MetricKey<TValue>
    强类型指标 Key，负责绑定指标名和指标值类型。

MetricBag
    指标容器，负责按类型存储和读取指标。

MetricKeys
    集中定义系统内使用的指标 Key。
```

整体关系：

```text
MetricKeys.SuccessCount
    类型是 MetricKey<int>
    名称是 "success_count"

MetricBag.Set(MetricKeys.SuccessCount, 10)
    写入 int 指标仓库

MetricBag.TryRead(MetricKeys.SuccessCount, out int value)
    从 int 指标仓库读取
```

这样可以做到：

```text
指标名有统一定义。
指标类型由 Key 决定。
写错类型时直接编译失败。
内部按类型分仓，避免值类型装箱。
```

---

## 4. 为什么需要 MetricKey<TValue>

如果只用字符串作为 key：

```csharp
bag.SetInt("success_count", 10);
bag.SetDouble("fail_rate", 0.25);
```

调用者需要自己记住：

```text
success_count 是 int。
fail_rate 是 double。
is_success 是 bool。
level_id 是 string。
```

这样容易出现错误：

```csharp
bag.SetDouble("success_count", 10.5);
bag.TryRead("fail_rate", out int value);
```

这些错误只能在运行时暴露，或者更糟糕的是悄悄输出错误数据。

因此引入：

```csharp
MetricKey<TValue>
```

它同时表达两件事：

```text
1. 指标名称是什么。
2. 指标值类型是什么。
```

例如：

```csharp
public static readonly MetricKey<int> SuccessCount =
    new("success_count");

public static readonly MetricKey<double> FailRate =
    new("fail_rate");
```

这样：

```csharp
bag.Set(MetricKeys.SuccessCount, 10);
```

是正确的。

而：

```csharp
bag.Set(MetricKeys.SuccessCount, 0.25);
```

会直接编译失败。

因为 `MetricKeys.SuccessCount` 的类型是：

```csharp
MetricKey<int>
```

只能写入 `int`。

---

## 5. 为什么不用 Dictionary<string, object>

一种简单写法是：

```csharp
private readonly Dictionary<string, object> _values = new();
```

然后：

```csharp
_values["move_count"] = 12;
_values["fail_rate"] = 0.25;
_values["is_success"] = true;
```

这个方案虽然灵活，但是有几个问题：

```text
int、double、bool 写入 object 时会装箱。
读取时需要拆箱。
类型错误只能在运行时发现。
CSV 输出时还要重新判断真实类型。
容器会变成弱类型杂物箱。
```

例如：

```csharp
_values["move_count"] = 12;      // int 装箱
_values["fail_rate"] = 0.25;     // double 装箱
_values["is_success"] = true;    // bool 装箱
```

读取时：

```csharp
var moveCount = (int)_values["move_count"];
```

这里存在拆箱和运行时类型错误风险。

MetricBag 选择按类型分仓存储：

```csharp
private readonly Dictionary<string, int> _ints = new();
private readonly Dictionary<string, double> _doubles = new();
private readonly Dictionary<string, bool> _bools = new();
private readonly Dictionary<string, string> _strings = new();

private readonly Dictionary<string, List<int>> _intLists = new();
private readonly Dictionary<string, List<double>> _doubleLists = new();
```

这样可以做到：

```text
int 存 int 字典。
double 存 double 字典。
bool 存 bool 字典。
string 存 string 字典。
List<int> 存 List<int> 字典。
List<double> 存 List<double> 字典。
```

避免把值类型塞进 `object`。

---

## 6. MetricBag 支持类型

MetricBag 第一版支持六类指标值：

```csharp
int
double
bool
string
List<int>
List<double>
```

内部结构：

```csharp
private readonly Dictionary<string, int> _ints = new();
private readonly Dictionary<string, double> _doubles = new();
private readonly Dictionary<string, bool> _bools = new();
private readonly Dictionary<string, string> _strings = new();

private readonly Dictionary<string, List<int>> _intLists = new();
private readonly Dictionary<string, List<double>> _doubleLists = new();
```

---

## 6.1 int 指标

`int` 适合表示计数类、步数类、次数类指标。

例如：

```text
success_count
failure_count
move_count
candidate_count
no_candidate_count
max_candidate_count
clear_count
match_count
run_count
```

典型含义：

```text
success_count
    成功次数

failure_count
    失败次数

move_count
    当前局点击步数

candidate_count
    当前步候选数量

no_candidate_count
    无候选次数

max_candidate_count
    最大候选数量
```

---

## 6.2 double 指标

`double` 适合表示比例、均值、分数、耗时等连续数值。

例如：

```text
fail_rate
success_rate
avg_score
avg_move_count
avg_candidate_count
avg_selected_score
elapsed_ms
avg_elapsed_ms
```

典型含义：

```text
fail_rate
    失败率

success_rate
    成功率

avg_score
    平均分数

avg_move_count
    平均移动步数

avg_candidate_count
    平均候选数量

avg_selected_score
    被选中候选的平均分数

elapsed_ms
    耗时，单位毫秒
```

---

## 6.3 bool 指标

`bool` 适合表示一次运行或一个关卡是否具备某种状态。

例如：

```text
is_success
has_no_candidate
has_dead_end
is_timeout
is_valid_level
has_parse_error
```

典型含义：

```text
is_success
    本次运行是否成功

has_no_candidate
    是否出现无候选

has_dead_end
    是否进入死局

is_timeout
    是否超时

is_valid_level
    关卡是否合法

has_parse_error
    是否出现解析错误
```

---

## 6.4 string 指标

`string` 适合表示标识、枚举名称、策略名称、错误原因、调试标签。

例如：

```text
level_id
finder_type
scorer_type
strategy_type
fail_reason
profile_name
debug_label
rule_name
input_file
```

典型含义：

```text
level_id
    关卡 ID

finder_type
    Finder 类型

scorer_type
    Scorer 类型

strategy_type
    策略类型

fail_reason
    失败原因

profile_name
    当前运行配置名称

debug_label
    调试标签
```

如果某个字段本质是固定枚举，第一版仍然可以用 `string` 输出，后续再考虑 enum 映射。

---

## 6.5 List<int> 指标

`List<int>` 适合表示一组离散计数序列。

例如：

```text
candidate_count_list
pruned_candidate_count_list
move_count_per_run
slot_usage_after_move_list
clear_step_list
```

典型含义：

```text
candidate_count_list
    每一步候选数量序列

pruned_candidate_count_list
    每一步剪枝后的候选数量序列

move_count_per_run
    每次模拟的移动步数

slot_usage_after_move_list
    每次点击后的槽位占用数量

clear_step_list
    发生清除的步数序列
```

---

## 6.6 List<double> 指标

`List<double>` 适合表示一组连续数值序列。

例如：

```text
candidate_score_list
selected_score_list
elapsed_ms_list
softmax_probability_list
score_delta_list
```

典型含义：

```text
candidate_score_list
    候选分数序列

selected_score_list
    被选中候选的分数序列

elapsed_ms_list
    每次运行耗时序列

softmax_probability_list
    候选被选中的概率序列

score_delta_list
    分数变化序列
```

---

## 7. MetricKey<TValue> 完整代码

```csharp
using System;

namespace ThreeTile.Core.Simulation.Metrics;

/// <summary>
/// 表示一个强类型指标 Key。
///
/// MetricKey 同时表达两件事：
/// 1. 指标名称
/// 2. 指标值类型
///
/// 例如：
/// MetricKey&lt;int&gt; SuccessCount = new("success_count");
/// MetricKey&lt;double&gt; FailRate = new("fail_rate");
/// </summary>
/// <typeparam name="TValue">指标值类型。</typeparam>
public readonly struct MetricKey<TValue>
{
    public MetricKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Metric key name cannot be empty.", nameof(name));

        Name = name;
    }

    /// <summary>
    /// 指标名称。
    /// 通常对应 CSV 列名。
    /// </summary>
    public string Name { get; }

    public override string ToString()
    {
        return Name;
    }
}
```

---

## 8. MetricBag 完整代码

```csharp
using System;
using System.Collections.Generic;

namespace ThreeTile.Core.Simulation.Metrics;

/// <summary>
/// 指标容器。
///
/// 第一版只支持六类指标：
/// int
/// double
/// bool
/// string
/// List&lt;int&gt;
/// List&lt;double&gt;
///
/// 内部不使用 Dictionary&lt;string, object&gt;，而是按类型分仓存储，
/// 避免 int / double / bool 等值类型装箱。
/// </summary>
public sealed class MetricBag
{
    private readonly Dictionary<string, int> _ints = new();
    private readonly Dictionary<string, double> _doubles = new();
    private readonly Dictionary<string, bool> _bools = new();
    private readonly Dictionary<string, string> _strings = new();

    private readonly Dictionary<string, List<int>> _intLists = new();
    private readonly Dictionary<string, List<double>> _doubleLists = new();

    // ==============================
    // int
    // ==============================

    public void Set(MetricKey<int> key, int value)
    {
        _ints[key.Name] = value;
    }

    public void Add(MetricKey<int> key, int value)
    {
        if (_ints.TryGetValue(key.Name, out int oldValue))
            _ints[key.Name] = oldValue + value;
        else
            _ints[key.Name] = value;
    }

    public bool TryRead(MetricKey<int> key, out int value)
    {
        return _ints.TryGetValue(key.Name, out value);
    }

    public int GetOrDefault(MetricKey<int> key, int defaultValue = default)
    {
        return _ints.TryGetValue(key.Name, out int value)
            ? value
            : defaultValue;
    }

    // ==============================
    // double
    // ==============================

    public void Set(MetricKey<double> key, double value)
    {
        _doubles[key.Name] = value;
    }

    public void Add(MetricKey<double> key, double value)
    {
        if (_doubles.TryGetValue(key.Name, out double oldValue))
            _doubles[key.Name] = oldValue + value;
        else
            _doubles[key.Name] = value;
    }

    public bool TryRead(MetricKey<double> key, out double value)
    {
        return _doubles.TryGetValue(key.Name, out value);
    }

    public double GetOrDefault(MetricKey<double> key, double defaultValue = default)
    {
        return _doubles.TryGetValue(key.Name, out double value)
            ? value
            : defaultValue;
    }

    // ==============================
    // bool
    // ==============================

    public void Set(MetricKey<bool> key, bool value)
    {
        _bools[key.Name] = value;
    }

    public bool TryRead(MetricKey<bool> key, out bool value)
    {
        return _bools.TryGetValue(key.Name, out value);
    }

    public bool GetOrDefault(MetricKey<bool> key, bool defaultValue = default)
    {
        return _bools.TryGetValue(key.Name, out bool value)
            ? value
            : defaultValue;
    }

    // ==============================
    // string
    // ==============================

    public void Set(MetricKey<string> key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _strings[key.Name] = value;
    }

    public bool TryRead(MetricKey<string> key, out string value)
    {
        if (_strings.TryGetValue(key.Name, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string GetOrDefault(MetricKey<string> key, string defaultValue = "")
    {
        return _strings.TryGetValue(key.Name, out string? value)
            ? value
            : defaultValue;
    }

    // ==============================
    // List<int>
    // ==============================

    public void Set(MetricKey<List<int>> key, List<int> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _intLists[key.Name] = value;
    }

    public void Append(MetricKey<List<int>> key, int value)
    {
        if (!_intLists.TryGetValue(key.Name, out var list))
        {
            list = new List<int>();
            _intLists[key.Name] = list;
        }

        list.Add(value);
    }

    public bool TryRead(
        MetricKey<List<int>> key,
        out IReadOnlyList<int> value)
    {
        if (_intLists.TryGetValue(key.Name, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<int>();
        return false;
    }

    public IReadOnlyList<int> GetOrEmpty(MetricKey<List<int>> key)
    {
        return _intLists.TryGetValue(key.Name, out var list)
            ? list
            : Array.Empty<int>();
    }

    // ==============================
    // List<double>
    // ==============================

    public void Set(MetricKey<List<double>> key, List<double> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _doubleLists[key.Name] = value;
    }

    public void Append(MetricKey<List<double>> key, double value)
    {
        if (!_doubleLists.TryGetValue(key.Name, out var list))
        {
            list = new List<double>();
            _doubleLists[key.Name] = list;
        }

        list.Add(value);
    }

    public bool TryRead(
        MetricKey<List<double>> key,
        out IReadOnlyList<double> value)
    {
        if (_doubleLists.TryGetValue(key.Name, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<double>();
        return false;
    }

    public IReadOnlyList<double> GetOrEmpty(MetricKey<List<double>> key)
    {
        return _doubleLists.TryGetValue(key.Name, out var list)
            ? list
            : Array.Empty<double>();
    }

    // ==============================
    // 清理
    // ==============================

    /// <summary>
    /// 彻底清空指标结构。
    ///
    /// 包括所有标量指标，以及 List 指标本身。
    /// 适合不同任务之间切换，或者指标集合完全不同的情况。
    /// </summary>
    public void Clear()
    {
        _ints.Clear();
        _doubles.Clear();
        _bools.Clear();
        _strings.Clear();

        _intLists.Clear();
        _doubleLists.Clear();
    }

    /// <summary>
    /// 清空当前指标值，但保留 List 容器和 List 内部容量。
    ///
    /// 标量指标会被清空。
    /// List 指标不会从字典中移除，只会 Clear 每个 List。
    ///
    /// 适合重复跑同一批指标，想复用 List 容量，减少 GC 的场景。
    /// </summary>
    public void ResetValues()
    {
        _ints.Clear();
        _doubles.Clear();
        _bools.Clear();
        _strings.Clear();

        foreach (var pair in _intLists)
            pair.Value.Clear();

        foreach (var pair in _doubleLists)
            pair.Value.Clear();
    }
}
```

---

## 9. MetricKeys 示例

建议单独建立一个集中定义指标 Key 的类。

```csharp
using System.Collections.Generic;

namespace ThreeTile.Core.Simulation.Metrics;

public static class MetricKeys
{
    // ==============================
    // int
    // ==============================

    public static readonly MetricKey<int> SuccessCount =
        new("success_count");

    public static readonly MetricKey<int> FailureCount =
        new("failure_count");

    public static readonly MetricKey<int> MoveCount =
        new("move_count");

    public static readonly MetricKey<int> CandidateCount =
        new("candidate_count");

    public static readonly MetricKey<int> NoCandidateCount =
        new("no_candidate_count");

    public static readonly MetricKey<int> MaxCandidateCount =
        new("max_candidate_count");

    // ==============================
    // double
    // ==============================

    public static readonly MetricKey<double> FailRate =
        new("fail_rate");

    public static readonly MetricKey<double> SuccessRate =
        new("success_rate");

    public static readonly MetricKey<double> AvgScore =
        new("avg_score");

    public static readonly MetricKey<double> AvgMoveCount =
        new("avg_move_count");

    public static readonly MetricKey<double> AvgCandidateCount =
        new("avg_candidate_count");

    public static readonly MetricKey<double> AvgSelectedScore =
        new("avg_selected_score");

    public static readonly MetricKey<double> ElapsedMs =
        new("elapsed_ms");

    // ==============================
    // bool
    // ==============================

    public static readonly MetricKey<bool> IsSuccess =
        new("is_success");

    public static readonly MetricKey<bool> HasNoCandidate =
        new("has_no_candidate");

    public static readonly MetricKey<bool> HasDeadEnd =
        new("has_dead_end");

    public static readonly MetricKey<bool> IsTimeout =
        new("is_timeout");

    public static readonly MetricKey<bool> IsValidLevel =
        new("is_valid_level");

    public static readonly MetricKey<bool> HasParseError =
        new("has_parse_error");

    // ==============================
    // string
    // ==============================

    public static readonly MetricKey<string> LevelId =
        new("level_id");

    public static readonly MetricKey<string> FinderType =
        new("finder_type");

    public static readonly MetricKey<string> ScorerType =
        new("scorer_type");

    public static readonly MetricKey<string> StrategyType =
        new("strategy_type");

    public static readonly MetricKey<string> FailReason =
        new("fail_reason");

    public static readonly MetricKey<string> ProfileName =
        new("profile_name");

    public static readonly MetricKey<string> DebugLabel =
        new("debug_label");

    public static readonly MetricKey<string> RuleName =
        new("rule_name");

    public static readonly MetricKey<string> InputFile =
        new("input_file");

    // ==============================
    // List<int>
    // ==============================

    public static readonly MetricKey<List<int>> CandidateCountList =
        new("candidate_count_list");

    public static readonly MetricKey<List<int>> PrunedCandidateCountList =
        new("pruned_candidate_count_list");

    public static readonly MetricKey<List<int>> MoveCountPerRun =
        new("move_count_per_run");

    public static readonly MetricKey<List<int>> SlotUsageAfterMoveList =
        new("slot_usage_after_move_list");

    public static readonly MetricKey<List<int>> ClearStepList =
        new("clear_step_list");

    // ==============================
    // List<double>
    // ==============================

    public static readonly MetricKey<List<double>> CandidateScoreList =
        new("candidate_score_list");

    public static readonly MetricKey<List<double>> SelectedScoreList =
        new("selected_score_list");

    public static readonly MetricKey<List<double>> ElapsedMsList =
        new("elapsed_ms_list");

    public static readonly MetricKey<List<double>> SoftmaxProbabilityList =
        new("softmax_probability_list");

    public static readonly MetricKey<List<double>> ScoreDeltaList =
        new("score_delta_list");
}
```

---

## 10. 使用示例

```csharp
var bag = new MetricBag();

bag.Set(MetricKeys.LevelId, "tile2-4-rong_part0197");
bag.Set(MetricKeys.SuccessCount, 10);
bag.Set(MetricKeys.FailureCount, 3);
bag.Set(MetricKeys.FailRate, 0.23);
bag.Set(MetricKeys.IsSuccess, true);

bag.Add(MetricKeys.SuccessCount, 1);

bag.Append(MetricKeys.CandidateCountList, 5);
bag.Append(MetricKeys.CandidateCountList, 8);
bag.Append(MetricKeys.CandidateCountList, 3);

bag.Append(MetricKeys.SelectedScoreList, 0.91);
bag.Append(MetricKeys.SelectedScoreList, 0.74);

if (bag.TryRead(MetricKeys.FailRate, out var failRate))
{
    Console.WriteLine(failRate);
}

var candidateCounts = bag.GetOrEmpty(MetricKeys.CandidateCountList);
```

错误示例：

```csharp
bag.Set(MetricKeys.SuccessCount, 0.25);
```

这会编译失败，因为 `SuccessCount` 是：

```csharp
MetricKey<int>
```

只能写入 `int`。

---

## 11. Clear 与 ResetValues

MetricBag 提供两个清空方法：

```text
Clear()
    彻底清空指标结构。

ResetValues()
    清空当前值，但保留 List 容器和容量。
```

---

## 11.1 Clear

```csharp
public void Clear()
{
    _ints.Clear();
    _doubles.Clear();
    _bools.Clear();
    _strings.Clear();

    _intLists.Clear();
    _doubleLists.Clear();
}
```

`Clear()` 表示彻底清空所有字典。

包括：

```text
int 指标。
double 指标。
bool 指标。
string 指标。
List<int> 指标。
List<double> 指标。
```

其中 List 指标的 key 也会被移除，List 容器和内部容量不再保留。

适合：

```text
不同任务之间切换。
指标集合完全不同。
不考虑复用 List 容量。
```

---

## 11.2 ResetValues

```csharp
public void ResetValues()
{
    _ints.Clear();
    _doubles.Clear();
    _bools.Clear();
    _strings.Clear();

    foreach (var pair in _intLists)
        pair.Value.Clear();

    foreach (var pair in _doubleLists)
        pair.Value.Clear();
}
```

`ResetValues()` 表示清空当前指标值，但保留 List 指标结构。

具体来说：

```text
int / double / bool / string 标量指标会被清空。
List<int> / List<double> 的 key 会保留。
List 容器会保留。
List 内部容量会保留。
List 内容会被清空。
```

适合：

```text
重复跑同一种指标。
每局都要收集 candidate_count_list / selected_score_list。
希望减少 List 反复分配。
```

注意：

```text
ResetValues() 不会保留标量指标值。
ResetValues() 只复用 List 容器和 List 容量。
```

---

## 12. 线程安全

MetricBag 第一版不是线程安全的。

不建议多个线程同时写同一个 MetricBag。

推荐使用方式：

```text
每个 run 一个 MetricBag。
每个 worker 一个 MetricBag。
批量聚合时，由上层合并结果。
```

如果后续需要并发写入，应由上层做隔离或加锁，不建议在 MetricBag 内部直接引入锁。

原因是：

```text
MetricBag 更偏热路径数据容器。
内部加锁会增加所有写入成本。
多线程聚合的边界应该放在更高层。
```

---

## 13. 命名约定

MetricKey 的名称建议使用 snake_case。

例如：

```text
success_count
failure_count
fail_rate
avg_move_count
candidate_count_list
selected_score_list
```

原因：

```text
更适合作为 CSV 列名。
和 TOML / 配置字段风格一致。
方便跨语言处理。
```

不建议：

```text
SuccessCount
successCount
SUCCESS_COUNT
```

---

## 14. 使用边界

MetricBag 适合保存：

```text
一次模拟的指标。
一批模拟的聚合指标。
关卡分析结果。
策略运行结果。
需要导出 CSV 的结构化指标。
```

不适合保存：

```text
完整 LevelCore。
完整 Tile 对象。
复杂图结构。
大型中间状态。
高频搜索过程中的临时状态。
```

如果某个数据只在算法内部使用，不需要输出或聚合，不应该放入 MetricBag。

---

## 15. 设计总结

MetricBag 的核心设计可以总结为：

```text
MetricKey<TValue>
    负责把指标名和指标类型绑定起来。

MetricBag
    负责按类型分仓存储指标值。

MetricKeys
    负责集中管理所有指标名。
```

最终效果：

```text
类型安全。
无 object 杂物箱。
减少装箱/拆箱。
方便 CSV 输出。
方便指标聚合。
适合 MVP 阶段快速扩展指标。
```

一句话总结：

```text
MetricBag 解决的是“指标多、类型杂、输出要统一读取”的问题；
MetricKey<TValue> 解决的是“指标名和指标类型必须绑定”的问题。
```

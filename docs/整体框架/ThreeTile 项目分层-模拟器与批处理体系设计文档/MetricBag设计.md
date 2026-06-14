
ThreeTile 指标系统设计文档
1. 核心结论
指标系统分成四个角色：
MetricBag
    通用指标结果容器

MetricCalculator
    纯指标计算器

MetricObserver / MetricCollector
    模拟器事件适配器

MetricResult
    强类型指标结果结构体

最重要的边界是：
指标计算器保持纯。
指标计算器不实现 ISimulationObserver。
指标计算器不实现 IPolicyObserver。
指标计算器不直接依赖 SimulationRunner。
指标计算器只接收必要参数，返回强类型结果。

模拟器事件和指标计算之间，需要一个适配层：
SimulationRunner / Policy
    发事件

MetricObserver
    监听事件
    累计过程数据
    调用纯 MetricCalculator

MetricCalculator
    纯计算

MetricResult
    承载结果

MetricBag
    存储最终指标值

一句话：
Calculator 纯计算；
Observer 做事件适配；
MetricResult 承载强类型结果；
MetricBag 聚合最终值。


2. 分层边界
2.1 Core
Core 可以提供基础容器能力：
MetricBag
MetricBag 基础读写接口

Core 不关心具体指标含义。
Core 不应该知道：
PiKa 指标
Tokiki 指标
TileEval 指标
CSV 输出
TOML 配置
outputs.order
具体指标启用列表


2.2 Services
Services 放具体指标系统。
推荐结构：
ThreeTile.Services
└── TileEval
    ├── Metrics
    │   ├── LevelResultMetrics.cs
    │   ├── PolicyCandidateMetrics.cs
    │   ├── ScoreDistributionMetrics.cs
    │   └── ...
    │
    ├── SimulationObservers
    │   ├── TileEvalRunMetricObserver.cs
    │   ├── TileEvalPolicyMetricObserver.cs
    │   ├── TileEvalScoreMetricObserver.cs
    │   └── TileEvalMetricObserverSet.cs
    │
    └── TileEvalService.cs

其中：
Metrics/*.cs
    放纯指标三件套

SimulationObservers/*.cs
    放模拟器事件适配器


2.3 CLI
CLI 负责：
读取 metrics.toml
决定启用哪些指标
决定输出顺序
写 CSV
写 Console
处理文件路径

CLI 不负责指标计算。

3. MetricBag 定位
MetricBag 是通用指标结果容器。
它负责：
按指标名存储不同类型的指标值
支持标量值
支持序列值
支持设置、累加、追加
支持按类型读取
支持清空和复用

它不负责：
指标怎么算
指标属于哪个 group
指标输出顺序
CSV 怎么写
TOML 怎么解析
哪些指标启用

一句话：
MetricBag 只存结果，不算指标，不管输出。


4. MetricBag 支持类型
MetricBag 第一版支持六类：
int
double
bool
string
List<int>
List<double>

内部结构：
private readonly Dictionary<string, int> _ints = new();
private readonly Dictionary<string, double> _doubles = new();
private readonly Dictionary<string, bool> _bools = new();
private readonly Dictionary<string, string> _strings = new();

private readonly Dictionary<string, List<int>> _intLists = new();
private readonly Dictionary<string, List<double>> _doubleLists = new();

4.1 int 适合
success_count
failure_count
move_count
candidate_count
no_candidate_count
max_candidate_count

4.2 double 适合
fail_rate
avg_score
avg_move_count
avg_candidate_count
avg_selected_score

4.3 bool 适合
is_success
has_no_candidate
has_dead_end
is_timeout

4.4 string 适合
level_id
finder_type
scorer_type
strategy_type
fail_reason
profile_name
debug_label

4.5 List 适合
candidate_count_list
pruned_candidate_count_list
move_count_per_run

4.6 List 适合
candidate_score_list
selected_score_list
elapsed_ms_list


5. 为什么不用 Dictionary<string, object?>
不建议使用：
Dictionary<string, object?>

原因：
类型不安全
读取时需要强转
容易写错类型
可能产生装箱
标量和序列混在一起
后续统计不清晰

更好的方式是类型分离：
Dictionary<string, int>
Dictionary<string, double>
Dictionary<string, bool>
Dictionary<string, string>
Dictionary<string, List<int>>
Dictionary<string, List<double>>

这样读写语义更清楚。

6. MetricBag 写入语义
写入分三类：
Set
    设置最终值

Add
    累加值

Append
    追加序列值

示例：
success_count
    AddInt，每成功一次 +1

fail_rate
    SetDouble，最终计算后写入

candidate_count_list
    AppendInt，每一步记录候选数

candidate_score_list
    AppendDouble，每个候选记录评分

finder_type
    SetString，记录当前 finder 类型

不要只用一个 Write<T>，因为它语义不清楚。
Write("move_count", 1);

这句话不明确：
是覆盖 move_count = 1？
还是 move_count += 1？
还是追加到列表？

因此主入口使用明确方法。

7. MetricBag 完整接口
using System;
using System.Collections.Generic;

namespace ThreeTile.Core.Simulation.Metrics;

public sealed class MetricBag
{
    private readonly Dictionary<string, int> _ints = new();
    private readonly Dictionary<string, double> _doubles = new();
    private readonly Dictionary<string, bool> _bools = new();
    private readonly Dictionary<string, string> _strings = new();

    private readonly Dictionary<string, List<int>> _intLists = new();
    private readonly Dictionary<string, List<double>> _doubleLists = new();

    public void SetInt(string name, int value)
    {
        _ints[name] = value;
    }

    public void AddInt(string name, int value)
    {
        if (_ints.TryGetValue(name, out int oldValue))
            _ints[name] = oldValue + value;
        else
            _ints[name] = value;
    }

    public void AppendInt(string name, int value)
    {
        if (!_intLists.TryGetValue(name, out var list))
        {
            list = new List<int>();
            _intLists[name] = list;
        }

        list.Add(value);
    }

    public void SetDouble(string name, double value)
    {
        _doubles[name] = value;
    }

    public void AddDouble(string name, double value)
    {
        if (_doubles.TryGetValue(name, out double oldValue))
            _doubles[name] = oldValue + value;
        else
            _doubles[name] = value;
    }

    public void AppendDouble(string name, double value)
    {
        if (!_doubleLists.TryGetValue(name, out var list))
        {
            list = new List<double>();
            _doubleLists[name] = list;
        }

        list.Add(value);
    }

    public void SetBool(string name, bool value)
    {
        _bools[name] = value;
    }

    public void SetString(string name, string value)
    {
        _strings[name] = value;
    }

    public bool TryRead(string name, out int value)
    {
        return _ints.TryGetValue(name, out value);
    }

    public bool TryRead(string name, out double value)
    {
        return _doubles.TryGetValue(name, out value);
    }

    public bool TryRead(string name, out bool value)
    {
        return _bools.TryGetValue(name, out value);
    }

    public bool TryRead(string name, out string value)
    {
        if (_strings.TryGetValue(name, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryReadIntList(
        string name,
        out IReadOnlyList<int> value)
    {
        if (_intLists.TryGetValue(name, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<int>();
        return false;
    }

    public bool TryReadDoubleList(
        string name,
        out IReadOnlyList<double> value)
    {
        if (_doubleLists.TryGetValue(name, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<double>();
        return false;
    }

    public void Clear()
    {
        _ints.Clear();
        _doubles.Clear();
        _bools.Clear();
        _strings.Clear();

        _intLists.Clear();
        _doubleLists.Clear();
    }

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


8. Clear 与 ResetValues
MetricBag 提供两个清空方法：
Clear()
    彻底清空指标结构

ResetValues()
    清空当前值，但保留 List 容器和容量

8.1 Clear
public void Clear()
{
    _ints.Clear();
    _doubles.Clear();
    _bools.Clear();
    _strings.Clear();

    _intLists.Clear();
    _doubleLists.Clear();
}

适合：
不同任务之间切换
指标集合完全不同
不考虑复用 List 容量


8.2 ResetValues
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

适合：
批处理中复用 MetricBag
多关卡循环计算
指标集合稳定
希望减少 List 反复 new

注意：
ResetValues 会保留 List 指标名和 List 容器。
Clear 会清掉整个 List 字典。


9. 指标文件三件套
每组指标一个文件。
每个指标文件内部包含三件套：
1. 指标名映射
2. 指标结果结构体
3. 指标计算器

例如：
LevelResultMetrics.cs
    ├── LevelResultMetricNames
    ├── LevelResultMetrics
    └── LevelResultMetricCalculator

原则：
谁计算，谁声明自己能产出哪些指标名。
不要建立一个巨大的全局指标名总表。


10. 指标名映射
示例：
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

指标名可以进一步区分：
单次指标名
批量指标名

例如：
public static class PolicyCandidateRunMetricNames
{
    public const string AvgCandidateCount =
        "run.policy.avgCandidateCount";

    public const string MaxCandidateCount =
        "run.policy.maxCandidateCount";

    public const string NoCandidateCount =
        "run.policy.noCandidateCount";

    public static readonly string[] All =
    {
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    };
}

public static class PolicyCandidateBatchMetricNames
{
    public const string AvgCandidateCount =
        "batch.policy.avgCandidateCount";

    public const string MaxCandidateCount =
        "batch.policy.maxCandidateCount";

    public const string NoCandidateCount =
        "batch.policy.noCandidateCount";

    public static readonly string[] All =
    {
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    };
}


11. 指标结果结构体
指标结果结构体负责承载一组指标的最终结果。
例如：
public readonly record struct LevelResultMetrics(
    int SuccessCount,
    int FailCount,
    double FailRate,
    double AvgScore)
{
    public void WriteTo(MetricBag bag)
    {
        bag.SetInt(
            LevelResultMetricNames.SuccessCount,
            SuccessCount);

        bag.SetInt(
            LevelResultMetricNames.FailCount,
            FailCount);

        bag.SetDouble(
            LevelResultMetricNames.FailRate,
            FailRate);

        bag.SetDouble(
            LevelResultMetricNames.AvgScore,
            AvgScore);
    }

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
}

注意：
ToCsvValues 只提供值。
最终 CSV 列顺序仍由 CLI / Output 层决定。


12. 指标计算器保持纯
指标计算器只接收必要依赖。
它不做：
不实现 ISimulationObserver
不实现 IPolicyObserver
不持有 SimulationContext
不持有 MetricBag
不写 CSV
不读 TOML
不关心输出顺序

它只做：
接收必要参数
计算强类型 MetricResult
返回结果

例如：
public static class LevelResultMetricCalculator
{
    public static LevelResultMetrics Calculate(
        int successCount,
        int failCount,
        double avgScore)
    {
        int total = successCount + failCount;

        double failRate = total == 0
            ? 0
            : (double)failCount / total;

        return new LevelResultMetrics(
            successCount,
            failCount,
            failRate,
            avgScore);
    }
}

这就是纯指标计算器。

13. 事件适配器：MetricObserver
指标计算器保持纯，所以需要一个事件适配器。
这个适配器可以叫：
MetricObserver
MetricCollector
SimulationMetricObserver
TileEvalPolicyMetricObserver

它负责：
实现 ISimulationObserver / IPolicyObserver
监听模拟事件
累计过程数据
在 RunCompleted / BatchCompleted 时调用纯 Calculator
把 MetricResult 写入 MetricBag

也就是说：
事件脏活、状态累计、生命周期处理
    放 Observer

纯指标计算
    放 Calculator


14. 示例：PolicyCandidateMetrics 三件套
14.1 指标名
public static class PolicyCandidateRunMetricNames
{
    public const string AvgCandidateCount =
        "run.policy.avgCandidateCount";

    public const string MaxCandidateCount =
        "run.policy.maxCandidateCount";

    public const string NoCandidateCount =
        "run.policy.noCandidateCount";

    public static readonly string[] All =
    {
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    };
}

public static class PolicyCandidateBatchMetricNames
{
    public const string AvgCandidateCount =
        "batch.policy.avgCandidateCount";

    public const string MaxCandidateCount =
        "batch.policy.maxCandidateCount";

    public const string NoCandidateCount =
        "batch.policy.noCandidateCount";

    public static readonly string[] All =
    {
        AvgCandidateCount,
        MaxCandidateCount,
        NoCandidateCount,
    };
}


14.2 单次指标结果
public readonly record struct PolicyCandidateRunMetrics(
    double AvgCandidateCount,
    int MaxCandidateCount,
    int NoCandidateCount)
{
    public void WriteTo(MetricBag bag)
    {
        bag.SetDouble(
            PolicyCandidateRunMetricNames.AvgCandidateCount,
            AvgCandidateCount);

        bag.SetInt(
            PolicyCandidateRunMetricNames.MaxCandidateCount,
            MaxCandidateCount);

        bag.SetInt(
            PolicyCandidateRunMetricNames.NoCandidateCount,
            NoCandidateCount);
    }
}


14.3 批量指标结果
public readonly record struct PolicyCandidateBatchMetrics(
    double AvgCandidateCount,
    int MaxCandidateCount,
    int NoCandidateCount)
{
    public void WriteTo(MetricBag bag)
    {
        bag.SetDouble(
            PolicyCandidateBatchMetricNames.AvgCandidateCount,
            AvgCandidateCount);

        bag.SetInt(
            PolicyCandidateBatchMetricNames.MaxCandidateCount,
            MaxCandidateCount);

        bag.SetInt(
            PolicyCandidateBatchMetricNames.NoCandidateCount,
            NoCandidateCount);
    }
}


14.4 纯指标计算器
public static class PolicyCandidateMetricCalculator
{
    public static PolicyCandidateRunMetrics CalculateRun(
        int candidateCountSum,
        int candidateEventCount,
        int maxCandidateCount,
        int noCandidateCount)
    {
        double avgCandidateCount = candidateEventCount == 0
            ? 0
            : (double)candidateCountSum / candidateEventCount;

        return new PolicyCandidateRunMetrics(
            avgCandidateCount,
            maxCandidateCount,
            noCandidateCount);
    }

    public static PolicyCandidateBatchMetrics CalculateBatch(
        long candidateCountSum,
        int candidateEventCount,
        int maxCandidateCount,
        int noCandidateCount)
    {
        double avgCandidateCount = candidateEventCount == 0
            ? 0
            : (double)candidateCountSum / candidateEventCount;

        return new PolicyCandidateBatchMetrics(
            avgCandidateCount,
            maxCandidateCount,
            noCandidateCount);
    }
}

这个 Calculator 是纯的。
它不知道：
SimulationRunner
PolicyObserver
MetricBag
CSV
TOML
CLI


15. 示例：TileEvalPolicyMetricObserver
这个类才实现 IPolicyObserver。
它内部持有过程状态，并调用纯计算器。
using ThreeTile.Core.Simulation.Metrics;

namespace ThreeTile.Services.TileEval.SimulationObservers;

public sealed class TileEvalPolicyMetricObserver : PolicyObserverBase
{
    private readonly MetricBag _runMetricBag;
    private readonly MetricBag _batchMetricBag;

    private int _runCandidateCountSum;
    private int _runCandidateEventCount;
    private int _runMaxCandidateCount;
    private int _runNoCandidateCount;

    private long _batchCandidateCountSum;
    private int _batchCandidateEventCount;
    private int _batchMaxCandidateCount;
    private int _batchNoCandidateCount;

    public TileEvalPolicyMetricObserver(
        MetricBag runMetricBag,
        MetricBag batchMetricBag)
    {
        _runMetricBag = runMetricBag;
        _batchMetricBag = batchMetricBag;
    }

    public override void OnCandidateFindingCompleted(
        SimulationContext context,
        int candidateCount)
    {
        _runCandidateCountSum += candidateCount;
        _runCandidateEventCount++;

        if (candidateCount > _runMaxCandidateCount)
            _runMaxCandidateCount = candidateCount;
    }

    public override void OnNoCandidate(
        SimulationContext context)
    {
        _runNoCandidateCount++;
    }

    public void ResetRun()
    {
        _runCandidateCountSum = 0;
        _runCandidateEventCount = 0;
        _runMaxCandidateCount = 0;
        _runNoCandidateCount = 0;

        _runMetricBag.ResetValues();
    }

    public void ResetBatch()
    {
        ResetRun();

        _batchCandidateCountSum = 0;
        _batchCandidateEventCount = 0;
        _batchMaxCandidateCount = 0;
        _batchNoCandidateCount = 0;

        _batchMetricBag.ResetValues();
    }

    public void CompleteRun()
    {
        var runMetrics =
            PolicyCandidateMetricCalculator.CalculateRun(
                _runCandidateCountSum,
                _runCandidateEventCount,
                _runMaxCandidateCount,
                _runNoCandidateCount);

        runMetrics.WriteTo(_runMetricBag);

        _batchCandidateCountSum += _runCandidateCountSum;
        _batchCandidateEventCount += _runCandidateEventCount;

        if (_runMaxCandidateCount > _batchMaxCandidateCount)
            _batchMaxCandidateCount = _runMaxCandidateCount;

        _batchNoCandidateCount += _runNoCandidateCount;
    }

    public void CompleteBatch()
    {
        var batchMetrics =
            PolicyCandidateMetricCalculator.CalculateBatch(
                _batchCandidateCountSum,
                _batchCandidateEventCount,
                _batchMaxCandidateCount,
                _batchNoCandidateCount);

        batchMetrics.WriteTo(_batchMetricBag);
    }
}

这个类的职责是：
接收策略事件
累计 run 级过程数据
在 run 完成时调用 CalculateRun
把 run 指标写入 runMetricBag
同时合并到 batch 累计
在 batch 完成时调用 CalculateBatch
把 batch 指标写入 batchMetricBag

它不是纯计算器。
它是事件适配器。

16. Run MetricBag 与 Batch MetricBag
建议区分两个容器：
RunMetricBag
    存单次模拟指标

BatchMetricBag
    存批量聚合指标

原因：
单次指标和批量指标生命周期不同
单次指标每次 run 都要 Reset
批量指标整批结束后才 Reset
输出目标可能不同

例如：
var runMetricBag = new MetricBag();
var batchMetricBag = new MetricBag();

var policyMetricObserver = new TileEvalPolicyMetricObserver(
    runMetricBag,
    batchMetricBag);


17. MetricObserverSet
如果有多个指标观察器，可以统一管理生命周期。
public interface IMetricObserverLifecycle
{
    void ResetRun();

    void ResetBatch();

    void CompleteRun();

    void CompleteBatch();
}

public sealed class TileEvalMetricObserverSet :
    SimulationObserverBase,
    IPolicyObserver,
    IMetricObserverLifecycle
{
    private readonly IMetricObserverLifecycle[] _lifecycles;
    private readonly ISimulationObserver[] _simulationObservers;
    private readonly IPolicyObserver[] _policyObservers;

    public TileEvalMetricObserverSet(
        IMetricObserverLifecycle[] lifecycles,
        ISimulationObserver[] simulationObservers,
        IPolicyObserver[] policyObservers)
    {
        _lifecycles = lifecycles;
        _simulationObservers = simulationObservers;
        _policyObservers = policyObservers;
    }

    public void ResetRun()
    {
        foreach (var lifecycle in _lifecycles)
            lifecycle.ResetRun();
    }

    public void ResetBatch()
    {
        foreach (var lifecycle in _lifecycles)
            lifecycle.ResetBatch();
    }

    public void CompleteRun()
    {
        foreach (var lifecycle in _lifecycles)
            lifecycle.CompleteRun();
    }

    public void CompleteBatch()
    {
        foreach (var lifecycle in _lifecycles)
            lifecycle.CompleteBatch();
    }

    public override void OnRunStarted(
        SimulationContext context)
    {
        foreach (var observer in _simulationObservers)
            observer.OnRunStarted(context);
    }

    public override void OnRunCompleted(
        SimulationContext context,
        SimulationRunResult result)
    {
        foreach (var observer in _simulationObservers)
            observer.OnRunCompleted(context, result);
    }

    public void OnCandidateFindingStarted(
        SimulationContext context)
    {
        foreach (var observer in _policyObservers)
            observer.OnCandidateFindingStarted(context);
    }

    public void OnCandidateFindingCompleted(
        SimulationContext context,
        int candidateCount)
    {
        foreach (var observer in _policyObservers)
            observer.OnCandidateFindingCompleted(context, candidateCount);
    }

    public void OnCandidatePruningCompleted(
        SimulationContext context,
        int beforeCount,
        int afterCount)
    {
        foreach (var observer in _policyObservers)
            observer.OnCandidatePruningCompleted(
                context,
                beforeCount,
                afterCount);
    }

    public void OnCandidateScored(
        SimulationContext context,
        int candidateIndex,
        double score)
    {
        foreach (var observer in _policyObservers)
            observer.OnCandidateScored(
                context,
                candidateIndex,
                score);
    }

    public void OnCandidateSelected(
        SimulationContext context,
        int selectedIndex,
        double selectedScore)
    {
        foreach (var observer in _policyObservers)
            observer.OnCandidateSelected(
                context,
                selectedIndex,
                selectedScore);
    }

    public void OnNoCandidate(
        SimulationContext context)
    {
        foreach (var observer in _policyObservers)
            observer.OnNoCandidate(context);
    }
}

这个类可以统一把多个 MetricObserver 组合起来。

18. 批量聚合流程
模拟器端可以这样使用：
BatchStarted
    metricObserverSet.ResetBatch()

RunStarted
    metricObserverSet.ResetRun()

Policy / Simulation 事件
    metricObserverSet 接收事件
    内部各 observer 累计过程数据

RunCompleted
    metricObserverSet.CompleteRun()
    runMetricBag 得到单次指标

BatchCompleted
    metricObserverSet.CompleteBatch()
    batchMetricBag 得到批量指标

伪代码：
metricObserverSet.ResetBatch();

for (int i = 0; i < simulationCount; i++)
{
    metricObserverSet.ResetRun();

    var runResult = RunOne(...);

    metricObserverSet.CompleteRun();

    WriteRunMetrics(runMetricBag);
}

metricObserverSet.CompleteBatch();

WriteBatchMetrics(batchMetricBag);

这个流程非常适合事件系统。

19. ToCsv 的边界
指标结果结构体可以提供：
WriteTo(MetricBag)
ToDict
ToCsvValues

但它不决定：
最终输出哪些列
列顺序是什么
写哪个 CSV 文件

这些属于 CLI / Output。
指标结果结构体只说明：
我有哪些值
这些值如何转成字符串

CLI / Output 负责：
根据 outputs.order 选择列
按顺序从 MetricBag 或 MetricResult 中取值
写 CSV


20. 为什么不用统一 MetricBinder
不采用这种模式：
模拟结束后
    MetricBinder 统一读取所有过程指标
    统一计算结果指标
    统一写回 MetricBag

原因：
所有指标逻辑容易集中到一个大类
不同指标之间边界变模糊
指标名、结果结构、计算逻辑被拆散
后期会形成新的巨型调度器

更好的方式：
每组指标自己管理三件套
事件适配器只负责把模拟事件转成该指标所需参数
纯计算器只做必要计算
结果结构体自己负责写入 MetricBag


21. 最终推荐结构
ThreeTile.Services
└── TileEval
    ├── Metrics
    │   ├── LevelResultMetrics.cs
    │   ├── PolicyCandidateMetrics.cs
    │   ├── ScoreDistributionMetrics.cs
    │   └── MoveCountMetrics.cs
    │
    ├── SimulationObservers
    │   ├── TileEvalRunMetricObserver.cs
    │   ├── TileEvalPolicyMetricObserver.cs
    │   ├── TileEvalScoreMetricObserver.cs
    │   └── TileEvalMetricObserverSet.cs
    │
    └── TileEvalService.cs

每个 Metrics/*.cs 文件内部：
MetricNames
MetricResult
MetricCalculator

每个 SimulationObservers/*.cs 文件内部：
事件监听
状态累计
生命周期控制
调用纯 MetricCalculator
写入 MetricBag


22. 最终职责总结
MetricBag
存储指标值
按类型读写
支持 ResetValues / Clear
不计算指标
不关心输出

MetricNames
声明本指标组能产出的指标名
每组指标自己声明
不做全局大表

MetricResult
强类型承载结果
可以 WriteTo(MetricBag)
可以 ToDict
可以 ToCsvValues

MetricCalculator
纯计算
只接收必要依赖
返回 MetricResult
不实现 Observer
不持有状态
不关心模拟器事件

MetricObserver / MetricCollector
实现 ISimulationObserver / IPolicyObserver
监听事件
累计过程状态
调用纯 MetricCalculator
把结果写入 MetricBag

Service
选择启用哪些 MetricObserver
组装 RunMetricBag / BatchMetricBag
管理单次与批量生命周期
返回 TileEvalResult

CLI
读取配置
决定输出列
决定输出顺序
写 CSV


23. 最终一句话
指标计算器要纯；
指标观察器要脏；
MetricBag 只存值；
Service 负责组装；
CLI 负责输出。

更完整地说：
每组指标自己声明名字、结果结构和纯计算器；
模拟器端针对需要的指标封装 MetricObserver；
MetricObserver 实现模拟器观察接口，收集事件数据；
RunCompleted / BatchCompleted 时调用纯计算器；
结果写入 MetricBag；
最终输出由 CLI 决定。

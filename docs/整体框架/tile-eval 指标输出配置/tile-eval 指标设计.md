tile-eval 指标配置与计算体系设计文档
1. 目标
tile-eval 在模拟运行过程中会产生大量指标。
这些指标分为两类：
SolveInfo：单次模拟 / 单局求解结果
LevelInfo：多次模拟 / 聚合统计结果

当模拟运行很多次时，例如 1000 次，需要支持：
1. 指定哪些指标需要计算
2. 指定哪些指标需要输出
3. 指定指标最终输出顺序
4. 指标按分组计算
5. 指标配置文件可以由命令自动导出

设计核心：
groups 管“怎么算”
outputs.order 管“怎么排”


2. 配置文件
指标输出配置独立于主运行配置。
推荐文件名：
tile-eval.metrics.toml

该文件只负责指标输出配置，不负责 tile-eval 的运行参数。

3. 核心结构
指标配置文件分为两大部分：
outputs
groups

职责：
outputs：
  控制哪些输出域启用。
  控制最终指标输出顺序。

groups：
  控制指标属于哪个计算分组。
  一个 group 对应一组计算逻辑。


4. 输出域
当前支持两个输出域：
solve
level

含义：
solve：
  单次模拟输出。
  对应 SolveInfo。

level：
  多次模拟聚合输出。
  对应 LevelInfo。

结构：
[outputs.solve]
enabled = false
order = []

[outputs.level]
enabled = true
order = []

说明：
enabled：
  是否启用该输出域。

order：
  该输出域最终输出指标顺序。


5. 指标命名规范
指标名使用：
<scope>.<metricName>

其中：
scope：
  solve 或 level

metricName：
  具体指标名

示例：
solve.failed
solve.totalTiles
solve.score

level.totalRuns
level.failRate
level.avgScore

这样可以避免单局指标和聚合指标重名。

6. groups 结构
groups 用于描述指标计算分组。
结构：
[groups.<scope>.<groupName>]
metrics = []

示例：
[groups.solve.base]
metrics = []

[groups.level.result]
metrics = []

含义：
scope：
  solve 或 level

groupName：
  指标分组名称

metrics：
  这个分组下包含哪些指标


7. groups 与 outputs 的关系
groups 管指标属于哪个计算组
outputs.order 管最终输出顺序

同一个指标通常应该：
1. 出现在某个 groups.<scope>.<groupName>.metrics 中
2. 出现在 outputs.<scope>.order 中

这样它既知道：
怎么算

也知道：
排在哪里


8. 配置文件结构模板
version = 1
command = "tile-eval"


# =========================================================
# 输出配置
# outputs 只关心：
# 1. 哪个输出域启用
# 2. 最终输出顺序
# =========================================================

[outputs.solve]
enabled = false

order = [
  # "solve.xxx",
  # "solve.xxx",
  # "solve.xxx",
]


[outputs.level]
enabled = true

order = [
  # "level.xxx",
  # "level.xxx",
  # "level.xxx",
]


# =========================================================
# SolveInfo 指标分组
# groups.solve.* 只关心：
# 指标属于哪个单局计算组
# =========================================================

[groups.solve.base]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.tags]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.result]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.smooth]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.swap]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.hardSwap]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.slotPressure]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.dead]
metrics = [
  # "solve.xxx",
]

[groups.solve.colorHold]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.curves]
metrics = [
  # "solve.xxx",
]


# =========================================================
# LevelInfo 指标分组
# groups.level.* 只关心：
# 指标属于哪个聚合计算组
# =========================================================

[groups.level.base]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.result]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.failPosition]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.relive]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.smooth]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.swap]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.hardSwap]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.slotPressure]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.dead]
metrics = [
  # "level.xxx",
]

[groups.level.colorHold]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.curves]
metrics = [
  # "level.xxx",
]


9. 指标配置读取流程
程序读取 tile-eval.metrics.toml 后，按以下流程处理：
1. 读取 outputs.solve / outputs.level
2. 判断哪些输出域 enabled = true
3. 读取对应 scope 下的 outputs.<scope>.order
4. 读取 groups.<scope>.*
5. 建立 metricName -> groupName 的映射
6. 根据 outputs.<scope>.order 得到最终输出列顺序
7. 根据 order 中的指标反推需要执行哪些 group
8. 执行对应 group 的计算逻辑
9. 将计算结果写入 MetricResultContext
10. 按 order 输出结果


10. 校验规则
建议读取后做基础校验：
1. command 必须等于 tile-eval
2. enabled = true 的输出域，order 可以为空，但应允许
3. outputs.<scope>.order 中的指标名必须属于对应 scope
4. groups.<scope>.*.metrics 中的指标名必须属于对应 scope
5. 同一个指标不应出现在同一个 scope 的多个 group 中
6. outputs.<scope>.order 中重复指标应报错
7. 未知指标名应报错

未知指标名的判断依据来自指标计算器注册信息。

11. 指标计算体系总览
指标计算体系由以下部分组成：
MetricContext
MetricBinding
MetricCalculator
MetricResultContext
MetricOutputWriter
MetricRegistry

职责：
MetricContext：
  指标计算所需依赖数据。

MetricBinding：
  指标计算器和 Context 之间的粘合层。
  负责 Bind(指标器, Context)。

MetricCalculator：
  指标计算器。
  尽量无内置状态。
  只做计算。

MetricResultContext：
  统一收集指标结果。

MetricOutputWriter：
  按 outputs.order 从 MetricResultContext 取值并输出。

MetricRegistry：
  收集所有指标计算器信息。
  为配置校验和配置导出提供依据。


12. 指标计算核心流程
整体流程：
Simulator
  ↓
生成 SolveInfo / LevelInfo / Trace 等
  ↓
构造 SolveMetricContext / LevelMetricContext
  ↓
读取 tile-eval.metrics.toml
  ↓
根据 outputs.order 得到要输出哪些指标
  ↓
根据 groups 得到需要执行哪些计算组
  ↓
MetricBinding 调用对应指标计算器
  ↓
指标计算器返回强类型指标结构体
  ↓
指标结构体 ToDict()
  ↓
MetricResultContext 收集所有结果
  ↓
MetricOutputWriter 按 outputs.order 输出


13. MetricContext
Context 包含指标计算需要的依赖数据。
建议区分：
SolveMetricContext
LevelMetricContext

示例：
public sealed class SolveMetricContext
{
    public required SolveInfo SolveInfo { get; init; }

    // 如果某些指标不能只从 SolveInfo 得出，
    // 可以放 solver trace / raw steps / slot states 等。
}

public sealed class LevelMetricContext
{
    public required LevelInfo LevelInfo { get; init; }

    public required IReadOnlyList<SolveInfo> SolveInfos { get; init; }

    // 如果聚合指标需要额外中间数据，也可以放这里。
}

原则：
Context 可以大。
Calculator 不直接依赖大 Context。
MetricBinding 负责从 Context 中提取必要数据。


14. MetricBinding
MetricBinding 是所有指标计算器和 Context 之间的唯一粘合层。
核心形式：
Bind(指标器, Context) => 指标结果

更具体：
MetricBinding：
  从 Context 提取必要数据
  调用指标计算器
  得到指标结果结构体
  将结果结构体 ToDict()
  收集到 MetricResultContext

推荐形式：
public static class MetricBinding
{
    public static void BindLevel(
        string groupName,
        LevelMetricContext context,
        IReadOnlySet<string> requiredMetrics,
        MetricResultContext result)
    {
        switch (groupName)
        {
            case "base":
                BindLevelBase(context, requiredMetrics, result);
                break;

            case "result":
                BindLevelResult(context, requiredMetrics, result);
                break;

            case "smooth":
                BindLevelSmooth(context, requiredMetrics, result);
                break;

            case "swap":
                BindLevelSwap(context, requiredMetrics, result);
                break;

            case "hardSwap":
                BindLevelHardSwap(context, requiredMetrics, result);
                break;

            case "slotPressure":
                BindLevelSlotPressure(context, requiredMetrics, result);
                break;

            case "dead":
                BindLevelDead(context, requiredMetrics, result);
                break;

            case "colorHold":
                BindLevelColorHold(context, requiredMetrics, result);
                break;

            case "curves":
                BindLevelCurves(context, requiredMetrics, result);
                break;

            default:
                throw new ArgumentException($"Unknown level metric group: {groupName}");
        }
    }
}

MetricBinding 可以集中在一个文件中，作为指标绑定总表。

15. MetricResultContext
MetricResultContext 负责统一收集指标结果。
public sealed class MetricResultContext
{
    private readonly Dictionary<string, object?> _values = new();

    public void Set(string metricName, object? value)
    {
        _values[metricName] = value;
    }

    public bool TryGet(string metricName, out object? value)
    {
        return _values.TryGetValue(metricName, out value);
    }

    public object? Get(string metricName)
    {
        return _values.TryGetValue(metricName, out var value)
            ? value
            : null;
    }
}

输出层只从 MetricResultContext 取值。

16. 指标计算器三件套
每组指标由三件东西组成：
1. 指标名映射
2. 指标结果结构体
3. 指标计算器

也就是：
MetricNames
MetricResult
MetricCalculator

原则：
指标名由指标计算器自己提供。
指标结果使用强类型结构体。
指标结构体提供 ToString 和 ToDict。
指标计算器尽量无状态。


17. 示例：LevelResult 指标组
17.1 指标名映射
public static class LevelResultMetricNames
{
    public const string SuccessCount = "level.successCount";
    public const string FailCount = "level.failCount";
    public const string FailRate = "level.failRate";
    public const string AvgScore = "level.avgScore";
    public const string AvgNewDifficultyScore = "level.avgNewDifficultyScore";

    public static readonly string[] All =
    {
        SuccessCount,
        FailCount,
        FailRate,
        AvgScore,
        AvgNewDifficultyScore
    };
}


17.2 指标结果结构体
public readonly record struct LevelResultMetrics(
    int SuccessCount,
    int FailCount,
    double FailRate,
    double AvgScore,
    double AvgNewDifficultyScore)
{
    public override string ToString()
    {
        return
            $"SuccessCount={SuccessCount}, " +
            $"FailCount={FailCount}, " +
            $"FailRate={FailRate}, " +
            $"AvgScore={AvgScore}, " +
            $"AvgNewDifficultyScore={AvgNewDifficultyScore}";
    }

    public Dictionary<string, object?> ToDict()
    {
        return new Dictionary<string, object?>
        {
            [LevelResultMetricNames.SuccessCount] = SuccessCount,
            [LevelResultMetricNames.FailCount] = FailCount,
            [LevelResultMetricNames.FailRate] = FailRate,
            [LevelResultMetricNames.AvgScore] = AvgScore,
            [LevelResultMetricNames.AvgNewDifficultyScore] = AvgNewDifficultyScore,
        };
    }
}


17.3 指标计算器
public static class LevelResultMetricCalculator
{
    public static string Scope => "level";

    public static string GroupName => "result";

    public static IReadOnlyList<string> MetricNames => LevelResultMetricNames.All;

    public static LevelResultMetrics Calculate(LevelInfo info)
    {
        return new LevelResultMetrics(
            SuccessCount: info.SuccessCount,
            FailCount: info.FailCount,
            FailRate: info.FailRate,
            AvgScore: info.AvgScore,
            AvgNewDifficultyScore: info.AvgNewDifficultyScore
        );
    }
}


17.4 Binding 使用方式
private static void BindLevelResult(
    LevelMetricContext context,
    IReadOnlySet<string> requiredMetrics,
    MetricResultContext result)
{
    var metrics = LevelResultMetricCalculator.Calculate(context.LevelInfo);
    var dict = metrics.ToDict();

    foreach (var metricName in requiredMetrics)
    {
        if (dict.TryGetValue(metricName, out var value))
        {
            result.Set(metricName, value);
        }
    }
}


18. 指标名来源
指标名不放全局总表。
指标名由对应指标计算器自己声明。
原则：
谁计算，谁声明自己能产出哪些指标名。

每个指标计算器提供：
Scope
GroupName
MetricNames

这样它本身就是完整单元：
我属于哪个输出域
我属于哪个 group
我能产出哪些指标
我怎么计算


19. MetricRegistry
MetricRegistry 从指标计算器收集元信息。
用途：
1. 获取所有合法指标名
2. 获取每个 scope 下有哪些指标
3. 获取每个 group 下有哪些指标
4. 校验 tile-eval.metrics.toml
5. 导出 tile-eval.metrics.toml 模板

概念结构：
public sealed class MetricCalculatorInfo
{
    public required string Scope { get; init; }

    public required string GroupName { get; init; }

    public required IReadOnlyList<string> MetricNames { get; init; }
}

注册表：
public sealed class MetricRegistry
{
    private readonly List<MetricCalculatorInfo> _items = new();

    public void Add(MetricCalculatorInfo item)
    {
        _items.Add(item);
    }

    public IReadOnlyList<MetricCalculatorInfo> Items => _items;

    public IReadOnlySet<string> GetAllMetricNames()
    {
        return _items
            .SelectMany(x => x.MetricNames)
            .ToHashSet();
    }
}


20. 指标配置文件导出
由于每个指标计算器都提供：
Scope
GroupName
MetricNames

所以可以通过命令直接导出指标配置文件。
命令：
./ThreeTile.CLI tile-eval metrics export

导出到文件：
./ThreeTile.CLI tile-eval metrics export \
  --output ./tile-eval.metrics.toml

覆盖已有文件：
./ThreeTile.CLI tile-eval metrics export \
  --output ./tile-eval.metrics.toml \
  --force


21. 导出依据
导出器不靠手写维护指标清单。
导出依据来自：
MetricRegistry
  ↓
所有指标计算器提供的 Scope / GroupName / MetricNames

导出流程：
遍历 MetricRegistry
  ↓
按 scope 分组
  ↓
按 groupName 分组
  ↓
生成 outputs.<scope>.order
  ↓
生成 groups.<scope>.<groupName>.metrics
  ↓
写出 TOML


22. 导出的 TOML 示例
version = 1
command = "tile-eval"

[outputs.solve]
enabled = false
order = [
  "solve.failed",
  "solve.totalTiles",
  "solve.tileStep",
]

[outputs.level]
enabled = true
order = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  "level.avgScore",
  "level.avgNewDifficultyScore",
]

[groups.solve.base]
metrics = [
  "solve.failed",
  "solve.totalTiles",
  "solve.tileStep",
]

[groups.level.result]
metrics = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  "level.avgScore",
  "level.avgNewDifficultyScore",
]

用户拿到后可以：
1. 注释不需要的指标
2. 调整 outputs.<scope>.order 中的顺序
3. 删除不需要的 group
4. 保留自己关心的输出配置


23. 输出顺序的依据
每个指标计算器的 MetricNames 顺序，就是默认输出顺序。
例如：
public static readonly string[] All =
{
    SuccessCount,
    FailCount,
    FailRate,
    AvgScore,
    AvgNewDifficultyScore,
};

会导出为：
[outputs.level]
order = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  "level.avgScore",
  "level.avgNewDifficultyScore",
]

规则：
指标默认顺序由指标计算器决定。
最终顺序由 TOML outputs.order 覆写。


24. MetricOutputWriter
输出层只关心：
outputs.order
MetricResultContext

示例：
public static IReadOnlyList<object?> BuildRow(
    IReadOnlyList<string> order,
    MetricResultContext result)
{
    var row = new object?[order.Count];

    for (int i = 0; i < order.Count; i++)
    {
        row[i] = result.Get(order[i]);
    }

    return row;
}

输出层不关心：
这个指标怎么算
它属于哪个 group
它从 Context 哪里取数据

它只关心：
order 里要哪些列
ResultContext 里有没有值


25. 推荐目录结构
第一版可以保持简单：
Metrics
├── MetricBinding.cs
├── MetricResultContext.cs
├── MetricRegistry.cs
├── SolveMetricContext.cs
├── LevelMetricContext.cs
└── Calculators
    ├── LevelResultMetrics.cs
    ├── LevelSmoothMetrics.cs
    ├── LevelSwapMetrics.cs
    ├── SolveResultMetrics.cs
    └── SolveSwapMetrics.cs

每个指标组文件内部包含三件套：
XXXMetricNames
XXXMetrics
XXXMetricCalculator

例如：
LevelResultMetrics.cs
  ├── LevelResultMetricNames
  ├── LevelResultMetrics
  └── LevelResultMetricCalculator


26. 最终闭环
完整闭环：
指标计算器声明指标名
  ↓
Registry 收集所有指标
  ↓
metrics export 生成 tile-eval.metrics.toml
  ↓
用户注释 / 调整 order
  ↓
运行时读取 tile-eval.metrics.toml
  ↓
根据 groups 找计算组
  ↓
MetricBinding 调用计算器
  ↓
计算器返回强类型 Metrics 结构体
  ↓
Metrics.ToDict()
  ↓
MetricResultContext 收集结果
  ↓
Writer 按 outputs.order 输出


27. 最终结论
本方案最终固定为：
指标配置文件：
  tile-eval.metrics.toml

指标配置结构：
  outputs 管输出域与输出顺序
  groups 管指标计算分组

指标计算结构：
  MetricContext 提供依赖数据
  MetricBinding 负责绑定 Context 与 Calculator
  MetricCalculator 负责计算
  Metrics 结构体负责承载结果并 ToDict
  MetricResultContext 负责收集
  MetricOutputWriter 负责按 order 输出
  MetricRegistry 负责收集指标元信息

指标导出：
  从 MetricRegistry 自动导出 tile-eval.metrics.toml

一句话总结：
groups 管怎么算，outputs.order 管怎么排；
指标计算器既是计算单元，也是指标配置模板的生成依据。

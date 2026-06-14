ThreeTile 项目分层、模拟器与批处理体系设计文档
1. 目标
ThreeTile 项目需要同时支持：
1. 关卡核心能力
2. 复杂分析能力
3. 指标计算能力
4. 模拟器组合能力
5. 大文件分片、计算、合并
6. CLI 配置、命令、输出

为了避免职责混乱，项目拆成三层：
ThreeTile.Core
ThreeTile.Services
ThreeTile.CLI

最终依赖方向：
ThreeTile.CLI
      ↓
ThreeTile.Services
      ↓
ThreeTile.Core

一句话总结：
Core 提供能力；
Services 编排任务；
CLI 处理配置和输入输出。


2. 三项目职责总览
2.1 ThreeTile.Core
ThreeTile.Core 是纯业务能力库。
它提供：
Core：
  关卡核心结构。

Analysis：
  复杂分析能力。

Metrics：
  指标定义和指标计算器。

Simulation：
  模拟器能力。

Difficulty：
  难度计算能力。

它不关心：
TOML
命令行参数
配置文件路径
CSV 输出
控制台输出
大文件路径
分片目录
批处理日志


2.2 ThreeTile.Services
ThreeTile.Services 是用例编排层。
它负责把 Core 里的能力组合成完整任务。
它负责：
1. 执行一次 tile-eval 任务
2. 执行大文件分片、计算、合并任务
3. 汇总指标计算器信息
4. 提供指标配置导出的结构化数据

它不负责：
TOML 解析
命令行参数
控制台输出格式
CSV 文件具体写法


2.3 ThreeTile.CLI
ThreeTile.CLI 是命令行适配层。
它负责：
1. 解析命令行参数
2. 读取 threetile.toml
3. 读取 tile-eval.metrics.toml
4. 合并配置
5. 选择 finder / scorer / preset / profile
6. 选择启用哪些指标器
7. 调用 Services
8. 按 outputs.order 输出 CSV
9. 导出配置文件
10. 打印日志和进度


3. 项目依赖关系
ThreeTile.Core
  不依赖 Services
  不依赖 CLI
  不依赖 Tomlyn
  不依赖文件系统批处理细节

ThreeTile.Services
  依赖 ThreeTile.Core
  不依赖 CLI
  不依赖 Tomlyn
  不解析命令行

ThreeTile.CLI
  依赖 ThreeTile.Services
  依赖 ThreeTile.Core
  依赖 Tomlyn
  负责配置、命令、输出

依赖图：
ThreeTile.CLI
      ↓
ThreeTile.Services
      ↓
ThreeTile.Core

禁止反向依赖：
Core 不依赖 Services
Core 不依赖 CLI
Services 不依赖 CLI
Simulation 不依赖 TOML
Metrics 不依赖 CLI
Metrics 不依赖 TOML


第一部分：ThreeTile.Core
4. ThreeTile.Core 总目录
推荐结构：
ThreeTile.Core
├── Core
│   ├── Types
│   ├── Moves
│   ├── Zones
│   ├── LevelCore.cs
│   └── Tile.cs
│
├── Analysis
│   ├── Cluster
│   ├── DAG
│   ├── Residue
│   └── UnlockPath
│
├── Metrics
│   ├── LevelResultMetrics.cs
│   ├── LevelSmoothMetrics.cs
│   ├── LevelSwapMetrics.cs
│   ├── LevelHardSwapMetrics.cs
│   ├── LevelSlotPressureMetrics.cs
│   ├── LevelColorHoldMetrics.cs
│   ├── LevelMiscMetrics.cs
│   ├── SolveResultMetrics.cs
│   ├── SolveSmoothMetrics.cs
│   ├── SolveSwapMetrics.cs
│   └── SolveMiscMetrics.cs
│
├── Simulation
│   ├── Context
│   ├── CandidateFinding
│   ├── CandidateScoring
│   ├── Strategies
│   ├── Builders
│   ├── Simulators
│   ├── SimulationMetricBinding.cs
│   └── SimulationMetricResult.cs
│
└── Difficulty
    ├── Evaluators
    └── Scores


5. Core：关卡核心
Core 只放最稳定的领域结构。
它回答：
关卡是什么？
Tile 是什么？
槽位是什么？
移动是什么？
规则枚举是什么？

示例：
Core
├── Types
│   ├── LockRuleTypeEnum.cs
│   ├── SlotTypeEnum.cs
│   └── ...
├── Moves
├── Zones
├── LevelCore.cs
└── Tile.cs

Core 不依赖：
Simulation
Metrics
Services
CLI
Config


6. Analysis：复杂分析能力
Analysis 放可被模拟器或指标复用的复杂分析能力。
例如：
最大簇
DAG
解锁路径
残留结构
祖先关系

这些不是指标本身，也不是模拟器本身。
推荐结构：
Analysis
├── Cluster
│   ├── MaxClusterAnalyzer.cs
│   └── ClusterResult.cs
│
├── DAG
│   ├── UnlockDagBuilder.cs
│   └── UnlockDag.cs
│
├── Residue
│   └── ...
│
└── UnlockPath
    ├── UnlockPathFinder.cs
    └── UnlockPathResult.cs

原则：
Analysis 依赖 Core。
Analysis 不依赖 Metrics。
Analysis 不依赖 Services。
Analysis 不依赖 CLI。


7. Metrics：纯指标层
Metrics 层只放指标。
它不放：
MetricContext
SimulationContext
MetricBinding
配置读取
TOML
CSV 输出顺序

这些不是指标本身。

8. Metrics 的三件套
每组指标一个文件。
每个文件内部包含三件套：
1. 指标名映射
2. 指标结果结构体
3. 指标计算器

例如：
LevelResultMetrics.cs
  ├── LevelResultMetricNames
  ├── LevelResultMetrics
  └── LevelResultMetricCalculator


9. 指标名映射
指标名由指标计算器所在文件自己提供。
原则：
谁计算，谁声明自己能产出哪些指标名。

不要建立一个巨大的全局指标名总表。
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


10. 指标结果结构体
指标结果结构体负责承载一组指标计算结果。
它提供：
ToString
ToDict

示例：
public readonly record struct LevelResultMetrics(
    int SuccessCount,
    int FailCount,
    double FailRate,
    double AvgScore)
{
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

    public override string ToString()
    {
        return
            $"SuccessCount={SuccessCount}, " +
            $"FailCount={FailCount}, " +
            $"FailRate={FailRate}, " +
            $"AvgScore={AvgScore}";
    }
}


11. 指标计算器
指标计算器尽量无状态。
它只接收必要依赖数据，不直接依赖模拟器内部结构。
示例：
public static class LevelResultMetricCalculator
{
    public const string Scope = "level";
    public const string Group = "result";

    public static IReadOnlyList<string> MetricNames => LevelResultMetricNames.All;

    public static LevelResultMetrics Calculate(LevelInfo info)
    {
        return new LevelResultMetrics(
            info.SuccessCount,
            info.FailCount,
            info.FailRate,
            info.AvgScore
        );
    }
}

指标计算器自己提供：
Scope
Group
MetricNames
Calculate(...)

这样它本身就是完整单元：
我属于哪个输出域
我属于哪个 group
我能产出哪些指标
我怎么计算


12. misc 指标分组
对于暂时无法明确归类的指标，可以使用 misc。
misc 表示：
miscellaneous
杂项 / 暂未归类

使用规则：
misc = 暂时不知道如何分组的指标

不是：
misc = 懒得分类的指标

代码中可以有：
Metrics
├── LevelMiscMetrics.cs
└── SolveMiscMetrics.cs

TOML 中可以有：
[groups.solve.misc]
metrics = []

[groups.level.misc]
metrics = []

约束：
misc 只是过渡分组。
当 misc 中的指标逐渐形成稳定主题时，应拆分为明确 group。


13. Metrics 层结论
Metrics 层只有指标。

指标计算器不关心模拟器怎么跑。
指标计算器不关心用户最终输出哪些列。
指标计算器不关心输出顺序。
指标计算器不关心 TOML。
指标计算器不关心 CLI。


14. Simulation：模拟能力
Simulation 是模拟器能力层。
它负责：
1. 候选组寻找
2. 候选组打分
3. 选择策略
4. 构建具体模拟器
5. 执行模拟
6. 收集指标依赖
7. 调用指标计算器
8. 返回模拟结果

Simulation 不负责：
读取 TOML
决定用户最终输出哪些列
决定 CSV 输出顺序
写 CSV / 文件

这些属于 CLI。

15. Simulation 的组合模型
模拟器由多个组件组合而成：
候选组寻找方式
  +
候选组打分方式
  +
选择策略
  +
环境规则
  ↓
具体模拟器

目录：
Simulation
├── Context
├── CandidateFinding
├── CandidateScoring
├── Strategies
├── Builders
├── Simulators
├── SimulationMetricBinding.cs
└── SimulationMetricResult.cs


16. CandidateFinding：候选组寻找
放不同的候选组生成方式。
例如：
FSE
解锁路径
FSE + 解锁路径组合
其他候选组生成方式

接口示例：
public interface ICandidateFinder
{
    CandidateGroupSet FindCandidates(SimulationContext context);
}

可能实现：
FseCandidateFinder
UnlockPathCandidateFinder
CompositeCandidateFinder

其中 CompositeCandidateFinder 可以表示：
FSE 和解锁路径组合得到候选组


17. CandidateScoring：候选组打分
放不同的候选组打分方法。
例如：
PiKa
Tokiki
Feature

接口示例：
public interface ICandidateScorer
{
    CandidateScore Score(
        SimulationContext context,
        CandidateGroup candidate);
}

可能实现：
PiKaCandidateScorer
TokikiCandidateScorer
FeatureCandidateScorer


18. 寻找方式与打分方式分离
寻找和打分不是完全匹配关系。
有些寻找方式只能配某些打分方式。
因此需要集中处理兼容性。
推荐：
Simulation/Builders/SimulatorCompatibility.cs

职责：
判断 CandidateFinder 和 CandidateScorer 是否可以组合。

示例：
public static class SimulatorCompatibility
{
    public static bool CanUse(
        CandidateFinderKind finder,
        CandidateScorerKind scorer,
        SimulationEnvironment env)
    {
        // 集中判断兼容性
    }
}

兼容性规则不要散落到各个模拟器里。

19. Builders：构建具体模拟器
Builder 负责把组件组装成模拟器。
示例：
var simulator = new SimulatorBuilder()
    .UseFinder(CandidateFinderKind.FseUnlockPath)
    .UseScorer(CandidateScorerKind.PiKa)
    .UseSelection(CandidateSelectionKind.Greedy)
    .Build();

Builder 职责：
1. 接收已经选择好的组件
2. 创建 Finder
3. 创建 Scorer
4. 创建 Strategy
5. 检查兼容性
6. 组装 Simulator


20. SimulationMetricBinding：模拟层指标绑定
因为模拟层负责提供指标依赖，所以绑定写在 Simulation 层。
不放 Metrics 层。
SimulationMetricBinding 负责：
1. 从模拟器产生的数据中提取指标计算器需要的依赖
2. 调用 Metrics 层的指标计算器
3. 得到强类型指标结果结构体
4. 调用 ToDict()
5. 收集到 SimulationMetricResult

核心形式：
Bind(指标计算器, Simulation 数据) => 指标结果

示例：
public static class SimulationMetricBinding
{
    public static void BindLevelResult(
        SimulationContext context,
        SimulationMetricResult result)
    {
        LevelInfo levelInfo = context.LevelInfo;

        var metrics = LevelResultMetricCalculator.Calculate(levelInfo);

        result.AddRange(metrics.ToDict());
    }
}

如果某个指标需要复杂数据：
public static void BindLevelSwap(
    SimulationContext context,
    SimulationMetricResult result)
{
    var metrics = LevelSwapMetricCalculator.Calculate(
        context.SolveInfos,
        context.SwapTrace,
        context.PairCount,
        context.SlotCapacity
    );

    result.AddRange(metrics.ToDict());
}


21. SimulationMetricResult
SimulationMetricResult 是指标结果收集器。
它不负责排序。
它不负责输出。
它只负责：
metricName -> value

示例：
public sealed class SimulationMetricResult
{
    private readonly Dictionary<string, object?> _values = new();

    public void AddRange(IReadOnlyDictionary<string, object?> values)
    {
        foreach (var pair in values)
            _values[pair.Key] = pair.Value;
    }

    public object? Get(string metricName)
    {
        return _values.TryGetValue(metricName, out var value)
            ? value
            : null;
    }

    public IReadOnlyDictionary<string, object?> Values => _values;
}


22. Difficulty：难度能力
Difficulty 放难度相关计算能力。
推荐结构：
Difficulty
├── Evaluators
└── Scores

例如：
Difficulty
├── Evaluators
│   ├── ClassicDifficultyEvaluator.cs
│   ├── FeatureDifficultyEvaluator.cs
│   └── NewDifficultyEvaluator.cs
└── Scores
    ├── DifficultyScore.cs
    └── NewDifficultyScore.cs


第二部分：ThreeTile.Services
23. ThreeTile.Services 总目录
推荐结构：
ThreeTile.Services
├── TileEval
│   ├── TileEvalService.cs
│   ├── TileEvalRequest.cs
│   ├── TileEvalResult.cs
│   ├── TileEvalBatchService.cs
│   ├── TileEvalShardPlanner.cs
│   ├── TileEvalShardProcessor.cs
│   └── TileEvalShardMerger.cs
│
├── Metrics
│   ├── TileEvalMetricCatalog.cs
│   ├── TileEvalMetricExportService.cs
│   └── TileEvalMetricSelection.cs
│
└── Common
    ├── ServiceResult.cs
    └── ErrorInfo.cs


24. Services 的定位
Services 是用例层。
它负责把 Core 中的能力组合成一个完整任务。
它不负责：
TOML
命令行
控制台输出
CSV 具体格式
配置文件路径

它接收已经整理好的 Request。

25. TileEvalService：单次用例服务
TileEvalService 负责执行一次完整的 tile-eval 用例。
它接收：
关卡
候选组寻找方式
候选组打分方式
总模拟次数
最大成功次数
指标器

返回：
LevelInfo
SolveInfos
SimulationMetricResult


26. TileEvalRequest
示例：
public sealed class TileEvalRequest
{
    public required LevelCore Level { get; init; }

    public required CandidateFinderKind FinderKind { get; init; }

    public required CandidateScorerKind ScorerKind { get; init; }

    public int TotalRuns { get; init; }

    public int MaxSuccessCount { get; init; }

    public IReadOnlyList<object> MetricCalculators { get; init; }
        = Array.Empty<object>();
}


27. TileEvalResult
示例：
public sealed class TileEvalResult
{
    public required LevelInfo LevelInfo { get; init; }

    public required IReadOnlyList<SolveInfo> SolveInfos { get; init; }

    public required SimulationMetricResult MetricResult { get; init; }
}


28. TileEvalService
示例：
public sealed class TileEvalService
{
    public TileEvalResult Run(TileEvalRequest request)
    {
        var simulator = new SimulatorBuilder()
            .UseFinder(request.FinderKind)
            .UseScorer(request.ScorerKind)
            .Build();

        var result = simulator.Run(
            request.Level,
            request.TotalRuns,
            request.MaxSuccessCount,
            request.MetricCalculators);

        return new TileEvalResult
        {
            LevelInfo = result.LevelInfo,
            SolveInfos = result.SolveInfos,
            MetricResult = result.MetricResult
        };
    }
}


29. 大文件分片、计算、合并
大文件分片、计算、合并属于 Services 层。
更准确地说，属于：
Batch / Pipeline / Job Orchestration

也就是：
大文件分片
  ↓
每片调用 Core / Simulation 计算
  ↓
合并结果
  ↓
输出最终文件

它不是 Core，也不是 Metrics，也不是 Simulation 本身。

30. 为什么大文件分片不属于 Core
Core 只应该知道：
关卡结构
规则
算法
指标
模拟器

Core 不应该知道：
大文件
CSV
分片
目录
多进程
合并文件
断点续跑

这些是任务执行方式，不是关卡核心能力。

31. 为什么大文件分片不属于 Simulation
Simulation 只负责：
给定关卡 / 一组关卡
给定 finder / scorer / runs / metrics
跑出模拟结果

它不应该关心：
这个关卡来自哪个大 CSV
当前是第几个 shard
结果要不要落盘
最后怎么 merge

Simulation 是计算单元。
Batch Service 是批处理编排。

32. TileEvalBatchService
TileEvalBatchService 负责：
1. 读取大文件任务描述
2. 分片
3. 为每个 shard 创建处理任务
4. 每个 shard 调用 TileEvalService / Simulation
5. 写 shard result
6. 写 shard error
7. 合并 result
8. 合并 error
9. 返回 BatchResult

推荐结构：
ThreeTile.Services
└── TileEval
    ├── TileEvalBatchService.cs
    ├── TileEvalShardPlanner.cs
    ├── TileEvalShardProcessor.cs
    └── TileEvalShardMerger.cs


33. 分片处理职责
TileEvalShardPlanner
负责：
根据输入大文件生成 shard 计划

例如：
input.csv
  ↓
shard-000.csv
shard-001.csv
shard-002.csv


TileEvalShardProcessor
负责：
处理单个 shard
对 shard 中每一行调用 TileEvalService 或核心模拟能力
生成 shard result / shard error


TileEvalShardMerger
负责：
合并所有 shard result
合并所有 shard error
生成最终输出


TileEvalBatchService
负责整体协调：
Plan shards
  ↓
Process shards
  ↓
Merge shards
  ↓
Return BatchResult


34. Metrics 服务
指标配置导出不放 Core，也不完全塞 CLI。
更合适放 Services 中提供结构化目录。
原因：
Core 提供指标器能力；
Services 汇总这些指标器，形成“可导出的指标目录”；
CLI 调用导出服务，再写成 TOML 文件。

推荐：
ThreeTile.Services
└── Metrics
    ├── TileEvalMetricCatalog.cs
    ├── TileEvalMetricExportService.cs
    └── TileEvalMetricSelection.cs


35. TileEvalMetricCatalog
负责收集所有 tile-eval 可用指标器。
示例：
public sealed class TileEvalMetricCatalog
{
    public IReadOnlyList<MetricCalculatorInfo> GetAll()
    {
        return new[]
        {
            new MetricCalculatorInfo(
                Scope: LevelResultMetricCalculator.Scope,
                Group: LevelResultMetricCalculator.Group,
                MetricNames: LevelResultMetricCalculator.MetricNames),

            // 其他指标器...
        };
    }
}


36. TileEvalMetricExportService
负责生成指标配置模板的结构化数据。
建议：
Services 不直接依赖 Tomlyn。
Services 不直接返回 TOML 字符串。
Services 返回结构化 MetricsConfigTemplate。
CLI 再负责写成 TOML。

这样依赖更干净。

第三部分：ThreeTile.CLI
37. ThreeTile.CLI 总目录
推荐结构：
ThreeTile.CLI
├── Commands
│   ├── TileEvalCommand.cs
│   ├── TileEvalCliOptions.cs
│   └── MetricsExportCommand.cs
│
├── Config
│   ├── TomlConfigLoader.cs
│   ├── TileEvalCommandConfig.cs
│   ├── MetricsConfig.cs
│   ├── MetricsConfigLoader.cs
│   └── MetricsConfigExporter.cs
│
├── Output
│   ├── CsvMetricWriter.cs
│   └── ConsoleReporter.cs
│
└── Program.cs


38. CLI 职责
CLI 负责：
1. 解析命令行参数
2. 读取 threetile.toml
3. 读取 tile-eval.metrics.toml
4. 合并命令默认配置 + TOML + CLI 覆写
5. 根据配置选择 Finder / Scorer / MetricCalculators
6. 组装 TileEvalRequest 或 TileEvalBatchRequest
7. 调用 Services
8. 根据 outputs.order 从 MetricResult 中取值
9. 写 CSV / 控制台输出
10. 导出指标配置文件


39. CLI 配置边界
配置只属于 CLI。
包括：
threetile.toml
tile-eval.metrics.toml
命令行参数
outputs.order
CSV 输出顺序

这些都在 CLI 层处理。
Services 只接收：
TileEvalRequest
TileEvalBatchRequest

Core 只提供：
领域能力和模拟能力


40. 指标输出顺序属于 CLI
指标结果的组内哪些要输出、哪些不要、顺序是什么，都属于 CLI 层。
也就是说：
groups 管怎么算
outputs.order 管怎么排

但是：
groups / outputs.order 是 CLI 配置层处理的事情。

Simulation 不关心：
用户最终想输出哪些列
这些列怎么排序
哪些列被注释了

Simulation 只负责：
按 Services 传入的指标器，算出指标结果。

CLI 拿到 SimulationMetricResult 后：
根据 outputs.order 取值
写出 CSV


41. tile-eval.metrics.toml
指标配置文件独立：
tile-eval.metrics.toml

它属于 CLI 层处理。
核心结构：
version = 1
command = "tile-eval"

[outputs.level]
enabled = true
order = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  "level.avgScore",
]

[groups.level.result]
metrics = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  "level.avgScore",
]

[groups.level.misc]
metrics = []

含义：
outputs.order：
  最终输出顺序。

groups：
  指标属于哪个计算组。


42. 指标配置文件导出
CLI 调用 Services 的指标目录服务，导出配置模板。
命令：
./ThreeTile.CLI tile-eval metrics export \
  --output ./tile-eval.metrics.toml

导出依据：
所有可用指标计算器
  ↓
Scope
  ↓
Group
  ↓
MetricNames

用户可以在导出的配置中：
注释不需要的指标
调整 outputs.order 顺序
删除不需要的 group
添加 misc 临时分组


第四部分：整体执行流程
43. 单文件 tile-eval 流程
CLI
  读取 threetile.toml
  读取 tile-eval.metrics.toml
  解析命令行
  选择 finder / scorer / metrics
  组装 TileEvalRequest
  ↓

Services
  TileEvalService.Run(request)
  组装模拟器
  调用 Simulation
  返回 TileEvalResult
  ↓

Core / Simulation
  执行模拟
  收集指标依赖
  调用 Metrics 指标器
  生成 SimulationMetricResult
  ↓

CLI
  根据 outputs.order 从 MetricResult 取值
  写 CSV / 控制台输出


44. 大文件批处理流程
CLI
  读取配置
  解析 input / output / error / shardCount / processCount
  组装 TileEvalBatchRequest
  ↓

Services
  TileEvalBatchService.Run(request)
    ↓
    TileEvalShardPlanner 生成分片计划
    ↓
    TileEvalShardProcessor 处理每个分片
      ↓
      对每条记录调用 TileEvalService / Simulation
      ↓
      写 shard result / shard error
    ↓
    TileEvalShardMerger 合并所有分片
    ↓
    返回 BatchResult
  ↓

CLI
  打印日志
  输出最终文件路径


第五部分：原目录迁移建议
45. 当前目录迁移
当前可能存在：
Cluster
Core
DAG
DifficultyEvaluators
FeatureSolving
FocusSolving
Metrics
Residue
Solving

建议迁移：
Cluster              -> Analysis/Cluster
DAG                  -> Analysis/DAG
Residue              -> Analysis/Residue
DifficultyEvaluators -> Difficulty/Evaluators

FeatureSolving       -> Simulation/CandidateScoring 或 Simulation/CandidateFinding
FocusSolving         -> Simulation/CandidateFinding 或 Simulation/Strategies
Solving              -> Simulation
Metrics              -> Metrics
Core                 -> Core

判断规则：
如果它负责生成候选组：
  放 Simulation/CandidateFinding

如果它负责给候选组打分：
  放 Simulation/CandidateScoring

如果它负责决定选择哪个候选组：
  放 Simulation/Strategies

如果它是复杂分析能力：
  放 Analysis

如果它是指标计算：
  放 Metrics

如果暂时无法分类的指标：
  放 Metrics/LevelMiscMetrics.cs 或 Metrics/SolveMiscMetrics.cs


第六部分：最终结论
46. 最终项目结构
ThreeTile.Core
  提供领域能力、分析能力、指标能力、模拟能力。

ThreeTile.Services
  提供用例编排能力。
  包括单次 tile-eval 和大文件批处理。

ThreeTile.CLI
  提供命令行入口。
  负责配置、命令、输出。


47. 最终职责边界
Core：
  关卡核心。

Analysis：
  复杂可复用分析。

Metrics：
  只有指标。
  每组指标包含：
    1. 指标名映射
    2. 指标结果结构体
    3. 指标计算器

Simulation：
  负责模拟能力。
  负责组合 CandidateFinder 和 CandidateScorer。
  负责收集指标依赖。
  负责调用指标器。
  负责返回 SimulationResult。

Services：
  负责用例编排。
  负责 TileEvalService。
  负责大文件分片、计算、合并。
  负责指标目录的结构化汇总。

CLI：
  负责 TOML。
  负责命令行。
  负责配置合并。
  负责选择指标。
  负责 outputs.order。
  负责 CSV / 文件输出。


48. 最终一句话
Core 提供能力；
Simulation 完成计算；
Services 编排任务；
CLI 处理配置和输入输出。

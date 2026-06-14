ThreeTile Simulation 架构设计文档
1. 核心目标
Simulation 模块的目标不是堆功能，而是沉淀一套稳定、业务无关、可组合、可观察的模拟能力。
核心原则：
Core 沉淀稳定能力；
Services 编排具体任务；
CLI 处理配置和输入输出。

对于模拟器来说，最终目标是：
SimulationRunner
    负责稳定跑模拟主流程

ISimulationPolicy
    负责决定下一组行为

ISimulationObserver
    负责观察主流程事件

IPolicyObserver
    负责观察策略内部决策过程

SimulationResult
    负责承载稳定结果

MetricBag
    负责承载扩展指标

一句话总结：
模拟主流程要稳定；
候选发现要正交；
策略决策要可观察；
指标和进度条只作为旁路观察，不污染主流程。


2. 分层边界
项目整体仍然保持三层：
ThreeTile.CLI
    ↓
ThreeTile.Services
    ↓
ThreeTile.Core

2.1 Core 负责什么
Core 负责业务无关的稳定能力：
LevelCore
Tile
Move
规则枚举
基础分析能力
模拟器接口
模拟主流程
候选表达
候选发现
候选剪枝
候选组合
默认 baseline 策略
事件观察接口
基础结果结构

Core 可以提供：
FSE 默认候选生成
Tile 解锁路径候选生成
Classic 解锁路径候选生成
簇分析
剪枝规则
默认 baseline scorer
默认 simulation policy

Core 不应该知道：
CSV 输出
TOML 配置
outputs.order
TileEvalRequest
PiKa 指标
Tokiki 指标
具体业务指标启用列表
批处理文件路径


2.2 Services 负责什么
Services 是用例编排层。
Services 负责：
TileEvalService
TileEvalBatchService
TileEvalRequest
TileEvalResult
具体指标绑定
具体指标选择
具体 scorer 组合
具体 observer 组合
进度条 observer
调试 observer
策略指标 observer

Services 可以使用 Core 的模拟能力，但不能反向让 Core 依赖 Services。
Services 适合放：
PiKaCandidateScorer
TokikiCandidateScorer
FeatureCandidateScorer
TileEvalMetricsObserver
TileEvalPolicyMetricsObserver
TileEvalProgressObserver
TileEvalMetricBinder
TileEvalMetricCatalog
TileEvalPolicyFactory


2.3 CLI 负责什么
CLI 负责用户输入和最终输出：
命令行参数
TOML 读取
配置合并
metrics.toml
outputs.order
CSV writer
ConsoleReporter
文件路径
批处理命令入口

CLI 不应该进入 Core 的模拟主流程。

3. Simulation 总体结构
推荐目录：
ThreeTile.Core
└── Simulation
    ├── Running
    │   ├── SimulationRunner.cs
    │   ├── SimulationContext.cs
    │   ├── SimulationBatchContext.cs
    │   ├── SimulationRunResult.cs
    │   └── SimulationBatchResult.cs
    │
    ├── Candidates
    │   ├── TileCandidate.cs
    │   ├── TileCandidateSet.cs
    │   ├── BehaviourGroup.cs
    │   └── BehaviourGroupSet.cs
    │
    ├── CandidateFinding
    │   ├── ITileCandidateFinder.cs
    │   ├── IBehaviourGroupFinder.cs
    │   │
    │   ├── Fse
    │   │   ├── FseBehaviourGroupFinder.cs
    │   │   └── FseOptions.cs
    │   │
    │   ├── UnlockPath
    │   │   ├── TileUnlockPathCandidateFinder.cs
    │   │   └── ClassicUnlockPathCandidateFinder.cs
    │   │
    │   └── GeneralElimination
    │       ├── GeneralEliminationBehaviourGroupFinder.cs
    │       └── TileCandidateCombiner.cs
    │
    ├── Pruning
    │   ├── ITileCandidatePruner.cs
    │   ├── IBehaviourGroupPruner.cs
    │   ├── CompositeTileCandidatePruner.cs
    │   └── CompositeBehaviourGroupPruner.cs
    │
    ├── Scoring
    │   ├── ICandidateScorer.cs
    │   └── DefaultCandidateScorer.cs
    │
    ├── Strategies
    │   ├── ICandidateSelectionStrategy.cs
    │   ├── GreedySelectionStrategy.cs
    │   └── SoftmaxSelectionStrategy.cs
    │
    ├── Policies
    │   ├── ISimulationPolicy.cs
    │   ├── DefaultSimulationPolicy.cs
    │   ├── IPolicyObserver.cs
    │   ├── PolicyObserverBase.cs
    │   ├── EmptyPolicyObserver.cs
    │   └── CompositePolicyObserver.cs
    │
    ├── Observers
    │   ├── ISimulationObserver.cs
    │   ├── SimulationObserverBase.cs
    │   ├── EmptySimulationObserver.cs
    │   └── CompositeSimulationObserver.cs
    │
    └── Metrics
        └── MetricBag.cs


4. 模拟主流程
SimulationRunner 是稳定骨架。
它只负责：
跑 N 次模拟
每次 Clone Level
每次调用 policy.NextBehaviourGroup(context)
执行 BehaviourGroup 中的 Move
发出主流程事件
收集单次结果
生成批量结果

它不负责：
Finder 具体怎么找
Scorer 具体怎么打分
指标怎么计算
CSV 怎么输出
进度条怎么显示

主流程应该保持极简：
public sealed class SimulationRunner
{
    public SimulationBatchResult Simulate(
        LevelCore level,
        int simulationCount,
        ISimulationPolicy policy,
        ISimulationObserver? observer = null)
    {
        observer ??= EmptySimulationObserver.Instance;

        var result = new SimulationBatchResult(simulationCount);

        var batchContext = new SimulationBatchContext(
            simulationCount,
            Stopwatch.GetTimestamp());

        observer.OnBatchStarted(batchContext);

        for (int i = 0; i < simulationCount; i++)
        {
            var runResult = RunOne(
                level,
                i,
                policy,
                observer);

            result.Add(runResult);

            observer.OnRunCompleted(
                runResult.Context,
                runResult);
        }

        observer.OnBatchCompleted(result);

        return result;
    }

    private SimulationRunResult RunOne(
        LevelCore originLevel,
        int simulationIndex,
        ISimulationPolicy policy,
        ISimulationObserver observer)
    {
        var level = originLevel.CloneForSimulation();

        var context = new SimulationContext(
            simulationIndex,
            level,
            new Random(),
            Stopwatch.GetTimestamp());

        observer.OnRunStarted(context);

        while (!level.IsFinished)
        {
            BehaviourGroup group = policy.NextBehaviourGroup(context);

            if (group.IsEmpty)
                break;

            observer.OnBeforeBehaviourGroup(context, group);

            for (int i = 0; i < group.Count; i++)
            {
                Move move = group.Moves[i];

                observer.OnBeforeMove(context, move);

                level.ApplyMove(move);
                context.AdvanceMove();

                observer.OnAfterMove(context, move);
            }

            context.AdvanceBehaviourGroup();

            observer.OnAfterBehaviourGroup(context, group);
        }

        bool success = level.IsSuccess;

        return new SimulationRunResult(
            context,
            simulationIndex,
            success,
            level.MoveCount,
            context.ElapsedMilliseconds);
    }
}

注意：
Runner 只知道 BehaviourGroup。
Runner 不知道 FSE。
Runner 不知道 PiKa。
Runner 不知道指标。
Runner 不知道输出。


5. SimulationContext 设计
SimulationContext 不应该是万能上下文。
它只表示：
当前这一次模拟运行时的公共现场。

推荐第一版：
public sealed class SimulationContext
{
    public int SimulationIndex { get; }

    public LevelCore Level { get; }

    public Random Random { get; }

    public long StartTimestamp { get; }

    public int BehaviourGroupIndex { get; private set; }

    public int MoveIndex { get; private set; }

    public int AppliedMoveCount => Level.MoveCount;

    public double ElapsedMilliseconds =>
        (Stopwatch.GetTimestamp() - StartTimestamp) * 1000.0 / Stopwatch.Frequency;

    public SimulationContext(
        int simulationIndex,
        LevelCore level,
        Random random,
        long startTimestamp)
    {
        SimulationIndex = simulationIndex;
        Level = level;
        Random = random;
        StartTimestamp = startTimestamp;
    }

    public void AdvanceBehaviourGroup()
    {
        BehaviourGroupIndex++;
    }

    public void AdvanceMove()
    {
        MoveIndex++;
    }
}

它可以包含：
SimulationIndex
Level
Random
StartTimestamp
BehaviourGroupIndex
MoveIndex
AppliedMoveCount
ElapsedMilliseconds

它不应该包含：
CSV 字段
TOML 配置
MetricBag
TileEvalRequest
PiKa 专属参数
Tokiki 专属参数
inputFile
outputFile
shardIndex
CLI 参数

公共运行现场放 SimulationContext。
算法私有配置放自己的 Options。
任务配置放 Services Request。
CLI 配置放 CLI Config。

6. SimulationBatchContext
批量上下文描述的是整批模拟：
public readonly struct SimulationBatchContext
{
    public readonly int TotalSimulationCount;
    public readonly int MaxSuccessCount;
    public readonly long StartTimestamp;

    public SimulationBatchContext(
        int totalSimulationCount,
        int maxSuccessCount,
        long startTimestamp)
    {
        TotalSimulationCount = totalSimulationCount;
        MaxSuccessCount = maxSuccessCount;
        StartTimestamp = startTimestamp;
    }

    public double ElapsedMilliseconds =>
        (Stopwatch.GetTimestamp() - StartTimestamp) * 1000.0 / Stopwatch.Frequency;
}

它适合给：
进度条
批量耗时统计
最大成功次数控制
批量开始 / 结束事件


7. 候选表达方式
当前稳定的候选表达方式有两种：
Tile 模式：
    返回单个 Tile
    返回多个 Tile 候选

BehaviourGroup 模式：
    多个 Tile 一组
    一起选
    返回单个 BehaviourGroup
    返回多个 BehaviourGroup 候选

这两种不要强行合并。
7.1 TileCandidate
public readonly struct TileCandidate
{
    public readonly int TileIndex;
    public readonly int Color;

    public TileCandidate(
        int tileIndex,
        int color)
    {
        TileIndex = tileIndex;
        Color = color;
    }
}

7.2 BehaviourGroup
public readonly struct BehaviourGroup
{
    public static readonly BehaviourGroup Empty = new(
        Array.Empty<Move>(),
        0);

    public readonly Move[] Moves;
    public readonly int Count;

    public bool IsEmpty => Count == 0;

    public BehaviourGroup(
        Move[] moves,
        int count)
    {
        Moves = moves;
        Count = count;
    }
}

第一版可以用 Move[] + Count。
后续热路径优化时，再考虑：
预分配 buffer
Span
ref struct
对象池


8. CandidateFinder 设计
不要只有一个万能 ICandidateFinder。
建议拆成两个：
public interface ITileCandidateFinder
{
    int Find(
        SimulationContext context,
        Span<TileCandidate> output);
}

public interface IBehaviourGroupFinder
{
    int Find(
        SimulationContext context,
        Span<BehaviourGroup> output);
}

语义：
ITileCandidateFinder
    找 Tile 候选

IBehaviourGroupFinder
    找 BehaviourGroup 候选

这样不会强迫所有算法都返回同一种东西。

9. FSE 候选寻找
FSE 属于 Core。
因为 FSE 回答的是：
当前局面有哪些可行动作组？

它是业务无关的候选发现能力。
推荐：
public sealed class FseBehaviourGroupFinder : IBehaviourGroupFinder
{
    private readonly FseOptions _options;

    public FseBehaviourGroupFinder(
        FseOptions options)
    {
        _options = options;
    }

    public int Find(
        SimulationContext context,
        Span<BehaviourGroup> output)
    {
        // 1. 扫描当前可选 Tile
        // 2. 构造可消除行为组
        // 3. 写入 output
        // 4. 返回 count

        return 0;
    }
}

FSE 可以进入 Core：
Simulation/CandidateFinding/Fse

但 FSE 不应该直接包含 PiKa / Tokiki / Feature 业务评分。

10. 解锁路径候选寻找
解锁路径也属于 Core 的候选发现能力。
当前有两类：
Tile 模式的解锁路径
Classic 规则下的解锁路径

推荐：
public sealed class TileUnlockPathCandidateFinder : ITileCandidateFinder
{
    public int Find(
        SimulationContext context,
        Span<TileCandidate> output)
    {
        // 返回 Tile 模式下的解锁路径候选
        return 0;
    }
}

public sealed class ClassicUnlockPathCandidateFinder : ITileCandidateFinder
{
    public int Find(
        SimulationContext context,
        Span<TileCandidate> output)
    {
        // 返回 Classic 规则下的解锁路径候选
        return 0;
    }
}

它们可以依赖 Analysis 层的 UnlockPathAnalyzer。

11. 簇寻找
簇寻找更像 Analysis 能力，不应该直接变成 SimulationPolicy。
它回答的是：
哪些 Tile / 颜色 / 区域形成扎堆结构？
哪些候选可能陷入堆积？
哪些区域需要特殊处理？

推荐放：
ThreeTile.Core
└── Analysis
    └── Cluster
        ├── ClusterAnalyzer.cs
        ├── ClusterResult.cs
        └── ClusterOptions.cs

然后 CandidateFinder 可以使用它：
ClusterAnalyzer
    ↓
ClusterAwareCandidateFinder
    ↓
返回 TileCandidate / BehaviourGroup

也就是：
簇分析本身是 Analysis；
簇参与候选生成时，才进入 CandidateFinding。


12. 剪枝规则
剪枝不要散落在 FSE、UnlockPath、Cluster 里面。
推荐抽象：
public interface ITileCandidatePruner
{
    int Prune(
        SimulationContext context,
        Span<TileCandidate> candidates);
}

public interface IBehaviourGroupPruner
{
    int Prune(
        SimulationContext context,
        Span<BehaviourGroup> candidates);
}

组合剪枝：
public sealed class CompositeBehaviourGroupPruner : IBehaviourGroupPruner
{
    private readonly IBehaviourGroupPruner[] _pruners;

    public CompositeBehaviourGroupPruner(
        IBehaviourGroupPruner[] pruners)
    {
        _pruners = pruners;
    }

    public int Prune(
        SimulationContext context,
        Span<BehaviourGroup> candidates)
    {
        int count = candidates.Length;

        for (int i = 0; i < _pruners.Length; i++)
        {
            count = _pruners[i].Prune(
                context,
                candidates.Slice(0, count));
        }

        return count;
    }
}

常见剪枝：
DuplicateCandidatePruner
MaxCandidateCountPruner
DeadEndPruner
UnlockPathDepthPruner
ClusterNoisePruner
SameColorLimitPruner


13. 广义消除寻找
广义消除寻找可以理解为：
先找 Tile 候选
再组合成 BehaviourGroup
再剪枝
最后返回 BehaviourGroup 候选

这不应该硬塞进 FSE。
它应该是组合型 Finder：
GeneralEliminationBehaviourGroupFinder
    使用 ITileCandidateFinder
    使用 ITileCandidateCombiner
    使用 IBehaviourGroupPruner
    输出 BehaviourGroup

示例：
public sealed class GeneralEliminationBehaviourGroupFinder
    : IBehaviourGroupFinder
{
    private readonly ITileCandidateFinder _tileFinder;
    private readonly ITileCandidateCombiner _combiner;
    private readonly IBehaviourGroupPruner _pruner;

    private readonly TileCandidate[] _tileBuffer;

    public GeneralEliminationBehaviourGroupFinder(
        ITileCandidateFinder tileFinder,
        ITileCandidateCombiner combiner,
        IBehaviourGroupPruner pruner,
        int maxTileCandidateCount)
    {
        _tileFinder = tileFinder;
        _combiner = combiner;
        _pruner = pruner;
        _tileBuffer = new TileCandidate[maxTileCandidateCount];
    }

    public int Find(
        SimulationContext context,
        Span<BehaviourGroup> output)
    {
        int tileCount = _tileFinder.Find(
            context,
            _tileBuffer);

        int groupCount = _combiner.Combine(
            context,
            _tileBuffer.AsSpan(0, tileCount),
            output);

        return _pruner.Prune(
            context,
            output.Slice(0, groupCount));
    }
}

这样可以表达：
Tile 解锁路径
    ↓
Tile 候选组合
    ↓
广义消除行为组


14. Scorer 边界
Core 可以提供一个极薄的默认评分器：
public sealed class DefaultCandidateScorer : ICandidateScorer
{
    public double Score(
        SimulationContext context,
        BehaviourGroup candidate)
    {
        return 0.0;
    }
}

或者简单 baseline：
public sealed class SimpleCandidateScorer : ICandidateScorer
{
    public double Score(
        SimulationContext context,
        BehaviourGroup candidate)
    {
        double score = 0;

        score -= candidate.Count * 0.1;

        return score;
    }
}

但这些只能是：
默认 baseline
简单可用
业务无关

不要放入 Core 的评分：
PiKaCandidateScorer
TokikiCandidateScorer
FeatureCandidateScorer
难度实验评分
失败率实验评分

这些属于 Services。

15. SimulationPolicy
SimulationPolicy 组合：
Finder
Pruner
Scorer
SelectionStrategy

推荐接口：
public interface ISimulationPolicy
{
    BehaviourGroup NextBehaviourGroup(
        SimulationContext context);
}

默认实现：
public sealed class DefaultSimulationPolicy : ISimulationPolicy
{
    private readonly IBehaviourGroupFinder _finder;
    private readonly IBehaviourGroupPruner _pruner;
    private readonly ICandidateScorer _scorer;
    private readonly ICandidateSelectionStrategy _strategy;
    private readonly IPolicyObserver _observer;

    private readonly BehaviourGroup[] _candidateBuffer;
    private readonly double[] _scoreBuffer;

    public DefaultSimulationPolicy(
        IBehaviourGroupFinder finder,
        IBehaviourGroupPruner pruner,
        ICandidateScorer scorer,
        ICandidateSelectionStrategy strategy,
        int maxCandidateCount,
        IPolicyObserver? observer = null)
    {
        _finder = finder;
        _pruner = pruner;
        _scorer = scorer;
        _strategy = strategy;
        _observer = observer ?? EmptyPolicyObserver.Instance;

        _candidateBuffer = new BehaviourGroup[maxCandidateCount];
        _scoreBuffer = new double[maxCandidateCount];
    }

    public BehaviourGroup NextBehaviourGroup(
        SimulationContext context)
    {
        _observer.OnCandidateFindingStarted(context);

        int count = _finder.Find(
            context,
            _candidateBuffer);

        _observer.OnCandidateFindingCompleted(
            context,
            count);

        if (count <= 0)
        {
            _observer.OnNoCandidate(context);
            return BehaviourGroup.Empty;
        }

        int beforePruneCount = count;

        count = _pruner.Prune(
            context,
            _candidateBuffer.AsSpan(0, count));

        _observer.OnCandidatePruningCompleted(
            context,
            beforePruneCount,
            count);

        if (count <= 0)
        {
            _observer.OnNoCandidate(context);
            return BehaviourGroup.Empty;
        }

        for (int i = 0; i < count; i++)
        {
            double score = _scorer.Score(
                context,
                _candidateBuffer[i]);

            _scoreBuffer[i] = score;

            _observer.OnCandidateScored(
                context,
                i,
                score);
        }

        int selectedIndex = _strategy.Select(
            _scoreBuffer.AsSpan(0, count),
            context.Random);

        double selectedScore = _scoreBuffer[selectedIndex];

        _observer.OnCandidateSelected(
            context,
            selectedIndex,
            selectedScore);

        return _candidateBuffer[selectedIndex];
    }
}


16. SelectionStrategy
选择策略负责从候选分数中选一个。
接口：
public interface ICandidateSelectionStrategy
{
    int Select(
        ReadOnlySpan<double> scores,
        Random random);
}

常见实现：
GreedySelectionStrategy
RandomSelectionStrategy
SoftmaxSelectionStrategy

注意：
Strategy 只负责选择。
Strategy 不负责找候选。
Strategy 不负责评分。
Strategy 不负责执行 Move。


17. ISimulationObserver：主流程事件
ISimulationObserver 观察 Runner 主流程。
它关心：
批量开始
单次开始
行为组前
Move 前
Move 后
行为组后
单次完成
批量完成

接口：
public interface ISimulationObserver
{
    void OnBatchStarted(
        SimulationBatchContext context);

    void OnRunStarted(
        SimulationContext context);

    void OnBeforeBehaviourGroup(
        SimulationContext context,
        BehaviourGroup group);

    void OnBeforeMove(
        SimulationContext context,
        Move move);

    void OnAfterMove(
        SimulationContext context,
        Move move);

    void OnAfterBehaviourGroup(
        SimulationContext context,
        BehaviourGroup group);

    void OnRunCompleted(
        SimulationContext context,
        SimulationRunResult result);

    void OnBatchCompleted(
        SimulationBatchResult result);
}

它不关心：
Finder 返回了多少候选
Pruner 剪掉了多少候选
每个候选评分是多少
最终选择了哪个候选

这些属于 IPolicyObserver。

18. IPolicyObserver：策略事件
IPolicyObserver 观察 policy.NextBehaviourGroup(context) 的内部决策过程。
接口：
public interface IPolicyObserver
{
    void OnCandidateFindingStarted(
        SimulationContext context);

    void OnCandidateFindingCompleted(
        SimulationContext context,
        int candidateCount);

    void OnCandidatePruningCompleted(
        SimulationContext context,
        int beforeCount,
        int afterCount);

    void OnCandidateScored(
        SimulationContext context,
        int candidateIndex,
        double score);

    void OnCandidateSelected(
        SimulationContext context,
        int selectedIndex,
        double selectedScore);

    void OnNoCandidate(
        SimulationContext context);
}

这套接口覆盖：
候选寻找开始
候选寻找完成
剪枝前后数量
候选评分
最终选择
无候选

不要写成：
OnFseCandidateFound
OnPiKaScore
OnTokikiScore
OnUnlockPathFound

因为这些会把 Core 接口绑定到具体算法。
Core 应该观察通用阶段：
Find
Prune
Score
Select
NoCandidate


19. ObserverBase
为了减少空方法，实现 base class：
public abstract class SimulationObserverBase : ISimulationObserver
{
    public virtual void OnBatchStarted(
        SimulationBatchContext context) { }

    public virtual void OnRunStarted(
        SimulationContext context) { }

    public virtual void OnBeforeBehaviourGroup(
        SimulationContext context,
        BehaviourGroup group) { }

    public virtual void OnBeforeMove(
        SimulationContext context,
        Move move) { }

    public virtual void OnAfterMove(
        SimulationContext context,
        Move move) { }

    public virtual void OnAfterBehaviourGroup(
        SimulationContext context,
        BehaviourGroup group) { }

    public virtual void OnRunCompleted(
        SimulationContext context,
        SimulationRunResult result) { }

    public virtual void OnBatchCompleted(
        SimulationBatchResult result) { }
}

public abstract class PolicyObserverBase : IPolicyObserver
{
    public virtual void OnCandidateFindingStarted(
        SimulationContext context) { }

    public virtual void OnCandidateFindingCompleted(
        SimulationContext context,
        int candidateCount) { }

    public virtual void OnCandidatePruningCompleted(
        SimulationContext context,
        int beforeCount,
        int afterCount) { }

    public virtual void OnCandidateScored(
        SimulationContext context,
        int candidateIndex,
        double score) { }

    public virtual void OnCandidateSelected(
        SimulationContext context,
        int selectedIndex,
        double selectedScore) { }

    public virtual void OnNoCandidate(
        SimulationContext context) { }
}


20. Empty Observer
空实现避免主流程中到处判断 null。
public sealed class EmptySimulationObserver : SimulationObserverBase
{
    public static readonly EmptySimulationObserver Instance = new();

    private EmptySimulationObserver()
    {
    }
}

public sealed class EmptyPolicyObserver : PolicyObserverBase
{
    public static readonly EmptyPolicyObserver Instance = new();

    private EmptyPolicyObserver()
    {
    }
}


21. Composite Observer
多个观察器组合时，不让 Runner 或 Policy 知道多个 Observer。
public sealed class CompositeSimulationObserver : ISimulationObserver
{
    private readonly ISimulationObserver[] _observers;

    public CompositeSimulationObserver(
        ISimulationObserver[] observers)
    {
        _observers = observers;
    }

    public void OnBatchStarted(
        SimulationBatchContext context)
    {
        foreach (var observer in _observers)
            observer.OnBatchStarted(context);
    }

    public void OnRunStarted(
        SimulationContext context)
    {
        foreach (var observer in _observers)
            observer.OnRunStarted(context);
    }

    public void OnBeforeBehaviourGroup(
        SimulationContext context,
        BehaviourGroup group)
    {
        foreach (var observer in _observers)
            observer.OnBeforeBehaviourGroup(context, group);
    }

    public void OnBeforeMove(
        SimulationContext context,
        Move move)
    {
        foreach (var observer in _observers)
            observer.OnBeforeMove(context, move);
    }

    public void OnAfterMove(
        SimulationContext context,
        Move move)
    {
        foreach (var observer in _observers)
            observer.OnAfterMove(context, move);
    }

    public void OnAfterBehaviourGroup(
        SimulationContext context,
        BehaviourGroup group)
    {
        foreach (var observer in _observers)
            observer.OnAfterBehaviourGroup(context, group);
    }

    public void OnRunCompleted(
        SimulationContext context,
        SimulationRunResult result)
    {
        foreach (var observer in _observers)
            observer.OnRunCompleted(context, result);
    }

    public void OnBatchCompleted(
        SimulationBatchResult result)
    {
        foreach (var observer in _observers)
            observer.OnBatchCompleted(result);
    }
}

PolicyObserver 同理：
public sealed class CompositePolicyObserver : IPolicyObserver
{
    private readonly IPolicyObserver[] _observers;

    public CompositePolicyObserver(
        IPolicyObserver[] observers)
    {
        _observers = observers;
    }

    public void OnCandidateFindingStarted(
        SimulationContext context)
    {
        foreach (var observer in _observers)
            observer.OnCandidateFindingStarted(context);
    }

    public void OnCandidateFindingCompleted(
        SimulationContext context,
        int candidateCount)
    {
        foreach (var observer in _observers)
            observer.OnCandidateFindingCompleted(context, candidateCount);
    }

    public void OnCandidatePruningCompleted(
        SimulationContext context,
        int beforeCount,
        int afterCount)
    {
        foreach (var observer in _observers)
            observer.OnCandidatePruningCompleted(context, beforeCount, afterCount);
    }

    public void OnCandidateScored(
        SimulationContext context,
        int candidateIndex,
        double score)
    {
        foreach (var observer in _observers)
            observer.OnCandidateScored(context, candidateIndex, score);
    }

    public void OnCandidateSelected(
        SimulationContext context,
        int selectedIndex,
        double selectedScore)
    {
        foreach (var observer in _observers)
            observer.OnCandidateSelected(context, selectedIndex, selectedScore);
    }

    public void OnNoCandidate(
        SimulationContext context)
    {
        foreach (var observer in _observers)
            observer.OnNoCandidate(context);
    }
}


22. 事件异常处理
事件不能影响主流程。
原则：
Observer 抛异常
    不影响模拟
    不影响 Finder
    不影响 Scorer
    不影响 Strategy
    不影响 Runner

可以在 CompositeObserver 中保护：
public void OnCandidateScored(
    SimulationContext context,
    int candidateIndex,
    double score)
{
    foreach (var observer in _observers)
    {
        try
        {
            observer.OnCandidateScored(
                context,
                candidateIndex,
                score);
        }
        catch
        {
            // Observer 不影响主流程
        }
    }
}

为了减少委托分配，不建议热路径使用：
SafeInvoke(Action action)

优先直接写 try/catch。

23. 为什么不用 EventManager
不建议第一版使用：
eventManager.Emit("policy.candidate.found", payload);

原因：
字符串事件名容易写错
payload 类型不稳定
热路径可能产生额外分配
IDE 不好跳转
重构不安全
后期容易变成小型消息总线

当前项目更适合：
强类型接口
稳定阶段事件
低分配
好重构
好跳转

因此：
主流程事件用 ISimulationObserver
策略事件用 IPolicyObserver
不要使用全局 EventManager


24. 是否暴露完整候选内容
第一版不建议基础 IPolicyObserver 暴露完整候选。
不要一开始就这样：
void OnCandidateFound(
    SimulationContext context,
    BehaviourGroup candidate);

原因：
BehaviourGroup 可能持有临时 buffer
候选内容可能比较重
Observer 如果保存引用，可能造成生命周期问题
热路径容易被拖慢

第一版只暴露：
candidateCount
candidateIndex
score
selectedIndex
selectedScore

这已经足够支持：
候选数量分布
剪枝比例
评分分布
选择倾向
无候选统计


25. Debug Snapshot 扩展
如果后续确实需要观察完整候选内容，可以新增调试接口：
public interface IPolicyCandidateSnapshotObserver : IPolicyObserver
{
    void OnCandidatesAvailable(
        SimulationContext context,
        ReadOnlySpan<BehaviourGroup> candidates);

    void OnCandidateScoresAvailable(
        SimulationContext context,
        ReadOnlySpan<BehaviourGroup> candidates,
        ReadOnlySpan<double> scores);
}

Policy 内部可选调用：
if (_observer is IPolicyCandidateSnapshotObserver snapshotObserver)
{
    snapshotObserver.OnCandidatesAvailable(
        context,
        _candidateBuffer.AsSpan(0, count));
}

评分后：
if (_observer is IPolicyCandidateSnapshotObserver snapshotObserver)
{
    snapshotObserver.OnCandidateScoresAvailable(
        context,
        _candidateBuffer.AsSpan(0, count),
        _scoreBuffer.AsSpan(0, count));
}

约束：
ReadOnlySpan 只能当场读取
Observer 不应该保存 Span
Snapshot 主要用于调试
不要作为基础指标通道


26. 进度条如何接入
有了事件后，进度条就是一个普通 ISimulationObserver。
它不需要改 Runner。
进度条最适合监听：
OnBatchStarted
OnRunCompleted
OnBatchCompleted

不要监听：
OnBeforeMove
OnAfterMove

因为 Move 事件太频繁，频繁刷新控制台会拖慢热路径。
示例：
public sealed class TileEvalProgressObserver : SimulationObserverBase
{
    private int _total;
    private int _completed;
    private int _success;
    private int _failure;

    public override void OnBatchStarted(
        SimulationBatchContext context)
    {
        _total = context.TotalSimulationCount;
        _completed = 0;
        _success = 0;
        _failure = 0;
    }

    public override void OnRunCompleted(
        SimulationContext context,
        SimulationRunResult result)
    {
        _completed++;

        if (result.Success)
            _success++;
        else
            _failure++;

        PrintProgress();
    }

    public override void OnBatchCompleted(
        SimulationBatchResult result)
    {
        Console.WriteLine();
    }

    private void PrintProgress()
    {
        double percent = _total == 0
            ? 1.0
            : (double)_completed / _total;

        Console.Write(
            $"\r模拟进度: {_completed}/{_total} " +
            $"({percent:P1}) 成功={_success} 失败={_failure}");
    }
}


27. 指标如何接入
指标分两类：
过程指标
    通过 Observer 收集

结果指标
    模拟结束后统一计算

过程指标例如：
move_count
behaviour_group_count
success_count
failure_count
candidate_count
pruned_count
score_distribution
no_candidate_count

这些可以通过：
ISimulationObserver
IPolicyObserver

收集。
结果指标例如：
fail_rate
avg_move_count
avg_elapsed_ms
avg_candidate_count
avg_score

这些可以在模拟结束后由 Services 的 MetricBinder 计算。

28. MetricBag
Core 可以提供基础 MetricBag。
但具体指标计算和绑定放 Services。
public sealed class MetricBag
{
    private readonly Dictionary<string, int> _intMetrics = new();
    private readonly Dictionary<string, double> _doubleMetrics = new();

    public void AddInt(
        string name,
        int value)
    {
        if (_intMetrics.TryGetValue(name, out int old))
            _intMetrics[name] = old + value;
        else
            _intMetrics[name] = value;
    }

    public void AddDouble(
        string name,
        double value)
    {
        if (_doubleMetrics.TryGetValue(name, out double old))
            _doubleMetrics[name] = old + value;
        else
            _doubleMetrics[name] = value;
    }

    public bool TryGetInt(
        string name,
        out int value)
    {
        return _intMetrics.TryGetValue(name, out value);
    }

    public bool TryGetDouble(
        string name,
        out double value)
    {
        return _doubleMetrics.TryGetValue(name, out value);
    }
}

Core 只提供容器能力。
Services 决定：
有哪些指标
指标怎么命名
指标怎么绑定
指标怎么导出


29. SimulationRunResult
单次模拟稳定结果：
public readonly struct SimulationRunResult
{
    public readonly SimulationContext Context;
    public readonly int SimulationIndex;
    public readonly bool Success;
    public readonly int MoveCount;
    public readonly double ElapsedMilliseconds;

    public SimulationRunResult(
        SimulationContext context,
        int simulationIndex,
        bool success,
        int moveCount,
        double elapsedMilliseconds)
    {
        Context = context;
        SimulationIndex = simulationIndex;
        Success = success;
        MoveCount = moveCount;
        ElapsedMilliseconds = elapsedMilliseconds;
    }
}


30. SimulationBatchResult
批量结果：
public sealed class SimulationBatchResult
{
    private readonly SimulationRunResult[] _runs;
    private int _count;

    public int Count => _count;

    public int SuccessCount { get; private set; }

    public int FailureCount => _count - SuccessCount;

    public double SuccessRate =>
        _count == 0 ? 0 : (double)SuccessCount / _count;

    public double FailRate =>
        _count == 0 ? 0 : (double)FailureCount / _count;

    public MetricBag Metrics { get; } = new();

    public SimulationBatchResult(
        int capacity)
    {
        _runs = new SimulationRunResult[capacity];
    }

    public void Add(
        SimulationRunResult result)
    {
        _runs[_count++] = result;

        if (result.Success)
            SuccessCount++;
    }

    public ReadOnlySpan<SimulationRunResult> Runs =>
        _runs.AsSpan(0, _count);
}

稳定字段直接做属性：
Count
SuccessCount
FailureCount
SuccessRate
FailRate
Runs

实验性指标放 MetricBag。

31. Services 如何组装
Services 中的 TileEvalService 可以这样组装：
public sealed class TileEvalService
{
    public TileEvalResult Run(
        TileEvalRequest request)
    {
        var metricBag = new MetricBag();

        var simulationObserver = new CompositeSimulationObserver(
            new ISimulationObserver[]
            {
                request.EnableProgress
                    ? new TileEvalProgressObserver()
                    : EmptySimulationObserver.Instance,

                new TileEvalRunMetricsObserver(metricBag)
            });

        var policyObserver = new CompositePolicyObserver(
            new IPolicyObserver[]
            {
                new TileEvalPolicyMetricsObserver(metricBag),
                request.EnableDebugTrace
                    ? new DebugPolicyTraceObserver()
                    : EmptyPolicyObserver.Instance
            });

        var policy = BuildPolicy(
            request,
            policyObserver);

        var runner = new SimulationRunner();

        var simulationResult = runner.Simulate(
            request.Level,
            request.SimulationCount,
            policy,
            simulationObserver);

        TileEvalMetricBinder.Bind(
            simulationResult,
            metricBag,
            request.EnabledMetrics);

        return new TileEvalResult
        {
            SimulationResult = simulationResult,
            Metrics = metricBag
        };
    }
}

职责清晰：
Core:
    提供 Runner / Policy / Observer 接口

Services:
    组装具体 Policy
    组装具体 Observer
    绑定具体指标

CLI:
    决定配置和输出


32. 最终推荐事件层级
推荐三层，但当前只实现前两层：
第一层：ISimulationObserver
    观察 Runner 主流程

第二层：IPolicyObserver
    观察策略决策过程

第三层：IFinderObserver / IScorerObserver
    暂时不做
    只有当 FSE 或 Scorer 内部复杂到需要单独观察时再加

当前阶段不要提前做：
GlobalEventManager
EventBus
Dictionary<string, object>
万能事件系统


33. 最终边界总结
Core 沉淀
SimulationRunner
SimulationContext
SimulationBatchContext
SimulationRunResult
SimulationBatchResult

ISimulationObserver
SimulationObserverBase
EmptySimulationObserver
CompositeSimulationObserver

ISimulationPolicy
DefaultSimulationPolicy

IPolicyObserver
PolicyObserverBase
EmptyPolicyObserver
CompositePolicyObserver

TileCandidate
BehaviourGroup

ITileCandidateFinder
IBehaviourGroupFinder

FseBehaviourGroupFinder
TileUnlockPathCandidateFinder
ClassicUnlockPathCandidateFinder
GeneralEliminationBehaviourGroupFinder

ITileCandidatePruner
IBehaviourGroupPruner
CompositePruner

ICandidateScorer
DefaultCandidateScorer

ICandidateSelectionStrategy
GreedySelectionStrategy
SoftmaxSelectionStrategy

MetricBag


Services 接管
PiKaCandidateScorer
TokikiCandidateScorer
FeatureCandidateScorer

TileEvalService
TileEvalBatchService
TileEvalRequest
TileEvalResult

TileEvalProgressObserver
TileEvalRunMetricsObserver
TileEvalPolicyMetricsObserver
DebugPolicyTraceObserver

TileEvalMetricBinder
TileEvalMetricCatalog
TileEvalMetricSelection

TileEvalPolicyFactory


CLI 保留
TOML
命令行参数
metrics.toml
outputs.order
CSV writer
ConsoleReporter
配置合并
文件输入输出


34. 最终一句话
SimulationRunner 只跑主流程；
SimulationPolicy 决定下一组行为；
ISimulationObserver 观察主流程；
IPolicyObserver 观察策略内部决策；
Core 沉淀业务无关能力；
Services 组合具体指标、进度条、调试和业务评分；
CLI 处理配置和输出。

这个结构的优势是：
主流程稳定
候选发现正交
策略决策可观察
指标不污染 Core
进度条容易接入
调试能力可扩展
后续性能优化路径清晰

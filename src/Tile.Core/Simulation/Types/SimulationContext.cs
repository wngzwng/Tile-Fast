using Tile.Core.Core;

namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 运行时上下文。Hook 可读取该对象观察 batch、run 和当前 step 状态。
/// </summary>
public sealed class SimulationContext
{
    private readonly LevelCore _initialLevel;

    public SimulationContext(
        LevelCore sourceLevel,
        int simulationCount,
        Random random,
        SimulationCandidateMode candidateMode = SimulationCandidateMode.Tile)
    {
        if (simulationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(simulationCount));

        _initialLevel = sourceLevel?.Clone() ?? throw new ArgumentNullException(nameof(sourceLevel));
        SourceLevel = _initialLevel.Clone();
        SimulationCount = simulationCount;
        Random = random ?? throw new ArgumentNullException(nameof(random));
        CandidateMode = candidateMode;
        Candidates = CreateCandidates(
            candidateMode,
            sourceLevel.Mapping.TileCount);
    }

    #region Batch Context

    /// <summary>
    /// 当前 run 正在模拟的关卡副本。
    /// </summary>
    public LevelCore SourceLevel { get; private set; }

    /// <summary>
    /// 当前 run 的 0-based 序号；batch 尚未开始 run 时为 -1。
    /// </summary>
    public int SimulationIndex { get; private set; } = -1;

    /// <summary>
    /// 当前 run 的 1-based 序号。
    /// </summary>
    public int SimulationNumber => SimulationIndex + 1;

    /// <summary>
    /// 本 batch 计划执行的 run 数量。
    /// </summary>
    public int SimulationCount { get; }

    /// <summary>
    /// 当前 batch 已完成的成功 run 数量。
    /// </summary>
    public int SuccessCount { get; private set; }

    /// <summary>
    /// 当前 batch 已完成的失败 run 数量。
    /// </summary>
    public int FailureCount { get; private set; }

    /// <summary>
    /// 当前模拟候选容器。
    /// </summary>
    public ISimulationCandidateSet Candidates { get; }

    /// <summary>
    /// 当前模拟 tile 候选容器；仅 Tile 模式可用。
    /// </summary>
    public SimulationCandidateSet<int> TileCandidates =>
        Candidates as SimulationCandidateSet<int>
        ?? throw new InvalidOperationException("Current simulation candidate mode is not Tile.");

    /// <summary>
    /// 当前模拟 behaviour 候选容器；仅 Behaviour 模式可用。
    /// </summary>
    public SimulationCandidateSet<Behaviour> BehaviourCandidates =>
        Candidates as SimulationCandidateSet<Behaviour>
        ?? throw new InvalidOperationException("Current simulation candidate mode is not Behaviour.");

    /// <summary>
    /// 当前模拟候选来源模式。
    /// </summary>
    public SimulationCandidateMode CandidateMode { get; }

    /// <summary>
    /// 当前 batch 使用的随机数生成器。
    /// </summary>
    public Random Random { get; }

    public IReadOnlyList<int> CandidateBuffer => TileCandidates.Items;

    /// <summary>
    /// 复用的候选 tile 列表；仅用于 Tile 候选模式。
    /// </summary>
    public IReadOnlyList<int> SelectableBuffer => CandidateBuffer;

    internal void StartRun(int simulationIndex)
    {
        if ((uint)simulationIndex >= (uint)SimulationCount)
            throw new ArgumentOutOfRangeException(nameof(simulationIndex));

        if (SuccessCount + FailureCount != simulationIndex)
            throw new InvalidOperationException("Simulation context is not aligned with the next run.");

        SimulationIndex = simulationIndex;
        RunStatus = LevelRunStatus.Pending;
        MoveCount = 0;
        Candidates.Clear();
        SourceLevel = _initialLevel.Clone();
    }

    internal void CompleteRun(SimulationRunMetrics metrics)
    {
        if (metrics.IsFailed)
            FailureCount++;
        else
            SuccessCount++;
    }

    #endregion

    #region Current Run State

    public LevelRunStatus RunStatus { get; private set; } =
        LevelRunStatus.Pending;

    /// <summary>
    /// 当前 step 收集到的候选数量。
    /// </summary>
    public int CandidateCount => Candidates.Count;

    /// <summary>
    /// 当前 run 已执行的行为数量。
    /// </summary>
    public int MoveCount { get; private set; }

    /// <summary>
    /// 当前已选候选在 <see cref="CandidateBuffer"/> 中的 offset；未选定时为 -1。
    /// </summary>
    public int SelectedCandidateOffset => Candidates.SelectedOffset;

    /// <summary>
    /// 当前 step 选中的候选值；未选定时为 -1。
    /// 候选值的含义由 <see cref="CandidateMode"/> 决定。
    /// </summary>
    public int SelectedCandidateValue =>
        CandidateMode == SimulationCandidateMode.Tile &&
        TileCandidates.TryGetSelectedItem(out var item)
            ? item
            : -1;

    /// <summary>
    /// 当前 step 选中的 tileIndex；仅在 Tile 候选模式下有意义，未选定时为 -1。
    /// </summary>
    public int SelectedTileIndex => SelectedCandidateValue;

    internal void SetSelectedCandidateOffset(int selectedCandidateOffset)
    {
        if (Candidates is SimulationCandidateSet<int> tileCandidates)
            tileCandidates.SetSelectedOffset(selectedCandidateOffset);
        else if (Candidates is SimulationCandidateSet<Behaviour> behaviourCandidates)
            behaviourCandidates.SetSelectedOffset(selectedCandidateOffset);
        else
            throw new InvalidOperationException("Current simulation candidate container is unsupported.");
    }

    internal void ClearSelectedCandidate()
    {
        Candidates.ClearSelected();
    }

    internal void MarkSuccess()
    {
        RunStatus = LevelRunStatus.Success;
    }

    internal void MarkFailure()
    {
        RunStatus = LevelRunStatus.Failure;
    }

    internal void IncreaseMoveCount()
    {
        MoveCount++;
    }

    #endregion

    private static ISimulationCandidateSet CreateCandidates(
        SimulationCandidateMode candidateMode,
        int tileCandidateCapacity)
    {
        return candidateMode switch
        {
            SimulationCandidateMode.Tile => new SimulationCandidateSet<int>(
                SimulationCandidateMode.Tile,
                tileCandidateCapacity),
            SimulationCandidateMode.Behaviour => new SimulationCandidateSet<Behaviour>(
                SimulationCandidateMode.Behaviour),
            _ => throw new ArgumentOutOfRangeException(nameof(candidateMode)),
        };
    }
}

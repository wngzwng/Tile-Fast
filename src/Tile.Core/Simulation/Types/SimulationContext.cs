using Tile.Core.Core;

namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 的可复用运行上下文，记录当前 batch、run 和 step 的状态。
/// </summary>
public sealed class SimulationContext : IDisposable
{
    private LevelCore? _initialLevel;
    private LevelCore? _sourceLevel;
    private ISimulationCandidateSet? _candidates;

    public SimulationContext()
    {
    }

    public SimulationContext(
        LevelCore sourceLevel,
        int simulationCount,
        Random random,
        SimulationCandidateMode candidateMode = SimulationCandidateMode.Tile)
    {
        ResetBatch(
            sourceLevel,
            simulationCount,
            random,
            candidateMode);
    }

    /// <summary>
    /// 当前上下文是否已经通过 <see cref="ResetBatch"/> 完成 batch 初始化。
    /// </summary>
    public bool IsInitialized { get; private set; }

    #region Batch State

    /// <summary>
    /// 当前 batch 计划执行的 run 数量。
    /// </summary>
    public int SimulationCount { get; private set; }

    /// <summary>
    /// 当前 run 的 0-based 序号；batch 尚未开始 run 时为 -1。
    /// </summary>
    public int SimulationIndex { get; private set; } = -1;

    /// <summary>
    /// 当前 run 的 1-based 序号；batch 尚未开始 run 时为 0。
    /// </summary>
    public int SimulationNumber => SimulationIndex + 1;

    /// <summary>
    /// 当前 batch 已完成的成功 run 数量。
    /// </summary>
    public int SuccessCount { get; private set; }

    /// <summary>
    /// 当前 batch 已完成的失败 run 数量。
    /// </summary>
    public int FailureCount { get; private set; }

    /// <summary>
    /// 当前 batch 使用的随机数生成器。
    /// </summary>
    public Random Random { get; private set; } = null!;

    #endregion

    #region Run State

    /// <summary>
    /// 当前 run 正在模拟的关卡副本。
    /// </summary>
    public LevelCore SourceLevel =>
        _sourceLevel ?? throw CreateNotInitializedException();

    /// <summary>
    /// 当前 run 的状态。
    /// </summary>
    public LevelRunStatus RunStatus { get; private set; } =
        LevelRunStatus.Pending;

    /// <summary>
    /// 当前 run 已执行的行为数量。
    /// </summary>
    public int MoveCount { get; private set; }

    #endregion

    #region Candidate State

    /// <summary>
    /// 当前模拟候选来源模式。
    /// </summary>
    public SimulationCandidateMode CandidateMode { get; private set; }

    /// <summary>
    /// 当前 step 的候选快照。
    /// </summary>
    public ISimulationCandidateSet Candidates =>
        _candidates ?? throw CreateNotInitializedException();

    /// <summary>
    /// 当前 step 的 tile 候选快照；仅 Tile 模式可用。
    /// </summary>
    public SimulationCandidateSet<int> TileCandidates =>
        Candidates as SimulationCandidateSet<int>
        ?? throw new InvalidOperationException("Current simulation candidate mode is not Tile.");

    /// <summary>
    /// 当前 step 的 behaviour 候选快照；仅 Behaviour 模式可用。
    /// </summary>
    public BehaviourCandidateSet BehaviourCandidates =>
        Candidates as BehaviourCandidateSet
        ?? throw new InvalidOperationException("Current simulation candidate mode is not Behaviour.");

    /// <summary>
    /// 当前 step 收集到的候选数量；未初始化时为 0。
    /// </summary>
    public int CandidateCount => _candidates?.Count ?? 0;

    /// <summary>
    /// 当前已选候选在 <see cref="Candidates"/> 中的 offset；未选定时为 -1。
    /// </summary>
    public int SelectedCandidateOffset => _candidates?.SelectedOffset ?? -1;

    /// <summary>
    /// 当前 step 是否已经选定候选。
    /// </summary>
    public bool HasSelectedCandidate => SelectedCandidateOffset >= 0;

    /// <summary>
    /// 当前 step 选中的 tileIndex；仅 Tile 模式有意义，未选定时为 -1。
    /// </summary>
    public int SelectedTileIndex =>
        _candidates is SimulationCandidateSet<int> tileCandidates &&
        tileCandidates.TryGetSelectedItem(out var item)
            ? item
            : -1;

    /// <summary>
    /// 当前 step 选中的 Behaviour；仅 Behaviour 模式有意义，未选定时为 null。
    /// </summary>
    public Behaviour? SelectedBehaviour =>
        _candidates is BehaviourCandidateSet behaviourCandidates &&
        behaviourCandidates.TryGetSelectedItem(out var behaviour)
            ? behaviour
            : null;

    #endregion

    /// <summary>
    /// 初始化或重置一个 batch。可复用同一个 context 连续执行多个 batch。
    /// </summary>
    public void ResetBatch(
        LevelCore sourceLevel,
        int simulationCount,
        Random random,
        SimulationCandidateMode candidateMode = SimulationCandidateMode.Tile)
    {
        if (simulationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(simulationCount));

        var level = sourceLevel ?? throw new ArgumentNullException(nameof(sourceLevel));

        _initialLevel = level.Clone();
        _sourceLevel = _initialLevel.Clone();
        if (_candidates is not null && _candidates.Mode != candidateMode)
        {
            if (_candidates is IDisposable disposable)
                disposable.Dispose();
            else
                _candidates.Clear();

            _candidates = null;
        }

        _candidates = GetOrCreateCandidateSet(
            candidateMode,
            level.Mapping.TileCount);
        _candidates.Clear();

        SimulationCount = simulationCount;
        SimulationIndex = -1;
        SuccessCount = 0;
        FailureCount = 0;
        Random = random ?? throw new ArgumentNullException(nameof(random));

        CandidateMode = candidateMode;
        RunStatus = LevelRunStatus.Pending;
        MoveCount = 0;
        IsInitialized = true;
    }

    internal void StartRun(int simulationIndex)
    {
        EnsureInitialized();

        if ((uint)simulationIndex >= (uint)SimulationCount)
            throw new ArgumentOutOfRangeException(nameof(simulationIndex));

        if (SuccessCount + FailureCount != simulationIndex)
            throw new InvalidOperationException("Simulation context is not aligned with the next run.");

        SimulationIndex = simulationIndex;
        RunStatus = LevelRunStatus.Pending;
        MoveCount = 0;
        Candidates.Clear();

        // 每个 run 都从初始关卡 clone，避免前一次模拟污染本次 run。
        _sourceLevel = _initialLevel!.Clone();
    }

    internal void CompleteRun(SimulationRunMetrics metrics)
    {
        EnsureInitialized();

        if (metrics.IsFailed)
            FailureCount++;
        else
            SuccessCount++;
    }

    internal void SetSelectedCandidateOffset(int selectedCandidateOffset)
    {
        EnsureInitialized();
        Candidates.SetSelectedOffset(selectedCandidateOffset);
    }

    internal void ClearSelectedCandidate()
    {
        EnsureInitialized();
        Candidates.ClearSelected();
    }

    internal void MarkSuccess()
    {
        EnsureInitialized();
        RunStatus = LevelRunStatus.Success;
    }

    internal void MarkFailure()
    {
        EnsureInitialized();
        RunStatus = LevelRunStatus.Failure;
    }

    internal void IncreaseMoveCount()
    {
        EnsureInitialized();
        MoveCount++;
    }

    public void Dispose()
    {
        if (_candidates is IDisposable disposable)
            disposable.Dispose();
        else
            _candidates?.Clear();
    }

    internal void EnsureInitialized()
    {
        if (!IsInitialized)
            throw CreateNotInitializedException();
    }

    private ISimulationCandidateSet GetOrCreateCandidateSet(
        SimulationCandidateMode candidateMode,
        int tileCandidateCapacity)
    {
        if (_candidates?.Mode == candidateMode)
            return _candidates;

        return CreateCandidateSet(
            candidateMode,
            tileCandidateCapacity);
    }

    private static ISimulationCandidateSet CreateCandidateSet(
        SimulationCandidateMode candidateMode,
        int tileCandidateCapacity)
    {
        return candidateMode switch
        {
            SimulationCandidateMode.Tile => new SimulationCandidateSet<int>(
                SimulationCandidateMode.Tile,
                tileCandidateCapacity),
            SimulationCandidateMode.Behaviour => new BehaviourCandidateSet(),
            _ => throw new ArgumentOutOfRangeException(nameof(candidateMode)),
        };
    }

    private static InvalidOperationException CreateNotInitializedException()
    {
        return new InvalidOperationException("SimulationContext has not been initialized. Call ResetBatch before running simulation.");
    }
}

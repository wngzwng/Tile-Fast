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
        SelectableBuffer = new int[sourceLevel.Mapping.TileCount];
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
    /// 当前模拟候选来源模式。
    /// </summary>
    public SimulationCandidateMode CandidateMode { get; }

    /// <summary>
    /// 当前 batch 使用的随机数生成器。
    /// </summary>
    public Random Random { get; }

    /// <summary>
    /// 复用的候选 tile 缓冲区；有效长度由 <see cref="CandidateCount"/> 表示。
    /// </summary>
    public int[] SelectableBuffer { get; }

    internal void StartRun(int simulationIndex)
    {
        if ((uint)simulationIndex >= (uint)SimulationCount)
            throw new ArgumentOutOfRangeException(nameof(simulationIndex));

        if (SuccessCount + FailureCount != simulationIndex)
            throw new InvalidOperationException("Simulation context is not aligned with the next run.");

        SimulationIndex = simulationIndex;
        RunStatus = LevelRunStatus.Pending;
        MoveCount = 0;
        CandidateCount = 0;
        ClearSelectedCandidate();
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
    public int CandidateCount { get; private set; }

    /// <summary>
    /// 当前 run 已执行的行为数量。
    /// </summary>
    public int MoveCount { get; private set; }

    /// <summary>
    /// 当前已选候选在 <see cref="SelectableBuffer"/> 中的 offset；未选定时为 -1。
    /// </summary>
    public int SelectedCandidateOffset { get; private set; } = -1;

    /// <summary>
    /// 当前 step 选中的 tileIndex；未选定时为 -1。
    /// </summary>
    public int SelectedTileIndex { get; private set; } = -1;

    internal void SetCandidateCount(int candidateCount)
    {
        if (candidateCount < 0)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));

        CandidateCount = candidateCount;
        ClearSelectedCandidate();
    }

    internal void SetSelectedCandidate(
        int selectedCandidateOffset,
        int selectedTileIndex)
    {
        if (selectedCandidateOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(selectedCandidateOffset));

        if (selectedTileIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(selectedTileIndex));

        SelectedCandidateOffset = selectedCandidateOffset;
        SelectedTileIndex = selectedTileIndex;
    }

    internal void ClearSelectedCandidate()
    {
        SelectedCandidateOffset = -1;
        SelectedTileIndex = -1;
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
}

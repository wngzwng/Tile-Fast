namespace Tile.Core.Simulation;

/// <summary>
/// 将 FSE 轻量候选转换为 Simulation 使用的池化 Behaviour 候选。
/// </summary>
public sealed class BehaviourCandidateFinder : ISimulationCandidateFinder
{
    public static BehaviourCandidateFinder Instance { get; } = new();

    private readonly FseFinder _fseFinder;

    public BehaviourCandidateFinder()
        : this(FseFinder.Instance)
    {
    }

    public BehaviourCandidateFinder(FseFinder fseFinder)
    {
        _fseFinder = fseFinder ?? throw new ArgumentNullException(nameof(fseFinder));
    }

    public SimulationCandidateMode CandidateMode => SimulationCandidateMode.Behaviour;

    public int FindCandidates(SimulationContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var candidates = context.BehaviourCandidates;
        candidates.Clear();

        return _fseFinder.FindCandidates(
            context.SourceLevel,
            candidates.Rent,
            candidates.MutableItems.Add);
    }
}

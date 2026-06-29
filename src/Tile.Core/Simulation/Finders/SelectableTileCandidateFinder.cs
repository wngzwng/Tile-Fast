namespace Tile.Core.Simulation;

/// <summary>
/// 默认候选生成器：直接使用当前关卡的可选 tile 集合。
/// </summary>
public sealed class SelectableTileCandidateFinder : ISimulationCandidateFinder
{
    public static SelectableTileCandidateFinder Instance { get; } = new();

    private SelectableTileCandidateFinder()
    {
    }

    public SimulationCandidateMode CandidateMode => SimulationCandidateMode.Tile;

    public int FindCandidates(
        SimulationContext context,
        IList<int> candidates)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (candidates is null)
            throw new ArgumentNullException(nameof(candidates));

        candidates.Clear();

        foreach (var tileIndex in context.SourceLevel.Pasture.SelectableTiles)
            candidates.Add(tileIndex);

        return candidates.Count;
    }
}

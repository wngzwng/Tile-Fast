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

    public int FindCandidates(SimulationContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var candidates = context.TileCandidates;

        foreach (var tileIndex in context.SourceLevel.Pasture.SelectableTiles)
            candidates.MutableItems.Add(tileIndex);

        return candidates.Count;
    }
}

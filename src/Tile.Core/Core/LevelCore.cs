using Tile.Core.Core.Mapping;
using Tile.Core.Core.Zones;

namespace Tile.Core.Core;

public sealed class LevelCore
{
    public LevelRuleSpec RuleSpec { get; }

    public TileMappingTable Mapping { get; }

    public Pasture Pasture { get; }

    public StagingArea StagingArea { get; }

    public Corral Corral { get; }

    public LevelCore(
        LevelRuleSpec ruleSpec,
        TileMappingTable mapping,
        Pasture pasture,
        StagingArea stagingArea,
        Corral corral)
    {
        RuleSpec = ruleSpec ?? throw new ArgumentNullException(nameof(ruleSpec));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        Pasture = pasture ?? throw new ArgumentNullException(nameof(pasture));
        StagingArea = stagingArea ?? throw new ArgumentNullException(nameof(stagingArea));
        Corral = corral ?? throw new ArgumentNullException(nameof(corral));
    }

     public LevelCore Clone()
    {
        return new LevelCore(
            RuleSpec,
            Mapping,
            Pasture.Clone(),
            StagingArea.Clone(),
            Corral.Clone());
    }
}

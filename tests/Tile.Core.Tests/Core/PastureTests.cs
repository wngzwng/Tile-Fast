using NUnit.Framework;
using Tile.Core.Common.BitSet;
using Tile.Core.Core;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Types;
using Tile.Core.Core.Zones;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class PastureTests
{
    [Test]
    public void ToString_ContainsCurrentStateSummary()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        var text = pasture.ToString();

        Assert.That(text, Does.Contain("Pasture("));
        Assert.That(text, Does.Contain("Tiles=2"));
        Assert.That(text, Does.Contain("Rule=Tile"));
        Assert.That(text, Does.Contain("Present=2"));
        Assert.That(text, Does.Contain("Visible=1"));
        Assert.That(text, Does.Contain("Selectable=1"));
        Assert.That(text, Does.Contain("VisibleTiles=[1]"));
        Assert.That(text, Does.Contain("SelectableTiles=[1]"));
        Assert.That(text, Does.Contain("IsEmpty=False"));

        pasture.Lift(1);

        text = pasture.ToString();

        Assert.That(text, Does.Contain("Present=1"));
        Assert.That(text, Does.Contain("Visible=1"));
        Assert.That(text, Does.Contain("Selectable=1"));
        Assert.That(text, Does.Contain("VisibleTiles=[0]"));
        Assert.That(text, Does.Contain("SelectableTiles=[0]"));
    }

    [Test]
    public void Constructor_MarksAllTilesPresent()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (2, 0, 0).PackXyz()),
            CreateTile(2, (4, 0, 0).PackXyz())
        ]);

        Assert.That(pasture.PresentTiles.Count(), Is.EqualTo(3));
        Assert.That(pasture.IsPresent(0), Is.True);
        Assert.That(pasture.IsPresent(1), Is.True);
        Assert.That(pasture.IsPresent(2), Is.True);
    }

    [Test]
    public void Constructor_WhenTileHasNoCover_MarksVisibleAndSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz())
        ]);

        Assert.That(pasture.GetExposedArea(0), Is.EqualTo(4));
        Assert.That(pasture.IsVisible(0), Is.True);
        Assert.That(pasture.IsSelectable(0), Is.True);
    }

    [Test]
    public void Constructor_WhenTilePartiallyCovered_MarksVisibleButNotSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (1, 0, 1).PackXyz())
        ]);

        Assert.That(pasture.GetExposedArea(0), Is.EqualTo(2));
        Assert.That(pasture.IsVisible(0), Is.True);
        Assert.That(pasture.IsSelectable(0), Is.False);
        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.True);
    }

    [Test]
    public void Constructor_WhenTileFullyCovered_MarksNotVisibleAndNotSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        Assert.That(pasture.GetExposedArea(0), Is.Zero);
        Assert.That(pasture.IsVisible(0), Is.False);
        Assert.That(pasture.IsSelectable(0), Is.False);
        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.True);
    }

    [Test]
    public void GetUpperCoverTileBits_ReturnsDistinctCoverTiles()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        ulong[] coverTileBits = [ulong.MaxValue];

        var coverTileCount = pasture.GetUpperCoverTileBits(0, coverTileBits);

        Assert.That(coverTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(coverTileBits, 1), Is.True);
        Assert.That(BitSetOperations.Get(coverTileBits, 0), Is.False);
    }

    [Test]
    public void SimulateState_WhenUpperCoverIgnored_HitsHigherCover()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz()),
            CreateTile(2, (0, 0, 2).PackXyz())
        ]);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 1);

        pasture.SimulateState(
            0,
            ignoredTileBits,
            out var visible,
            out var selectable);

        Assert.That(visible, Is.False);
        Assert.That(selectable, Is.False);
    }

    [Test]
    public void SimulateUpperCoverTileBits_WhenMiddleCoverIgnored_HitsHigherCover()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz()),
            CreateTile(2, (0, 0, 2).PackXyz())
        ]);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 1);

        ulong[] realCoverTileBits = [0UL];
        ulong[] simulatedCoverTileBits = [0UL];

        var realCoverTileCount = pasture.GetUpperCoverTileBits(0, realCoverTileBits);
        var simulatedCoverTileCount = pasture.SimulateUpperCoverTileBits(
            0,
            ignoredTileBits,
            simulatedCoverTileBits);

        Assert.That(realCoverTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(realCoverTileBits, 1), Is.True);
        Assert.That(simulatedCoverTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(simulatedCoverTileBits, 2), Is.True);
        Assert.That(BitSetOperations.Get(simulatedCoverTileBits, 1), Is.False);
    }

    [Test]
    public void GetLowerCoveredTileBits_WhenNoLowerTile_ReturnsZeroAndClearsBuffer()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz())
        ]);

        ulong[] coveredTileBits = [ulong.MaxValue];

        var coveredTileCount = pasture.GetLowerCoveredTileBits(0, coveredTileBits);

        Assert.That(coveredTileCount, Is.Zero);
        Assert.That(coveredTileBits[0], Is.Zero);
    }

    [Test]
    public void GetLowerCoveredTileBits_ReturnsDistinctCoveredTiles()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        ulong[] coveredTileBits = [ulong.MaxValue];

        var coveredTileCount = pasture.GetLowerCoveredTileBits(1, coveredTileBits);

        Assert.That(coveredTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(coveredTileBits, 0), Is.True);
        Assert.That(BitSetOperations.Get(coveredTileBits, 1), Is.False);
    }

    [Test]
    public void GetLowerCoveredTileBits_WhenMiddleLayerMissing_HitsLowerTile()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz()),
            CreateTile(2, (0, 0, 2).PackXyz())
        ]);

        pasture.Lift(1);

        ulong[] coveredTileBits = [0UL];

        var coveredTileCount = pasture.GetLowerCoveredTileBits(2, coveredTileBits);

        Assert.That(coveredTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(coveredTileBits, 0), Is.True);
        Assert.That(BitSetOperations.Get(coveredTileBits, 1), Is.False);
    }

    [Test]
    public void SimulateLowerCoveredTileBits_WhenMiddleCoveredIgnored_HitsLowerCovered()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz()),
            CreateTile(2, (0, 0, 2).PackXyz())
        ]);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 1);

        ulong[] realCoveredTileBits = [0UL];
        ulong[] simulatedCoveredTileBits = [0UL];

        var realCoveredTileCount = pasture.GetLowerCoveredTileBits(2, realCoveredTileBits);
        var simulatedCoveredTileCount = pasture.SimulateLowerCoveredTileBits(
            2,
            ignoredTileBits,
            simulatedCoveredTileBits);

        Assert.That(realCoveredTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(realCoveredTileBits, 1), Is.True);
        Assert.That(simulatedCoveredTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(simulatedCoveredTileBits, 0), Is.True);
        Assert.That(BitSetOperations.Get(simulatedCoveredTileBits, 1), Is.False);
    }

    [Test]
    public void SimulateAffectedTileBits_WhenMiddleCoveredIgnored_HitsLowerAffected()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz()),
            CreateTile(2, (0, 0, 2).PackXyz())
        ]);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 1);

        ulong[] affectedTileBits = [0UL];

        var affectedTileCount = pasture.SimulateAffectedTileBits(
            2,
            ignoredTileBits,
            affectedTileBits);

        Assert.That(affectedTileCount, Is.EqualTo(1));
        Assert.That(BitSetOperations.Get(affectedTileBits, 0), Is.True);
        Assert.That(BitSetOperations.Get(affectedTileBits, 1), Is.False);
    }

    [Test]
    public void SimulateAffectedTileBits_WhenClassicRule_IncludesSideNeighbors()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (2, 0, 0).PackXyz()),
            CreateTile(2, (4, 0, 0).PackXyz())
        ],
            LevelRuleSpec.PairClassic);

        ulong[] affectedTileBits = [0UL];

        var affectedTileCount = pasture.SimulateAffectedTileBits(
            1,
            ignoredTileBits: default,
            affectedTileBits);

        Assert.That(affectedTileCount, Is.EqualTo(2));
        Assert.That(BitSetOperations.Get(affectedTileBits, 0), Is.True);
        Assert.That(BitSetOperations.Get(affectedTileBits, 2), Is.True);
    }

    [Test]
    public void Lift_RemovesTileAndRefreshesAffectedTiles()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        pasture.Lift(1);

        Assert.That(pasture.IsPresent(1), Is.False);
        Assert.That(pasture.IsVisible(1), Is.False);
        Assert.That(pasture.IsSelectable(1), Is.False);
        Assert.That(pasture.IsVisible(0), Is.True);
        Assert.That(pasture.IsSelectable(0), Is.True);
    }

    [Test]
    public void Place_RestoresTileAndRefreshesAffectedTiles()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        pasture.Lift(1);
        pasture.Place(1);

        Assert.That(pasture.IsPresent(1), Is.True);
        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.True);
        Assert.That(pasture.IsVisible(0), Is.False);
        Assert.That(pasture.IsSelectable(0), Is.False);
    }

    [Test]
    public void Lift_WhenMiddleLayerAlreadyMissing_RefreshesLowerDynamicHit()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz()),
            CreateTile(2, (0, 0, 2).PackXyz())
        ]);

        pasture.Lift(1);

        Assert.That(pasture.IsVisible(0), Is.False);
        Assert.That(pasture.IsSelectable(0), Is.False);

        pasture.Lift(2);

        Assert.That(pasture.IsVisible(0), Is.True);
        Assert.That(pasture.IsSelectable(0), Is.True);
    }

    [Test]
    public void Reset_RestoresInitialPresentVisibleAndSelectableState()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        pasture.Lift(1);

        Assert.That(pasture.IsPresent(1), Is.False);
        Assert.That(pasture.IsVisible(0), Is.True);
        Assert.That(pasture.IsSelectable(0), Is.True);

        pasture.Reset();

        Assert.That(pasture.IsPresent(0), Is.True);
        Assert.That(pasture.IsPresent(1), Is.True);
        Assert.That(pasture.IsVisible(0), Is.False);
        Assert.That(pasture.IsSelectable(0), Is.False);
        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.True);
    }

    [Test]
    public void Classic_WhenBothSideNeighborsArePresent_MarksTileNotSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (2, 0, 0).PackXyz()),
            CreateTile(2, (4, 0, 0).PackXyz())
        ],
            LevelRuleSpec.PairClassic);

        Span<int> neighbors = stackalloc int[4];

        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.False);
        Assert.That(pasture.HasPresentNeighbor(1, NeighborDirEnum.Left), Is.True);
        Assert.That(pasture.HasPresentNeighbor(1, NeighborDirEnum.Right), Is.True);
        Assert.That(pasture.GetPresentNeighbors(1, NeighborDirEnum.Left, neighbors), Is.EqualTo(1));
        Assert.That(neighbors[0], Is.EqualTo(0));
    }

    [Test]
    public void Classic_WhenOneSideNeighborIsMissing_MarksTileSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (2, 0, 0).PackXyz()),
            CreateTile(2, (4, 0, 0).PackXyz())
        ],
            LevelRuleSpec.PairClassic);

        pasture.Lift(0);

        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.True);
        Assert.That(pasture.HasPresentNeighbor(1, NeighborDirEnum.Left), Is.False);
        Assert.That(pasture.HasPresentNeighbor(1, NeighborDirEnum.Right), Is.True);
    }

    [Test]
    public void SimulateState_WhenUpperCoverIgnored_MakesCoveredTileVisibleAndSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (0, 0, 1).PackXyz())
        ]);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 1);

        pasture.SimulateState(
            0,
            ignoredTileBits,
            out var visible,
            out var selectable);

        Assert.That(visible, Is.True);
        Assert.That(selectable, Is.True);
        Assert.That(pasture.IsVisible(0), Is.False);
        Assert.That(pasture.IsSelectable(0), Is.False);
    }

    [Test]
    public void SimulateState_WhenClassicSideNeighborIgnored_MakesTileSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz()),
            CreateTile(1, (2, 0, 0).PackXyz()),
            CreateTile(2, (4, 0, 0).PackXyz())
        ],
            LevelRuleSpec.PairClassic);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 0);

        pasture.SimulateState(
            1,
            ignoredTileBits,
            out var visible,
            out var selectable);

        Assert.That(visible, Is.True);
        Assert.That(selectable, Is.True);
        Assert.That(pasture.IsSelectable(1), Is.False);
    }

    [Test]
    public void SimulateState_WhenTileItselfIgnored_ReturnsNotVisibleAndNotSelectable()
    {
        var pasture = CreatePasture(
        [
            CreateTile(0, (0, 0, 0).PackXyz())
        ]);

        ulong[] ignoredTileBits = [0UL];
        BitSetOperations.Set(ignoredTileBits, 0);

        pasture.SimulateState(
            0,
            ignoredTileBits,
            out var visible,
            out var selectable);

        Assert.That(visible, Is.False);
        Assert.That(selectable, Is.False);
        Assert.That(pasture.IsVisible(0), Is.True);
        Assert.That(pasture.IsSelectable(0), Is.True);
    }

    private static Pasture CreatePasture(
        IReadOnlyList<Tile> tiles,
        LevelRuleSpec? ruleSpec = null)
    {
        var mapping = CreateMapping(tiles, ruleSpec?.LockRuleType ?? LockRuleTypeEnum.Tile);
        return new Pasture(mapping, ruleSpec ?? LevelRuleSpec.TripleTile);
    }

    private static TileMappingTable CreateMapping(
        IReadOnlyList<Tile> tiles,
        LockRuleTypeEnum lockRule)
    {
        var maxCol = 0;
        var maxRow = 0;
        var maxLayer = 0;
        var (dx, dy, dz) = Tile.DefaultVolume.UnpackXyz();

        foreach (var tile in tiles)
        {
            var (x, y, z) = tile.Position.UnpackXyz();
            maxCol = Math.Max(maxCol, x + dx);
            maxRow = Math.Max(maxRow, y + dy);
            maxLayer = Math.Max(maxLayer, z + dz);
        }

        return TileMappingTable.Create(
            tiles,
            lockRule,
            maxCol,
            maxRow,
            maxLayer);
    }

    private static Tile CreateTile(int index, int position)
    {
        var tile = new Tile(index, position);
        tile.SetSuit(index);
        return tile;
    }
}

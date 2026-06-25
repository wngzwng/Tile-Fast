using NUnit.Framework;
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

        Assert.That(pasture.IsVisible(0), Is.False);
        Assert.That(pasture.IsSelectable(0), Is.False);
        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.True);
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

        Assert.That(pasture.IsVisible(1), Is.True);
        Assert.That(pasture.IsSelectable(1), Is.False);
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

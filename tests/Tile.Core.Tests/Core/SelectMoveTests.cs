using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.Core.Moves;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class SelectMoveTests
{
    [Test]
    public void ToString_WhenNotMatched_ContainsBasicState()
    {
        var move = new SelectMove(tileIndex: 3);

        var text = move.ToString();

        Assert.That(text, Is.EqualTo("SelectMove(TileIndex=3, HasMatch=False)"));
    }

    [Test]
    public void ToString_WithLevelContext_ContainsSuit()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz()
            ],
            suits: [7]);

        var move = new SelectMove(tileIndex: 0);

        var text = move.ToString(level);

        Assert.That(text, Is.EqualTo("SelectMove(TileIndex=0, Suit=7, HasMatch=False)"));
    }

    [Test]
    public void Do_WhenTileIsNotSelectable_Throws()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (0, 0, 1).PackXyz()
            ],
            suits: [1, 2]);

        var move = new SelectMove(tileIndex: 0);

        var error = Assert.Throws<InvalidOperationException>(() => move.Do(level));

        Assert.That(error!.Message, Does.Contain("无法选择棋子 0"));
        Assert.That(error.Message, Does.Contain("当前不可选"));
    }

    [Test]
    public void Do_WhenNoMatch_MovesTileFromPastureToStagingArea()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz()
            ],
            suits: [1, 2]);

        var move = new SelectMove(tileIndex: 0);

        move.Do(level);

        Assert.That(level.Pasture.IsPresent(0), Is.False);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.EqualTo(new[] { 0 }));
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void Undo_WhenNoMatch_RestoresTileToPasture()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz()
            ],
            suits: [1, 2]);

        var move = new SelectMove(tileIndex: 0);

        move.Do(level);
        move.Undo(level);

        Assert.That(level.Pasture.IsPresent(0), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.Empty);
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void Do_WhenMatchOccurs_MovesMatchedGroupToCorral()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);

        MoveTileFromPastureToStagingArea(level, tileIndex: 0);
        MoveTileFromPastureToStagingArea(level, tileIndex: 1);

        var move = new SelectMove(tileIndex: 2);

        move.Do(level);

        Assert.That(level.Pasture.IsPresent(2), Is.False);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.Empty);
        Assert.That(level.Corral.Count, Is.EqualTo(3));
        Assert.That(move.ToString(level), Is.EqualTo("SelectMove(TileIndex=2, Suit=1, HasMatch=True, Matched=[0, 1, 2])"));
    }

    [Test]
    public void Undo_WhenMatchOccurs_RestoresMatchedGroupAndSelectedTile()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);

        MoveTileFromPastureToStagingArea(level, tileIndex: 0);
        MoveTileFromPastureToStagingArea(level, tileIndex: 1);

        var move = new SelectMove(tileIndex: 2);

        move.Do(level);
        move.Undo(level);

        Assert.That(level.Pasture.IsPresent(2), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(level.Corral.Count, Is.Zero);
    }

    private static LevelCore CreateLevel(int[] positions, int[] suits)
    {
        return new LevelCore(positions, LevelRuleSpec.TripleTile, suits);
    }

    private static void MoveTileFromPastureToStagingArea(
        LevelCore level,
        int tileIndex)
    {
        level.Pasture.Lift(tileIndex);
        level.StagingArea.Enter(tileIndex);
    }
}

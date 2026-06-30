using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.Core.Moves;
using Tile.Core.Core.Types;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class LevelCoreTests
{
    [Test]
    public void DoMove_WhenMoveIsValid_AppliesMoveAndRecordsHistory()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz()
            ],
            suits: [1, 2]);

        level.DoMove(new SelectMove(tileIndex: 0));

        Assert.That(level.Pasture.IsPresent(0), Is.False);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.EqualTo(new[] { 0 }));
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void DoMove_WhenMoveIsNull_Throws()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz()
            ],
            suits: [1]);

        Assert.Throws<ArgumentNullException>(() => level.DoMove(null!));
    }

    [Test]
    public void DoMove_WhenMoveIsInvalid_ThrowsAndDoesNotChangeBoard()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (0, 0, 1).PackXyz()
            ],
            suits: [1, 2]);

        var error = Assert.Throws<InvalidOperationException>(() => level.DoMove(new SelectMove(tileIndex: 0)));

        Assert.That(error!.Message, Does.Contain("无法执行移动"));
        Assert.That(level.Pasture.IsPresent(0), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.Empty);
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void UnDoMove_WhenHistoryIsEmpty_Throws()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz()
            ],
            suits: [1]);

        var error = Assert.Throws<InvalidOperationException>(() => level.UnDoMove());

        Assert.That(error!.Message, Does.Contain("没有可撤销的移动"));
    }

    [Test]
    public void UndoMove_WhenHistoryIsEmpty_Throws()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz()
            ],
            suits: [1]);

        var error = Assert.Throws<InvalidOperationException>(() => level.UndoMove());

        Assert.That(error!.Message, Does.Contain("没有可撤销的移动"));
    }

    [Test]
    public void UnDoMove_WhenLastMoveHasNoMatch_RestoresPreviousState()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz()
            ],
            suits: [1, 2]);

        level.DoMove(new SelectMove(tileIndex: 0));
        level.UnDoMove();

        Assert.That(level.Pasture.IsPresent(0), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.Empty);
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void UndoMove_WhenLastMoveHasNoMatch_RestoresPreviousState()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz()
            ],
            suits: [1, 2]);

        level.DoMove(new SelectMove(tileIndex: 0));
        level.UndoMove();

        Assert.That(level.Pasture.IsPresent(0), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.Empty);
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void UnDoMove_WhenLastMoveHasMatch_RestoresMatchedGroup()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);

        level.DoMove(new SelectMove(tileIndex: 0));
        level.DoMove(new SelectMove(tileIndex: 1));
        level.DoMove(new SelectMove(tileIndex: 2));
        level.UnDoMove();

        Assert.That(level.Pasture.IsPresent(2), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(level.Corral.Count, Is.Zero);
    }

    [Test]
    public void Reset_RestoresInitialZonesAndClearsMoveHistory()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz()
            ],
            suits: [1, 2]);

        level.DoMove(new SelectMove(tileIndex: 0));

        Assert.That(level.Pasture.IsPresent(0), Is.False);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.EqualTo(new[] { 0 }));

        level.Reset();

        Assert.That(level.Pasture.IsPresent(0), Is.True);
        Assert.That(level.Pasture.IsPresent(1), Is.True);
        Assert.That(level.StagingArea.Tiles.ToArray(), Is.Empty);
        Assert.That(level.Corral.Count, Is.Zero);
        Assert.Throws<InvalidOperationException>(() => level.UndoMove());
    }

    [Test]
    public void IsFinish_WhenLevelIsInitial_ReturnsFalse()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);

        Assert.That(level.IsFinish(), Is.False);
    }

    [Test]
    public void IsFinish_WhenTilesRemainInStagingArea_ReturnsFalse()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);

        level.DoMove(new SelectMove(tileIndex: 0));

        Assert.That(level.IsFinish(), Is.False);
    }

    [Test]
    public void IsFinish_WhenAllTilesAreCleared_ReturnsTrue()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (2, 0, 0).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);

        level.DoMove(new SelectMove(tileIndex: 0));
        level.DoMove(new SelectMove(tileIndex: 1));
        level.DoMove(new SelectMove(tileIndex: 2));

        Assert.That(level.IsFinish(), Is.True);
    }

    [Test]
    public void Deserialize_CreatesEquivalentStateToConstructor()
    {
        int[] positions =
        [
            (0, 0, 0).PackXyz(),
            (6, 0, 0).PackXyz(),
            (2, 4, 1).PackXyz()
        ];

        int[] suits = [1, 2, 3];

        var fromConstructor = new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.TripleTile,
            suits.AsSpan());

        var fromText = LevelCore.Deserialize("000,6;142:123", LevelRuleSpec.TripleTile);

        Assert.That(fromText.ToString(), Is.EqualTo(fromConstructor.ToString()));
        Assert.That(fromText.Serialize(), Is.EqualTo(fromConstructor.Serialize()));
    }

    [Test]
    public void ToLevel_WithRuleSpec_ForwardsToDeserialize()
    {
        var level = "000,2:12".ToLevel(LevelRuleSpec.PairClassic);

        Assert.That(level.Serialize(), Is.EqualTo("000,2:12"));
        Assert.That(level.RuleSpec, Is.SameAs(LevelRuleSpec.PairClassic));
    }

    [Test]
    public void ToLevel_WithRuleValues_CreatesRuleSpec()
    {
        var level = "000,2:12".ToLevel(
            matchRequireCount: 2,
            slotCapacity: 4,
            lockRuleType: LockRuleTypeEnum.Classic);

        Assert.That(level.Serialize(), Is.EqualTo("000,2:12"));
        Assert.That(level.RuleSpec.MatchRequireCount, Is.EqualTo(2));
        Assert.That(level.RuleSpec.SlotCapacity, Is.EqualTo(4));
        Assert.That(level.RuleSpec.LockRuleType, Is.EqualTo(LockRuleTypeEnum.Classic));
    }

    [Test]
    public void Constructor_WhenColumnExceedsRow_ComputesMaxColFromColumn()
    {
        int[] positions =
        [
            (8, 1, 0).PackXyz()
        ];

        var level = new LevelCore(positions.AsSpan(), LevelRuleSpec.TripleTile);

        Assert.That(level.Mapping.MaxRow, Is.EqualTo(3));
        Assert.That(level.Mapping.MaxCol, Is.EqualTo(10));
        Assert.That(level.Mapping.MaxLayer, Is.EqualTo(1));
    }

    [Test]
    public void ToString_ContainsKeyStateSummary()
    {
        int[] positions =
        [
            (0, 0, 0).PackXyz(),
            (2, 0, 0).PackXyz(),
            (0, 2, 0).PackXyz()
        ];

        int[] suits = [1, 2, 3];

        var level = new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.TripleTile,
            suits.AsSpan());

        var text = level.ToString();

        Assert.That(text, Does.StartWith("LevelCore("));
        Assert.That(text, Does.Contain("Tiles=3"));
        Assert.That(text, Does.Contain("Size(rcz)="));
        Assert.That(text, Does.Contain("Rule=match3/slot7/Tile"));
        Assert.That(text, Does.Contain("Pasture=Pasture("));
        Assert.That(text, Does.Contain("VisibleTiles=[0, 1, 2]"));
        Assert.That(text, Does.Contain("SelectableTiles=[0, 1, 2]"));
        Assert.That(text, Does.Contain("StagingArea=StagingArea("));
        Assert.That(text, Does.Contain("Used=0/7"));
        Assert.That(text, Does.Contain("Corral=Corral("));
        Assert.That(text, Does.Contain("Tiles(last6)=[]"));
        Assert.That(text, Does.Not.Contain("\n"));
    }

    [Test]
    public void ToString_WhenMultiline_ContainsIndentedStateSummary()
    {
        int[] positions =
        [
            (0, 0, 0).PackXyz(),
            (2, 0, 0).PackXyz(),
            (0, 2, 0).PackXyz()
        ];

        int[] suits = [1, 2, 3];

        var level = new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.TripleTile,
            suits.AsSpan());

        var text = level.ToString(multiline: true);

        Assert.That(text, Does.StartWith("LevelCore(\n"));
        Assert.That(text, Does.Contain("  Tiles=3,\n"));
        Assert.That(text, Does.Contain("  Size(rcz)="));
        Assert.That(text, Does.Contain("  Rule=match3/slot7/Tile,\n"));
        Assert.That(text, Does.Contain("  Pasture=Pasture("));
        Assert.That(text, Does.Contain("  StagingArea=StagingArea("));
        Assert.That(text, Does.Contain("  Corral=Corral("));
        Assert.That(text, Does.EndWith("\n)"));
    }

    private static LevelCore CreateLevel(int[] positions, int[] suits)
    {
        return new LevelCore(positions, LevelRuleSpec.TripleTile, suits);
    }
}

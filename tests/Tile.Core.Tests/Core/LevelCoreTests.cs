using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class LevelCoreTests
{
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
}

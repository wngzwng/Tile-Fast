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
        Assert.That(text, Does.Contain("Rule=match3/slot7/Tile"));
        Assert.That(text, Does.Contain("Pasture="));
        Assert.That(text, Does.Contain("Staging=0/7"));
        Assert.That(text, Does.Contain("Corral=0"));
    }
}

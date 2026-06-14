using NUnit.Framework;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class TileTests
{
    [Test]
    public void SetSuit_ValidRange_UpdatesSuit()
    {
        var tile = new Tile(index: 0, position: (0, 0, 0).PackXyz());

        tile.SetSuit(0);
        Assert.That(tile.Suit, Is.EqualTo(0));

        tile.SetSuit(Tile.MaxSuitCount - 1);
        Assert.That(tile.Suit, Is.EqualTo(Tile.MaxSuitCount - 1));
    }

    [Test]
    public void SetSuit_OutOfRange_Throws()
    {
        var tile = new Tile(index: 0, position: (0, 0, 0).PackXyz());

        // 花色约定为 0-based，负数和超过上界都不允许。
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.SetSuit(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tile.SetSuit(Tile.MaxSuitCount));
    }
}

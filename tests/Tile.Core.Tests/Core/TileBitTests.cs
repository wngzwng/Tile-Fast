using NUnit.Framework;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class TileBitTests
{
    [Test]
    public void GetBit_WithPositionAndSuit_CanReadBack()
    {
        int position = (3, 4, 5).PackXyz();
        int bit = position.ToTileBit(suit: 12);

        Assert.That(bit.GetTilePositionFromBit(), Is.EqualTo(position));
        Assert.That(bit.GetTileSuitFromBit(), Is.EqualTo(12));
        Assert.That(bit.HasTileSuit(), Is.True);
    }

    [Test]
    public void GetBit_WithoutSuit_ReturnsUnspecifiedSuit()
    {
        int position = (1, 2, 3).PackXyz();
        int bit = position.ToTileBit();

        // TileBit 当前锁定语义：最后 1 byte 为 0 表示花色未指定。
        Assert.That(bit.GetTilePositionFromBit(), Is.EqualTo(position));
        Assert.That(bit.GetTileSuitFromBit(), Is.EqualTo(TileBitExtensions.SuitUnspecified));
        Assert.That(bit.HasTileSuit(), Is.False);
    }

    [Test]
    public void ToTile_CreatesTileFromBit()
    {
        int position = (7, 8, 9).PackXyz();
        int bit = position.ToTileBit(suit: Tile.MaxSuitCount - 1);

        var tile = bit.NewTileFromBit(index: 4);

        Assert.That(tile.Index, Is.EqualTo(4));
        Assert.That(tile.Position, Is.EqualTo(position));
        Assert.That(tile.Suit, Is.EqualTo(Tile.MaxSuitCount - 1));
    }

    [Test]
    public void GetBit_InvalidSuit_Throws()
    {
        int position = (0, 0, 0).PackXyz();

        Assert.Throws<ArgumentOutOfRangeException>(() => position.ToTileBit(suit: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.ToTileBit(suit: -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => position.ToTileBit(suit: Tile.MaxSuitCount));
    }

    [Test]
    public void SetMethods_UpdatePositionAndSuit()
    {
        int bit = (1, 2, 3).PackXyz().ToTileBit(suit: 10);
        int newPosition = (4, 5, 6).PackXyz();

        bit = bit.SetTilePositionToBit(newPosition);
        Assert.That(bit.GetTilePositionFromBit(), Is.EqualTo(newPosition));
        Assert.That(bit.GetTileSuitFromBit(), Is.EqualTo(10));

        bit = bit.SetTileSuitToBit(11);
        Assert.That(bit.GetTilePositionFromBit(), Is.EqualTo(newPosition));
        Assert.That(bit.GetTileSuitFromBit(), Is.EqualTo(11));
    }

    [Test]
    public void GetBit_InvalidPosition_Throws()
    {
        int positionUsingReservedBit = unchecked((int)0x8000_0000);

        Assert.Throws<ArgumentOutOfRangeException>(() => positionUsingReservedBit.ToTileBit());
    }
}

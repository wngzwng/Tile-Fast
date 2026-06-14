using NUnit.Framework;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Tests.Core;

public sealed class TileMappingTableTests
{
    [Test]
    public void Constructor_BuildsBasicMappings()
    {
        Tile[] tiles =
        [
            CreateTile(index: 1, suit: 2, position: (4, 0, 0).PackXyz()),
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz()),
            CreateTile(index: 2, suit: 1, position: (0, 2, 0).PackXyz())
        ];

        var table = new TileMappingTable(tiles);

        Assert.That(table.TileCount, Is.EqualTo(3));
    }

    [Test]
    public void GetCoordinatesByTileIndex_ReturnsExpandedFootprint()
    {
        var tile = CreateTile(index: 0, suit: 1, position: (1, 2, 3).PackXyz());
        var table = new TileMappingTable([tile]);

        // 默认体积是 (2, 2, 1)，所以应展开为四个平面坐标。
        var coordinates = table.GetCoordinatesByTileIndex(0).ToArray();

        Assert.That(coordinates.Length, Is.EqualTo(4));
        Assert.That(coordinates, Is.EqualTo(new[]
        {
            (1, 2, 3).PackXyz(),
            (2, 2, 3).PackXyz(),
            (1, 3, 3).PackXyz(),
            (2, 3, 3).PackXyz()
        }));
    }

    [Test]
    public void RegionQueries_EmptyLocation_ReturnsMinusOne()
    {
        var table = new TileMappingTable(
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz())
        ]);

        // 这里重点验证“未命中时返回 -1”这个默认约定。
        Assert.That(table.GetTileIndexAt(3, 3, 0), Is.EqualTo(TileMappingTable.EmptyTileIndex));
        Assert.That(table.GetTileIndexAtPosition((3, 3, 0).PackXyz()), Is.EqualTo(TileMappingTable.EmptyTileIndex));
    }

    [Test]
    public void TryGetTileIndexAt_WhenMissing_ReturnsFalseAndMinusOne()
    {
        var table = new TileMappingTable(
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz())
        ]);

        var ok = table.TryGetTileIndexAt(7, 7, 0, out var tileIndex);

        Assert.That(ok, Is.False);
        Assert.That(tileIndex, Is.EqualTo(TileMappingTable.EmptyTileIndex));
    }

    [Test]
    public void Indexer_WithXyz_ReturnsTileIndex()
    {
        var table = new TileMappingTable(
        [
            CreateTile(index: 0, suit: 1, position: (1, 1, 0).PackXyz())
        ]);

        Assert.That(table[1, 1, 0], Is.EqualTo(0));
    }

    [Test]
    public void Constructor_UnspecifiedSuit_IsAllowedInMvp()
    {
        var tile = new Tile(index: 0, position: (0, 0, 0).PackXyz());

        Assert.DoesNotThrow(() => _ = new TileMappingTable([tile]));
    }

    [Test]
    public void Constructor_NonContinuousIndex_Throws()
    {
        Tile[] tiles =
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz()),
            CreateTile(index: 2, suit: 2, position: (2, 0, 0).PackXyz())
        ];

        // Tile.Index 目前约定必须连续 0-based，缺口不允许静默放过。
        Assert.Throws<ArgumentException>(() => _ = new TileMappingTable(tiles));
    }

    [Test]
    public void Constructor_OverlappedCoordinates_Throws()
    {
        Tile[] tiles =
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz()),
            CreateTile(index: 1, suit: 2, position: (1, 1, 0).PackXyz())
        ];

        // 默认体积是 (2, 2, 1)，所以上面两张 Tile 会在 (1, 1, 0) 发生重叠。
        Assert.Throws<InvalidOperationException>(() => _ = new TileMappingTable(tiles));
    }

    [Test]
    public void QueryMethods_OutOfRange_Throws()
    {
        var table = new TileMappingTable(
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz())
        ]);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetTileIndexAt(99, 0, 0));
    }

    private static Tile CreateTile(int index, int suit, int position)
    {
        var tile = new Tile(index, position);
        tile.SetSuit(suit);
        return tile;
    }
}

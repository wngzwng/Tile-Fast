using NUnit.Framework;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Types;
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

        var table = CreateTable(tiles);

        Assert.That(table.TileCount, Is.EqualTo(3));
    }

    [Test]
    public void TileOccupiesPosition_ReturnsExpandedFootprint()
    {
        var tile = CreateTile(index: 0, suit: 1, position: (1, 2, 3).PackXyz());
        var table = CreateTable([tile]);

        // 默认体积是 (2, 2, 1)，所以应占据四个平面坐标。
        Assert.That(table.TileOccupiesPosition(0, (1, 2, 3).PackXyz()), Is.True);
        Assert.That(table.TileOccupiesPosition(0, (2, 2, 3).PackXyz()), Is.True);
        Assert.That(table.TileOccupiesPosition(0, (1, 3, 3).PackXyz()), Is.True);
        Assert.That(table.TileOccupiesPosition(0, (2, 3, 3).PackXyz()), Is.True);
        Assert.That(table.TileOccupiesPosition(0, (0, 0, 0).PackXyz()), Is.False);
    }

    [Test]
    public void RegionQueries_EmptyLocation_ReturnsMinusOne()
    {
        var table = TileMappingTable.Create(
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz())
        ],
            LockRuleTypeEnum.Tile,
            maxCol: 4,
            maxRow: 4,
            maxLayer: 1);

        // 这里重点验证“未命中时返回 -1”这个默认约定。
        Assert.That(table.TryGetTileIndexAtPosition((3, 3, 0).PackXyz(), out var tileIndex), Is.False);
        Assert.That(tileIndex, Is.EqualTo(-1));
    }

    [Test]
    public void TryGetTileIndexAt_WhenMissing_ReturnsFalseAndMinusOne()
    {
        var table = TileMappingTable.Create(
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz())
        ],
            LockRuleTypeEnum.Tile,
            maxCol: 8,
            maxRow: 8,
            maxLayer: 1);

        var ok = table.TryGetTileIndexAtPosition((7, 7, 0).PackXyz(), out var tileIndex);

        Assert.That(ok, Is.False);
        Assert.That(tileIndex, Is.EqualTo(-1));
    }

    [Test]
    public void Indexer_WithXyz_ReturnsTileIndex()
    {
        var table = CreateTable(
        [
            CreateTile(index: 0, suit: 1, position: (1, 1, 0).PackXyz())
        ]);

        Assert.That(table.GetTileIndexAtPosition((1, 1, 0).PackXyz()), Is.EqualTo(0));
    }

    [Test]
    public void Constructor_UnspecifiedSuit_IsAllowedInMvp()
    {
        var tile = new Tile(index: 0, position: (0, 0, 0).PackXyz());

        Assert.DoesNotThrow(() => _ = CreateTable([tile]));
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
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = CreateTable(tiles));
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
        Assert.Throws<InvalidOperationException>(() => _ = CreateTable(tiles));
    }

    [Test]
    public void QueryMethods_OutOfRange_Throws()
    {
        var table = CreateTable(
        [
            CreateTile(index: 0, suit: 1, position: (0, 0, 0).PackXyz())
        ]);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetTileIndexAtPosition((99, 0, 0).PackXyz()));
    }

    private static Tile CreateTile(int index, int suit, int position)
    {
        var tile = new Tile(index, position);
        tile.SetSuit(suit);
        return tile;
    }

    private static TileMappingTable CreateTable(IReadOnlyList<Tile> tiles)
    {
        var maxCol = 0;
        var maxRow = 0;
        var maxLayer = 0;
        var (dX, dY, dZ) = Tile.DefaultVolume.UnpackXyz();

        foreach (var tile in tiles)
        {
            var (x, y, z) = tile.Position.UnpackXyz();
            maxCol = Math.Max(maxCol, x + dX);
            maxRow = Math.Max(maxRow, y + dY);
            maxLayer = Math.Max(maxLayer, z + dZ);
        }

        return TileMappingTable.Create(
            tiles,
            LockRuleTypeEnum.Tile,
            maxCol,
            maxRow,
            maxLayer);
    }
}

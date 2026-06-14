ThreeTile TileMappingTable 扁平数组映射设计文档
1. 目标
TileMappingTable 用于统一管理 ThreeTile 中 Tile 的静态映射关系。
它负责把关卡中的 Tile 信息预处理成高性能数组索引，方便后续求解、特征提取、可见性判断、遮挡判断等逻辑使用。
核心目标：
高性能
低 GC
数组化
查询稳定
职责清晰
和 BitSet 动态状态分离

需要覆盖的映射关系：
Tile -> index
index -> Tile
index -> Suit
Suit -> index[]
Suit -> count
index -> Position
index -> 所占空间坐标
position / x,y,z -> regionId
regionId -> index

不使用：
Dictionary<int, Tile>
Dictionary<int, int>
HashSet<int>
LINQ
长期 List<int>


2. 分层原则
整体分成三层：
Tile
  单张牌的静态事实。

TileMappingTable
  全局静态映射表。

LevelState / BitSet
  动态状态，例如可见、可选、存在、锁定。

不要把所有信息都塞进 Tile。
推荐：
Tile 保存：
  Index
  Suit
  Position
  Volume
  Area
  AreaPositionIndex

TileMappingTable 保存：
  index -> Tile
  index -> Suit
  Suit -> index[]
  Suit -> count
  index -> coordinates
  regionId -> index

LevelState 保存：
  PresentBits
  VisibleBits
  SelectableBits
  LockedBits


3. 核心命名
统一使用：
Suit
  花色。

Position
  packed(x, y, z)，表示 Tile 的最小空间坐标。

RegionId
  连续空间区域 ID，用于数组下标。

TileIndex
  Tile 在关卡内的唯一编号，建议 1-based。

不建议混用：
Color / Suit
Coordinate / Position / Region
Index / ArrayIndex

最终建议：
花色统一叫 Suit。
空间最小坐标统一叫 Position。
连续空间下标统一叫 RegionId。
牌编号统一叫 TileIndex。


4. 为什么需要 RegionId
Position 是 packed 坐标，例如：
position = PackXyz(x, y, z)

它适合：
存储
传参
调试
序列化
解包

但是不一定适合直接作为数组下标。
例如 packed 方式如果是：
x | y << 10 | z << 20

那么 position 值可能很大，用它直接创建数组会浪费空间。
所以需要一个连续的 RegionId：
regionId = (z * MaxRow + y) * MaxCol + x

这样可以用：
int[] _indexAtRegion;

实现：
regionId -> tileIndex


5. RegionIndex
RegionIndex 负责把坐标转换成连续数组下标。
using ThreeTile.Core.ExtensionTools;

namespace ThreeTile.Core.WngZwng.Core;

public readonly struct RegionIndex
{
    public readonly int MaxCol;
    public readonly int MaxRow;
    public readonly int MaxLayer;

    public RegionIndex(
        int maxCol,
        int maxRow,
        int maxLayer)
    {
        if (maxCol <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCol));

        if (maxRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRow));

        if (maxLayer <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLayer));

        MaxCol = maxCol;
        MaxRow = maxRow;
        MaxLayer = maxLayer;
    }

    public int ToRegionId(int position)
    {
        var (x, y, z) = position.UnpackXyz();
        return ToRegionId(x, y, z);
    }

    public int ToRegionId(int x, int y, int z)
    {
#if DEBUG
        if ((uint)x >= (uint)MaxCol ||
            (uint)y >= (uint)MaxRow ||
            (uint)z >= (uint)MaxLayer)
        {
            throw new ArgumentOutOfRangeException(
                $"坐标越界：x={x}, y={y}, z={z}, " +
                $"MaxCol={MaxCol}, MaxRow={MaxRow}, MaxLayer={MaxLayer}");
        }
#endif

        return (z * MaxRow + y) * MaxCol + x;
    }

    public int GetRegionCount()
    {
        return MaxCol * MaxRow * MaxLayer;
    }
}


6. Tile 必要结构
Tile 只保存单张牌的必要静态信息。
using ThreeTile.Core.ExtensionTools;

namespace ThreeTile.Core.WngZwng.Core;

public sealed class Tile : IEquatable<Tile>
{
    public const int SuitUnspecified = -1;

    public const int MaxSuitCount = 64;

    public static readonly int DefaultVolume =
        (x: 2, y: 2, z: 1).PackXyz();

    /// <summary>关卡内唯一 ID，建议 1-based。</summary>
    public int Index { get; private set; }

    /// <summary>花色。</summary>
    public int Suit { get; private set; }

    /// <summary>Tile 的最小空间坐标，packed(x, y, z)。</summary>
    public int Position { get; private set; }

    /// <summary>Tile 的体积，packed(dx, dy, dz)。</summary>
    public int Volume { get; }

    /// <summary>当前所在区域。</summary>
    public TileArea Area { get; private set; } = TileArea.Pasture;

    /// <summary>
    /// 区域内位置索引。
    /// Pasture 下等于 Position。
    /// StagingArea 下可以表示 slot index。
    /// Removed / Corral 下可以表示对应容器 index。
    /// </summary>
    public int AreaPositionIndex { get; private set; }

    /// <summary>顶面 Z，用于遮挡判断。</summary>
    public int TopZ
    {
        get
        {
            var (_, _, z0) = Position.UnpackXyz();
            var (_, _, dz) = Volume.UnpackXyz();

            return z0 + dz - 1;
        }
    }

    public Tile(
        int index,
        int suit,
        int position,
        int? volume = null)
    {
        Index = index;
        Suit = suit;
        Position = position;
        Volume = volume ?? DefaultVolume;

        Area = TileArea.Pasture;
        AreaPositionIndex = position;
    }

    public void SetIndex(int index)
    {
        Index = index;
    }

    public void SetSuit(int suit)
    {
        Suit = suit;
    }

    public void SetPasturePosition(int position)
    {
        Position = position;
        Area = TileArea.Pasture;
        AreaPositionIndex = position;
    }

    public void SetArea(
        TileArea area,
        int areaPositionIndex)
    {
        Area = area;
        AreaPositionIndex = areaPositionIndex;
    }

    public (int x, int y, int z) GetPositionXyz()
    {
        return Position.UnpackXyz();
    }

    public (int dx, int dy, int dz) GetVolumeXyz()
    {
        return Volume.UnpackXyz();
    }

    public int FillCoordinates(Span<int> destination)
    {
        return TileCoordinateHelper.FillCoordinates(
            Position,
            Volume,
            destination);
    }

    public bool Equals(Tile? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Index == other.Index;
    }

    public override bool Equals(object? obj)
    {
        return obj is Tile other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Index;
    }

    public override string ToString()
    {
        return $"Tile #{Index} | " +
               $"Suit={Suit} | " +
               $"Position={Position.ToXyzString()} | " +
               $"Volume={Volume.ToXyzString()} | " +
               $"TopZ={TopZ} | " +
               $"Area={Area} | " +
               $"AreaPositionIndex={AreaPositionIndex}";
    }
}


7. TileArea
namespace ThreeTile.Core.WngZwng.Core;

public enum TileArea
{
    Pasture = 1 << 0,
    StagingArea = 1 << 1,
    Corral = 1 << 2,
    Removed = 1 << 3,
    Unknown = 1 << 4
}


8. TileCoordinateHelper
坐标展开逻辑单独放一个 helper，避免散落在多个类里。
using ThreeTile.Core.ExtensionTools;

namespace ThreeTile.Core.WngZwng.Core;

public static class TileCoordinateHelper
{
    public static int GetCoordinateCount(int volume)
    {
        var (dx, dy, dz) = volume.UnpackXyz();
        return dx * dy * dz;
    }

    public static int FillCoordinates(
        int position,
        int volume,
        Span<int> destination)
    {
        var (px, py, pz) = position.UnpackXyz();
        var (dx, dy, dz) = volume.UnpackXyz();

        int requiredLength = dx * dy * dz;

        if (destination.Length < requiredLength)
        {
            throw new ArgumentException(
                $"destination.Length({destination.Length}) < requiredLength({requiredLength})");
        }

        int count = 0;

        for (int z = 0; z < dz; z++)
        for (int y = 0; y < dy; y++)
        for (int x = 0; x < dx; x++)
        {
            destination[count++] = (px + x, py + y, pz + z).PackXyz();
        }

        return count;
    }
}


9. TileMappingTable 字段设计
TileMappingTable 内部全部使用扁平数组。
public sealed class TileMappingTable
{
    public const int EmptyTileIndex = -1;

    private readonly Tile[] _tiles;

    // index -> suit / position
    private readonly int[] _suitAtIndex;
    private readonly int[] _positionAtIndex;

    // suit -> flat indexes
    private readonly int[] _suitStart;
    private readonly int[] _suitCount;
    private readonly int[] _indexesBySuit;

    // tileIndex -> flat coordinates
    private readonly int[] _coordinateStartAtIndex;
    private readonly int[] _coordinateCountAtIndex;
    private readonly int[] _coordinatesByIndex;

    // regionId -> tileIndex
    private readonly int[] _indexAtRegion;

    private readonly RegionIndex _regionIndex;
}

MaxRow, MaxCol, MaxLayer, MaxTileIndex, MaxTileSuit


字段语义：
_tiles:
  index -> Tile。

_suitAtIndex:
  tileIndex -> suit。

_positionAtIndex:
  tileIndex -> position。

_suitStart:
  suit -> 在 _indexesBySuit 中的起点。

_suitCount:
  suit -> tile 数量。

_indexesBySuit:
  所有花色的 tileIndex 扁平存储。

_coordinateStartAtIndex:
  tileIndex -> 在 _coordinatesByIndex 中的起点。

_coordinateCountAtIndex:
  tileIndex -> 占据坐标数量。

_coordinatesByIndex:
  所有 tile 的坐标扁平存储。

_indexAtRegion:
  regionId -> tileIndex。

_regionIndex:
  position / x,y,z -> regionId。


10. Suit -> index[] 的扁平数组设计
不使用：
int[][] _tileIndexesAtSuit;

改成：
int[] _suitStart;

int[] _suitCount;
int[] _indexesBySuit;

例如：
Suit 0: [1, 4, 7]
Suit 1: [2, 3]
Suit 2: [5, 6, 8]

扁平化后：
_indexesBySuit = [1,4,7, 2,3, 5,6,8]

_suitStart[0] = 0
_suitCount[0] = 3

_suitStart[1] = 3
_suitCount[1] = 2

_suitStart[2] = 5
_suitCount[2] = 3

查询时：
ReadOnlySpan<int> indexes = _indexesBySuit.AsSpan(
    _suitStart[suit],
    _suitCount[suit]);

好处：
对象更少。
内存更连续。
查询不分配。
更适合热路径。


11. index -> coordinates 的扁平数组设计
不建议每个 Tile 长期保存：
int[] Coordinates

推荐用统一扁平表：
int[] _coordinateStartAtIndex;
int[] _coordinateCountAtIndex;
int[] _coordinatesByIndex;

例如：
Tile 1: [10, 11, 20, 21]
Tile 2: [30, 31, 40, 41]

扁平化后：
_coordinatesByIndex = [10, 11, 20, 21, 30, 31, 40, 41]

_coordinateStartAtIndex[1] = 0
_coordinateCountAtIndex[1] = 4

_coordinateStartAtIndex[2] = 4
_coordinateCountAtIndex[2] = 4

查询：
ReadOnlySpan<int> coordinates =
    _coordinatesByIndex.AsSpan(
        _coordinateStartAtIndex[tileIndex],
        _coordinateCountAtIndex[tileIndex]);


12. TileMappingTable 完整代码
using ThreeTile.Core.ExtensionTools;

namespace ThreeTile.Core.WngZwng.Core;

public sealed class TileMappingTable
{
    public const int EmptyTileIndex = -1;

    private readonly Tile[] _tiles;

    // index -> suit / position
    private readonly int[] _suitAtIndex;
    private readonly int[] _positionAtIndex;

    // suit -> flat indexes
    private readonly int[] _suitStart;
    private readonly int[] _suitCount;
    private readonly int[] _indexesBySuit;

    // tileIndex -> flat coordinates
    private readonly int[] _coordinateStartAtIndex;
    private readonly int[] _coordinateCountAtIndex;
    private readonly int[] _coordinatesByIndex;

    // regionId -> tileIndex
    private readonly int[] _indexAtRegion;

    private readonly RegionIndex _regionIndex;

    public int TileCount => _tiles.Length;

    public int MaxCol => _regionIndex.MaxCol;
    public int MaxRow => _regionIndex.MaxRow;
    public int MaxLayer => _regionIndex.MaxLayer;

    public TileMappingTable(
        ReadOnlySpan<int> positions,
        ReadOnlySpan<int> suits,
        int maxSuitCount)
    {
        if (positions.Length == 0)
            throw new ArgumentException("positions 不能为空", nameof(positions));

        if (positions.Length != suits.Length)
            throw new ArgumentException("positions.Length 必须等于 suits.Length");

        if (maxSuitCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSuitCount));

        int tileCount = positions.Length;

        _tiles = new Tile[tileCount];

        _suitAtIndex = new int[tileCount + 1];
        _positionAtIndex = new int[tileCount + 1];

        int maxX = 0;
        int maxY = 0;
        int maxZ = 0;

        // 1. 构建 Tile，同时计算空间边界
        for (int i = 0; i < tileCount; i++)
        {
            int tileIndex = i + 1;
            int suit = suits[i];
            int position = positions[i];
            int volume = Tile.DefaultVolume;

#if DEBUG
            if ((uint)suit >= (uint)maxSuitCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(suits),
                    $"suit 越界：suit={suit}, maxSuitCount={maxSuitCount}");
            }
#endif

            _tiles[i] = new Tile(
                index: tileIndex,
                suit: suit,
                position: position,
                volume: volume);

            _suitAtIndex[tileIndex] = suit;
            _positionAtIndex[tileIndex] = position;

            var (x, y, z) = position.UnpackXyz();
            var (dx, dy, dz) = volume.UnpackXyz();

            maxX = Math.Max(maxX, x + dx);
            maxY = Math.Max(maxY, y + dy);
            maxZ = Math.Max(maxZ, z + dz);
        }

        _regionIndex = new RegionIndex(
            maxCol: maxX,
            maxRow: maxY,
            maxLayer: maxZ);

        // 2. suit -> flat index
        BuildSuitMapping(
            _tiles,
            maxSuitCount,
            out _suitStart,
            out _suitCount,
            out _indexesBySuit);

        // 3. index -> flat coordinates
        BuildCoordinateMapping(
            _tiles,
            out _coordinateStartAtIndex,
            out _coordinateCountAtIndex,
            out _coordinatesByIndex);

        // 4. regionId -> tileIndex
        _indexAtRegion = BuildRegionMapping(
            _tiles,
            _regionIndex);
    }

    // ─────────────────────────
    // index -> Tile
    // ─────────────────────────

    public Tile GetTile(int tileIndex)
    {
        return _tiles[tileIndex - 1];
    }

    public int GetTileCount()
    {
        return _tiles.Length;
    }

    // ─────────────────────────
    // index -> Suit / Position
    // ─────────────────────────

    public int GetSuit(int tileIndex)
    {
        return _suitAtIndex[tileIndex];
    }

    public int GetPosition(int tileIndex)
    {
        return _positionAtIndex[tileIndex];
    }

    // ─────────────────────────
    // Suit -> index list
    // ─────────────────────────

    public int GetTileCountAtSuit(int suit)
    {
        return _suitCount[suit];
    }

    public ReadOnlySpan<int> GetTileIndexesAtSuit(int suit)
    {
        return _indexesBySuit.AsSpan(
            _suitStart[suit],
            _suitCount[suit]);
    }

    // ─────────────────────────
    // index -> coordinates
    // ─────────────────────────

    public ReadOnlySpan<int> GetCoordinatesByTileIndex(int tileIndex)
    {
        return _coordinatesByIndex.AsSpan(
            _coordinateStartAtIndex[tileIndex],
            _coordinateCountAtIndex[tileIndex]);
    }

    public int GetCoordinateCount(int tileIndex)
    {
        return _coordinateCountAtIndex[tileIndex];
    }

    // ─────────────────────────
    // coordinate / region -> index
    // ─────────────────────────

    public int ToRegionId(int position)
    {
        return _regionIndex.ToRegionId(position);
    }

    public int ToRegionId(int x, int y, int z)
    {
        return _regionIndex.ToRegionId(x, y, z);
    }

    public int GetTileIndexAtRegion(int regionId)
    {
        return _indexAtRegion[regionId];
    }

    public int GetTileIndexAtPosition(int position)
    {
        int regionId = _regionIndex.ToRegionId(position);
        return _indexAtRegion[regionId];
    }

    public int GetTileIndexAt(int x, int y, int z)
    {
        int regionId = _regionIndex.ToRegionId(x, y, z);
        return _indexAtRegion[regionId];
    }

    public bool TryGetTileIndexAtPosition(
        int position,
        out int tileIndex)
    {
        int regionId = _regionIndex.ToRegionId(position);
        tileIndex = _indexAtRegion[regionId];

        return tileIndex != EmptyTileIndex;
    }

    public bool TryGetTileIndexAt(
        int x,
        int y,
        int z,
        out int tileIndex)
    {
        int regionId = _regionIndex.ToRegionId(x, y, z);
        tileIndex = _indexAtRegion[regionId];

        return tileIndex != EmptyTileIndex;
    }

    // ─────────────────────────
    // Build: suit mapping
    // ─────────────────────────

    private static void BuildSuitMapping(
        Tile[] tiles,
        int maxSuitCount,
        out int[] suitStart,
        out int[] suitCount,
        out int[] indexesBySuit)
    {
        suitStart = new int[maxSuitCount];
        suitCount = new int[maxSuitCount];

        // 1. count
        for (int i = 0; i < tiles.Length; i++)
        {
            suitCount[tiles[i].Suit]++;
        }

        // 2. prefix sum
        int cursor = 0;

        for (int suit = 0; suit < maxSuitCount; suit++)
        {
            suitStart[suit] = cursor;
            cursor += suitCount[suit];
        }

        indexesBySuit = new int[tiles.Length];

        // 3. fill
        int[] offsets = new int[maxSuitCount];

        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];
            int suit = tile.Suit;

            int writeIndex = suitStart[suit] + offsets[suit]++;
            indexesBySuit[writeIndex] = tile.Index;
        }
    }

    // ─────────────────────────
    // Build: coordinate mapping
    // ─────────────────────────

    private static void BuildCoordinateMapping(
        Tile[] tiles,
        out int[] coordinateStartAtIndex,
        out int[] coordinateCountAtIndex,
        out int[] coordinatesByIndex)
    {
        int tileCount = tiles.Length;

        coordinateStartAtIndex = new int[tileCount + 1];
        coordinateCountAtIndex = new int[tileCount + 1];

        int totalCoordinateCount = 0;

        // 1. count
        for (int i = 0; i < tileCount; i++)
        {
            Tile tile = tiles[i];

            int count = TileCoordinateHelper.GetCoordinateCount(tile.Volume);

            coordinateCountAtIndex[tile.Index] = count;
            totalCoordinateCount += count;
        }

        coordinatesByIndex = new int[totalCoordinateCount];

        // 2. start + fill
        int cursor = 0;

        for (int i = 0; i < tileCount; i++)
        {
            Tile tile = tiles[i];
            int tileIndex = tile.Index;
            int count = coordinateCountAtIndex[tileIndex];

            coordinateStartAtIndex[tileIndex] = cursor;

            Span<int> destination = coordinatesByIndex.AsSpan(
                cursor,
                count);

            int written = TileCoordinateHelper.FillCoordinates(
                tile.Position,
                tile.Volume,
                destination);

#if DEBUG
            if (written != count)
            {
                throw new InvalidOperationException(
                    $"坐标写入数量异常：tileIndex={tileIndex}, written={written}, count={count}");
            }
#endif

            cursor += written;
        }
    }

    // ─────────────────────────
    // Build: region mapping
    // ─────────────────────────

    private static int[] BuildRegionMapping(
        Tile[] tiles,
        RegionIndex regionIndex)
    {
        int[] indexAtRegion = new int[regionIndex.GetRegionCount()];
        Array.Fill(indexAtRegion, EmptyTileIndex);

        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];

            int count = TileCoordinateHelper.GetCoordinateCount(tile.Volume);

            Span<int> coordinates = count <= 16
                ? stackalloc int[16]
                : new int[count];

            int written = TileCoordinateHelper.FillCoordinates(
                tile.Position,
                tile.Volume,
                coordinates);

            for (int j = 0; j < written; j++)
            {
                int position = coordinates[j];
                int regionId = regionIndex.ToRegionId(position);

                int oldTileIndex = indexAtRegion[regionId];

                if (oldTileIndex != EmptyTileIndex)
                {
                    throw new InvalidOperationException(
                        $"存在重复空间映射，regionId={regionId}, " +
                        $"oldTileIndex={oldTileIndex}, newTileIndex={tile.Index}, " +
                        $"position={position.ToXyzString()}");
                }

                indexAtRegion[regionId] = tile.Index;
            }
        }

        return indexAtRegion;
    }
}


13. 映射关系总览
13.1 Tile -> index
tile.Index


13.2 index -> Tile
Tile tile = mapping.GetTile(tileIndex);

内部：
_tiles[tileIndex - 1]


13.3 index -> Suit
int suit = mapping.GetSuit(tileIndex);

内部：
_suitAtIndex[tileIndex]


13.4 Suit -> index[]
ReadOnlySpan<int> indexes = mapping.GetTileIndexesAtSuit(suit);

内部：
_indexesBySuit.AsSpan(_suitStart[suit], _suitCount[suit])


13.5 Suit -> count
int count = mapping.GetTileCountAtSuit(suit);

内部：
_suitCount[suit]


13.6 index -> Position
int position = mapping.GetPosition(tileIndex);

内部：
_positionAtIndex[tileIndex]


13.7 index -> 所占空间坐标
ReadOnlySpan<int> coordinates =
    mapping.GetCoordinatesByTileIndex(tileIndex);

内部：
_coordinatesByIndex.AsSpan(
    _coordinateStartAtIndex[tileIndex],
    _coordinateCountAtIndex[tileIndex]);


13.8 position -> regionId
int regionId = mapping.ToRegionId(position);


13.9 x/y/z -> regionId
int regionId = mapping.ToRegionId(x, y, z);


13.10 regionId -> index
int tileIndex = mapping.GetTileIndexAtRegion(regionId);

内部：
_indexAtRegion[regionId]


13.11 position -> index
int tileIndex = mapping.GetTileIndexAtPosition(position);

等价于：
int regionId = mapping.ToRegionId(position);
int tileIndex = mapping.GetTileIndexAtRegion(regionId);


14. 使用示例
14.1 查询某个花色的全部 Tile
ReadOnlySpan<int> indexes = mapping.GetTileIndexesAtSuit(suit);

for (int i = 0; i < indexes.Length; i++)
{
    int tileIndex = indexes[i];

    Tile tile = mapping.GetTile(tileIndex);

    // use tile
}


14.2 查询某个坐标上是否有 Tile
int position = (x, y, z).PackXyz();

if (mapping.TryGetTileIndexAtPosition(position, out var tileIndex))
{
    Tile tile = mapping.GetTile(tileIndex);

    // use tile
}


14.3 查询 Tile 占据的所有坐标
ReadOnlySpan<int> coordinates =
    mapping.GetCoordinatesByTileIndex(tileIndex);

for (int i = 0; i < coordinates.Length; i++)
{
    int position = coordinates[i];

    // use position
}


14.4 查询 regionId 上的 Tile
int regionId = mapping.ToRegionId(x, y, z);

int tileIndex = mapping.GetTileIndexAtRegion(regionId);

if (tileIndex != TileMappingTable.EmptyTileIndex)
{
    Tile tile = mapping.GetTile(tileIndex);

    // use tile
}


15. 和 BitSet 的关系
TileMappingTable 只负责静态映射。
例如：
tileIndex -> Suit
tileIndex -> Position
regionId -> tileIndex
Suit -> tileIndex[]

动态状态交给 BitSet：
public sealed class LevelState
{
    public ulong[] PresentBits { get; }
    public ulong[] VisibleBits { get; }
    public ulong[] SelectableBits { get; }
    public ulong[] LockedBits { get; }
}

组合使用：
ReadOnlySpan<int> indexes = mapping.GetTileIndexesAtSuit(suit);

for (int i = 0; i < indexes.Length; i++)
{
    int tileIndex = indexes[i];

    if (state.IsSelectable(tileIndex))
    {
        // same suit and selectable
    }
}

或者：
int tileIndex = mapping.GetTileIndexAtPosition(position);

if (tileIndex != TileMappingTable.EmptyTileIndex &&
    state.IsPresent(tileIndex))
{
    // this position is occupied by an existing tile
}


16. 为什么这个版本高性能
原因：
1. index 查询是数组访问。
2. Suit 查询返回 ReadOnlySpan<int>，不分配。
3. coordinates 查询返回 ReadOnlySpan<int>，不分配。
4. region -> index 是 int[]，不是 Dictionary。
5. Tile 里不长期保存 Coordinates 数组。
6. 所有映射在构造阶段一次性完成。
7. 运行时只读查询，不产生临时集合。

例如：
int tileIndex = mapping.GetTileIndexAt(x, y, z);

内部只有：
x/y/z -> regionId
_indexAtRegion[regionId]


17. 为什么这个版本正交性好
因为职责拆得很清楚：
Tile:
  单张牌的事实。

RegionIndex:
  坐标转 regionId。

TileMappingTable:
  静态映射。

LevelState:
  动态状态。

BitOps:
  集合操作。

TileMappingTable 不关心：
是否可见
是否可选
是否已移除
是否锁定
dock
feature
score

所以它可以稳定复用。

18. 适用前提
扁平数组版适合：
空间区域相对紧凑。
MaxCol * MaxRow * MaxLayer 不大。
tileIndex 连续。
tile 数量固定。
构造后映射基本不变。

如果以后出现：
x/y/z 范围巨大
实际 Tile 很少
空间非常稀疏

那么：
int[] _indexAtRegion

可能浪费内存。
那时再考虑替换为：
Dictionary<int, int> _indexAtPosition;

但是 value 仍然建议是：
tileIndex

而不是：
Tile


19. 最终结论
最终结构：
Tile
  Index
  Suit
  Position
  Volume
  Area
  AreaPositionIndex

RegionIndex
  Position -> RegionId
  x/y/z -> RegionId

TileCoordinateHelper
  Position + Volume -> Coordinates

TileMappingTable
  index -> Tile
  index -> Suit
  index -> Position
  Suit -> index[]
  Suit -> count
  index -> coordinates
  regionId -> index

最终原则：
静态映射全部数组化。
动态状态全部 BitSet 化。
业务语义在上层组合。

一句话：
TileMappingTable 是 ThreeTile 的静态索引层；它用扁平数组把 Tile、Suit、Position、RegionId、TileIndex 之间的关系全部预处理好，为后续高性能求解提供基础。
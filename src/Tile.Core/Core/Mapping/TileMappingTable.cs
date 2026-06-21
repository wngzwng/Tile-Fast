
using System.Numerics;
using Tile.Core.Common.BitSet;
using Tile.Core.Core.Types;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Core.Mapping;

/// <summary>
/// TileMappingTable 是关卡构建完成后的静态索引表。
/// 它只保存构建后不变的 Tile 静态事实与高频静态映射。
/// </summary>
public sealed class TileMappingTable
{
    private readonly Tile[] _tiles;

    // tileIndex -> suit
    private readonly int[] _suitByTileIndex;

    // tileIndex -> packed position，Tile 的基准位置
    private readonly int[] _positionByTileIndex;

    // tileIndex -> Tile 占据的所有 regionId
    // 起始位置 = tileIndex * OccupiedRegionCountPerTile
    // 长度 = OccupiedRegionCountPerTile
    private readonly int[] _occupiedRegionIdsByTileIndexFlat;

    // regionId -> tileIndex
    // 未被占用时为 -1
    private readonly int[] _tileIndexByRegionId;

    // suit -> tile bitset
    // 起始位置 = suit * WordCount
    // 长度 = WordCount
    private readonly ulong[] _tileBitsBySuitFlat;

    // tileIndex -> affected tile bitset
    // 起始位置 = tileIndex * WordCount
    // 长度 = WordCount
    private readonly ulong[] _affectedTileBitsByTileIndexFlat;

    // 当前关卡实际出现过的 suit 集合
    private readonly ulong _suitBits;

    private readonly RegionIdMapper _regionIdMapper;

    public int TileCount => _tiles.Length;

    public int WordCount { get; }

    public int MaxSuitCount => Tile.MaxSuitCount;

    /// <summary>
    /// 当前关卡实际出现过的 suit 集合。
    /// 第 suit 位为 1，表示该 suit 出现过。
    /// </summary>
    public ulong SuitBits => _suitBits;

    /// <summary>
    /// 当前关卡实际出现过的 suit 数量。
    /// </summary>
    public int SuitCount => BitOperations.PopCount(_suitBits);

    /// <summary>
    /// 每个 Tile 固定占据的 region 数量。
    /// 当前 MVP 阶段所有 Tile 使用默认体积，所以该值对所有 Tile 相同。
    /// </summary>
    public int OccupiedRegionCountPerTile { get; }

    /// <summary>
    /// 所有 Tile 实际占据的 region 数量。
    /// </summary>
    public int OccupiedRegionCount => TileCount * OccupiedRegionCountPerTile;

    /// <summary>
    /// 当前空间可映射出的 region 总数量。
    /// </summary>
    public int RegionCount => _regionIdMapper.RegionCount;

    public int MaxCol => _regionIdMapper.MaxCol;

    public int MaxRow => _regionIdMapper.MaxRow;

    public int MaxLayer => _regionIdMapper.MaxLayer;

    public LockRuleTypeEnum LockRule { get; }

    private TileMappingTable(
        Tile[] tiles,
        LockRuleTypeEnum lockRule,
        int wordCount,
        int occupiedRegionCountPerTile,
        RegionIdMapper regionIdMapper,
        int[] suitByTileIndex,
        int[] positionByTileIndex,
        int[] occupiedRegionIdsByTileIndexFlat,
        int[] tileIndexByRegionId,
        ulong[] tileBitsBySuitFlat,
        ulong[] affectedTileBitsByTileIndexFlat,
        ulong suitBits)
    {
        _tiles = tiles;
        LockRule = lockRule;
        WordCount = wordCount;
        OccupiedRegionCountPerTile = occupiedRegionCountPerTile;
        _regionIdMapper = regionIdMapper;

        _suitByTileIndex = suitByTileIndex;
        _positionByTileIndex = positionByTileIndex;
        _occupiedRegionIdsByTileIndexFlat = occupiedRegionIdsByTileIndexFlat;
        _tileIndexByRegionId = tileIndexByRegionId;
        _tileBitsBySuitFlat = tileBitsBySuitFlat;
        _affectedTileBitsByTileIndexFlat = affectedTileBitsByTileIndexFlat;
        _suitBits = suitBits;
    }

    public static TileMappingTable Create(
        IReadOnlyList<Tile> tiles,
        LockRuleTypeEnum lockRule,
        int maxCol,
        int maxRow,
        int maxLayer)
    {
        if (tiles is null)
            throw new ArgumentNullException(nameof(tiles));

        var tileCount = tiles.Count;
        var wordCount = BitSetOperations.BitLenToU64Len(tileCount);

        var regionIdMapper = new RegionIdMapper(
            maxCol,
            maxRow,
            maxLayer);

        var occupiedRegionCountPerTile =
            TileBounds.Default.CountRegions();

        var tileArray = new Tile[tileCount];

        var suitByTileIndex = new int[tileCount];
        var positionByTileIndex = new int[tileCount];

        var occupiedRegionIdsByTileIndexFlat =
            new int[tileCount * occupiedRegionCountPerTile];

        var tileIndexByRegionId =
            new int[regionIdMapper.RegionCount];

        Array.Fill(tileIndexByRegionId, -1);

        var tileBitsBySuitFlat =
            new ulong[Tile.MaxSuitCount * wordCount];

        var affectedTileBitsByTileIndexFlat =
            new ulong[tileCount * wordCount];

        var suitBits = 0UL;

        for (var i = 0; i < tileCount; i++)
        {
            var tile = tiles[i];

            if (tile is null)
            {
                throw new ArgumentException(
                    $"tiles[{i}] 不能为 null。",
                    nameof(tiles));
            }

            var tileIndex = tile.Index;
            var suit = tile.Suit;

            if ((uint)tileIndex >= (uint)tileCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tile.Index),
                    $"Tile.Index 必须位于 [0, {tileCount}) 范围内，当前 index={tileIndex}。");
            }

            if ((uint)suit >= Tile.MaxSuitCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tile.Suit),
                    $"Tile.Suit 超出范围：suit={suit}, max={Tile.MaxSuitCount}。");
            }

            if (tileArray[tileIndex] is not null)
            {
                throw new ArgumentException(
                    $"存在重复的 Tile.Index：index={tileIndex}。",
                    nameof(tiles));
            }

            tileArray[tileIndex] = tile;

            suitByTileIndex[tileIndex] = suit;
            positionByTileIndex[tileIndex] = tile.Position;

            suitBits |= 1UL << suit;

            var suitTileBits = tileBitsBySuitFlat.AsSpan(
                suit * wordCount,
                wordCount);

            BitSetOperations.Set(suitTileBits, tileIndex);
        }

        for (var tileIndex = 0; tileIndex < tileArray.Length; tileIndex++)
        {
            if (tileArray[tileIndex] is null)
            {
                throw new ArgumentException(
                    $"tiles 缺少 Tile.Index={tileIndex} 的 Tile。Tile.Index 必须从 0 到 tileCount - 1 连续。",
                    nameof(tiles));
            }
        }

        BuildOccupiedRegionIds(
            tileArray,
            regionIdMapper,
            occupiedRegionCountPerTile,
            occupiedRegionIdsByTileIndexFlat,
            tileIndexByRegionId);

        var mapping = new TileMappingTable(
            tileArray,
            lockRule,
            wordCount,
            occupiedRegionCountPerTile,
            regionIdMapper,
            suitByTileIndex,
            positionByTileIndex,
            occupiedRegionIdsByTileIndexFlat,
            tileIndexByRegionId,
            tileBitsBySuitFlat,
            affectedTileBitsByTileIndexFlat,
            suitBits);

        mapping.BuildAffectedTileBits(
            tileArray,
            lockRule,
            wordCount,
            affectedTileBitsByTileIndexFlat);
        
        return mapping;
        
    }

    public Tile GetTile(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return _tiles[tileIndex];
    }

    public int GetSuit(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return _suitByTileIndex[tileIndex];
    }

    public int GetPosition(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return _positionByTileIndex[tileIndex];
    }

    public bool HasSuit(int suit)
    {
        if ((uint)suit >= Tile.MaxSuitCount)
            return false;

        return (_suitBits & (1UL << suit)) != 0;
    }

    public ReadOnlySpan<ulong> GetTileBitsBySuit(int suit)
    {
        ValidateSuit(suit);

        if (!HasSuit(suit))
            return ReadOnlySpan<ulong>.Empty;

        return _tileBitsBySuitFlat.AsSpan(
            suit * WordCount,
            WordCount);
    }

    public ReadOnlySpan<ulong> GetAffectedTileBits(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        return _affectedTileBitsByTileIndexFlat.AsSpan(
            tileIndex * WordCount,
            WordCount);
    }

    public ReadOnlySpan<int> GetOccupiedRegionIds(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        return _occupiedRegionIdsByTileIndexFlat.AsSpan(
            tileIndex * OccupiedRegionCountPerTile,
            OccupiedRegionCountPerTile);
    }

    public bool TryGetTileIndexAtRegionId(
        int regionId,
        out int tileIndex)
    {
        if ((uint)regionId >= (uint)_tileIndexByRegionId.Length)
        {
            tileIndex = -1;
            return false;
        }

        tileIndex = _tileIndexByRegionId[regionId];
        return tileIndex >= 0;
    }

    public int GetTileIndexAtRegionId(int regionId)
    {
        if (TryGetTileIndexAtRegionId(regionId, out var tileIndex))
            return tileIndex;

        throw new InvalidOperationException(
            $"regionId 未被任何 Tile 占据：regionId={regionId}。");
    }

    public bool TryGetTileIndexAtPosition(
        int position,
        out int tileIndex)
    {
        var regionId = _regionIdMapper.ToRegionId(position);
        return TryGetTileIndexAtRegionId(regionId, out tileIndex);
    }

    public int GetTileIndexAtPosition(int position)
    {
        var regionId = _regionIdMapper.ToRegionId(position);
        return GetTileIndexAtRegionId(regionId);
    }

    public int ToRegionId(int position)
    {
        return _regionIdMapper.ToRegionId(position);
    }

    public int ToRegionId(int x, int y, int z)
    {
        return _regionIdMapper.ToRegionId(x, y, z);
    }

    public bool TileOccupiesRegionId(int tileIndex, int regionId)
    {
        var regionIds = GetOccupiedRegionIds(tileIndex);

        foreach (var occupiedRegionId in regionIds)
        {
            if (occupiedRegionId == regionId)
                return true;
        }

        return false;
    }

    public bool TileOccupiesPosition(int tileIndex, int position)
    {
        var regionId = _regionIdMapper.ToRegionId(position);
        return TileOccupiesRegionId(tileIndex, regionId);
    }

    public IEnumerable<int> EnumerateSuits()
    {
        var bits = _suitBits;

        while (bits != 0)
        {
            var suit = BitOperations.TrailingZeroCount(bits);
            yield return suit;

            bits &= bits - 1;
        }
    }

    public TileIndexSet GetTileIndexSetBySuit(int suit)
    {
        return TileIndexSet.Wrap(GetTileBitsBySuit(suit));
    }

    public TileIndexSet GetAffectedTileIndexSet(int tileIndex)
    {
        return TileIndexSet.Wrap(GetAffectedTileBits(tileIndex));
    }

    private void ValidateTileIndex(int tileIndex)
    {
        if ((uint)tileIndex >= (uint)_tiles.Length)
            throw new ArgumentOutOfRangeException(nameof(tileIndex));
    }

    private static void ValidateSuit(int suit)
    {
        if ((uint)suit >= Tile.MaxSuitCount)
            throw new ArgumentOutOfRangeException(nameof(suit));
    }

    private static void BuildOccupiedRegionIds(
        Tile[] tiles,
        RegionIdMapper regionIdMapper,
        int occupiedRegionCountPerTile,
        int[] occupiedRegionIdsByTileIndexFlat,
        int[] tileIndexByRegionId)
    {
        foreach (var tile in tiles)
        {
            var regionIds = occupiedRegionIdsByTileIndexFlat.AsSpan(
                tile.Index * occupiedRegionCountPerTile,
                occupiedRegionCountPerTile);

            var bounds = TileBounds.FromTileDefaultVolume(tile);

            var filledCount = bounds.FillRegionIds(
                regionIdMapper,
                regionIds);

            if (filledCount != occupiedRegionCountPerTile)
            {
                throw new InvalidOperationException(
                    $"Tile occupied region 数量异常：tileIndex={tile.Index}, " +
                    $"expected={occupiedRegionCountPerTile}, actual={filledCount}。");
            }

            foreach (var regionId in regionIds)
            {
                var existingTileIndex = tileIndexByRegionId[regionId];

                if (existingTileIndex >= 0)
                {
                    throw new InvalidOperationException(
                        $"Tile 空间位置重叠：regionId={regionId}, " +
                        $"existingTileIndex={existingTileIndex}, currentTileIndex={tile.Index}。");
                }

                tileIndexByRegionId[regionId] = tile.Index;
            }
        }
    }
    
    private  void BuildAffectedTileBits(
        Tile[] tiles,
        LockRuleTypeEnum lockRule,
        int wordCount,
        ulong[] affectedTileBitsByTileIndexFlat)
    {
        
        var boardBounds = PositionPacker.PackXyz(MaxCol, MaxRow, MaxLayer);
        foreach (var tile in tiles)
        {
            var affectedBits = affectedTileBitsByTileIndexFlat.AsSpan(
                tile.Index * wordCount,
                wordCount);

            var (startX, startY, startZ) = PositionPacker.UnpackXyz(tile.Position);

            // ------------------------------------------------------------
            // 1. 收集下方受影响的棋子
            //
            // 当前 Tile 默认覆盖 2x2 面：
            // (x,     y)
            // (x + 1, y)
            // (x,     y + 1)
            // (x + 1, y + 1)
            //
            // 对每个覆盖点，从当前层下方开始向下探测。
            // 每个垂直柱只命中第一个棋子。
            // ------------------------------------------------------------
            if (startZ > 0)
            {
                AddFirstHitBelow(
                    startX,
                    startY,
                    startZ,
                    affectedBits);

                AddFirstHitBelow(
                    startX + 1,
                    startY,
                    startZ,
                    affectedBits);

                AddFirstHitBelow(
                    startX,
                    startY + 1,
                    startZ,
                    affectedBits);

                AddFirstHitBelow(
                    startX + 1,
                    startY + 1,
                    startZ,
                    affectedBits);
            }

            // ------------------------------------------------------------
            // 2. Classic 规则额外收集左右锁定相关棋子
            //
            // Tile 模式：
            // 只关心上下遮挡关系。
            //
            // Classic 模式：
            // 还要关心左右方向上的邻接棋子。
            //
            // 当前 Tile 默认占用 2x2 面：
            // 左侧边界：x
            // 右侧边界：x + 1
            //
            // 因此：
            // - 左侧检查 (x, y, z) 和 (x, y + 1, z) 的左邻居
            // - 右侧检查 (x + 1, y, z) 和 (x + 1, y + 1, z) 的右邻居
            // ------------------------------------------------------------
            if (lockRule == LockRuleTypeEnum.Classic)
            {
                AddNeighborHit(
                    NeighborDir.Left,
                    PositionPacker.PackXyz(startX, startY, startZ),
                    boardBounds,
                    affectedBits);

                AddNeighborHit(
                    NeighborDir.Left,
                    PositionPacker.PackXyz(startX, startY + 1, startZ),
                    boardBounds,
                    affectedBits);

                AddNeighborHit(
                    NeighborDir.Right,
                    PositionPacker.PackXyz(startX + 1, startY, startZ),
                    boardBounds,
                    affectedBits);

                AddNeighborHit(
                    NeighborDir.Right,
                    PositionPacker.PackXyz(startX + 1, startY + 1, startZ),
                    boardBounds,
                    affectedBits);
            }
        }

       

        void AddNeighborHit(
            NeighborDir dir,
            int position,
            int bounds,
            Span<ulong> affectedBits)
        {
            var next = dir.Move(position, bounds, out var isOver);

            if (isOver)
                return;

            if (TryGetTileIndexAtPosition(next, out var tileIndex))
                BitSetOperations.Set(affectedBits, tileIndex);
        }
    }

    public void AddNeighborHit(
            NeighborDir dir,
            int position,
            int bounds,
            Span<ulong> affectedBits)
        {
            var next = dir.Move(position, bounds, out var isOver);

            if (isOver)
                return;

            if (TryGetTileIndexAtPosition(next, out var tileIndex))
                BitSetOperations.Set(affectedBits, tileIndex);
        }

     void AddFirstHitBelow(
            int x,
            int y,
            int startZ,
            Span<ulong> affectedBits)
    {
        for (var z = startZ - 1; z >= 0; z--)
        {
            var position = PositionPacker.PackXyz(x, y, z);

            if (!TryGetTileIndexAtPosition(position, out var tileIndex))
                continue;

            BitSetOperations.Set(affectedBits, tileIndex);
            break;
        }
    }

    




    /// <summary>
    /// TileMappingTable 内部使用的 Tile 默认体积包围盒。
    /// 当前 MVP 阶段所有 Tile 使用默认体积。
    /// </summary>
    private readonly struct TileBounds
    {
        public static TileBounds Default
        {
            get
            {
                var (dx, dy, dz) = Tile.DefaultVolume.UnpackXyz();
                return new TileBounds(0, 0, 0, dx, dy, dz);
            }
        }

        public int MinX { get; }

        public int MinY { get; }

        public int MinZ { get; }

        /// <summary>
        /// X 方向右边界，exclusive。
        /// </summary>
        public int MaxX { get; }

        /// <summary>
        /// Y 方向右边界，exclusive。
        /// </summary>
        public int MaxY { get; }

        /// <summary>
        /// Z 方向右边界，exclusive。
        /// </summary>
        public int MaxZ { get; }

        public int SizeX => MaxX - MinX;

        public int SizeY => MaxY - MinY;

        public int SizeZ => MaxZ - MinZ;

        private TileBounds(
            int minX,
            int minY,
            int minZ,
            int maxX,
            int maxY,
            int maxZ)
        {
            if (maxX <= minX)
                throw new ArgumentOutOfRangeException(nameof(maxX), "MaxX 必须大于 MinX。");

            if (maxY <= minY)
                throw new ArgumentOutOfRangeException(nameof(maxY), "MaxY 必须大于 MinY。");

            if (maxZ <= minZ)
                throw new ArgumentOutOfRangeException(nameof(maxZ), "MaxZ 必须大于 MinZ。");

            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        public static TileBounds FromTileDefaultVolume(Tile tile)
        {
            var (x, y, z) = tile.Position.UnpackXyz();
            var (dx, dy, dz) = Tile.DefaultVolume.UnpackXyz();

            return new TileBounds(
                x,
                y,
                z,
                x + dx,
                y + dy,
                z + dz);
        }

        public int CountRegions()
        {
            return SizeX * SizeY * SizeZ;
        }

        public int FillRegionIds(
            RegionIdMapper regionIdMapper,
            Span<int> buffer)
        {
            var count = CountRegions();

            if (buffer.Length < count)
                throw new ArgumentException("buffer 长度不足。", nameof(buffer));

            var offset = 0;

            for (var z = MinZ; z < MaxZ; z++)
            {
                for (var y = MinY; y < MaxY; y++)
                {
                    for (var x = MinX; x < MaxX; x++)
                    {
                        buffer[offset++] = regionIdMapper.ToRegionId(x, y, z);
                    }
                }
            }

            return offset;
        }

        public bool Contains(int x, int y, int z)
        {
            return x >= MinX && x < MaxX
                && y >= MinY && y < MaxY
                && z >= MinZ && z < MaxZ;
        }

        public bool ContainsPosition(int position)
        {
            var (x, y, z) = position.UnpackXyz();
            return Contains(x, y, z);
        }

        public bool Overlaps(TileBounds other)
        {
            return MinX < other.MaxX
                && MaxX > other.MinX
                && MinY < other.MaxY
                && MaxY > other.MinY
                && MinZ < other.MaxZ
                && MaxZ > other.MinZ;
        }

        public bool TouchOrOverlap(TileBounds other)
        {
            return MinX <= other.MaxX
                && MaxX >= other.MinX
                && MinY <= other.MaxY
                && MaxY >= other.MinY
                && MinZ <= other.MaxZ
                && MaxZ >= other.MinZ;
        }
    }
}
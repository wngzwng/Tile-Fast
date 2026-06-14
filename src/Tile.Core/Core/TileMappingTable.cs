using Tile.Core.ExtensionTools;

namespace Tile.Core;

/// <summary>
/// Tile 的静态扁平映射表。
/// 它是 <c>LevelCore</c> 的辅助组件，
/// 专门负责 Tile 相关的静态映射处理，
/// 不承载动态状态，也不单独作为核心领域对象存在。
/// </summary>
public sealed class TileMappingTable
{
    /// <summary>
    /// 表示“该区域没有 Tile”的默认返回值。
    /// 未找到时统一返回 <c>-1</c>。
    /// </summary>
    public const int EmptyTileIndex = -1;

    /// <summary>
    /// 当前映射表中 Tile 的总数量。
    /// 约定 Tile.Index 的有效范围为 <c>[0, _tileCount - 1]</c>。
    /// </summary>
    private readonly int _tileCount;

    /// <summary>
    /// 单个 Tile 占用的固定空间单元数量。
    /// 当前 MVP 阶段所有 Tile 都使用 <see cref="Tile.DefaultVolume"/>，
    /// 因此每个 Tile 的空间坐标数量都固定为 <see cref="_tileVolumeSize"/> 个，
    /// 且该值对整张映射表恒定不变。
    /// 公式：
    /// <c>tileVolumeSize = dx * dy * dz</c>。
    /// </summary>
    private readonly int _tileVolumeSize;

    /// <summary>
    /// 映射表覆盖空间的 X 轴边界上限。
    /// X 表示横轴；
    /// 有效范围为 <c>[0, MaxX - 1]</c>。
    /// </summary>
    private readonly int _maxX;

    /// <summary>
    /// 映射表覆盖空间的 Y 轴边界上限。
    /// Y 表示纵轴；
    /// 有效范围为 <c>[0, MaxY - 1]</c>。
    /// </summary>
    private readonly int _maxY;

    /// <summary>
    /// 映射表覆盖空间的 Z 轴边界上限。
    /// Z 表示层高方向；
    /// 有效范围为 <c>[0, MaxZ - 1]</c>。
    /// </summary>
    private readonly int _maxZ;

    /// <summary>
    /// 按 Tile.Index 顺序扁平存储所有 Tile 占用的空间坐标。
    /// 每个 Tile 恰好占用 <see cref="_tileVolumeSize"/> 个坐标槽位。
    /// </summary>
    private readonly int[] _coordinatesByIndex;

    /// <summary>
    /// 按连续 RegionId 记录该空间位置上对应的 Tile.Index。
    /// 若某个 RegionId 没有 Tile，则该位置存放 <see cref="EmptyTileIndex"/>。
    /// </summary>
    private readonly int[] _indexAtRegion;

    private readonly RegionIndex _regionIndex;

    /// <summary>
    /// Tile 总数。
    /// </summary>
    public int TileCount => _tileCount;

    /// <summary>
    /// 最大 X 轴边界。
    /// X 表示横轴；
    /// 有效范围为 <c>[0, MaxCol - 1]</c>。
    /// </summary>
    public int MaxCol => _maxX;

    /// <summary>
    /// 最大 Y 轴边界。
    /// Y 表示纵轴；
    /// 有效范围为 <c>[0, MaxRow - 1]</c>。
    /// </summary>
    public int MaxRow => _maxY;

    /// <summary>
    /// 最大 Z 轴边界。
    /// Z 表示层高方向；
    /// 有效范围为 <c>[0, MaxLayer - 1]</c>。
    /// </summary>
    public int MaxLayer => _maxZ;

    /// <summary>
    /// 使用 Tile 集合创建静态映射表。
    /// 约定：
    /// 1. <see cref="Tile.Index"/> 必须是连续的 0-based；
    /// 2. 当前 MVP 约定每个 Tile 的空间大小固定为 <see cref="Tile.DefaultVolume"/>；
    ///    如果传入非默认体积，会直接报出“暂未适配”的限制；
    /// 3. 不允许两个 Tile 占用同一个空间坐标。
    /// </summary>
    public TileMappingTable(ReadOnlySpan<Tile> tiles)
    {
        if (tiles.IsEmpty)
            throw new ArgumentException("tiles 不能为空。", nameof(tiles));

        int tileCount = tiles.Length;
        _tileCount = tileCount;
        _tileVolumeSize = TileCoordinateHelper.GetCoordinateCount(Tile.DefaultVolume);
        bool[] seenIndexes = new bool[tileCount];
        int maxX = 0;
        int maxY = 0;
        int maxZ = 0;

        for (int i = 0; i < tileCount; i++)
        {
            Tile tile = tiles[i];

            ValidateTile(tile, tileCount);

            if (seenIndexes[tile.Index])
                throw new ArgumentException($"存在重复 Tile.Index：{tile.Index}。", nameof(tiles));

            seenIndexes[tile.Index] = true;

            var (x, y, z) = tile.GetPositionXyz();
            var (dx, dy, dz) = tile.GetVolumeXyz();

            maxX = Math.Max(maxX, x + dx);
            maxY = Math.Max(maxY, y + dy);
            maxZ = Math.Max(maxZ, z + dz);
        }

        for (int i = 0; i < seenIndexes.Length; i++)
        {
            if (!seenIndexes[i])
                throw new ArgumentException($"Tile.Index 必须连续覆盖 [0, {tileCount - 1}]，缺少 index={i}。", nameof(tiles));
        }

        _maxX = maxX;
        _maxY = maxY;
        _maxZ = maxZ;
        _regionIndex = new RegionIndex(maxX, maxY, maxZ);

        BuildCoordinateMapping(
            tiles,
            out _coordinatesByIndex);

        _indexAtRegion = BuildRegionMapping(tiles, _regionIndex);
    }

    /// <summary>
    /// 返回 Tile 总数。
    /// 与 <see cref="TileCount"/> 等价。
    /// </summary>
    /// <summary>
    /// 返回指定 Tile.Index 占用的全部坐标。
    /// 当前 MVP 阶段，每个 Tile 的空间坐标数量固定为 <see cref="_tileVolumeSize"/> 个。
    /// </summary>
    public ReadOnlySpan<int> GetCoordinatesByTileIndex(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return _coordinatesByIndex.AsSpan(
            tileIndex * _tileVolumeSize,
            _tileVolumeSize);
    }

    /// <summary>
    /// 将 packed(x, y, z) 坐标转换为连续区域下标。
    /// </summary>
    private int ToRegionId(int position)
    {
        return _regionIndex.ToRegionId(position);
    }

    /// <summary>
    /// 将 <c>(x, y, z)</c> 坐标转换为连续区域下标。
    /// </summary>
    private int ToRegionId(int x, int y, int z)
    {
        return _regionIndex.ToRegionId(x, y, z);
    }

    /// <summary>
    /// 根据区域下标返回占用该区域的 Tile.Index。
    /// 如果该区域没有 Tile，返回 <see cref="EmptyTileIndex"/>。
    /// </summary>
    private int GetTileIndexAtRegion(int regionId)
    {
        if ((uint)regionId >= (uint)_indexAtRegion.Length)
            throw new ArgumentOutOfRangeException(nameof(regionId), $"regionId 越界：{regionId}。");

        return _indexAtRegion[regionId];
    }

    /// <summary>
    /// 根据 packed(x, y, z) 查询该位置的 Tile.Index。
    /// 未找到时返回 <see cref="EmptyTileIndex"/>。
    /// </summary>
    public int GetTileIndexAtPosition(int position)
    {
        int regionId = ToRegionId(position);
        return GetTileIndexAtRegion(regionId);
    }

    /// <summary>
    /// 根据 <c>(x, y, z)</c> 查询该位置的 Tile.Index。
    /// 未找到时返回 <see cref="EmptyTileIndex"/>。
    /// </summary>
    public int GetTileIndexAt(int x, int y, int z)
    {
        int regionId = ToRegionId(x, y, z);
        return GetTileIndexAtRegion(regionId);
    }

    /// <summary>
    /// 尝试按 packed(x, y, z) 查询 Tile.Index。
    /// 成功返回 <see langword="true"/>，失败时返回 <see langword="false"/> 且 <paramref name="tileIndex"/> 为 <see cref="EmptyTileIndex"/>。
    /// </summary>
    public bool TryGetTileIndexAtPosition(int position, out int tileIndex)
    {
        int regionId = ToRegionId(position);
        tileIndex = GetTileIndexAtRegion(regionId);
        return tileIndex != EmptyTileIndex;
    }

    /// <summary>
    /// 尝试按 <c>(x, y, z)</c> 查询 Tile.Index。
    /// 成功返回 <see langword="true"/>，失败时返回 <see langword="false"/> 且 <paramref name="tileIndex"/> 为 <see cref="EmptyTileIndex"/>。
    /// </summary>
    public bool TryGetTileIndexAt(int x, int y, int z, out int tileIndex)
    {
        int regionId = ToRegionId(x, y, z);
        tileIndex = GetTileIndexAtRegion(regionId);
        return tileIndex != EmptyTileIndex;
    }

    /// <summary>
    /// 使用 packed(x, y, z) 坐标直接索引对应的 Tile.Index。
    /// 若该位置没有 Tile，则返回 <see cref="EmptyTileIndex"/>。
    /// </summary>
    public int this[int position] => GetTileIndexAtPosition(position);

    /// <summary>
    /// 使用 <c>(x, y, z)</c> 坐标直接索引对应的 Tile.Index。
    /// 若该位置没有 Tile，则返回 <see cref="EmptyTileIndex"/>。
    /// </summary>
    public int this[int x, int y, int z] => GetTileIndexAt(x, y, z);

    private static void ValidateTile(Tile tile, int tileCount)
    {
        if ((uint)tile.Index >= (uint)tileCount)
        {
            throw new ArgumentException(
                $"Tile.Index 越界：index={tile.Index}，要求连续 0-based，且必须落在 [0, {tileCount - 1}] 内。");
        }

        if (tile.Volume != Tile.DefaultVolume)
        {
            throw new NotSupportedException(
                $"检测到非默认体积 Tile.Volume={tile.Volume.ToXyzString()}；当前 TileMappingTable 仅适配 DefaultVolume={Tile.DefaultVolume.ToXyzString()}，非默认体积暂未适配。");
        }
    }

    private void ValidateTileIndex(int tileIndex)
    {
        if ((uint)tileIndex >= (uint)_tileCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tileIndex),
                $"tileIndex 越界：tileIndex={tileIndex}，有效范围为 [0, {_tileCount - 1}]。");
        }
    }

    private static void BuildCoordinateMapping(
        ReadOnlySpan<Tile> tiles,
        out int[] coordinatesByIndex)
    {
        int tileCount = tiles.Length;
        int coordinateCountPerTile = TileCoordinateHelper.GetCoordinateCount(Tile.DefaultVolume);
        coordinatesByIndex = new int[tileCount * coordinateCountPerTile];

        for (int i = 0; i < tileCount; i++)
        {
            Tile tile = tiles[i];
            int tileIndex = tile.Index;
            int start = tileIndex * coordinateCountPerTile;

            Span<int> destination = coordinatesByIndex.AsSpan(start, coordinateCountPerTile);
            _ = TileCoordinateHelper.FillCoordinates(tile.Position, tile.Volume, destination);
        }
    }

    private static int[] BuildRegionMapping(ReadOnlySpan<Tile> tiles, RegionIndex regionIndex)
    {
        int[] indexAtRegion = new int[regionIndex.GetRegionCount()];
        Array.Fill(indexAtRegion, EmptyTileIndex);

        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];
            int count = TileCoordinateHelper.GetCoordinateCount(tile.Volume);

            int[] coordinates = new int[count];

            int written = TileCoordinateHelper.FillCoordinates(tile.Position, tile.Volume, coordinates);

            for (int j = 0; j < written; j++)
            {
                int position = coordinates[j];
                int regionId = regionIndex.ToRegionId(position);

                if (indexAtRegion[regionId] != EmptyTileIndex)
                {
                    throw new InvalidOperationException(
                        $"存在重复空间映射：regionId={regionId}, position={position.ToXyzString()}, oldTileIndex={indexAtRegion[regionId]}, newTileIndex={tile.Index}。");
                }

                indexAtRegion[regionId] = tile.Index;
            }
        }

        return indexAtRegion;
    }

    /// <summary>
    /// TileMappingTable 内部使用的区域索引器。
    /// 它只服务于当前映射表的坐标压平，不对外单独暴露。
    /// </summary>
    private readonly struct RegionIndex
    {
        /// <summary>
        /// 空间 X 轴上限。
        /// X 表示横轴；
        /// 有效范围为 <c>[0, MaxCol - 1]</c>。
        /// </summary>
        public int MaxCol { get; }

        /// <summary>
        /// 空间 Y 轴上限。
        /// Y 表示纵轴；
        /// 有效范围为 <c>[0, MaxRow - 1]</c>。
        /// </summary>
        public int MaxRow { get; }

        /// <summary>
        /// 空间 Z 轴上限。
        /// Z 表示层高方向；
        /// 有效范围为 <c>[0, MaxLayer - 1]</c>。
        /// </summary>
        public int MaxLayer { get; }

        /// <summary>
        /// 创建一个区域索引器。
        /// </summary>
        public RegionIndex(int maxCol, int maxRow, int maxLayer)
        {
            if (maxCol <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCol), "MaxCol 必须大于 0。");

            if (maxRow <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRow), "MaxRow 必须大于 0。");

            if (maxLayer <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLayer), "MaxLayer 必须大于 0。");

            MaxCol = maxCol;
            MaxRow = maxRow;
            MaxLayer = maxLayer;
        }

        /// <summary>
        /// 将 packed(x, y, z) 坐标转换为连续区域下标。
        /// 坐标越界时抛出异常，而不是返回默认值。
        /// </summary>
        public int ToRegionId(int position)
        {
            var (x, y, z) = position.UnpackXyz();
            return ToRegionId(x, y, z);
        }

        /// <summary>
        /// 将 <c>(x, y, z)</c> 转换为连续区域下标。
        /// 公式：
        /// <c>regionId = (z * MaxRow + y) * MaxCol + x</c>。
        /// </summary>
        public int ToRegionId(int x, int y, int z)
        {
            if ((uint)x >= (uint)MaxCol)
                throw new ArgumentOutOfRangeException(nameof(x), $"x 越界：x={x}, 有效范围为 [0, {MaxCol - 1}]。");

            if ((uint)y >= (uint)MaxRow)
                throw new ArgumentOutOfRangeException(nameof(y), $"y 越界：y={y}, 有效范围为 [0, {MaxRow - 1}]。");

            if ((uint)z >= (uint)MaxLayer)
                throw new ArgumentOutOfRangeException(nameof(z), $"z 越界：z={z}, 有效范围为 [0, {MaxLayer - 1}]。");

            return (z * MaxRow + y) * MaxCol + x;
        }

        /// <summary>
        /// 返回整个空间可覆盖的区域总数。
        /// </summary>
        public int GetRegionCount()
        {
            return MaxCol * MaxRow * MaxLayer;
        }
    }

    /// <summary>
    /// TileMappingTable 内部使用的坐标展开工具。
    /// 专门负责把 <c>Position + Volume</c> 展开为 Tile 实际占用的全部坐标。
    /// </summary>
    private static class TileCoordinateHelper
    {
        /// <summary>
        /// 返回一个 Tile 会占用多少个坐标单元。
        /// 公式：
        /// <c>count = dx * dy * dz</c>。
        /// </summary>
        public static int GetCoordinateCount(int volume)
        {
            var (dx, dy, dz) = volume.UnpackXyz();
            return dx * dy * dz;
        }

        /// <summary>
        /// 将 Tile 占用的全部坐标写入目标缓冲区。
        /// 返回实际写入数量。
        /// 当 <paramref name="destination"/> 长度不足时抛出异常，不做部分写入成功的约定。
        /// </summary>
        public static int FillCoordinates(int position, int volume, Span<int> destination)
        {
            var (px, py, pz) = position.UnpackXyz();
            var (dx, dy, dz) = volume.UnpackXyz();

            int requiredLength = dx * dy * dz;

            if (destination.Length < requiredLength)
            {
                throw new ArgumentException(
                    $"destination 长度不足：destination.Length={destination.Length}, requiredLength={requiredLength}。",
                    nameof(destination));
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
}

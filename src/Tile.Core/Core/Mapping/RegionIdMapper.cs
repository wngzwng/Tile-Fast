using Tile.Core.ExtensionTools;

namespace Tile.Core.Core.Mapping;
/// <summary>
/// TileMappingTable 内部使用的 regionId 映射器。
/// 它负责将三维空间坐标 <c>(x, y, z)</c> 压平成连续 regionId。
/// 该结构只服务于当前映射表，不对外单独暴露。
/// </summary>
internal readonly struct RegionIdMapper
    {
        /// <summary>
        /// 空间 X 轴可容纳的列数。
        /// X 表示横轴；
        /// 有效范围为 <c>[0, MaxCol - 1]</c>。
        /// </summary>
        public int MaxCol { get; }

        /// <summary>
        /// 空间 Y 轴可容纳的行数。
        /// Y 表示纵轴；
        /// 有效范围为 <c>[0, MaxRow - 1]</c>。
        /// </summary>
        public int MaxRow { get; }

        /// <summary>
        /// 空间 Z 轴可容纳的层数。
        /// Z 表示层高方向；
        /// 有效范围为 <c>[0, MaxLayer - 1]</c>。
        /// </summary>
        public int MaxLayer { get; }

        /// <summary>
        /// 当前三维空间可映射出的 region 总数量。
        /// </summary>
        public int RegionCount => MaxCol * MaxRow * MaxLayer;

        public RegionIdMapper(
            int maxCol,
            int maxRow,
            int maxLayer)
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

        public int ToRegionId(int position)
        {
            var (x, y, z) = position.UnpackXyz();
            return ToRegionId(x, y, z);
        }

        public int ToRegionId(int x, int y, int z)
        {
            if ((uint)x >= (uint)MaxCol)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    $"x 越界：x={x}, 有效范围为 [0, {MaxCol - 1}]。");
            }

            if ((uint)y >= (uint)MaxRow)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(y),
                    $"y 越界：y={y}, 有效范围为 [0, {MaxRow - 1}]。");
            }

            if ((uint)z >= (uint)MaxLayer)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(z),
                    $"z 越界：z={z}, 有效范围为 [0, {MaxLayer - 1}]。");
            }

            return (z * MaxRow + y) * MaxCol + x;
        }
    }
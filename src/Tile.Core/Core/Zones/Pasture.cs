using Tile.Core.Common.BitSet;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Types;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Core.Zones;

/// <summary>
/// 维护 Pasture 区域的运行时盘面状态。
/// </summary>
/// <remarks>
/// <see cref="Pasture"/> 只维护动态状态：在场、可见、可选。
/// 静态空间映射、邻居候选和受影响集合由 <see cref="TileMappingTable"/> 提供。
/// </remarks>
public sealed class Pasture
{
    #region Fields

    private readonly TileMappingTable _mapping;
    private readonly LockRuleTypeEnum _lockRule;

    private readonly ulong[] _present;
    private readonly ulong[] _visible;
    private readonly ulong[] _selectable;

    #endregion

    #region State Properties

    /// <summary>
    /// 当前仍在 Pasture 中的棋子集合。
    /// </summary>
    public TileIndexSet PresentTiles => TileIndexSet.Wrap(_present);

    /// <summary>
    /// 当前可见的棋子集合。
    /// </summary>
    public TileIndexSet VisibleTiles => TileIndexSet.Wrap(_visible);

    /// <summary>
    /// 当前可选的棋子集合。
    /// </summary>
    public TileIndexSet SelectableTiles => TileIndexSet.Wrap(_selectable);

    /// <summary>
    /// Pasture 中是否已经没有任何棋子。
    /// </summary>
    public bool IsEmpty => PresentTiles.IsEmpty();

    #endregion

    #region Construction

    /// <summary>
    /// 创建 Pasture，并根据映射表与规则初始化盘面状态。
    /// </summary>
    public Pasture(
        TileMappingTable mapping,
        LevelRuleSpec ruleSpec)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        _lockRule = ruleSpec.LockRuleType;

        var wordCount = mapping.WordCount;

        _present = new ulong[wordCount];
        _visible = new ulong[wordCount];
        _selectable = new ulong[wordCount];

        Initialize();
    }

    private Pasture(
        TileMappingTable mapping,
        LockRuleTypeEnum lockRule,
        ulong[] present,
        ulong[] visible,
        ulong[] selectable)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _lockRule = lockRule;
        _present = present ?? throw new ArgumentNullException(nameof(present));
        _visible = visible ?? throw new ArgumentNullException(nameof(visible));
        _selectable = selectable ?? throw new ArgumentNullException(nameof(selectable));
    }

    #endregion

    #region State Queries

    /// <summary>
    /// 指定棋子当前是否仍在 Pasture 中。
    /// </summary>
    public bool IsPresent(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return BitSetOperations.Get(_present, tileIndex);
    }

    /// <summary>
    /// 指定棋子当前是否可见。
    /// </summary>
    public bool IsVisible(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return BitSetOperations.Get(_visible, tileIndex);
    }

    /// <summary>
    /// 指定棋子当前是否可选。
    /// </summary>
    public bool IsSelectable(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return BitSetOperations.Get(_selectable, tileIndex);
    }

    #endregion

    #region State Actions

    /// <summary>
    /// 从 Pasture 上拿起一张棋子。
    /// </summary>
    public void Lift(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (!IsPresent(tileIndex))
            throw new InvalidOperationException($"Tile {tileIndex} 当前不在 Pasture 中。");

        BitSetOperations.Clear(_present, tileIndex);

        RefreshAffected(tileIndex);
    }

    /// <summary>
    /// 将一张棋子放回 Pasture。
    /// </summary>
    public void Place(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (IsPresent(tileIndex))
            throw new InvalidOperationException($"Tile {tileIndex} 已经在 Pasture 中。");

        BitSetOperations.Set(_present, tileIndex);
        RefreshAffected(tileIndex);
    }

    /// <summary>
    /// 重置 Pasture 到初始盘面状态。
    /// </summary>
    public void Reset()
    {
        Rebuild();
    }

    #endregion

    #region Refresh

    /// <summary>
    /// 刷新指定棋子以及受它变化影响的在场棋子。
    /// </summary>
    private void RefreshAffected(int tileIndex)
    {
        Span<ulong> affectedTileBits = stackalloc ulong[_mapping.WordCount];
        _mapping.GetAffectedTileBits(tileIndex).CopyTo(affectedTileBits);
        affectedTileBits.AndWith(_present);

        RefreshOne(tileIndex);

        foreach (var affectedTileIndex in TileIndexSet.Wrap(affectedTileBits))
            RefreshOne(affectedTileIndex);
    }

    /// <summary>
    /// 重新计算单张棋子的可见与可选状态。
    /// </summary>
    private void RefreshOne(int tileIndex)
    {
        BitSetOperations.Clear(_visible, tileIndex);
        BitSetOperations.Clear(_selectable, tileIndex);

        if (!IsPresent(tileIndex))
            return;

        var exposedArea = GetExposedArea(tileIndex);

        if (exposedArea <= 0)
            return;

        BitSetOperations.Set(_visible, tileIndex);

        if (exposedArea != 4)
            return;

        var selectable = _lockRule switch
        {
            LockRuleTypeEnum.Tile => true,
            LockRuleTypeEnum.Classic =>
                !HasPresentNeighbor(tileIndex, NeighborDirEnum.Left)
                || !HasPresentNeighbor(tileIndex, NeighborDirEnum.Right),
            _ => throw new InvalidOperationException($"未知锁定规则：{_lockRule}。")
        };

        if (selectable)
            BitSetOperations.Set(_selectable, tileIndex);
    }

    /// <summary>
    /// 初始化内部状态。
    /// </summary>
    private void Initialize()
    {
        Rebuild();
    }

    /// <summary>
    /// 全量重建 present、visible、selectable。
    /// </summary>
    private void Rebuild()
    {
        BitSetOperations.ClearAll(_present);
        BitSetOperations.ClearAll(_visible);
        BitSetOperations.ClearAll(_selectable);

        for (var tileIndex = 0; tileIndex < _mapping.TileCount; tileIndex++)
            BitSetOperations.Set(_present, tileIndex);

        for (var tileIndex = 0; tileIndex < _mapping.TileCount; tileIndex++)
            RefreshOne(tileIndex);
    }

    #endregion

    #region Exposure Queries

    /// <summary>
    /// 获取棋子顶面当前未被在场棋子遮挡的面积。
    /// </summary>
    private int GetExposedArea(int tileIndex)
    {
        var tile = _mapping.GetTile(tileIndex);
        var (x, y, z) = tile.Position.UnpackXyz();
        var topZ = tile.TopZ;
        Span<int> positions = stackalloc int[]
        {
            (x + 0, y + 0, topZ).PackXyz(),
            (x + 1, y + 0, topZ).PackXyz(),
            (x + 0, y + 1, topZ).PackXyz(),
            (x + 1, y + 1, topZ).PackXyz(),
        };

        var coveredArea = 0;

        foreach (var position in positions)
        {
            // 每个顶面点只需要知道是否被上方第一张在场棋子遮挡。
            for (var layer = topZ + 1; layer < _mapping.MaxLayer; layer++)
            {
                var nextPosition = position.WithZ(layer);

                if (_mapping.TryGetTileIndexAtPosition(nextPosition, out var nextTileIndex)
                    && IsPresent(nextTileIndex))
                {
                    coveredArea++;
                    break;
                }
            }
        }

        return 4 - coveredArea;
    }

    #endregion

    #region Present Neighbor Queries

    /// <summary>
    /// 指定方向上是否存在当前仍在场的邻居。
    /// </summary>
    private bool HasPresentNeighbor(int tileIndex, NeighborDirEnum dir)
    {
        Span<int> buffer = stackalloc int[4];
        return GetPresentNeighbors(tileIndex, dir, buffer) > 0;
    }

    /// <summary>
    /// 获取指定方向上当前仍在场的邻居。
    /// </summary>
    private int GetPresentNeighbors(
        int tileIndex,
        NeighborDirEnum dir,
        Span<int> buffer)
    {
        Span<int> candidates = stackalloc int[4];

        var candidateCount = _mapping.GetNeighbors(
            tileIndex,
            dir,
            candidates);

        var count = 0;

        for (var i = 0; i < candidateCount; i++)
        {
            var candidate = candidates[i];

            if (!IsPresent(candidate))
                continue;

            buffer[count++] = candidate;
        }

        return count;
    }

    #endregion

    #region Validation

    /// <summary>
    /// 验证 tileIndex 是否位于当前映射表范围内。
    /// </summary>
    private void ValidateTileIndex(int tileIndex)
    {
        if ((uint)tileIndex >= (uint)_mapping.TileCount)
            throw new ArgumentOutOfRangeException(nameof(tileIndex));
    }

    #endregion

    #region Clone

    /// <summary>
    /// 克隆当前 Pasture 状态。
    /// </summary>
    public Pasture Clone()
    {
        return new Pasture(
            _mapping,
            _lockRule,
            (ulong[])_present.Clone(),
            (ulong[])_visible.Clone(),
            (ulong[])_selectable.Clone());
    }

    #endregion
}

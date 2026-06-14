


using ThreeTile.Core.ExtensionTools;
using ThreeTile.Core.WngZwng.Core.Types;

namespace ThreeTile.Core.WngZwng.Core.Zones;

/// <summary>
/// Pasture
/// ------------------------------------------------------------
/// 盘面区域状态与几何解释层。
///
/// 职责：
/// 1. 维护当前仍在盘面的 TileIndex 集合
/// 2. 维护当前盘面的 Visible / Selectable 快照
/// 3. 基于当前盘面解释：
///    - 是否可见
///    - 是否锁定
///    - 是否可选
/// 4. 提供几何关系查询：
///    - 上 / 下投影命中
///    - 左右前后邻居
///    - 邻居链
///    - 受影响 Tile 收集
///
/// 约定：
/// 1. Visible 为宽松语义：
///    - 无上方遮挡 => Visible
///    - Selectable ⊆ Visible
/// 2. Tile 是否“存在于当前盘面”，以 _tileIndexes 为准
/// 3. 所有几何查询都只基于“当前盘面存在的 Tile”
/// </summary>
public sealed class Pasture
{
    // =========================================================
    // Fields
    // =========================================================

    /// <summary>
    /// 当前仍在盘面的 TileIndex。
    /// </summary>
    private readonly HashSet<int> _tileIndexes = new();

    /// <summary>
    /// 当前宽松可见的 TileIndex。
    /// </summary>
    private readonly HashSet<int> _visibleTileIndexes = new();

    /// <summary>
    /// 当前可选中的 TileIndex。
    /// </summary>
    private readonly HashSet<int> _selectableTileIndexes = new();

    // =========================================================
    // Ctor / Properties
    // =========================================================

    public Pasture(LevelCore level)
    {
        Parent = level ?? throw new ArgumentNullException(nameof(level));
    }

    /// <summary>
    /// 所属关卡。
    /// </summary>
    public LevelCore Parent { get; }

    /// <summary>
    /// 当前盘面 Tile 数量。
    /// </summary>
    public int TileCount => _tileIndexes.Count;

    /// <summary>
    /// 当前关卡使用的锁定规则。
    /// </summary>
    public LockRuleTypeEnum LockRuleType => Parent.LockRuleType;

    /// <summary>
    /// 当前宽松可见的 TileIndex 集合。
    /// </summary>
    public IReadOnlySet<int> VisibleTileIndexes => _visibleTileIndexes;

    /// <summary>
    /// 当前可选中的 TileIndex 集合。
    /// </summary>
    public IReadOnlySet<int> SelectableTileIndexes => _selectableTileIndexes;

    // =========================================================
    // 1. 真实盘面维护
    // =========================================================

    /// <summary>
    /// 当前盘面是否包含指定 tile。
    /// </summary>
    public bool ContainsTile(int tileIndex)
    {
        return _tileIndexes.Contains(tileIndex);
    }

    /// <summary>
    /// 向当前盘面加入一个 tile。
    ///
    /// 注意：
    /// - 这里只做单个增量更新
    /// - 加入后会基于该 tile 做局部刷新
    /// </summary>
    public void AddTile(int tileIndex)
    {
        if (_tileIndexes.Contains(tileIndex))
            return;

        if (!Parent.TryGetTileByIndex(tileIndex, out var tile))
            return;

        _tileIndexes.Add(tileIndex);
        PartialRefresh(_selectableTileIndexes, _visibleTileIndexes, tile);
    }

    /// <summary>
    /// 从当前盘面移除一个 tile。
    ///
    /// 注意：
    /// - 先从 _tileIndexes 中移除
    /// - 再使用该 tile 作为“几何影响源”做局部刷新
    /// </summary>
    public void RemoveTile(int tileIndex)
    {
        if (!Parent.TryGetTileByIndex(tileIndex, out var tile))
            return;

        if (!_tileIndexes.Remove(tileIndex))
            return;

        PartialRefresh(_selectableTileIndexes, _visibleTileIndexes, tile);
    }

    /// <summary>
    /// 批量加入 tile。
    ///
    /// 当前策略：
    /// - 批量操作直接走全量重建，逻辑更稳
    /// </summary>
    public void AddTiles(IEnumerable<int> tileIndexes)
    {
        if (tileIndexes is null)
            throw new ArgumentNullException(nameof(tileIndexes));

        bool changed = false;

        foreach (var tileIndex in tileIndexes)
        {
            if (_tileIndexes.Contains(tileIndex))
                continue;

            if (!Parent.TryGetTileByIndex(tileIndex, out _))
                continue;

            _tileIndexes.Add(tileIndex);
            changed = true;
        }

        if (changed)
            RebuildFromCurrentTiles();
    }

    /// <summary>
    /// 批量移除 tile。
    ///
    /// 当前策略：
    /// - 批量操作直接走全量重建，逻辑更稳
    /// </summary>
    public void RemoveTiles(IEnumerable<int> tileIndexes)
    {
        if (tileIndexes is null)
            throw new ArgumentNullException(nameof(tileIndexes));

        bool changed = false;

        foreach (var tileIndex in tileIndexes)
        {
            if (_tileIndexes.Remove(tileIndex))
                changed = true;
        }

        if (changed)
            RebuildFromCurrentTiles();
    }

    /// <summary>
    /// 使用关卡中的全部 Tile 初始化当前盘面。
    /// </summary>
    public void InitAllTiles()
    {
        _tileIndexes.Clear();

        foreach (var tile in Parent.Tiles)
            _tileIndexes.Add(tile.Index);

        RebuildFromCurrentTiles();
    }

    /// <summary>
    /// 清空盘面状态与缓存。
    /// </summary>
    public void Reset()
    {
        _tileIndexes.Clear();
        _visibleTileIndexes.Clear();
        _selectableTileIndexes.Clear();
    }

    /// <summary>
    /// 基于当前 _tileIndexes 全量重建 Visible / Selectable 快照。
    /// </summary>
    public void RebuildFromCurrentTiles()
    {
        _visibleTileIndexes.Clear();
        _selectableTileIndexes.Clear();

        foreach (var tileIndex in _tileIndexes)
        {
            if (!Parent.TryGetTileByIndex(tileIndex, out var tile))
                continue;

            if (IsVisible(tile))
                _visibleTileIndexes.Add(tileIndex);

            if (IsSelectable(tile))
                _selectableTileIndexes.Add(tileIndex);
        }
    }

    /// <summary>
    /// 克隆到新的关卡实例。
    /// </summary>
    public Pasture Clone(LevelCore newParent)
    {
        if (newParent is null)
            throw new ArgumentNullException(nameof(newParent));

        var clone = new Pasture(newParent);
        clone.AddTiles(_tileIndexes);
        return clone;
    }

    /// <summary>
    /// 获取当前仍在盘面的 Tile 实例数组。
    /// </summary>
    public Tile[] GetPresentTiles()
    {
        var result = new List<Tile>(_tileIndexes.Count);

        foreach (var tileIndex in _tileIndexes)
        {
            if (Parent.TryGetTileByIndex(tileIndex, out var tile))
                result.Add(tile);
        }

        return result.ToArray();
    }

    // =========================================================
    // 2. 状态解释
    // =========================================================

    /// <summary>
    /// 是否被锁定。
    ///
    /// Tile 模式：
    /// - 上方有遮挡 => Locked
    ///
    /// Classic 模式：
    /// - 上方有遮挡 => Locked
    /// - 左右两侧同时存在邻居 => Locked
    /// </summary>
    public bool IsLocked(Tile tile, ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        return Parent.LockRuleType switch
        {
            LockRuleTypeEnum.Tile =>
                HasUpOver(tile, exclude),

            LockRuleTypeEnum.Classic =>
                HasUpOver(tile, exclude) ||
                (HasAnyNeighbor(NeighborDir.Left, tile, exclude) &&
                 HasAnyNeighbor(NeighborDir.Right, tile, exclude)),

            _ => throw new InvalidOperationException($"Unsupported lock rule type: {Parent.LockRuleType}")
        };
    }

    /// <summary>
    /// 是否可选。
    ///
    /// 可选 = 未被锁定
    /// </summary>
    public bool IsSelectable(Tile tile, ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        return !IsLocked(tile, exclude);
    }

    /// <summary>
    /// 按 tileIndex 判断是否可选。
    /// </summary>
    public bool IsSelectable(int tileIndex, ReadOnlySpan<bool> exclude = default)
    {
        return IsSelectable(Parent.GetTileByIndex(tileIndex), exclude);
    }

    /// <summary>
    /// 是否宽松可见。
    ///
    /// 规则：
    /// - 上方无遮挡 => Visible
    /// - Selectable ⊆ Visible
    /// </summary>
    public bool IsVisible(Tile tile, ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, NeighborDir.Up))
        {
            bool blocked = false;

            for (int z = tile.TopZ + 1; z <= Parent.MaxLayer; z++)
            {
                if (!TryGetPresentTileByPosition(pos.WithZ(z), out var neighbor))
                    continue;

                if (neighbor == tile)
                    continue;

                if (IsExcluded(neighbor.Index, exclude))
                    continue;

                // 找到一个真实遮挡
                blocked = true;
                break;
            }

            // ⭐ 关键点：只要有一个方向没有被挡住 → 可见
            if (!blocked)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 按 tileIndex 判断是否宽松可见。
    /// </summary>
    public bool IsVisible(int tileIndex, ReadOnlySpan<bool> exclude = default)
    {
        return IsVisible(Parent.GetTileByIndex(tileIndex), exclude);
    }

    /// <summary>
    /// 获取 Tile 顶面 2x2 投影中未被遮挡的面积份数。
    /// 返回值范围为 0..4；4 表示顶面四个投影格都可见。
    /// </summary>
    public int GetVisibleArea(Tile tile, ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        int visibleArea = 0;
        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, NeighborDir.Up))
        {
            bool blocked = false;

            for (int z = tile.TopZ + 1; z <= Parent.MaxLayer; z++)
            {
                if (!TryGetPresentTileByPosition(pos.WithZ(z), out var neighbor))
                    continue;

                if (neighbor == tile)
                    continue;

                if (IsExcluded(neighbor.Index, exclude))
                    continue;

                blocked = true;
                break;
            }

            if (!blocked)
                visibleArea++;
        }

        return visibleArea;
    }

    // =========================================================
    // 3. 局部刷新
    // =========================================================

    /// <summary>
    /// 基于指定 tile 做局部刷新。
    ///
    /// 流程：
    /// 1. 收集受影响 Tile 集合
    /// 2. 从 selectable / visible 中移除这些 tile 的旧状态
    /// 3. 对仍在盘面的 affected tile 重新计算状态并回填
    ///
    /// 注意：
    /// - affectedIndexes 默认先包含 tile 自己
    /// - 这很重要，因为新增/移除 tile 自己的状态也可能变化
    /// </summary>
    public bool PartialRefresh(
        HashSet<int> selectableTileIndexes,
        HashSet<int> visibleTileIndexes,
        Tile tile,
        ReadOnlySpan<bool> exclude = default)
    {
        if (selectableTileIndexes is null)
            throw new ArgumentNullException(nameof(selectableTileIndexes));
        if (visibleTileIndexes is null)
            throw new ArgumentNullException(nameof(visibleTileIndexes));
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        var affectedIndexes = new HashSet<int>
        {
            tile.Index
        };

        CollectAffectedTiles(tile, affectedIndexes, exclude);

        // 先移除旧状态
        selectableTileIndexes.ExceptWith(affectedIndexes);
        visibleTileIndexes.ExceptWith(affectedIndexes);

        // 再按新状态回填
        foreach (var index in affectedIndexes)
        {
            if (!ContainsTile(index))
                continue;
            
            if (IsExcluded(index, exclude))
                continue;

            if (IsVisible(index, exclude))
                visibleTileIndexes.Add(index);

            if (IsSelectable(index, exclude))
                selectableTileIndexes.Add(index);
        }

        return affectedIndexes.Count > 0;
    }

    // =========================================================
    // 4. HasAny 查询
    // =========================================================

    /// <summary>
    /// 当前 tile 上方投影区域，是否存在任意一个“当前盘面中存在”的遮挡 tile。
    /// </summary>
    public bool HasUpOver(Tile tile, ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, NeighborDir.Up))
        {
            for (int z = tile.TopZ + 1; z <= Parent.MaxLayer; z++)
            {
                if (!TryGetPresentTileByPosition(pos.WithZ(z), out var neighbor))
                    continue;

                if (neighbor == tile)
                    continue;

                if (IsExcluded(neighbor.Index, exclude))
                    continue;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 当前 tile 下方投影区域，是否存在任意一个“当前盘面中存在”的 tile。
    /// </summary>
    public bool HasDownUnder(Tile tile, ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, NeighborDir.Down))
        {
            for (int z = tile.TopZ - 1; z >= 0; z--)
            {
                if (!TryGetPresentTileByPosition(pos.WithZ(z), out var neighbor))
                    continue;

                if (neighbor == tile)
                    continue;

                if (IsExcluded(neighbor.Index, exclude))
                    continue;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 指定方向上是否存在任意一个直接邻居。
    ///
    /// 注意：
    /// - 这里只处理平面方向：Left / Right / Front / Back
    /// - Up / Down 属于投影扫描语义，不走这里
    /// </summary>
    public bool HasAnyNeighbor(
        NeighborDir dir,
        Tile tile,
        ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));

        return HasAnyNeighborCore(tile, dir, exclude);
    }

    private bool HasAnyNeighborCore(
        Tile tile,
        NeighborDir dir,
        ReadOnlySpan<bool> exclude)
    {
        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, dir))
        {
            if (!TryGetPresentTileByPosition(pos, out var neighbor))
                continue;

            if (neighbor == tile)
                continue;

            if (IsExcluded(neighbor.Index, exclude))
                continue;

            return true;
        }

        return false;
    }

    // =========================================================
    // 5. Collect - 组合查询
    // =========================================================

    /// <summary>
    /// 收集受当前 tile 影响的 tile。
    ///
    /// 规则：
    /// - 一定收集下方命中的 tile
    /// - Classic 模式下，额外收集左右邻居
    /// </summary>
    public bool CollectAffectedTiles(
        Tile tile,
        HashSet<int> affectedTileIndexes,
        ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));
        if (affectedTileIndexes is null)
            throw new ArgumentNullException(nameof(affectedTileIndexes));

        bool collected = false;

        if (CollectDownHitTile(tile, affectedTileIndexes, exclude))
            collected = true;

        if (Parent.LockRuleType == LockRuleTypeEnum.Classic)
        {
            if (CollectNeighbors(NeighborDir.Left, tile, affectedTileIndexes, exclude))
                collected = true;

            if (CollectNeighbors(NeighborDir.Right, tile, affectedTileIndexes, exclude))
                collected = true;
        }

        return collected;
    }

    // =========================================================
    // 6. Collect - 投影命中
    // =========================================================

    /// <summary>
    /// 收集当前 tile 上方投影列中，每列命中的第一个 tile。
    /// </summary>
    public bool CollectUpHitTile(
        Tile tile,
        HashSet<int> hitTileIndexes,
        ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));
        if (hitTileIndexes is null)
            throw new ArgumentNullException(nameof(hitTileIndexes));

        int beforeCount = hitTileIndexes.Count;

        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, NeighborDir.Up))
        {
            for (int z = tile.TopZ + 1; z <= Parent.MaxLayer; z++)
            {
                if (!TryGetPresentTileByPosition(pos.WithZ(z), out var neighbor))
                    continue;

                if (neighbor == tile)
                    continue;

                if (IsExcluded(neighbor.Index, exclude))
                    continue;

                hitTileIndexes.Add(neighbor.Index);
                break;
            }
        }

        return hitTileIndexes.Count > beforeCount;
    }

    /// <summary>
    /// 收集当前 tile 下方投影列中，每列命中的第一个 tile。
    /// </summary>
    public bool CollectDownHitTile(
        Tile tile,
        HashSet<int> hitTileIndexes,
        ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));
        if (hitTileIndexes is null)
            throw new ArgumentNullException(nameof(hitTileIndexes));

        int beforeCount = hitTileIndexes.Count;

        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, NeighborDir.Down))
        {
            for (int z = tile.TopZ - 1; z >= 0; z--)
            {
                if (!TryGetPresentTileByPosition(pos.WithZ(z), out var neighbor))
                    continue;

                if (neighbor == tile)
                    continue;

                if (IsExcluded(neighbor.Index, exclude))
                    continue;

                hitTileIndexes.Add(neighbor.Index);
                break;
            }
        }

        return hitTileIndexes.Count > beforeCount;
    }

    // =========================================================
    // 7. Collect - 邻居
    // =========================================================

    /// <summary>
    /// 收集指定方向上的直接邻居。
    /// </summary>
    public bool CollectNeighbors(
        NeighborDir dir,
        Tile tile,
        HashSet<int> neighborTileIndexes,
        ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));
        if (neighborTileIndexes is null)
            throw new ArgumentNullException(nameof(neighborTileIndexes));

        int beforeCount = neighborTileIndexes.Count;

        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, dir))
        {
            if (!TryGetPresentTileByPosition(pos, out var neighbor))
                continue;

            if (neighbor == tile)
                continue;

            if (IsExcluded(neighbor.Index, exclude))
                continue;

            neighborTileIndexes.Add(neighbor.Index);
        }

        return neighborTileIndexes.Count > beforeCount;
    }

    /// <summary>
    /// 递归收集指定方向上的邻居链。
    /// </summary>
    public bool CollectNeighborChain(
        NeighborDir dir,
        Tile tile,
        HashSet<int> neighborTileIndexes,
        ReadOnlySpan<bool> exclude = default)
    {
        if (tile is null)
            throw new ArgumentNullException(nameof(tile));
        if (neighborTileIndexes is null)
            throw new ArgumentNullException(nameof(neighborTileIndexes));

        int beforeCount = neighborTileIndexes.Count;

        CollectNeighborChainCore(dir, tile, neighborTileIndexes, exclude);

        return neighborTileIndexes.Count > beforeCount;
    }

    private void CollectNeighborChainCore(
        NeighborDir dir,
        Tile tile,
        HashSet<int> neighborTileIndexes,
        ReadOnlySpan<bool> exclude)
    {
        foreach (var pos in GetNeighborPositions(tile.TilePositionIndex, dir))
        {
            if (!TryGetPresentTileByPosition(pos, out var neighbor))
                continue;

            if (neighbor == tile)
                continue;

            if (IsExcluded(neighbor.Index, exclude))
                continue;

            if (!neighborTileIndexes.Add(neighbor.Index))
                continue;

            CollectNeighborChainCore(dir, neighbor, neighborTileIndexes, exclude);
        }
    }

    // =========================================================
    // 8. Internal Helpers
    // =========================================================

    /// <summary>
    /// 按 position 获取“当前盘面中仍存在”的 tile。
    /// </summary>
    private bool TryGetPresentTileByPosition(int position, out Tile tile)
    {
        tile = null!;

        if (!Parent.TryGetTileByPosition(position, out var found))
            return false;

        if (!_tileIndexes.Contains(found.Index))
            return false;

        tile = found;
        return true;
    }

    /// <summary>
    /// 判断某 tileIndex 是否在 exclude 中被排除。
    /// </summary>
    private static bool IsExcluded(int tileIndex, ReadOnlySpan<bool> exclude)
    {
        return !exclude.IsEmpty && exclude[tileIndex];
    }

    /// <summary>
    /// 获取平面邻居位置。
    ///
    /// 这里只接受：
    /// Left / Right / Front / Back
    /// </summary>
    private IEnumerable<int> GetNeighborPositions(int pos, NeighborDir dir)
    {
        return dir switch
        {
            NeighborDir.Left  => pos.GetPositionLeftNeighbourPositions(),
            NeighborDir.Right => pos.GetPositionRightNeighbourPositions(),
            NeighborDir.Front => pos.GetPositionFrontNeighbourPositions(),
            NeighborDir.Back  => pos.GetPositionBehindNeighbourPositions(),
            NeighborDir.Up =>  pos.GetPositionUpNeighbourPositions(),
            NeighborDir.Down =>  pos.GetPositionDownNeighbourPositions(),
            _ => throw new ArgumentException($"Invalid direction: {dir}", nameof(dir))
        };
    }

    // /// <summary>
    // /// 获取上方投影位置集合。
    // /// </summary>
    // private IEnumerable<int> GetUpNeighborPositions(int pos)
    // {
    //     return pos.GetPositionUpNeighbourPositions();
    // }
    //
    // /// <summary>
    // /// 获取下方投影位置集合。
    // /// </summary>
    // private IEnumerable<int> GetDownNeighborPositions(int pos)
    // {
    //     return pos.GetPositionDownNeighbourPositions();
    // }
}
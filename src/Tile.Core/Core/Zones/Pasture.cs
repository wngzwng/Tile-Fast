using Tile.Core.Common.BitSet;
using Tile.Core.Core;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Types;
using Tile.Core.Core.Utils;

namespace Tile.Core.Core.Zones;

/// <summary>
/// 维护 Pasture 区域的运行时盘面状态。
/// </summary>
/// <remarks>
/// <see cref="Pasture"/> 只维护动态状态：在场、可见、可选。
/// 静态空间映射由 <see cref="TileMappingTable"/> 提供。
/// 受影响集合由 Pasture 基于当前在场状态和规则即时计算。
/// </remarks>
public sealed class Pasture
{
    #region Types

    private enum VerticalScanDir
    {
        Up,
        Down
    }

    #endregion

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

    #region Object Basics

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

    /// <summary>
    /// 重置 Pasture 到初始盘面状态。
    /// </summary>
    public void Reset()
    {
        Rebuild();
    }

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

    /// <summary>
    /// 返回 Pasture 当前状态摘要。
    /// </summary>
    public override string ToString()
    {
        return $"Pasture(" +
               $"Tiles={_mapping.TileCount}, " +
               $"Rule={_lockRule}, " +
               $"Present={PresentTiles.Count()}, " +
               $"Visible={VisibleTiles.Count()}, " +
               $"Selectable={SelectableTiles.Count()}, " +
               $"VisibleTiles=[{FormatTileIndexes(VisibleTiles)}], " +
               $"SelectableTiles=[{FormatTileIndexes(SelectableTiles)}], " +
               $"IsEmpty={IsEmpty})";
    }

    private static string FormatTileIndexes(TileIndexSet tileIndexes)
    {
        var text = string.Empty;

        foreach (var tileIndex in tileIndexes)
        {
            if (text.Length > 0)
                text += ", ";

            text += tileIndex;
        }

        return text;
    }

    #endregion

    #region Real Board Semantics

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

    /// <summary>
    /// 获取棋子顶面当前未被在场棋子遮挡的面积。
    /// </summary>
    public int GetExposedArea(int tileIndex)
    {
        var coveredArea = CollectUpperCoverTileBits(
            tileIndex,
            ignoredTileBits: default,
            coverTileBits: default,
            out _);

        return 4 - coveredArea;
    }

    /// <summary>
    /// 获取当前覆盖指定棋子顶面的上方棋子集合。
    /// </summary>
    /// <returns>覆盖棋子的去重数量。</returns>
    public int GetUpperCoverTileBits(
        int tileIndex,
        Span<ulong> coverTileBits)
    {
        CollectUpperCoverTileBits(
            tileIndex,
            ignoredTileBits: default,
            coverTileBits,
            out var coverTileCount);

        return coverTileCount;
    }

    /// <summary>
    /// 获取当前被指定棋子底面覆盖的下方棋子集合。
    /// </summary>
    /// <returns>被覆盖棋子的去重数量。</returns>
    public int GetLowerCoveredTileBits(
        int tileIndex,
        Span<ulong> coveredTileBits)
    {
        CollectLowerCoveredTileBits(
            tileIndex,
            ignoredTileBits: default,
            coveredTileBits,
            out var coveredTileCount);

        return coveredTileCount;
    }

    /// <summary>
    /// 指定方向上是否存在当前仍在场的邻居。
    /// </summary>
    public bool HasPresentNeighbor(int tileIndex, NeighborDirEnum dir)
    {
        Span<int> buffer = stackalloc int[4];
        return GetPresentNeighbors(
            tileIndex,
            dir,
            default,
            buffer) > 0;
    }

    /// <summary>
    /// 获取指定方向上当前仍在场的邻居。
    /// </summary>
    public int GetPresentNeighbors(
        int tileIndex,
        NeighborDirEnum dir,
        Span<int> buffer)
    {
        return GetPresentNeighbors(
            tileIndex,
            dir,
            default,
            buffer);
    }

    #endregion

    #region Simulation Semantics

    /// <summary>
    /// 在忽略指定棋子的模拟盘面中，计算目标棋子的可见与可选状态。
    /// </summary>
    public void SimulateState(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        out bool visible,
        out bool selectable)
    {
        GetTileState(
            tileIndex,
            ignoredTileBits,
            out visible,
            out selectable);
    }

    /// <summary>
    /// 在忽略指定棋子的模拟盘面中，获取覆盖目标棋子顶面的上方棋子集合。
    /// </summary>
    /// <returns>覆盖棋子的去重数量。</returns>
    public int SimulateUpperCoverTileBits(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> coverTileBits)
    {
        CollectUpperCoverTileBits(
            tileIndex,
            ignoredTileBits,
            coverTileBits,
            out var coverTileCount);

        return coverTileCount;
    }

    /// <summary>
    /// 在忽略指定棋子的模拟盘面中，获取被目标棋子底面覆盖的下方棋子集合。
    /// </summary>
    /// <returns>被覆盖棋子的去重数量。</returns>
    public int SimulateLowerCoveredTileBits(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> coveredTileBits)
    {
        CollectLowerCoveredTileBits(
            tileIndex,
            ignoredTileBits,
            coveredTileBits,
            out var coveredTileCount);

        return coveredTileCount;
    }

    /// <summary>
    /// 在忽略指定棋子的模拟盘面中，获取目标棋子变化后受影响的棋子集合。
    /// </summary>
    /// <returns>受影响棋子的去重数量。</returns>
    public int SimulateAffectedTileBits(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> affectedTileBits)
    {
        CollectAffectedTileBits(
            tileIndex,
            ignoredTileBits,
            affectedTileBits,
            out var affectedTileCount);

        return affectedTileCount;
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

    #endregion

    #region Refresh

    /// <summary>
    /// 刷新指定棋子以及受它变化影响的在场棋子。
    /// </summary>
    private void RefreshAffected(int tileIndex)
    {
        Span<ulong> affectedTileBits = stackalloc ulong[_mapping.WordCount];
        CollectAffectedTileBits(
            tileIndex,
            ignoredTileBits: default,
            affectedTileBits,
            out _);

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

        GetTileState(
            tileIndex,
            ignoredTileBits: default,
            out var visible,
            out var selectable);

        if (visible)
            BitSetOperations.Set(_visible, tileIndex);

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

    #region Stable Core Semantics

    /// <summary>
    /// 判断棋子在当前计算视角下是否有效在场。
    /// </summary>
    private bool IsEffectivelyPresent(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits)
    {
        if (!BitSetOperations.Get(_present, tileIndex))
            return false;

        return ignoredTileBits.IsEmpty
            || !BitSetOperations.Get(ignoredTileBits, tileIndex);
    }

    /// <summary>
    /// 基于当前在场状态和忽略集合，获取单张棋子的可见与可选状态。
    /// </summary>
    private void GetTileState(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        out bool visible,
        out bool selectable)
    {
        ValidateTileIndex(tileIndex);
        ValidateIgnoredTileBits(ignoredTileBits);

        visible = false;
        selectable = false;

        if (!IsEffectivelyPresent(tileIndex, ignoredTileBits))
            return;

        var coveredArea = CollectUpperCoverTileBits(
            tileIndex,
            ignoredTileBits,
            coverTileBits: default,
            out _);

        var exposedArea = 4 - coveredArea;

        if (exposedArea <= 0)
            return;

        visible = true;

        if (exposedArea != 4)
            return;

        selectable = _lockRule switch
        {
            LockRuleTypeEnum.Tile => true,
            LockRuleTypeEnum.Classic =>
                !HasPresentNeighbor(tileIndex, NeighborDirEnum.Left, ignoredTileBits)
                || !HasPresentNeighbor(tileIndex, NeighborDirEnum.Right, ignoredTileBits),
            _ => throw new InvalidOperationException($"未知锁定规则：{_lockRule}。")
        };
    }

    /// <summary>
    /// 收集上方覆盖者：谁盖住我。
    /// </summary>
    private int CollectUpperCoverTileBits(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> coverTileBits,
        out int coverTileCount)
    {
        ValidateTileIndex(tileIndex);
        ValidateIgnoredTileBits(ignoredTileBits);

        if (coverTileBits.IsEmpty)
        {
            Span<ulong> temporaryCoverTileBits = stackalloc ulong[_mapping.WordCount];
            return CollectVerticalHitTileBits(
                tileIndex,
                VerticalScanDir.Up,
                ignoredTileBits,
                temporaryCoverTileBits,
                out coverTileCount);
        }

        ValidateCoverTileBits(coverTileBits);

        return CollectVerticalHitTileBits(
            tileIndex,
            VerticalScanDir.Up,
            ignoredTileBits,
            coverTileBits,
            out coverTileCount);
    }

    /// <summary>
    /// 收集下方被覆盖者：我盖住谁。
    /// </summary>
    private int CollectLowerCoveredTileBits(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> coveredTileBits,
        out int coveredTileCount)
    {
        ValidateTileIndex(tileIndex);
        ValidateIgnoredTileBits(ignoredTileBits);
        ValidateCoveredTileBits(coveredTileBits);

        return CollectVerticalHitTileBits(
            tileIndex,
            VerticalScanDir.Down,
            ignoredTileBits,
            coveredTileBits,
            out coveredTileCount);
    }

    /// <summary>
    /// 垂直 hit 机制：从水平面 4 个点沿 Z 方向扫描，收集每个点命中的第一张有效在场棋子。
    /// </summary>
    private int CollectVerticalHitTileBits(
        int tileIndex,
        VerticalScanDir dir,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> hitTileBits,
        out int hitTileCount)
    {
        hitTileBits.Clear();
        hitTileCount = 0;

        var tile = _mapping.GetTile(tileIndex);
        var (x, y, bottomZ) = PositionPacker.UnpackXyz(tile.Position);
        var scanUp = dir == VerticalScanDir.Up;
        var startZ = scanUp ? tile.TopZ : bottomZ;
        Span<int> positions = stackalloc int[]
        {
            PositionPacker.PackXyz(x + 0, y + 0, startZ),
            PositionPacker.PackXyz(x + 1, y + 0, startZ),
            PositionPacker.PackXyz(x + 0, y + 1, startZ),
            PositionPacker.PackXyz(x + 1, y + 1, startZ),
        };

        var hitArea = 0;

        foreach (var position in positions)
        {
            var layer = scanUp ? startZ + 1 : startZ - 1;

            while (layer >= 0 && layer < _mapping.MaxLayer)
            {
                var nextPosition = PositionPacker.WithZ(position, layer);

                if (_mapping.TryGetTileIndexAtPosition(nextPosition, out var nextTileIndex)
                    && IsEffectivelyPresent(nextTileIndex, ignoredTileBits))
                {
                    hitArea++;

                    if (!BitSetOperations.Get(hitTileBits, nextTileIndex))
                    {
                        BitSetOperations.Set(hitTileBits, nextTileIndex);
                        hitTileCount++;
                    }

                    break;
                }

                layer += scanUp ? 1 : -1;
            }
        }

        return hitArea;
    }

    /// <summary>
    /// 获取指定方向上当前仍在场的邻居。
    /// </summary>
    private int GetPresentNeighbors(
        int tileIndex,
        NeighborDirEnum dir,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<int> buffer)
    {
        ValidateTileIndex(tileIndex);
        ValidateIgnoredTileBits(ignoredTileBits);

        if (buffer.Length < 4)
            throw new ArgumentException("邻居缓冲区长度不足。", nameof(buffer));

        Span<int> candidates = stackalloc int[4];

        var candidateCount = GetNeighborCandidates(
            tileIndex,
            dir,
            candidates);

        var count = 0;

        for (var i = 0; i < candidateCount; i++)
        {
            var candidate = candidates[i];

            if (!IsEffectivelyPresent(candidate, ignoredTileBits))
                continue;

            buffer[count++] = candidate;
        }

        return count;
    }

    private bool HasPresentNeighbor(
        int tileIndex,
        NeighborDirEnum dir,
        ReadOnlySpan<ulong> ignoredTileBits)
    {
        Span<int> buffer = stackalloc int[4];

        return GetPresentNeighbors(
            tileIndex,
            dir,
            ignoredTileBits,
            buffer) > 0;
    }

    /// <summary>
    /// 获取指定方向上的空间邻居候选。
    /// </summary>
    private int GetNeighborCandidates(
        int tileIndex,
        NeighborDirEnum dir,
        Span<int> buffer)
    {
        if (buffer.Length < 4)
            throw new ArgumentException("邻居缓冲区长度不足。", nameof(buffer));

        var tile = _mapping.GetTile(tileIndex);
        var (x, y, z) = PositionPacker.UnpackXyz(tile.Position);
        var (sizeX, sizeY, sizeZ) = PositionPacker.UnpackXyz(Tile.DefaultVolume);

        var count = 0;

        switch (dir)
        {
            case NeighborDirEnum.Left:
                TryAddNeighborCandidateAt(x - 1, y, z, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x - 1, y + 1, z, tileIndex, buffer, ref count);
                break;

            case NeighborDirEnum.Right:
                TryAddNeighborCandidateAt(x + sizeX, y, z, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + sizeX, y + 1, z, tileIndex, buffer, ref count);
                break;

            case NeighborDirEnum.Front:
                TryAddNeighborCandidateAt(x, y - 1, z, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + 1, y - 1, z, tileIndex, buffer, ref count);
                break;

            case NeighborDirEnum.Back:
                TryAddNeighborCandidateAt(x, y + sizeY, z, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + 1, y + sizeY, z, tileIndex, buffer, ref count);
                break;

            case NeighborDirEnum.Up:
                TryAddNeighborCandidateAt(x, y, z + sizeZ, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + 1, y, z + sizeZ, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x, y + 1, z + sizeZ, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + 1, y + 1, z + sizeZ, tileIndex, buffer, ref count);
                break;

            case NeighborDirEnum.Down:
                TryAddNeighborCandidateAt(x, y, z - 1, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + 1, y, z - 1, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x, y + 1, z - 1, tileIndex, buffer, ref count);
                TryAddNeighborCandidateAt(x + 1, y + 1, z - 1, tileIndex, buffer, ref count);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(dir), dir, "未知邻接方向。");
        }

        return count;
    }

    /// <summary>
    /// 收集指定棋子变化后需要重新计算状态的棋子。
    /// </summary>
    private int CollectAffectedTileBits(
        int tileIndex,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> affectedTileBits,
        out int affectedTileCount)
    {
        ValidateTileIndex(tileIndex);
        ValidateIgnoredTileBits(ignoredTileBits);
        ValidateAffectedTileBits(affectedTileBits);

        affectedTileBits.Clear();
        affectedTileCount = 0;

        CollectLowerCoveredTileBits(
            tileIndex,
            ignoredTileBits,
            affectedTileBits,
            out affectedTileCount);

        if (_lockRule == LockRuleTypeEnum.Classic)
        {
            AddPresentNeighborsToAffected(
                tileIndex,
                NeighborDirEnum.Left,
                ignoredTileBits,
                affectedTileBits,
                ref affectedTileCount);

            AddPresentNeighborsToAffected(
                tileIndex,
                NeighborDirEnum.Right,
                ignoredTileBits,
                affectedTileBits,
                ref affectedTileCount);
        }

        return affectedTileCount;
    }

    private void AddPresentNeighborsToAffected(
        int tileIndex,
        NeighborDirEnum dir,
        ReadOnlySpan<ulong> ignoredTileBits,
        Span<ulong> affectedTileBits,
        ref int affectedTileCount)
    {
        Span<int> neighbors = stackalloc int[4];

        var neighborCount = GetPresentNeighbors(
            tileIndex,
            dir,
            ignoredTileBits,
            neighbors);

        for (var i = 0; i < neighborCount; i++)
            AddAffectedTile(neighbors[i], affectedTileBits, ref affectedTileCount);
    }

    private void TryAddNeighborCandidateAt(
        int x,
        int y,
        int z,
        int sourceTileIndex,
        Span<int> buffer,
        ref int count)
    {
        if (!IsInsideBoard(x, y, z))
            return;

        var position = PositionPacker.PackXyz(x, y, z);

        if (!_mapping.TryGetTileIndexAtPosition(position, out var tileIndex))
            return;

        if (tileIndex == sourceTileIndex)
            return;

        if (Contains(buffer, count, tileIndex))
            return;

        buffer[count++] = tileIndex;
    }

    private bool IsInsideBoard(int x, int y, int z)
    {
        return x >= 0 && x < _mapping.MaxCol
            && y >= 0 && y < _mapping.MaxRow
            && z >= 0 && z < _mapping.MaxLayer;
    }

    private static void AddAffectedTile(
        int tileIndex,
        Span<ulong> affectedTileBits,
        ref int affectedTileCount)
    {
        if (BitSetOperations.Get(affectedTileBits, tileIndex))
            return;

        BitSetOperations.Set(affectedTileBits, tileIndex);
        affectedTileCount++;
    }

    private static bool Contains(
        ReadOnlySpan<int> buffer,
        int count,
        int tileIndex)
    {
        for (var i = 0; i < count; i++)
        {
            if (buffer[i] == tileIndex)
                return true;
        }

        return false;
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

    private void ValidateIgnoredTileBits(ReadOnlySpan<ulong> ignoredTileBits)
    {
        if (!ignoredTileBits.IsEmpty && ignoredTileBits.Length < _mapping.WordCount)
            throw new ArgumentException("忽略棋子 bit 缓冲区长度不足。", nameof(ignoredTileBits));
    }

    private void ValidateCoverTileBits(Span<ulong> coverTileBits)
    {
        if (coverTileBits.Length < _mapping.WordCount)
            throw new ArgumentException("覆盖棋子 bit 缓冲区长度不足。", nameof(coverTileBits));
    }

    private void ValidateCoveredTileBits(Span<ulong> coveredTileBits)
    {
        if (coveredTileBits.Length < _mapping.WordCount)
            throw new ArgumentException("被覆盖棋子 bit 缓冲区长度不足。", nameof(coveredTileBits));
    }

    private void ValidateAffectedTileBits(Span<ulong> affectedTileBits)
    {
        if (affectedTileBits.Length < _mapping.WordCount)
            throw new ArgumentException("受影响棋子 bit 缓冲区长度不足。", nameof(affectedTileBits));
    }

    #endregion
}

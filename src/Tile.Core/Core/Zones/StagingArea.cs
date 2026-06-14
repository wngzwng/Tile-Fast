using ThreeTile.Core.WngZwng.Core.Types;

namespace ThreeTile.Core.WngZwng.Core.Zones;

/// <summary>
/// 暂存槽 / 卡槽区域。
///
/// 职责：
/// - 维护当前在槽中的 Tile
/// - 维护按颜色分组
/// - 维护运行时容量
///
/// 不负责：
/// - 匹配策略选择
/// - 自动消除策略
/// - Move 生成与评分
/// </summary>
public sealed class StagingArea
{
    private readonly HashSet<int> _tileIndexSet = new();
    private readonly Dictionary<int, List<int>> _colorGroups = new();
    private List<int> _colorSortList;

    public StagingArea(LevelCore level)
    {
        Parent = level ?? throw new ArgumentNullException(nameof(level));
        Capacity = level.SlotCapacity;
        _colorSortList = new List<int>(Capacity);
    }

    public LevelCore Parent { get; }

    /// <summary>关卡要求的配对消除数。</summary>
    public int MatchRequireCount => Parent.MatchRequireCount;

    /// <summary>当前运行时容量，可用于模拟时临时扩容。</summary>
    public int Capacity { get; private set; }

    public SlotTypeEnum SlotType => Parent.SlotType;

    public int TileCount => _tileIndexSet.Count;
    public int UsedCapacity => _tileIndexSet.Count;

    public int AvailableCapacity => Capacity - TileCount;

    public bool IsFull => TileCount >= Capacity;

    public bool IsEmpty => TileCount == 0;

    /// <summary>
    /// 当前槽中颜色集合快照。
    /// </summary>
    public int[] GetColors()
    {
        if (_colorSortList.Count == 0)
            return Array.Empty<int>();

        return _colorSortList.ToArray();
    }

    public bool ContainsTile(int tileIndex)
    {
        return _tileIndexSet.Contains(tileIndex);
    }

    public int GetColorCount(int color)
    {
        return _colorGroups.TryGetValue(color, out var group) ? group.Count : 0;
    }

    public Dictionary<int, int> GetColorCountMap()
    {
        return _colorGroups.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }

    /// <summary>
    /// 获取某颜色在槽中的 tileIndex 快照。
    /// </summary>
    public int[] GetTilesOfColor(int color)
    {
        return _colorGroups.TryGetValue(color, out var group)
            ? group.ToArray()
            : Array.Empty<int>();
    }

    public void SetCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        if (capacity < TileCount)
        {
            throw new InvalidOperationException(
                $"Cannot set capacity to {capacity}, current tile count is {TileCount}.");
        }

        Capacity = capacity;
        _colorSortList.Capacity = capacity;
    }

    public void Reset()
    {
        _tileIndexSet.Clear();
        _colorGroups.Clear();
        Capacity = Parent.SlotCapacity;
        _colorSortList.Clear();
        _colorSortList.Capacity = Capacity;
    }

    public StagingArea Clone(LevelCore newParent)
    {
        if (newParent is null)
            throw new ArgumentNullException(nameof(newParent));

        var clone = new StagingArea(newParent);
        clone.Capacity = Capacity;

        foreach (var tileIndex in _tileIndexSet)
            clone._tileIndexSet.Add(tileIndex);

        foreach (var (color, group) in _colorGroups)
            clone._colorGroups[color] = new List<int>(group);

        clone._colorSortList = new List<int>(_colorSortList)
        {
            Capacity = Capacity
        };

        return clone;
    }

    /// <summary>
    /// 向槽中加入一个 tile。
    /// 重复加入直接忽略。
    /// </summary>
    public void AddTile(int tileIndex)
    {
        if (!Parent.TryGetTileByIndex(tileIndex, out var tile))
            return;

        if (!_tileIndexSet.Add(tileIndex))
            return;

        if (_tileIndexSet.Count > Capacity)
        {
            _tileIndexSet.Remove(tileIndex);
            throw new InvalidOperationException(
                $"StagingArea is full. Capacity={Capacity}, Count={TileCount}");
        }

        if (!_colorGroups.TryGetValue(tile.Color, out var group))
        {
            group = new List<int>();
            _colorGroups.Add(tile.Color, group);
            _colorSortList.Add(tile.Color);
        }

        group.Add(tileIndex);
    }

    /// <summary>
    /// 从槽中移除一个 tile。
    /// 不存在则忽略。
    /// </summary>
    public void RemoveTile(int tileIndex)
    {
        if (!_tileIndexSet.Remove(tileIndex))
            return;

        if (!Parent.TryGetTileByIndex(tileIndex, out var tile))
            return;

        if (!_colorGroups.TryGetValue(tile.Color, out var group))
            return;

        group.Remove(tileIndex);
        if (group.Count == 0)
        {
            _colorGroups.Remove(tile.Color);
            _colorSortList.Remove(tile.Color);
        }
    }

    /// <summary>
    /// 移除某颜色的全部 tile，并返回被移除的 tileIndex。
    /// </summary>
    public int[] RemoveColor(int color)
    {
        if (!_colorGroups.Remove(color, out var group) || group.Count == 0)
            return Array.Empty<int>();

        var removed = group.ToArray();

        foreach (var tileIndex in removed)
            _tileIndexSet.Remove(tileIndex);

        _colorSortList.Remove(color);

        return removed;
    }

    /// <summary>
    /// 当前该颜色是否已达到可匹配条件。
    /// </summary>
    public bool CanMatch(int color)
    {
        return GetColorCount(color) >= MatchRequireCount;
    }

    /// <summary>
    /// 查看当前该颜色若执行一次匹配，将取出的 tileIndex。
    /// 不修改内部状态。
    /// </summary>
    public int[] PeekMatch(int color)
    {
        if (!_colorGroups.TryGetValue(color, out var group))
            return Array.Empty<int>();

        if (group.Count < MatchRequireCount)
            return Array.Empty<int>();

        var result = new int[MatchRequireCount];
        for (int i = 0; i < MatchRequireCount; i++)
            result[i] = group[i];

        return result;
    }

    /// <summary>
    /// 尝试执行一次匹配。
    /// 若成功，移除该颜色的一组 tile。
    /// </summary>
    public bool TryMatch(int color, out int[] matchedTileIndexes)
    {
        matchedTileIndexes = PeekMatch(color);
        if (matchedTileIndexes.Length == 0)
            return false;

        if (!_colorGroups.TryGetValue(color, out var group))
        {
            matchedTileIndexes = Array.Empty<int>();
            return false;
        }

        foreach (var tileIndex in matchedTileIndexes)
            _tileIndexSet.Remove(tileIndex);

        group.RemoveRange(0, matchedTileIndexes.Length);

        if (group.Count == 0)
        {
            _colorGroups.Remove(color);
            _colorSortList.Remove(color);
        }

        return true;
    }
}
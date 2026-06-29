using Tile.Core.Common.BitSet;
using Tile.Core.Core.Mapping;

namespace Tile.Core.Core.Zones;

/// <summary>
/// 卡槽区，维护进入卡槽的棋子顺序、花色集合和花色数量。
/// </summary>
public sealed class StagingArea
{
    #region Fields

    private readonly TileMappingTable _mapping;
    private readonly int _matchRequireCount;

    private int[] _tiles;
    private readonly byte[] _suitCounts;
    private int _count;
    private ulong _suitBits;

    #endregion

    #region Capacity Properties

    /// <summary>
    /// 卡槽总容量。
    /// </summary>
    public int Capacity => _tiles.Length;

    /// <summary>
    /// 当前已使用的卡槽数量。
    /// </summary>
    public int UsedCapacity => _count;

    /// <summary>
    /// 当前剩余可用卡槽数量。
    /// </summary>
    public int AvailableCapacity => Capacity - UsedCapacity;

    /// <summary>
    /// 当前卡槽是否已满。
    /// </summary>
    public bool IsFull => UsedCapacity >= Capacity;

    /// <summary>
    /// 当前卡槽是否为空。
    /// </summary>
    public bool IsEmpty => UsedCapacity == 0;

    #endregion

    #region State Properties

    /// <summary>
    /// 当前卡槽已有花色集合，使用 suit 作为 bit 下标。
    /// </summary>
    public ulong SuitBits => _suitBits;

    /// <summary>
    /// 当前卡槽内的棋子顺序。
    /// </summary>
    public ReadOnlySpan<int> Tiles => _tiles.AsSpan(0, UsedCapacity);

    #endregion

    #region Object Basics

    /// <summary>
    /// 创建卡槽区。
    /// </summary>
    public StagingArea(
        TileMappingTable mapping,
        int matchRequireCount,
        int capacity)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        if (matchRequireCount < 2)
            throw new ArgumentOutOfRangeException(
                nameof(matchRequireCount),
                "匹配所需数量不能小于 2。");

        if (capacity < matchRequireCount)
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "卡槽容量不能小于匹配所需数量。");

        _matchRequireCount = matchRequireCount;

        _tiles = new int[capacity];
        _suitCounts = new byte[Tile.MaxSuitCount];
    }

    private StagingArea(
        TileMappingTable mapping,
        int matchRequireCount,
        int[] tiles,
        int count,
        ulong suitBits,
        byte[] suitCounts)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _matchRequireCount = matchRequireCount;
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        _count = count;
        _suitBits = suitBits;
        _suitCounts = suitCounts ?? throw new ArgumentNullException(nameof(suitCounts));
    }

    /// <summary>
    /// 清空卡槽状态，容量保持不变。
    /// </summary>
    public void Reset()
    {
        _tiles.AsSpan(0, _count).Clear();
        _suitCounts.AsSpan().Clear();
        _count = 0;
        _suitBits = 0UL;
    }

    /// <summary>
    /// 创建当前卡槽状态的独立副本。
    /// </summary>
    public StagingArea Clone()
    {
        return new StagingArea(
            _mapping,
            _matchRequireCount,
            (int[])_tiles.Clone(),
            _count,
            _suitBits,
            (byte[])_suitCounts.Clone());
    }

    /// <summary>
    /// 返回卡槽当前状态摘要。
    /// </summary>
    public override string ToString()
    {
        return $"StagingArea(" +
               $"Used={UsedCapacity}/{Capacity}, " +
               $"Available={AvailableCapacity}, " +
               $"IsFull={IsFull}, " +
               $"IsEmpty={IsEmpty}, " +
               $"Tiles=[{FormatTiles()}], " +
               $"SuitBits={SuitBits}, " +
               $"SuitCounts=[{FormatSuitCounts()}])";
    }

    #endregion

    #region Capacity Actions

    /// <summary>
    /// 调整卡槽容量；不能小于当前已使用容量。
    /// </summary>
    public void SetCapacity(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "卡槽容量不能小于当前已使用容量。");

        if (capacity == Capacity)
            return;

        Array.Resize(ref _tiles, capacity);
    }

    #endregion

    #region Tile Actions

    /// <summary>
    /// 使棋子进入卡槽；同花色棋子会插入到同花色组末尾。
    /// </summary>
    public void Enter(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (IsFull)
            throw new InvalidOperationException("卡槽已满，无法添加棋子。");

        var suit = _mapping.GetSuit(tileIndex);
        var insertIndex = FindInsertIndexBySuit(suit);

        InsertAt(insertIndex, tileIndex);
        AddSuit(suit);
    }

    /// <summary>
    /// 使指定棋子离开卡槽。
    /// </summary>
    public void Leave(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        var index = Array.IndexOf(_tiles, tileIndex, 0, _count);
        if (index < 0)
            throw new InvalidOperationException($"棋子 {tileIndex} 不在卡槽中。");

        RemoveAt(index);
        RemoveSuit(_mapping.GetSuit(tileIndex));
    }

    #endregion

    #region Suit Actions

    /// <summary>
    /// 移除指定花色的全部棋子，并返回被移除的棋子集合。
    /// </summary>
    public int[] RemoveSuitGroup(int suit)
    {
        var removedTileIds = GetSuitTiles(suit);
        if (removedTileIds.Length == 0)
            return removedTileIds;

        var write = 0;
        for (var read = 0; read < _count; read++)
        {
            var tileIndex = _tiles[read];
            if (_mapping.GetSuit(tileIndex) != suit)
                _tiles[write++] = tileIndex;
        }

        _count = write;
        _suitBits &= ~(1UL << suit);
        _suitCounts[suit] = 0;

        return removedTileIds;
    }

    #endregion

    #region Suit Queries

    /// <summary>
    /// 获取指定花色在卡槽中的棋子数量。
    /// </summary>
    public int GetSuitCount(int suit)
    {
        ValidateSuit(suit);
        return _suitCounts[suit];
    }

    /// <summary>
    /// 获取指定花色在卡槽中的棋子集合，顺序与卡槽内顺序一致。
    /// </summary>
    public int[] GetSuitTiles(int suit)
    {
        ValidateSuit(suit);

        var suitTiles = new int[_suitCounts[suit]];
        var write = 0;

        for (var read = 0; read < _count; read++)
        {
            var tileIndex = _tiles[read];
            if (_mapping.GetSuit(tileIndex) == suit)
                suitTiles[write++] = tileIndex;
        }

        return suitTiles;
    }

    /// <summary>
    /// 将指定花色的棋子集合写入调用方提供的 bit 缓冲区。
    /// </summary>
    /// <returns>指定花色的棋子数量。</returns>
    public int GetSuitTileBits(int suit, Span<ulong> tileBits)
    {
        ValidateSuit(suit);

        if (tileBits.Length < _mapping.WordCount)
            throw new ArgumentException("棋子 bit 缓冲区长度不足。", nameof(tileBits));

        // 调用方复用缓冲区，因此这里先清空，保证输出确定。
        tileBits.Clear();

        if ((_suitBits & (1UL << suit)) == 0)
            return 0;

        var resultBits = tileBits[.._mapping.WordCount];
        for (var i = 0; i < _count; i++)
        {
            var tileIndex = _tiles[i];
            if (_mapping.GetSuit(tileIndex) == suit)
                BitSetOperations.Set(resultBits, tileIndex);
        }

        return _suitCounts[suit];
    }

    #endregion

    #region Matching

    /// <summary>
    /// 如果指定花色满足匹配数量，则从卡槽移除该花色组。
    /// </summary>
    public bool TryMatch(int suit, out int[] matchedTileIds)
    {
        matchedTileIds = [];

        if (GetSuitCount(suit) < _matchRequireCount)
            return false;

        matchedTileIds = RemoveSuitGroup(suit);

        return true;
    }

    #endregion

    #region Stable Core Semantics

    private int FindInsertIndexBySuit(int suit)
    {
        // 同花色棋子保持成组；新花色追加到末尾。
        for (var i = _count - 1; i >= 0; i--)
        {
            if (_mapping.GetSuit(_tiles[i]) == suit)
                return i + 1;
        }

        return _count;
    }

    private void InsertAt(int index, int tileIndex)
    {
        Array.Copy(_tiles, index, _tiles, index + 1, _count - index);
        _tiles[index] = tileIndex;
        _count++;
    }

    private void RemoveAt(int index)
    {
        Array.Copy(_tiles, index + 1, _tiles, index, _count - index - 1);
        _count--;
    }

    private void AddSuit(int suit)
    {
        if (_suitCounts[suit] == 0)
            _suitBits |= 1UL << suit;

        _suitCounts[suit]++;
    }

    private void RemoveSuit(int suit)
    {
        _suitCounts[suit]--;

        if (_suitCounts[suit] == 0)
            _suitBits &= ~(1UL << suit);
    }

    private string FormatTiles()
    {
        var text = string.Empty;

        for (var i = 0; i < _count; i++)
        {
            if (text.Length > 0)
                text += ", ";

            var tileIndex = _tiles[i];
            var suit = _mapping.GetSuit(tileIndex);

            text += $"t{tileIndex}_s{suit}";
        }

        return text;
    }

    private string FormatSuitCounts()
    {
        var text = string.Empty;

        for (var suit = 0; suit < _suitCounts.Length; suit++)
        {
            var count = _suitCounts[suit];
            if (count == 0)
                continue;

            if (text.Length > 0)
                text += ", ";

            text += $"s{suit}_c{count}";
        }

        return text;
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

    /// <summary>
    /// 验证 suit 是否位于合法花色范围内。
    /// </summary>
    private static void ValidateSuit(int suit)
    {
        if ((uint)suit >= Tile.MaxSuitCount)
            throw new ArgumentOutOfRangeException(nameof(suit));
    }

    #endregion
}

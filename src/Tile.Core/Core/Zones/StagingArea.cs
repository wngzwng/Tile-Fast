using Tile.Core.Core.Mapping;

namespace Tile.Core.Core.Zones;
public sealed class StagingArea
{
    private readonly TileMappingTable _mapping;

    private readonly int _matchRequireCount;
    private readonly int _capacity;

    private readonly int[] _tiles;
    private int _count;
    private readonly int[] _orderSuits;
    private int _orderSuitIndex;

    public int MatchRequireCount => _matchRequireCount;

    public int Capacity => _capacity;

    public int Count => _count;

    public int AvailableCapacity => _capacity - _count;

    public bool IsFull => _count >= _capacity;

    public bool IsEmpty => _count == 0;

    public ReadOnlySpan<int> Tiles => _tiles.AsSpan(0, _count);

    public StagingArea(
        TileMappingTable mapping,
        LevelRuleSpec ruleSpec)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        _matchRequireCount = ruleSpec.MatchRequireCount;
        _capacity = ruleSpec.SlotCapacity;

        _tiles = new int[_capacity];
        _orderSuits = new int[_capacity];
    }

    public void Add(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (IsFull)
            throw new InvalidOperationException("Staging area is full.");

        _tiles[_count++] = tileIndex;
        
        var suit = _mapping.GetSuit(tileIndex);
        if (_orderSuits.AsSpan(0, _orderSuitIndex + 1).Contains(suit))
           return;
        
        _orderSuits[_orderSuitIndex++] = suit;
    }

    public void Remove(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        var index = Array.IndexOf(_tiles, tileIndex, 0, _count);
        if (index < 0)
            throw new InvalidOperationException($"Tile {tileIndex} is not in staging area.");

        // 将 index 之后的元素前移一位
        Array.Copy(_tiles, index + 1, _tiles, index, _count - index - 1);
        _count--;

        // 获取对应花色，组内无对应花色其他棋子，移除此花色
        var suit = _mapping.GetSuit(tileIndex);
        var hasSameSuit = false;
        for (var i = 0; i < _count; i++)
        {
            if (_mapping.GetSuit(_tiles[i]) == suit)
            {
                hasSameSuit = true;
                break;
            }
        }   
        if (hasSameSuit)
            return;

        var suitIndex = Array.IndexOf(_orderSuits, suit, 0, _orderSuitIndex);
        if (suitIndex < 0)
            return;

        // 将 suitIndex 之后的元素前移一位
        Array.Copy(_orderSuits, suitIndex + 1, _orderSuits, suitIndex, _orderSuitIndex - suitIndex - 1);
        _orderSuitIndex--;
    }

    public int GetSuitCount(int suit)
    {
        var count = 0;

        for (var i = 0; i < _count; i++)
        {
            if (_mapping.GetSuit(_tiles[i]) == suit)
                count++;
        }

        return count;
    }

    public bool CanMatch(int suit)
    {
        return GetSuitCount(suit) >= _matchRequireCount;
    }

    public bool TryMatch(int suit, out int[] matchedTileIds)
    {
        matchedTileIds = null;

        if (!CanMatch(suit))
            return false;

        // 收集对应花色的棋子
        int[] sameSuitTiles = new int[_matchRequireCount];
    
        var write = 0;
        for (var read = 0; read < _count; read++)
        {
            var tileIndex = _tiles[read];

            if (_mapping.GetSuit(tileIndex) == suit)
                sameSuitTiles[write++] = tileIndex;
        }

        // 移除对应花色的棋子   
        for (var i = 0; i < _matchRequireCount; i++)
        {
            Remove(sameSuitTiles[i]);
        }
        matchedTileIds = sameSuitTiles;

        return true;
    }

    private void ValidateTileIndex(int tileIndex)
    {
        if ((uint)tileIndex >= (uint)_mapping.TileCount)
            throw new ArgumentOutOfRangeException(nameof(tileIndex));
    }

    private StagingArea(
        TileMappingTable mapping,
        int matchRequireCount,
        int capacity,
        int[] tiles,
        int count)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _matchRequireCount = matchRequireCount;
        _capacity = capacity;
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        _count = count;
    }

    public StagingArea Clone()
    {
        return new StagingArea(
            _mapping,
            _matchRequireCount,
            _capacity,
            (int[])_tiles.Clone(),
            _count);
    }
}

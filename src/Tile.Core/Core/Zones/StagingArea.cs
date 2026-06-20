using Tile.Core.Core.Mapping;

namespace Tile.Core.Core.Zones;
public sealed class StagingArea
{
    private readonly TileMappingTable _mapping;

    private readonly int _matchRequireCount;
    private readonly int _capacity;

    private readonly int[] _tiles;
    private int _count;

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
    }

    public void Add(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (IsFull)
            throw new InvalidOperationException("Staging area is full.");

        _tiles[_count++] = tileIndex;
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

    public bool TryMatch(
        int suit,
        Span<int> removedTiles,
        out int removedCount)
    {
        removedCount = 0;

        if (!CanMatch(suit))
            return false;

        if (removedTiles.Length < _matchRequireCount)
            throw new ArgumentException(
                "Removed tile buffer is too small.",
                nameof(removedTiles));

        var write = 0;

        for (var read = 0; read < _count; read++)
        {
            var tileIndex = _tiles[read];

            if (_mapping.GetSuit(tileIndex) == suit &&
                removedCount < _matchRequireCount)
            {
                removedTiles[removedCount++] = tileIndex;
                continue;
            }

            _tiles[write++] = tileIndex;
        }

        _count = write;
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

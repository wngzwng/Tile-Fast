public sealed class Corral
{
    private readonly int[] _tiles;
    private int _offset;


    public int Count => _offset;

    public bool IsEmpty => Count == 0;

    public Corral(int tileCount)
    {
        if (tileCount < 0)
            throw new ArgumentOutOfRangeException(nameof(tileCount), "Tile count cannot be negative.");

        _tiles = new int[tileCount];
        Array.Fill<int>(_tiles, -1);
    }

    public void Add(int tileIndex)
    {
        if (_offset >= _tiles.Length)
            throw new InvalidOperationException("Corral is full.");

        _tiles[_offset] = tileIndex;
        _offset++;
    }

    public void Remove(int tileCount)
    {
        if (tileCount < 0)
            throw new ArgumentOutOfRangeException(nameof(tileCount));

        if (tileCount > _offset)
            throw new InvalidOperationException(
                $"无法移除 {tileCount} 个 tile，当前只有 {_offset} 个。");

        _offset -= tileCount;

        _tiles.AsSpan(_offset, tileCount).Fill(-1);
    }


     private Corral(
        int[] tiles,
        int offset)
    {
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        _offset = offset;
    }

    public Corral Clone()
    {
        return new Corral(
            (int[])_tiles.Clone(),
            _offset);
    }
}

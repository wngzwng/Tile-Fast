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
    }


    public bool TryPeek(out int tileIndex)
    {
        if (_offset >= _tiles.Length)
        {
            tileIndex = -1;
            return false;
        }

        tileIndex = _tiles[_offset];
        return true;
    }

    public bool TryTake(out int tileIndex)
    {
        if (_offset >= _tiles.Length)
        {
            tileIndex = -1;
            return false;
        }

        tileIndex = _tiles[_offset++];
        return true;
    }


    public void Add(int tileIndex)
    {
        if (_offset >= _tiles.Length)
            throw new InvalidOperationException("Corral is full.");

        _tiles[_offset] = tileIndex;
        _offset++;
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

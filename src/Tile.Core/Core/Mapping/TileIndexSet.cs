using Tile.Core.Common.BitSet;

public readonly ref struct TileIndexSet
{
    private readonly ReadOnlySpan<ulong> _bits;

    public TileIndexSet(ReadOnlySpan<ulong> bits)
    {
        _bits = bits;
    }

    public ReadOnlySpan<ulong> Bits => _bits;

    public bool Contains(int tileIndex)
    {
        return BitSetOperations.Get(_bits, tileIndex);
    }

    public int Count()
    {
        return BitSetOperations.PopCount(_bits);
    }

    public bool IsEmpty()
    {
        return BitSetOperations.IsEmpty(_bits);
    }

    public UInt64BitIterator GetEnumerator()
    {
        return BitSetOperations.EnumerateSetBits(_bits);
    }

    public int CopyTo(Span<int> buffer)
    {
        var count = 0;

        foreach (var tileIndex in this)
        {
            if (count >= buffer.Length)
                break;

            buffer[count++] = tileIndex;
        }

        return count;
    }

    public static TileIndexSet Wrap(ReadOnlySpan<ulong> bits)
    {
        return new TileIndexSet(bits);
    }
}
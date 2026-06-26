using Tile.Core.Core.Utils;

namespace Tile.Core.ExtensionTools;

public static class CharExtensionTools
{
    public static char GetCharFromInt(this int num)
        => Base62CharCodec.GetCharFromInt(num);

    public static int CharToIndex(this char c)
        => Base62CharCodec.CharToIndex(c);

    public static int PairCharsToValue(char ch1, char ch2)
        => Base62CharCodec.PairCharsToValue(ch1, ch2);
}

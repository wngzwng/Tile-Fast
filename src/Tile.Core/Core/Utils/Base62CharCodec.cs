namespace Tile.Core.Core.Utils;

public static class Base62CharCodec
{
    public static char GetCharFromInt(int num)
        => num switch
        {
            < 10 => (char)(num + '0'),
            < 36 => (char)(num - 10 + 'A'),
            < 62 => (char)(num - 36 + 'a'),
            _ => throw new IndexOutOfRangeException("超出 61 进制上限")
        };

    public static int CharToIndex(char c)
        => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'Z' => c - 'A' + 10,
            >= 'a' and <= 'z' => c - 'a' + 36,
            _ => throw new InvalidOperationException("字符内容不正确")
        };

    public static int PairCharsToValue(char ch1, char ch2)
    {
        var tensPart = CharToIndex(ch1);
        var onesPart = ch2 - '0';
        return tensPart * 10 + onesPart;
    }
}

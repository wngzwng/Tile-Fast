namespace Tile.Core.ExtensionTools;

/// <summary>
/// 提供 TileBit 的 int 扩展方法。
/// TileBit 是 Tile 的紧凑 int 编码，当前 MVP 阶段只保存：
/// 1. packed(x, y, z) 位置；
/// 2. 可选花色。
/// </summary>
public static class TileBitExtensionTools
{
    private const int PositionMask = 0x00FF_FFFF;
    private const int SuitShift = 24;
    private const int SuitMask = 0xFF;


    /// <summary>
    /// 将 packed(x, y, z) 位置编码为 TileBit。
    /// 布局：
    /// <c>X(8 bit) | Y(8 bit) | Z(8 bit) | Suit(8 bit)</c>。
    /// 前 3 byte 为 packed xyz，最后 1 byte 为 0-based suit。
    /// <c>0xFF</c> 表示花色未指定。
    /// </summary>
    public static int ToTileBit(this int position, int suit)
    {
        return (position & PositionMask) | (suit << SuitShift);
    }

     /// <summary>
    /// 从 TileBit 创建新的 Tile。
    /// <paramref name="hasSuit"/> 为 <see langword="false"/> 时，即使编码中存在花色位，也不会写入 Tile.Suit。
    /// </summary>
    public static Tile ToTile(this int tileBit, int index)
    {
        var tile = new Tile(index, tileBit.GetTilePositionFromBit());
        int suit = tileBit.GetTileSuitFromBit();

        if (suit != Tile.SuitUnspecified)
            tile.SetSuit(suit);

        return tile;
    }


    /// <summary>
    /// 从 TileBit 中读取 packed(x, y, z) 位置。
    /// </summary>
    public static int GetTilePositionFromBit(this int tileBit)
    {
        return tileBit & PositionMask;
    }

    /// <summary>
    /// 从 TileBit 中读取花色。
    /// 如果编码中没有花色，则返回 <see cref="Tile.SuitUnspecified"/>。
    /// </summary>
    public static int GetTileSuitFromBit(this int tileBit)
    {
        return (tileBit >> SuitShift) & SuitMask;
    }

    /// <summary>
    /// 返回便于调试阅读的字符串。
    /// </summary>
    public static string ToTileBitDebugString(this int tileBit)
    {
        int position = tileBit.GetTilePositionFromBit();
        int suit = tileBit.GetTileSuitFromBit();
        return $"TileBit(Position={position.ToXyzString()}, Suit={suit})";
    }

}

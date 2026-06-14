namespace Tile.Core.ExtensionTools;

/// <summary>
/// 提供 packed xyz 的打包、解包与分量改写能力。
/// 当前布局：
/// <c>X | Y | Z | Reserved</c>，每个分量占 8 bit。
/// 约定：
/// X 等价于 Col；
/// Y 等价于 Row。
/// </summary>
public static class PackedXyzExtensionTools
{
    private const int XShift = 0;
    private const int YShift = 8;
    private const int ZShift = 16;
    private const int Mask = 0xFF;

    /// <summary>
    /// 将 <see cref="byte"/> 三元组打包成一个 <see cref="int"/>。
    /// </summary>
    public static int PackXyz(this (byte x, byte y, byte z) v)
        => v.x
           | (v.y << YShift)
           | (v.z << ZShift);

    /// <summary>
    /// 将 <see cref="int"/> 三元组打包成一个 <see cref="int"/>。
    /// 超出 8 bit 的高位会被截断，只保留低 8 bit。
    /// </summary>
    public static int PackXyz(this (int x, int y, int z) v)
        => (v.x & Mask)
           | ((v.y & Mask) << YShift)
           | ((v.z & Mask) << ZShift);

    /// <summary>
    /// 将 packed xyz 解包为 <see cref="byte"/> 三元组。
    /// </summary>
    public static (byte x, byte y, byte z) UnpackXyz(this int value)
        => ((byte)(value & Mask),
            (byte)((value >> YShift) & Mask),
            (byte)((value >> ZShift) & Mask));

    /// <summary>
    /// 读取 X 分量的 byte 版本。
    /// </summary>
    public static byte XByte(this int value) => (byte)(value & Mask);

    /// <summary>
    /// 读取 Y 分量的 byte 版本。
    /// </summary>
    public static byte YByte(this int value) => (byte)((value >> YShift) & Mask);

    /// <summary>
    /// 读取 Z 分量的 byte 版本。
    /// </summary>
    public static byte ZByte(this int value) => (byte)((value >> ZShift) & Mask);

    /// <summary>
    /// 读取 Col 分量的 byte 版本。
    /// Col 等价于 X。
    /// </summary>
    public static byte ColByte(this int value) => value.XByte();

    /// <summary>
    /// 读取 Row 分量的 byte 版本。
    /// Row 等价于 Y。
    /// </summary>
    public static byte RowByte(this int value) => value.YByte();

    /// <summary>
    /// 读取 X 分量的 int 版本。
    /// </summary>
    public static int X(this int value) => value & Mask;

    /// <summary>
    /// 读取 Y 分量的 int 版本。
    /// </summary>
    public static int Y(this int value) => (value >> YShift) & Mask;

    /// <summary>
    /// 读取 Z 分量的 int 版本。
    /// </summary>
    public static int Z(this int value) => (value >> ZShift) & Mask;

    /// <summary>
    /// 读取 Col 分量的 int 版本。
    /// Col 等价于 X。
    /// </summary>
    public static int Col(this int value) => value.X();

    /// <summary>
    /// 读取 Row 分量的 int 版本。
    /// Row 等价于 Y。
    /// </summary>
    public static int Row(this int value) => value.Y();

    /// <summary>
    /// 将 packed xyz 转成 <c>(x, y, z)</c> 字符串。
    /// </summary>
    public static string ToXyzString(this int value)
    {
        var (x, y, z) = value.UnpackXyz();
        return $"({x}, {y}, {z})";
    }

    /// <summary>
    /// 改写 X 分量，其余分量保持不变。
    /// 只保留 <paramref name="x"/> 的低 8 bit。
    /// </summary>
    public static int WithX(this int value, int x)
        => (value & ~Mask) | (x & Mask);

    /// <summary>
    /// 改写 Y 分量，其余分量保持不变。
    /// 只保留 <paramref name="y"/> 的低 8 bit。
    /// </summary>
    public static int WithY(this int value, int y)
        => (value & ~(Mask << YShift)) | ((y & Mask) << YShift);

    /// <summary>
    /// 改写 Z 分量，其余分量保持不变。
    /// 只保留 <paramref name="z"/> 的低 8 bit。
    /// </summary>
    public static int WithZ(this int value, int z)
        => (value & ~(Mask << ZShift)) | ((z & Mask) << ZShift);

    /// <summary>
    /// 改写 Col 分量，其余分量保持不变。
    /// Col 等价于 X。
    /// </summary>
    public static int WithCol(this int value, int col)
        => value.WithX(col);

    /// <summary>
    /// 改写 Row 分量，其余分量保持不变。
    /// Row 等价于 Y。
    /// </summary>
    public static int WithRow(this int value, int row)
        => value.WithY(row);

    /// <summary>
    /// 改写 X 分量，其余分量保持不变。
    /// </summary>
    public static int WithX(this int value, byte x)
        => (value & ~Mask) | x;

    /// <summary>
    /// 改写 Y 分量，其余分量保持不变。
    /// </summary>
    public static int WithY(this int value, byte y)
        => (value & ~(Mask << YShift)) | (y << YShift);

    /// <summary>
    /// 改写 Z 分量，其余分量保持不变。
    /// </summary>
    public static int WithZ(this int value, byte z)
        => (value & ~(Mask << ZShift)) | (z << ZShift);

    /// <summary>
    /// 改写 Col 分量，其余分量保持不变。
    /// Col 等价于 X。
    /// </summary>
    public static int WithCol(this int value, byte col)
        => value.WithX(col);

    /// <summary>
    /// 改写 Row 分量，其余分量保持不变。
    /// Row 等价于 Y。
    /// </summary>
    public static int WithRow(this int value, byte row)
        => value.WithY(row);
}

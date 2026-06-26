using Tile.Core.Core.Utils;

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
    /// <summary>
    /// 将 <see cref="byte"/> 三元组打包成一个 <see cref="int"/>。
    /// </summary>
    public static int PackXyz(this (byte x, byte y, byte z) v)
        => PositionPacker.PackXyz(v.x, v.y, v.z);

    /// <summary>
    /// 将 <see cref="int"/> 三元组打包成一个 <see cref="int"/>。
    /// 超出 8 bit 的高位会被截断，只保留低 8 bit。
    /// </summary>
    public static int PackXyz(this (int x, int y, int z) v)
        => PositionPacker.PackXyz(v.x, v.y, v.z);

    /// <summary>
    /// 将 packed xyz 解包为 <see cref="int"/> 三元组。
    /// </summary>
    public static (int x, int y, int z) UnpackXyz(this int value)
        => PositionPacker.UnpackXyz(value);

    public static (int row, int col, int z) UnpackRCZ(this int value)
        => PositionPacker.UnpackRcz(value);

    /// <summary>
    /// 读取 X 分量的 byte 版本。
    /// </summary>
    public static byte XByte(this int value) => (byte)PositionPacker.X(value);

    /// <summary>
    /// 读取 Y 分量的 byte 版本。
    /// </summary>
    public static byte YByte(this int value) => (byte)PositionPacker.Y(value);

    /// <summary>
    /// 读取 Z 分量的 byte 版本。
    /// </summary>
    public static byte ZByte(this int value) => (byte)PositionPacker.Z(value);

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
    public static int X(this int value) => PositionPacker.X(value);

    /// <summary>
    /// 读取 Y 分量的 int 版本。
    /// </summary>
    public static int Y(this int value) => PositionPacker.Y(value);

    /// <summary>
    /// 读取 Z 分量的 int 版本。
    /// </summary>
    public static int Z(this int value) => PositionPacker.Z(value);

    /// <summary>
    /// 读取 Col 分量的 int 版本。
    /// Col 等价于 X。
    /// </summary>
    public static int Col(this int value) => PositionPacker.Col(value);

    /// <summary>
    /// 读取 Row 分量的 int 版本。
    /// Row 等价于 Y。
    /// </summary>
    public static int Row(this int value) => PositionPacker.Row(value);

    /// <summary>
    /// 将 packed xyz 转成 <c>(x, y, z)</c> 字符串。
    /// </summary>
    public static string ToXyzString(this int value)
        => PositionPacker.ToXyzString(value);

    /// <summary>
    /// 改写 X 分量，其余分量保持不变。
    /// 只保留 <paramref name="x"/> 的低 8 bit。
    /// </summary>
    public static int WithX(this int value, int x)
        => PositionPacker.WithX(value, x);

    /// <summary>
    /// 改写 Y 分量，其余分量保持不变。
    /// 只保留 <paramref name="y"/> 的低 8 bit。
    /// </summary>
    public static int WithY(this int value, int y)
        => PositionPacker.WithY(value, y);

    /// <summary>
    /// 改写 Z 分量，其余分量保持不变。
    /// 只保留 <paramref name="z"/> 的低 8 bit。
    /// </summary>
    public static int WithZ(this int value, int z)
        => PositionPacker.WithZ(value, z);

    /// <summary>
    /// 改写 Col 分量，其余分量保持不变。
    /// Col 等价于 X。
    /// </summary>
    public static int WithCol(this int value, int col)
        => PositionPacker.WithCol(value, col);

    /// <summary>
    /// 改写 Row 分量，其余分量保持不变。
    /// Row 等价于 Y。
    /// </summary>
    public static int WithRow(this int value, int row)
        => PositionPacker.WithRow(value, row);

    /// <summary>
    /// 改写 X 分量，其余分量保持不变。
    /// </summary>
    public static int WithX(this int value, byte x)
        => PositionPacker.WithX(value, x);

    /// <summary>
    /// 改写 Y 分量，其余分量保持不变。
    /// </summary>
    public static int WithY(this int value, byte y)
        => PositionPacker.WithY(value, y);

    /// <summary>
    /// 改写 Z 分量，其余分量保持不变。
    /// </summary>
    public static int WithZ(this int value, byte z)
        => PositionPacker.WithZ(value, z);

    /// <summary>
    /// 改写 Col 分量，其余分量保持不变。
    /// Col 等价于 X。
    /// </summary>
    public static int WithCol(this int value, byte col)
        => PositionPacker.WithCol(value, col);

    /// <summary>
    /// 改写 Row 分量，其余分量保持不变。
    /// Row 等价于 Y。
    /// </summary>
    public static int WithRow(this int value, byte row)
        => PositionPacker.WithRow(value, row);
}

namespace Tile.Core.Core.Types;

/// <summary>
/// packed position 的打包、解包与分量改写工具。
///
/// 当前布局：
/// <code>
/// bit  0 -  7 : X
/// bit  8 - 15 : Y
/// bit 16 - 23 : Z
/// bit 24 - 31 : Reserved
/// </code>
///
/// 每个分量占 8 bit，因此单个分量范围为：
/// <code>
/// 0 ~ 255
/// </code>
///
/// 坐标语义：
/// <code>
/// XYZ = (x, y, z)
/// RCZ = (row, col, z)
/// </code>
///
/// 坐标映射：
/// <code>
/// RCZ(row, col, z) => XYZ(col, row, z)
///
/// x = col
/// y = row
/// z = z
/// </code>
/// </summary>
public static class PositionPacker
{
    private const int XShift = 0;
    private const int YShift = 8;
    private const int ZShift = 16;

    private const int Mask = 0xFF;

    /// <summary>
    /// 将 XYZ 三元组打包成 packed position。
    ///
    /// 注意：
    /// 超出 8 bit 的高位会被截断，只保留低 8 bit。
    /// </summary>
    public static int PackXyz(int x, int y, int z)
    {
        return (x & Mask)
             | ((y & Mask) << YShift)
             | ((z & Mask) << ZShift);
    }

    /// <summary>
    /// 将 RCZ 三元组打包成 packed position。
    ///
    /// RCZ 参数语义：
    /// - row 表示行，对应 Y
    /// - col 表示列，对应 X
    /// - z 表示层，对应 Z
    ///
    /// 实际打包等价于：
    /// <code>
    /// PackXyz(col, row, z)
    /// </code>
    ///
    /// 注意：
    /// 超出 8 bit 的高位会被截断，只保留低 8 bit。
    /// </summary>
    public static int PackRcz(int row, int col, int z)
    {
        return PackXyz(col, row, z);
    }

    /// <summary>
    /// 将 packed position 解包为 XYZ 三元组。
    ///
    /// 返回值语义：
    /// <code>
    /// (x, y, z)
    /// </code>
    /// </summary>
    public static (int x, int y, int z) UnpackXyz(int value)
    {
        return (
            value & Mask,
            (value >> YShift) & Mask,
            (value >> ZShift) & Mask
        );
    }

    /// <summary>
    /// 将 packed position 解包为 RCZ 三元组。
    ///
    /// 底层 XYZ 为：
    /// <code>
    /// (x, y, z)
    /// </code>
    ///
    /// 返回 RCZ 为：
    /// <code>
    /// (row, col, z) = (y, x, z)
    /// </code>
    /// </summary>
    public static (int row, int col, int z) UnpackRcz(int value)
    {
        return (
            (value >> YShift) & Mask,
            value & Mask,
            (value >> ZShift) & Mask
        );
    }

    /// <summary>
    /// 读取 X 分量。
    /// </summary>
    public static int X(int value)
    {
        return value & Mask;
    }

    /// <summary>
    /// 读取 Y 分量。
    /// </summary>
    public static int Y(int value)
    {
        return (value >> YShift) & Mask;
    }

    /// <summary>
    /// 读取 Z 分量。
    /// </summary>
    public static int Z(int value)
    {
        return (value >> ZShift) & Mask;
    }

    /// <summary>
    /// 读取 Row 分量。
    ///
    /// Row 对应 Y。
    /// </summary>
    public static int Row(int value)
    {
        return Y(value);
    }

    /// <summary>
    /// 读取 Col 分量。
    ///
    /// Col 对应 X。
    /// </summary>
    public static int Col(int value)
    {
        return X(value);
    }

    /// <summary>
    /// 改写 X 分量，其余分量保持不变。
    ///
    /// 注意：
    /// 只保留 <paramref name="x"/> 的低 8 bit。
    /// </summary>
    public static int WithX(int value, int x)
    {
        return (value & ~Mask)
             | (x & Mask);
    }

    /// <summary>
    /// 改写 Y 分量，其余分量保持不变。
    ///
    /// 注意：
    /// 只保留 <paramref name="y"/> 的低 8 bit。
    /// </summary>
    public static int WithY(int value, int y)
    {
        return (value & ~(Mask << YShift))
             | ((y & Mask) << YShift);
    }

    /// <summary>
    /// 改写 Z 分量，其余分量保持不变。
    ///
    /// 注意：
    /// 只保留 <paramref name="z"/> 的低 8 bit。
    /// </summary>
    public static int WithZ(int value, int z)
    {
        return (value & ~(Mask << ZShift))
             | ((z & Mask) << ZShift);
    }

    /// <summary>
    /// 改写 Row 分量，其余分量保持不变。
    ///
    /// Row 对应 Y。
    /// </summary>
    public static int WithRow(int value, int row)
    {
        return WithY(value, row);
    }

    /// <summary>
    /// 改写 Col 分量，其余分量保持不变。
    ///
    /// Col 对应 X。
    /// </summary>
    public static int WithCol(int value, int col)
    {
        return WithX(value, col);
    }

    /// <summary>
    /// 将 packed position 转成 XYZ 字符串。
    /// </summary>
    public static string ToXyzString(int value)
    {
        var (x, y, z) = UnpackXyz(value);
        return $"({x}, {y}, {z})";
    }

    /// <summary>
    /// 将 packed position 转成 RCZ 字符串。
    /// </summary>
    public static string ToRczString(int value)
    {
        var (row, col, z) = UnpackRcz(value);
        return $"({row}, {col}, {z})";
    }
}
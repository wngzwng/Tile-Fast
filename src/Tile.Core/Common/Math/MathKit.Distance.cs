namespace Tile.Core.Common.Math;

public static partial class MathKit
{
    /// <summary>
     /// 计算二维曼哈顿距离。
    /// 公式：<c>|x1 - x2| + |y1 - y2|</c>。
     /// </summary>
    public static int Manhattan2D(int x1, int y1, int x2, int y2)
        => System.Math.Abs(x1 - x2) + System.Math.Abs(y1 - y2);

    /// <summary>
     /// 计算三维曼哈顿距离。
    /// 公式：<c>|x1 - x2| + |y1 - y2| + |z1 - z2|</c>。
     /// </summary>
    public static int Manhattan3D(int x1, int y1, int z1, int x2, int y2, int z2)
        => System.Math.Abs(x1 - x2) + System.Math.Abs(y1 - y2) + System.Math.Abs(z1 - z2);

    /// <summary>
     /// 计算二维欧几里得距离。
    /// 公式：<c>sqrt((x1 - x2)^2 + (y1 - y2)^2)</c>。
     /// </summary>
    public static double Euclidean2D(int x1, int y1, int x2, int y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
     /// 计算三维欧几里得距离。
    /// 公式：<c>sqrt((x1 - x2)^2 + (y1 - y2)^2 + (z1 - z2)^2)</c>。
     /// </summary>
    public static double Euclidean3D(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dz = z1 - z2;
        return System.Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}

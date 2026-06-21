namespace Tile.Core.Core.Types;

/// <summary>
/// 邻接方向。
///
/// 坐标系约定：
/// - X：左(-) → 右(+)
/// - Y：前(-) → 后(+)
/// - Z：下(-) → 上(+)
///
/// 注意：
/// - 方向内部只使用 XYZ 语义。
/// - RCZ 只是 PositionPacker 提供的外部读写别名。
/// - packed position 与 packed bounds 都使用 XYZ 布局。
/// </summary>
public sealed class NeighborDir
{
    /// <summary>
    /// 左方向：X - 1。
    /// </summary>
    public static readonly NeighborDir Left = new(
        name: "Left",
        dx: -1,
        dy: 0,
        dz: 0);

    /// <summary>
    /// 右方向：X + 1。
    /// </summary>
    public static readonly NeighborDir Right = new(
        name: "Right",
        dx: 1,
        dy: 0,
        dz: 0);

    /// <summary>
    /// 前方向：Y - 1。
    /// </summary>
    public static readonly NeighborDir Front = new(
        name: "Front",
        dx: 0,
        dy: -1,
        dz: 0);

    /// <summary>
    /// 后方向：Y + 1。
    /// </summary>
    public static readonly NeighborDir Back = new(
        name: "Back",
        dx: 0,
        dy: 1,
        dz: 0);

    /// <summary>
    /// 上方向：Z + 1。
    /// </summary>
    public static readonly NeighborDir Up = new(
        name: "Up",
        dx: 0,
        dy: 0,
        dz: 1);

    /// <summary>
    /// 下方向：Z - 1。
    /// </summary>
    public static readonly NeighborDir Down = new(
        name: "Down",
        dx: 0,
        dy: 0,
        dz: -1);

    private NeighborDir(string name, int dx, int dy, int dz)
    {
        Name = name;
        Dx = dx;
        Dy = dy;
        Dz = dz;
    }

    /// <summary>
    /// 方向名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// X 方向偏移。
    /// </summary>
    public int Dx { get; }

    /// <summary>
    /// Y 方向偏移。
    /// </summary>
    public int Dy { get; }

    /// <summary>
    /// Z 方向偏移。
    /// </summary>
    public int Dz { get; }

    /// <summary>
    /// 按当前方向移动 packed position。
    ///
    /// position 使用 packed XYZ：
    /// - X = x
    /// - Y = y
    /// - Z = z
    ///
    /// bounds 也使用 packed XYZ：
    /// - X = maxX
    /// - Y = maxY
    /// - Z = maxZ
    ///
    /// bounds 表示最大坐标，不是长度。
    ///
    /// 如果移动后越界：
    /// - 返回 -1
    /// - isOver = true
    /// </summary>
    public int Move(int position, int bounds, out bool isOver)
    {
        var (x, y, z) = PositionPacker.UnpackXyz(position);
        var (maxX, maxY, maxZ) = PositionPacker.UnpackXyz(bounds);

        var next = Move(
            x,
            y,
            z,
            maxX,
            maxY,
            maxZ,
            out isOver);

        if (isOver)
            return -1;

        return PositionPacker.PackXyz(next.x, next.y, next.z);
    }

    /// <summary>
    /// 核心方法：按当前方向移动 XYZ 坐标。
    ///
    /// 边界约定：
    /// - x: [0, maxX]
    /// - y: [0, maxY]
    /// - z: [0, maxZ]
    ///
    /// 如果越界：
    /// - isOver = true
    /// - 返回值仍然是移动后的坐标
    ///
    /// 注意：
    /// 这个方法不负责 packed position 的拆包和重新打包。
    /// packed position 的封装由 <see cref="Move(int, int, out bool)"/> 完成。
    /// </summary>
    public (int x, int y, int z) Move(
        int x,
        int y,
        int z,
        int maxX,
        int maxY,
        int maxZ,
        out bool isOver)
    {
        var nextX = x + Dx;
        var nextY = y + Dy;
        var nextZ = z + Dz;

        isOver =
            nextX < 0 || nextX > maxX ||
            nextY < 0 || nextY > maxY ||
            nextZ < 0 || nextZ > maxZ;

        return (nextX, nextY, nextZ);
    }

    /// <summary>
    /// 获取当前方向的反方向。
    /// </summary>
    public NeighborDir Opposite()
    {
        if (this == Left)
            return Right;

        if (this == Right)
            return Left;

        if (this == Front)
            return Back;

        if (this == Back)
            return Front;

        if (this == Up)
            return Down;

        if (this == Down)
            return Up;

        throw new InvalidOperationException($"未知方向：{Name}");
    }

    public override string ToString()
    {
        return Name;
    }
}
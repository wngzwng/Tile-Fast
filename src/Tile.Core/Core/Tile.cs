using Tile.Core.ExtensionTools;

namespace Tile.Core;

/// <summary>
/// 表示 Tile 当前所在的静态区域类型。
/// </summary>
public enum TileZone
{
    Unspecified = 0,
    Pasture = 1,
    StagingArea = 2,
    Corral = 3,
}

/// <summary>
/// 表示单张 Tile 的必要静态事实。
/// 这一层只保存 Tile 本身的信息，不承担全局索引映射职责。
/// </summary>
public sealed class Tile(int index, int position) : IEquatable<Tile>
{
    /// <summary>
    /// 表示未指定花色时的默认值。
    /// </summary>
    public const int SuitUnspecified = 0;

    /// <summary>
    /// 当前约定的最大花色数量。
    /// 对应字符集：
    /// <c>0~9, A~Z, a~z</c>，
    /// 共 62 个可用花色槽位。
    /// </summary>
    public const int MaxSuitCount = 62;

    /// <summary>
    /// 默认体积，对应 <c>(dx: 2, dy: 2, dz: 1)</c>。
    /// </summary>
    public static readonly int DefaultVolume = (2, 2, 1).PackXyz();

    /// <summary>
    /// 关卡内唯一编号，使用 0-based。
    /// 也就是第一张牌的 Index 为 0。
    /// </summary>
    public int Index { get; private set; } = index >= 0
        ? index
        : throw new ArgumentOutOfRangeException(nameof(index), "Tile.Index 使用 0-based，不能小于 0。");

    /// <summary>
    /// Tile 花色，使用 0-based。
    /// 默认值是 <see cref="SuitUnspecified"/>；
    /// 一旦正式赋值，必须落在 <c>[0, MaxSuitCount - 1]</c> 范围内。
    /// </summary>
    public int Suit { get; private set; } = SuitUnspecified;

    /// <summary>
    /// Tile 的最小空间坐标，使用 packed(x, y, z) 表示。
    /// </summary>
    public int Position { get; private set; } = position;

    /// <summary>
    /// Tile 体积，使用 packed(dx, dy, dz) 表示。
    /// </summary>
    public int Volume { get; } = DefaultVolume;

    /// <summary>
    /// Tile 当前所在区域。
    /// 默认在 <see cref="TileZone.Unspecified"/>。
    /// </summary>
    public TileZone Zone { get; private set; } = TileZone.Unspecified;

    /// <summary>
    /// Tile 顶面所在的 z 值。
    /// 公式：<c>topZ = z0 + dz - 1</c>。
    /// </summary>
    public int TopZ
    {
        get
        {
            var (_, _, z0) = Position.UnpackXyz();
            var (_, _, dz) = Volume.UnpackXyz();
            return z0 + dz - 1;
        }
    }

    /// <summary>
    /// 更新 Tile 编号。
    /// </summary>
    public void SetIndex(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Tile.Index 使用 0-based，不能小于 0。");

        Index = index;
    }

    /// <summary>
    /// 更新 Tile 花色。
    /// 花色使用 0-based，必须落在 <c>[0, MaxSuitCount - 1]</c> 范围内。
    /// </summary>
    public void SetSuit(int suit)
    {
        if ((uint)suit >= (uint)MaxSuitCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(suit),
                $"Tile.Suit 必须落在 [0, {MaxSuitCount - 1}] 范围内；当前值为 {suit}。");
        }

        Suit = suit;
    }

    /// <summary>
    /// 更新 Tile 的空间位置。
    /// 这里只改 <see cref="Position"/>，不强制修改 <see cref="Zone"/>。
    /// </summary>
    public void SetPasturePosition(int position)
    {
        Position = position;
    }

    /// <summary>
    /// 更新 Tile 当前所在区域。
    /// </summary>
    public void SetZone(TileZone zone)
    {
        Zone = zone;
    }

    /// <summary>
    /// 返回 Position 解包后的三维坐标。
    /// </summary>
    public (int x, int y, int z) GetPositionXyz()
    {
        var (x, y, z) = Position.UnpackXyz();
        return (x, y, z);
    }

    /// <summary>
    /// 返回 Volume 解包后的三维尺寸。
    /// </summary>
    public (int dx, int dy, int dz) GetVolumeXyz()
    {
        var (dx, dy, dz) = Volume.UnpackXyz();
        return (dx, dy, dz);
    }

    public bool Equals(Tile? other)
    {
        if (ReferenceEquals(null, other))
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Index == other.Index &&
               Suit == other.Suit &&
               Position == other.Position &&
               Volume == other.Volume &&
               Zone == other.Zone;
    }

    public override bool Equals(object? obj)
    {
        return obj is Tile other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Index, Suit, Position, Volume, Zone);
    }

    public override string ToString()
    {
        return $"Tile(Index={Index}, Suit={Suit}, Position={Position.ToXyzString()}, Volume={Volume.ToXyzString()}, Zone={Zone})";
    }
}

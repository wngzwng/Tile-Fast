namespace Tile.Core.Core.Types;

/// <summary>
/// 邻接方向（3D 六方向）
/// 坐标系约定：
/// - X：左(-) → 右(+)
/// - Y：前(-) → 后(+)
/// - Z：下(-) → 上(+)
/// </summary>
public enum NeighborDir
{
    /// <summary>左（X-）</summary>
    Left,

    /// <summary>右（X+）</summary>
    Right,

    /// <summary>前（Y-）</summary>
    Front,

    /// <summary>后（Y+）</summary>
    Back,

    /// <summary>上（Z+）</summary>
    Up,

    /// <summary>下（Z-）</summary>
    Down
}
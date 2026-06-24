namespace Tile.Core.Core.Types;

/// <summary>
/// 邻接方向。
///
/// 坐标系约定：
/// - X：左(-) -> 右(+)
/// - Y：前(-) -> 后(+)
/// - Z：下(-) -> 上(+)
/// </summary>
public enum NeighborDirEnum
{
    /// <summary>
    /// 左方向：X - 1。
    /// </summary>
    Left = 0,

    /// <summary>
    /// 右方向：X + 1。
    /// </summary>
    Right = 1,

    /// <summary>
    /// 前方向：Y - 1。
    /// </summary>
    Front = 2,

    /// <summary>
    /// 后方向：Y + 1。
    /// </summary>
    Back = 3,

    /// <summary>
    /// 上方向：Z + 1。
    /// </summary>
    Up = 4,

    /// <summary>
    /// 下方向：Z - 1。
    /// </summary>
    Down = 5
}

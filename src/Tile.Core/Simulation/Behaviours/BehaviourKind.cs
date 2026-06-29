namespace Tile.Core.Simulation;

/// <summary>
/// 行为组类型，用于描述一组 SelectMove 的解题意图。
/// </summary>
public enum BehaviourKind
{
    /// <summary>
    /// 简单消除行为。
    /// </summary>
    EasyClear = 0,

    /// <summary>
    /// 困难消除行为。
    /// </summary>
    HardClear = 1,

    /// <summary>
    /// 广义消除行为。
    /// </summary>
    GeneralClear = 2,

    /// <summary>
    /// 翻盘行为。
    /// </summary>
    Flip = 3,
}

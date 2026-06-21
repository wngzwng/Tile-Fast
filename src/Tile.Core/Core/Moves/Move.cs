using Tile.Core.Core;

namespace Tile.Core.Core.Moves;

/// <summary>
/// 表示一个可执行、可撤销的操作抽象。
/// Move 负责描述：
/// 1. 会影响哪些 Tile；
/// 2. 当前是否允许执行；
/// 3. 如何执行；
/// 4. 如何撤销。
/// </summary>
public abstract class Move
{
    /// <summary>
    /// 返回该操作涉及的 Tile 编号。
    /// </summary>
    public int TileIndex { get; init; }

    /// <summary>
    /// 判断该操作当前是否可以在指定关卡上执行。
    /// </summary>
    public abstract bool CanDo(LevelCore level);

    /// <summary>
    /// 在指定关卡上执行该操作。
    /// 调用方通常应先调用 <see cref="CanDo"/> 做前置检查。
    /// </summary>
    public abstract void Do(LevelCore level);

    /// <summary>
    /// 撤销该操作对指定关卡产生的影响。
    /// 约定撤销后应尽量恢复到执行前状态。
    /// </summary>
    public abstract void Undo(LevelCore level);
}

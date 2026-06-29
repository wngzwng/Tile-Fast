namespace Tile.Core.Simulation;

/// <summary>
/// 关卡的一次模拟状态。Status 是过程/结果状态，会随模拟推进而变化。
/// </summary>
public enum LevelRunStatus
{
    /// <summary>
    /// 本次关卡模拟已创建或正在执行，尚未成功或失败。
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 本次关卡模拟已完成，关卡被清空。
    /// </summary>
    Success = 1,

    /// <summary>
    /// 本次关卡模拟已终止，无法继续有效执行。
    /// </summary>
    Failure = 2,
}

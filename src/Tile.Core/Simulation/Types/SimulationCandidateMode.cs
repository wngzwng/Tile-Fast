namespace Tile.Core.Simulation;

/// <summary>
/// Simulation 生成候选行为的模式。Mode 是输入配置，单次 run 中不随结果变化。
/// </summary>
public enum SimulationCandidateMode
{
    /// <summary>
    /// 直接从当前可选 tile 集合中选择下一步。
    /// </summary>
    Tile = 0,

    /// <summary>
    /// 从行为组集合中选择下一步。
    /// </summary>
    Behaviour = 1,
}

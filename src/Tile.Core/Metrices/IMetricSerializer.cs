namespace Tile.Core.Metrices;

/// <summary>
/// 指标序列化器，负责把 MetricBag 输出为字典。
/// </summary>
public interface IMetricSerializer
{
    /// <summary>
    /// 把 <paramref name="bag"/> 中由实现方关心的指标写入 <paramref name="output"/>。
    /// </summary>
    void Serialize(
        MetricBag bag,
        Dictionary<string, object?> output);
}

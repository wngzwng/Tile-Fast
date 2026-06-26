namespace Tile.Core.Metrices;

/// <summary>
/// 表示一个强类型指标 Key。
/// </summary>
/// <typeparam name="TValue">指标值类型。</typeparam>
public readonly struct MetricKey<TValue> : IMetricKey
{
    public MetricKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Metric key name cannot be empty.", nameof(name));

        Name = name;
    }

    /// <summary>
    /// 指标名称，通常对应 CSV 列名。
    /// </summary>
    public string Name { get; }

    public Type ValueType => typeof(TValue);

    public override string ToString()
    {
        return Name;
    }
}

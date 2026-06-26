namespace Tile.Core.Metrices;

public interface IMetricKey
{
    string Name { get; }

    Type ValueType { get; }
}

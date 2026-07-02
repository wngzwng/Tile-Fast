namespace Tile.Core.Simulation;

public delegate Behaviour BehaviourFactory(
    BehaviourKind kind,
    int color,
    ReadOnlySpan<int> selectIds);

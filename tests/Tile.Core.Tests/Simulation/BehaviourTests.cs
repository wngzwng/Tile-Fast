using NUnit.Framework;
using Tile.Core.Core.Moves;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class BehaviourTests
{
    [Test]
    public void Rent_WithEmptySelectIds_ReturnsEmptyBehaviour()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 7);
        var behaviour = candidates.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: []);

        Assert.That(behaviour.Count, Is.Zero);
        Assert.That(behaviour.SelectIds.ToArray(), Is.Empty);
    }

    [Test]
    public void Count_ReturnsSelectIdsLength()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 7);
        var behaviour = candidates.Rent(
            BehaviourKind.HardClear,
            color: 7,
            selectIds: [1, 2, 3]);

        Assert.That(behaviour.Count, Is.EqualTo(3));
    }

    [Test]
    public void ToMoves_ReturnsSelectMovesInOrder()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 7);
        var behaviour = candidates.Rent(
            BehaviourKind.GeneralClear,
            color: 7,
            selectIds: [3, 1, 4]);

        var moves = behaviour.ToMoves().Cast<SelectMove>().ToArray();

        Assert.That(
            moves.Select(move => move.TileIndex),
            Is.EqualTo(new[] { 3, 1, 4 }));
    }

    [Test]
    public void ToString_ReturnsStableSummary()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 7);
        var behaviour = candidates.Rent(
            BehaviourKind.Flip,
            color: 5,
            selectIds: [2, 8]);

        Assert.That(
            behaviour.ToString(),
            Is.EqualTo("[Flip] Color=5 | Select=[2,8]"));
    }

    [Test]
    public void Clear_ReusesReturnedBehaviourObject()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 7);
        var first = candidates.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]);
        candidates.Add(first);

        candidates.Clear();

        var second = candidates.Rent(
            BehaviourKind.HardClear,
            color: 2,
            selectIds: [2, 3]);

        Assert.That(ReferenceEquals(first, second), Is.True);
        Assert.That(second.Kind, Is.EqualTo(BehaviourKind.HardClear));
        Assert.That(second.Color, Is.EqualTo(2));
        Assert.That(second.SelectIds.ToArray(), Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public void Clear_InvalidatesReturnedBehaviour()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 7);
        var behaviour = candidates.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]);
        candidates.Add(behaviour);

        candidates.Clear();

        Assert.Throws<ObjectDisposedException>(() => _ = behaviour.Count);
    }

    [Test]
    public void Rent_WhenSelectIdsExceedDefaultCapacity_ExpandsBehaviourBuffer()
    {
        using var candidates = new BehaviourCandidateSet(defaultSelectCapacity: 1);
        var behaviour = candidates.Rent(
            BehaviourKind.HardClear,
            color: 2,
            selectIds: [4, 5, 6]);

        Assert.That(behaviour.SelectIds.ToArray(), Is.EqualTo(new[] { 4, 5, 6 }));
    }
}

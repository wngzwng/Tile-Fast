using NUnit.Framework;
using Tile.Core.Core.Moves;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class BehaviourTests
{
    [Test]
    public void Rent_WithEmptySelectIds_ReturnsEmptyBehaviour()
    {
        using var pool = new BehaviourPool();
        using var behaviour = pool.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: []);

        Assert.That(behaviour.Count, Is.Zero);
        Assert.That(behaviour.SelectIds.ToArray(), Is.Empty);
    }

    [Test]
    public void Count_ReturnsSelectIdsLength()
    {
        using var pool = new BehaviourPool();
        using var behaviour = pool.Rent(
            BehaviourKind.HardClear,
            color: 7,
            selectIds: [1, 2, 3]);

        Assert.That(behaviour.Count, Is.EqualTo(3));
    }

    [Test]
    public void ToMoves_ReturnsSelectMovesInOrder()
    {
        using var pool = new BehaviourPool();
        using var behaviour = pool.Rent(
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
        using var pool = new BehaviourPool();
        using var behaviour = pool.Rent(
            BehaviourKind.Flip,
            color: 5,
            selectIds: [2, 8]);

        Assert.That(
            behaviour.ToString(),
            Is.EqualTo("[Flip] Color=5 | Select=[2,8]"));
    }

    [Test]
    public void Rent_ReusesReturnedBehaviourObject()
    {
        using var pool = new BehaviourPool();
        var first = pool.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]);

        first.Dispose();

        using var second = pool.Rent(
            BehaviourKind.HardClear,
            color: 2,
            selectIds: [2, 3]);

        Assert.That(ReferenceEquals(first, second), Is.True);
        Assert.That(second.Kind, Is.EqualTo(BehaviourKind.HardClear));
        Assert.That(second.Color, Is.EqualTo(2));
        Assert.That(second.SelectIds.ToArray(), Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public void Rent_WhenPoolIsDisposed_Throws()
    {
        var pool = new BehaviourPool();
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            pool.Rent(
                BehaviourKind.EasyClear,
                color: 1,
                selectIds: [1]));
    }

    [Test]
    public void Dispose_WhenBorrowedBehaviourIsNotReturned_Throws()
    {
        var pool = new BehaviourPool();
        var behaviour = pool.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]);

        Assert.Throws<InvalidOperationException>(() => pool.Dispose());

        behaviour.Dispose();
        pool.Dispose();
    }

    [Test]
    public void Dispose_WhenBorrowedBehaviourIsReturned_DoesNotThrow()
    {
        var pool = new BehaviourPool();
        var behaviour = pool.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]);

        behaviour.Dispose();

        Assert.DoesNotThrow(() => pool.Dispose());
    }

    [Test]
    public void DisposeBehaviour_WhenReturnedTwice_DoesNotRecycleAgain()
    {
        using var pool = new BehaviourPool();
        var behaviour = pool.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]);

        behaviour.Dispose();

        Assert.DoesNotThrow(() => behaviour.Dispose());
    }
}

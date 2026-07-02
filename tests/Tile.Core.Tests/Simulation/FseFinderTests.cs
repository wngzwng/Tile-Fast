using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class FseFinderTests
{
    [Test]
    public void FindCandidates_WhenOriginSelectableCanClear_ReturnsEasyClear()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (3, 0, 0).PackXyz(),
                (6, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);
        using var destination = new BehaviourCollector();

        var count = new FseFinder().FindCandidates(level, destination.Rent, destination.Add);

        Assert.That(count, Is.EqualTo(1));
        var candidate = destination.Items.Single();
        Assert.That(candidate.Kind, Is.EqualTo(BehaviourKind.EasyClear));
        Assert.That(candidate.Color, Is.EqualTo(1));
        Assert.That(candidate.SelectIds.ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void FindCandidates_WhenVisibleSameSuitCanBeUnlocked_ReturnsHardClear()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (1, 0, 1).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);
        using var destination = new BehaviourCollector();

        var count = new FseFinder().FindCandidates(level, destination.Rent, destination.Add);

        Assert.That(count, Is.EqualTo(1));
        var candidate = destination.Items.Single();
        Assert.That(candidate.Kind, Is.EqualTo(BehaviourKind.HardClear));
        Assert.That(candidate.Color, Is.EqualTo(1));
        Assert.That(candidate.SelectIds.ToArray(), Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void FindCandidates_WhenSameSuitRequiresTwoExpansions_ReturnsHardClear()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (1, 0, 1).PackXyz(),
                (2, 0, 2).PackXyz()
            ],
            suits: [1, 1, 1]);
        using var destination = new BehaviourCollector();

        var count = new FseFinder().FindCandidates(level, destination.Rent, destination.Add);

        Assert.That(count, Is.EqualTo(1));
        var candidate = destination.Items.Single();
        Assert.That(candidate.Kind, Is.EqualTo(BehaviourKind.HardClear));
        Assert.That(candidate.Color, Is.EqualTo(1));
        Assert.That(candidate.SelectIds.ToArray(), Is.EqualTo(new[] { 2, 1, 0 }));
    }

    [Test]
    public void FindCandidates_WhenNoClearExists_ReturnsFlipCandidates()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (3, 0, 0).PackXyz()
            ],
            suits: [1, 2]);
        using var destination = new BehaviourCollector();

        var count = new FseFinder().FindCandidates(level, destination.Rent, destination.Add);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(
            destination.Items.Select(candidate => candidate.Kind),
            Is.All.EqualTo(BehaviourKind.Flip));
        Assert.That(
            destination.Items.SelectMany(candidate => candidate.SelectIds.ToArray()),
            Is.EqualTo(new[] { 0, 1 }));
    }

    private static LevelCore CreateLevel(
        int[] positions,
        int[] suits)
    {
        return new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.TripleTile,
            suits.AsSpan());
    }

    private sealed class BehaviourCollector : IDisposable
    {
        private readonly BehaviourCandidateSet _candidates = new(LevelRuleSpec.TripleTile.SlotCapacity);

        public IReadOnlyList<Behaviour> Items => _candidates.Items;

        public Behaviour Rent(
            BehaviourKind kind,
            int color,
            ReadOnlySpan<int> selectIds)
        {
            return _candidates.Rent(
                kind,
                color,
                selectIds);
        }

        public void Add(Behaviour behaviour)
        {
            _candidates.Add(behaviour);
        }

        public void Dispose()
        {
            _candidates.Dispose();
        }
    }
}

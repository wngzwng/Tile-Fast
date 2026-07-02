using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class BehaviourCandidateFinderTests
{
    [Test]
    public void FindCandidates_WhenOriginSelectableCanClear_BuildsEasyClear()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (3, 0, 0).PackXyz(),
                (6, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);
        var context = CreateContext(level);

        var count = BehaviourCandidateFinder.Instance.FindCandidates(context);

        Assert.That(count, Is.EqualTo(1));

        var behaviour = context.BehaviourCandidates.Items.Single();
        Assert.That(behaviour.Kind, Is.EqualTo(BehaviourKind.EasyClear));
        Assert.That(behaviour.Color, Is.EqualTo(1));
        Assert.That(behaviour.SelectIds.ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));

        context.BehaviourCandidates.Clear();
    }

    [Test]
    public void FindCandidates_WhenVisibleSameSuitCanBeUnlocked_BuildsHardClear()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (1, 0, 1).PackXyz(),
                (4, 0, 0).PackXyz()
            ],
            suits: [1, 1, 1]);
        var context = CreateContext(level);

        var count = BehaviourCandidateFinder.Instance.FindCandidates(context);

        Assert.That(count, Is.EqualTo(1));

        var behaviour = context.BehaviourCandidates.Items.Single();
        Assert.That(behaviour.Kind, Is.EqualTo(BehaviourKind.HardClear));
        Assert.That(behaviour.Color, Is.EqualTo(1));
        Assert.That(behaviour.SelectIds.ToArray(), Is.EqualTo(new[] { 1, 2, 0 }));

        context.BehaviourCandidates.Clear();
    }

    [Test]
    public void FindCandidates_WhenNoSuitCanClear_BuildsFlipForEachOriginSelectable()
    {
        var level = CreateLevel(
            positions:
            [
                (0, 0, 0).PackXyz(),
                (3, 0, 0).PackXyz()
            ],
            suits: [1, 2]);
        var context = CreateContext(level);

        var count = BehaviourCandidateFinder.Instance.FindCandidates(context);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(
            context.BehaviourCandidates.Items.Select(candidate => candidate.Kind),
            Is.All.EqualTo(BehaviourKind.Flip));
        Assert.That(
            context.BehaviourCandidates.Items.SelectMany(candidate => candidate.SelectIds.ToArray()),
            Is.EqualTo(new[] { 0, 1 }));

        context.BehaviourCandidates.Clear();
    }

    private static SimulationContext CreateContext(LevelCore level)
    {
        return new SimulationContext(
            level,
            simulationCount: 1,
            random: new Random(17),
            candidateMode: SimulationCandidateMode.Behaviour);
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
}

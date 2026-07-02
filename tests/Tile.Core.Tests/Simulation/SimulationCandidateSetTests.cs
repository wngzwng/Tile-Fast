using NUnit.Framework;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Simulation;

namespace Tile.Core.Tests.Simulation;

public sealed class SimulationCandidateSetTests
{
    [Test]
    public void SetSelected_WhenModeIsTile_ExposesSelectedTileIndex()
    {
        var candidates = new SimulationCandidateSet<int>(
            SimulationCandidateMode.Tile);

        candidates.Add(3);
        candidates.Add(7);
        candidates.Add(9);
        candidates.SetSelectedOffset(selectedOffset: 1);

        Assert.That(candidates.Count, Is.EqualTo(3));
        Assert.That(candidates.SelectedOffset, Is.EqualTo(1));
        Assert.That(candidates.SelectedItem, Is.EqualTo(7));
    }

    [Test]
    public void SetSelected_WhenModeIsBehaviour_ReturnsSelectedBehaviour()
    {
        using var candidates = new BehaviourCandidateSet();
        var behaviour = candidates.Rent(
            BehaviourKind.GeneralClear,
            color: 1,
            selectIds: [7]);

        candidates.Add(candidates.Rent(
            BehaviourKind.EasyClear,
            color: 1,
            selectIds: [1]));
        candidates.Add(behaviour);
        candidates.SetSelectedOffset(selectedOffset: 1);

        Assert.That(candidates.SelectedItem, Is.SameAs(behaviour));
    }

    [Test]
    public void Clear_RemovesCurrentSnapshotAndSelectedCandidate()
    {
        var candidates = new SimulationCandidateSet<int>(
            SimulationCandidateMode.Tile);

        candidates.Add(3);
        candidates.Add(7);
        candidates.SetSelectedOffset(selectedOffset: 1);

        candidates.Clear();

        Assert.That(candidates.Count, Is.Zero);
        Assert.That(candidates.SelectedOffset, Is.EqualTo(-1));
        Assert.That(candidates.TryGetSelectedItem(out _), Is.False);
    }

    [Test]
    public void SimulationContext_WhenModeIsBehaviour_CreatesOnlyBehaviourCandidateSet()
    {
        var context = new SimulationContext(
            CreateSingleMatchLevel(),
            simulationCount: 1,
            new Random(123),
            SimulationCandidateMode.Behaviour);

        Assert.That(context.CandidateMode, Is.EqualTo(SimulationCandidateMode.Behaviour));
        Assert.That(context.Candidates.Mode, Is.EqualTo(SimulationCandidateMode.Behaviour));
        Assert.That(context.BehaviourCandidates.Mode, Is.EqualTo(SimulationCandidateMode.Behaviour));
        Assert.Throws<InvalidOperationException>(() => _ = context.TileCandidates);
    }

    [Test]
    public void SimulationContext_WhenModeIsTile_CreatesOnlyTileCandidateSet()
    {
        var context = new SimulationContext(
            CreateSingleMatchLevel(),
            simulationCount: 1,
            new Random(123),
            SimulationCandidateMode.Tile);

        Assert.That(context.CandidateMode, Is.EqualTo(SimulationCandidateMode.Tile));
        Assert.That(context.Candidates.Mode, Is.EqualTo(SimulationCandidateMode.Tile));
        Assert.That(context.TileCandidates.Mode, Is.EqualTo(SimulationCandidateMode.Tile));
        Assert.Throws<InvalidOperationException>(() => _ = context.BehaviourCandidates);
    }

    [Test]
    public void SimulationContext_ResetBatch_WhenModeDoesNotChange_ReusesCandidateSet()
    {
        var context = new SimulationContext(
            CreateSingleMatchLevel(),
            simulationCount: 1,
            new Random(123),
            SimulationCandidateMode.Tile);
        var candidates = context.Candidates;
        context.TileCandidates.Add(1);
        context.TileCandidates.SetSelectedOffset(0);

        context.ResetBatch(
            CreateSingleMatchLevel(),
            simulationCount: 2,
            new Random(456),
            SimulationCandidateMode.Tile);

        Assert.That(context.Candidates, Is.SameAs(candidates));
        Assert.That(context.CandidateCount, Is.Zero);
        Assert.That(context.SelectedCandidateOffset, Is.EqualTo(-1));
    }

    [Test]
    public void SimulationContext_ResetBatch_WhenModeChanges_ReplacesCandidateSet()
    {
        var context = new SimulationContext(
            CreateSingleMatchLevel(),
            simulationCount: 1,
            new Random(123),
            SimulationCandidateMode.Tile);
        var candidates = context.Candidates;

        context.ResetBatch(
            CreateSingleMatchLevel(),
            simulationCount: 2,
            new Random(456),
            SimulationCandidateMode.Behaviour);

        Assert.That(context.Candidates, Is.Not.SameAs(candidates));
        Assert.That(context.CandidateMode, Is.EqualTo(SimulationCandidateMode.Behaviour));
        Assert.That(context.Candidates.Mode, Is.EqualTo(SimulationCandidateMode.Behaviour));
    }

    [Test]
    public void SimulationContext_ResetBatch_WhenLeavingBehaviourMode_ReturnsBorrowedBehaviours()
    {
        var level = CreateSingleMatchLevel();
        using var context = new SimulationContext(
            level,
            simulationCount: 1,
            new Random(123),
            SimulationCandidateMode.Behaviour);

        BehaviourCandidateFinder.Instance.FindCandidates(context);
        Assert.That(context.CandidateCount, Is.GreaterThan(0));

        Assert.DoesNotThrow(() =>
            context.ResetBatch(
                level,
                simulationCount: 1,
                new Random(456),
                SimulationCandidateMode.Tile));
    }

    private static LevelCore CreateSingleMatchLevel()
    {
        int[] positions =
        [
            (0, 0, 0).PackXyz(),
            (2, 0, 0).PackXyz(),
            (4, 0, 0).PackXyz()
        ];

        int[] suits = [1, 1, 1];

        return new LevelCore(
            positions.AsSpan(),
            LevelRuleSpec.TripleTile,
            suits.AsSpan());
    }
}

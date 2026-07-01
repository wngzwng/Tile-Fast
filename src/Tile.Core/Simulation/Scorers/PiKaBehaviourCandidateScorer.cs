using Tile.Core.Common.Math;

namespace Tile.Core.Simulation;

/// <summary>
/// PiKa 行为候选选择器。
/// 负责把单个行为评分器接入 Simulation 候选选择流程。
/// </summary>
public sealed class PiKaBehaviourCandidateScorer : ISimulationCandidateScorer
{
    private readonly BehaviourScorerForPiKa _behaviourScorer;
    private double _softMaxTemperature;

    public static PiKaBehaviourCandidateScorer Instance { get; } = new();

    public PiKaBehaviourCandidateScorer()
        : this(BehaviourScorerForPiKa.Instance)
    {
    }

    public PiKaBehaviourCandidateScorer(BehaviourScorerForPiKa behaviourScorer)
    {
        _behaviourScorer = behaviourScorer ?? throw new ArgumentNullException(nameof(behaviourScorer));
        _softMaxTemperature = 0.5d;
    }

    public SimulationCandidateMode CandidateMode => SimulationCandidateMode.Behaviour;

    public PiKaBehaviourCandidateScorer SetSoftMaxTemperature(double softMaxTemperature)
    {
        _softMaxTemperature = softMaxTemperature;
        return this;
    }

    public int SelectCandidateOffset(
        SimulationContext context,
        IReadOnlyList<int> candidates)
    {
        throw new NotSupportedException("This scorer only supports Behaviour candidates.");
    }

    public int SelectBehaviourCandidateOffset(
        SimulationContext context,
        IReadOnlyList<Behaviour> candidates)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));
        if (candidates is null)
            throw new ArgumentNullException(nameof(candidates));
        if (candidates.Count <= 0)
            throw new ArgumentOutOfRangeException(nameof(candidates));

        var scores = new double[candidates.Count];
        var weights = new double[candidates.Count];
        var level = context.SourceLevel;

        for (var offset = 0; offset < candidates.Count; offset++)
            scores[offset] = _behaviourScorer.EvaluateScore(level, candidates[offset]);

        MathKit.Softmax(scores, weights, temperature: _softMaxTemperature);

        var selectedOffset = MathKit.WeightedChoice(weights, context.Random);
        return selectedOffset >= 0
            ? selectedOffset
            : context.Random.Next(candidates.Count);
    }
}

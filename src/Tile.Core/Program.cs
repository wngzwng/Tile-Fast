using System.Diagnostics;
using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Simulation;

namespace Tile.Core;

internal static class Program
{
    private static void Main()
    {
        // string levelStr = "002,4,6,8.20,2,4,6,8,A.40,2,4,6,8,A.60,2,4,6,8,A.82,8.A0,2,8,A.C0,2,4,6,8,A.E2,4,6,8;112,4,6,8.30,2,4,6,8,A.50,3,7,A.72,8.92,8.C1,3,7,9.E3,7;222,4,6,8.40,3,7,A.82,8.C2,8;323,7:KaJAU594XMOFU7FX6aJ4PJYCGNA77OP99EOXX6CU7B3U95JBQE1YMHKaQFZNZDa3TZ1HTOIZIGFD";
        string levelStr = "002,6,A.22,4,6,8,A.40,2,6,A,C.62,4,6,8,A.80,2,4,6,8,A,C.A0,2,4,6,8,A,C.C0,2,4,6,8,A,C.E0,2,4,6,8,A,C.G0,2,4,6,8,A,C.I0,2,4,6,8,A,C;116.23,9.36.52,6,A.72,5,7,A.90,2,4,6,8,A,C.B0,2,4,6,8,A,C.D1,3,5,7,9,B.F1,3,5,7,9,B.H0,2,4,6,8,A,C;226.46.62,6,A.85,7.91,3,9,B.A6.B1,3,9,B.C5,7.D2,A.E5,7.G1,3,6,9,B;336.56.76.96.A2,A.C2,A.D5,7.F6.G2,A:8K9ZFM3XNS68Z4CCM9M6VCKLaNKBMBMaEO3FQB4PFBMMPHOW4MU11ZJOYKQVNWW5X2FRUCJ5PV7UWGIE2XAI67H5TQATZOXGQ7HJ5GV2UARYSH7PELTRYYLT2899LSE33aIJ61IG84RSN1aA";
        var levelCore = levelStr.ToLevel(LevelRuleSpec.PairClassic);
        PrintInitialFseCandidates(levelCore);

        var runner = new SimulationRunner(
            candidateFinder: BehaviourCandidateFinder.Instance

            );
        var random = new Random(20260626);
        var simulationCount = 1000;

        var stopwatch = Stopwatch.StartNew();
        var batchMetrics = runner.SimulateMany(
            levelCore,
            simulationCount: simulationCount,
            random);
        stopwatch.Stop();

        Console.WriteLine(levelCore.ToString(multiline: true));
        Console.WriteLine(batchMetrics);
        Console.WriteLine($"Simulation elapsed: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Simulation throughput: {simulationCount / stopwatch.Elapsed.TotalSeconds:F2} runs/s");
        Console.WriteLine($"Random next: {random.NextDouble()} ");
    }

    private static void PrintInitialFseCandidates(LevelCore levelCore)
    {
        using var context = new SimulationContext(
            levelCore,
            simulationCount: 1,
            random: new Random(20260626),
            candidateMode: SimulationCandidateMode.Behaviour);

        var candidateCount = BehaviourCandidateFinder.Instance.FindCandidates(context);
        var candidates = context.BehaviourCandidates.Items;
        var scorer = BehaviourScorerForPiKa.Instance;

        Console.WriteLine($"Initial FSE behaviour candidates: {candidateCount}");

        var scoredCandidates = candidates
            .Select((behaviour, offset) => new
            {
                Offset = offset,
                Behaviour = behaviour,
                Score = scorer.EvaluateScore(levelCore, behaviour)
            })
            .OrderBy(item => GetBehaviourKindSortOrder(item.Behaviour.Kind))
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.Behaviour.Color)
            .ToArray();

        foreach (var item in scoredCandidates)
        {
            var behaviour = item.Behaviour;

            Console.WriteLine(
                $"{item.Offset:D3} | Score={item.Score:F3} | Kind={behaviour.Kind} | Color={behaviour.Color} | Select=[{FormatSelectIds(behaviour)}]");
        }

        Console.WriteLine();
    }

    private static int GetBehaviourKindSortOrder(BehaviourKind kind)
    {
        return kind switch
        {
            BehaviourKind.EasyClear => 0,
            BehaviourKind.HardClear => 1,
            BehaviourKind.Flip => 2,
            _ => 3,
        };
    }

    private static string FormatSelectIds(Behaviour behaviour)
    {
        var selectIds = behaviour.SelectIds;
        return selectIds.IsEmpty
            ? "-"
            : string.Join(",", selectIds.ToArray());
    }
}

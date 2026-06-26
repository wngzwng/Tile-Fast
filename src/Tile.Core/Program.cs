using Tile.Core.Core;
using Tile.Core.ExtensionTools;
using Tile.Core.Simulation;

namespace Tile.Core;

internal static class Program
{
    private static void Main()
    {
        string levelStr = "002,4,6,8.20,2,4,6,8,A.40,2,4,6,8,A.60,2,4,6,8,A.82,8.A0,2,8,A.C0,2,4,6,8,A.E2,4,6,8;112,4,6,8.30,2,4,6,8,A.50,3,7,A.72,8.92,8.C1,3,7,9.E3,7;222,4,6,8.40,3,7,A.82,8.C2,8;323,7:KaJAU594XMOFU7FX6aJ4PJYCGNA77OP99EOXX6CU7B3U95JBQE1YMHKaQFZNZDa3TZ1HTOIZIGFD";
        var levelCore = levelStr.ToLevel(LevelRuleSpec.PairClassic);
        var runner = new SimulationRunner();
        var random = new Random(20260626);

        var runMetrics = runner.SimulateOne(levelCore, random);
        var batchMetrics = runner.SimulateMany(
            levelCore,
            simulationCount: 1000,
            random);

        Console.WriteLine(levelCore.ToString(multiline: true));
        Console.WriteLine(runMetrics);
        Console.WriteLine(batchMetrics);
    }
}

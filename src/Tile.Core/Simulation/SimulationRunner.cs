using Tile.Core.Core;
using Tile.Core.Core.Moves;

namespace Tile.Core.Simulation;

public sealed class SimulationRunner
{
    public SimulationRunMetrics SimulateOne(
        LevelCore level,
        Random? random = null)
    {
        if (level is null)
            throw new ArgumentNullException(nameof(level));

        random ??= Random.Shared;

        var working = level.Clone();
        var moveCount = 0;
        var selectableBuffer = new int[working.Mapping.TileCount];

        while (true)
        {
            if (working.Pasture.IsEmpty)
            {
                return new SimulationRunMetrics(
                    IsFailed: false,
                    FailPosition: -1);
            }

            if (working.StagingArea.IsFull)
            {
                return new SimulationRunMetrics(
                    IsFailed: true,
                    FailPosition: moveCount);
            }

            var selectableCount = working.Pasture.SelectableTiles.CopyTo(selectableBuffer);

            if (selectableCount == 0)
            {
                return new SimulationRunMetrics(
                    IsFailed: true,
                    FailPosition: moveCount);
            }

            var selectedOffset = random.Next(selectableCount);
            var tileIndex = selectableBuffer[selectedOffset];

            working.DoMove(new SelectMove(tileIndex));
            moveCount++;
        }
    }

    public SimulationBatchMetrics SimulateMany(
        LevelCore level,
        int simulationCount,
        Random? random = null)
    {
        if (level is null)
            throw new ArgumentNullException(nameof(level));

        if (simulationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(simulationCount), "Simulation count must be greater than 0.");

        random ??= Random.Shared;

        var successCount = 0;
        var failureCount = 0;
        var failPositionSum = 0;

        for (var i = 0; i < simulationCount; i++)
        {
            var runMetrics = SimulateOne(level, random);

            if (runMetrics.IsFailed)
            {
                failureCount++;
                failPositionSum += runMetrics.FailPosition;
            }
            else
            {
                successCount++;
            }
        }

        var failureRate = (double)failureCount / simulationCount;
        var averageFailPosition = failureCount == 0
            ? -1.0
            : (double)failPositionSum / failureCount;

        return new SimulationBatchMetrics(
            TotalCount: simulationCount,
            SuccessCount: successCount,
            FailureCount: failureCount,
            FailureRate: failureRate,
            AverageFailPosition: averageFailPosition);
    }
}

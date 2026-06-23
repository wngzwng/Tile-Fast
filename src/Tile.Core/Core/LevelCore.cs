using Tile.Core.Core.Mapping;
using Tile.Core.Core.Moves;
using Tile.Core.Core.Zones;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Core;

public sealed class LevelCore
{
    public LevelRuleSpec RuleSpec { get; }

    public TileMappingTable Mapping { get; }

    public Pasture Pasture { get; }

    public StagingArea StagingArea { get; }

    public Corral Corral { get; }

    private Move[] _historyMoves;
    private int _historyIndex = 0;

    public LevelCore(ReadOnlySpan<int> positons, LevelRuleSpec ruleSpec, ReadOnlySpan<int> sutis = default)
    {
        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        var tileCount = positons.Length;
        if (!sutis.IsEmpty && sutis.Length != tileCount)
            throw new ArgumentException("If provided, sutis length must match positons length.", nameof(sutis));

        RuleSpec = ruleSpec;
        int maxCol = 0, maxRow = 0, maxLayer = 0;
        var tiles = new Tile[tileCount];
        for (var i = 0; i < tileCount; i++)
        {
            var index = i;
            var position = positons[i];
            var suit = sutis.IsEmpty ? Tile.SuitUnspecified : sutis[i];

            tiles[i] = new Tile(index, position);
            tiles[i].SetSuit(suit);

            var (row, col, Layer) = position.UnpackRCZ();
            maxRow = Math.Max(row, maxRow);
            maxCol = Math.Max(col, maxRow);
            maxLayer = Math.Max(Layer, maxLayer);
        }

        var (dRow, dCol, dLayer) = Tile.DefaultVolume.UnpackRCZ();
        maxRow += dRow;
        maxCol += dCol;
        maxLayer += dLayer;     

        Mapping = TileMappingTable.Create(
            tiles,
            RuleSpec.LockRuleType,
            maxCol,
            maxRow,
            maxLayer);
        Pasture = new Pasture(Mapping, RuleSpec);
        StagingArea = new StagingArea(
            Mapping,
            RuleSpec.MatchRequireCount,
            RuleSpec.SlotCapacity);
        Corral = new Corral(Mapping.TileCount);
        _historyMoves = new Move[Mapping.TileCount];
    }


    public void DoMove(Move move)
    {
        if (!move.CanDo(this))
        {
            throw new InvalidOperationException($"{nameof(DoMove)}: 移动不合法");
        }

        move.Do(this);
        _historyMoves[_historyIndex++] = move;

    }

    public void UnDoMove()
    {
        if (_historyIndex < 0)
        {
            return;
        }

        var lastMove = _historyMoves[_historyIndex];
        if (!lastMove.CanDo(this))
        {
            throw new InvalidOperationException($"{nameof(UnDoMove)}: 移动不合法");
        }

        lastMove.Do(this);
        _historyIndex--;
    }

    private LevelCore(
        LevelRuleSpec ruleSpec,
        TileMappingTable mapping,
        Pasture pasture,
        StagingArea stagingArea,
        Corral corral,
        Move[] historyMoves,
        int historyIndex
        )
    {
        RuleSpec = ruleSpec ?? throw new ArgumentNullException(nameof(ruleSpec));
        Mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        Pasture = pasture ?? throw new ArgumentNullException(nameof(pasture));
        StagingArea = stagingArea ?? throw new ArgumentNullException(nameof(stagingArea));
        Corral = corral ?? throw new ArgumentNullException(nameof(corral));

        _historyMoves = historyMoves;
        _historyIndex = historyIndex;
    }

    public LevelCore Clone()
    {
        return new LevelCore(
            RuleSpec,
            Mapping,
            Pasture.Clone(),
            StagingArea.Clone(),
            Corral.Clone(),
            (Move[])_historyMoves.Clone(),
            _historyIndex
            );
    }

    public override string ToString()
    {
        return $"LevelCore(" +
               $"Tiles={Mapping.TileCount}, " +
               $"Size={Mapping.MaxCol}x{Mapping.MaxRow}x{Mapping.MaxLayer}, " +
               $"Rule=match{RuleSpec.MatchRequireCount}/slot{RuleSpec.SlotCapacity}/{RuleSpec.LockRuleType}, " +
               $"Pasture={Pasture.PresentTiles.Count()} present, {Pasture.VisibleTiles.Count()} visible, {Pasture.SelectableTiles.Count()} selectable, " +
               $"Staging={StagingArea.UsedCapacity}/{StagingArea.Capacity}, " +
               $"Corral={Corral.Count})";
    }
}

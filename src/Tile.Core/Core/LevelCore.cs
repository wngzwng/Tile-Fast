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

    private readonly Move?[] _historyMoves;
    private int _historyIndex;

    public LevelCore(ReadOnlySpan<int> positions, LevelRuleSpec ruleSpec, ReadOnlySpan<int> suits = default)
    {
        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        var tileCount = positions.Length;
        if (!suits.IsEmpty && suits.Length != tileCount)
            throw new ArgumentException("If provided, suits length must match positions length.", nameof(suits));

        RuleSpec = ruleSpec;
        int maxCol = 0, maxRow = 0, maxLayer = 0;
        var tiles = new Tile[tileCount];
        for (var i = 0; i < tileCount; i++)
        {
            var index = i;
            var position = positions[i];
            var suit = suits.IsEmpty ? Tile.SuitUnspecified : suits[i];

            tiles[i] = new Tile(index, position);
            tiles[i].SetSuit(suit);

            var (row, col, Layer) = position.UnpackRCZ();
            maxRow = Math.Max(row, maxRow);
            maxCol = Math.Max(col, maxCol);
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
        _historyMoves = new Move?[Mapping.TileCount];
    }

    public static LevelCore Create(
        ReadOnlySpan<int> positions,
        LevelRuleSpec ruleSpec,
        ReadOnlySpan<int> suits = default)
    {
        return new LevelCore(positions, ruleSpec, suits);
    }

    /// <summary>
    /// 执行完整 Move，并记录到撤销历史。
    /// 组件级 API 仍保持公开；直接写组件状态时，调用方负责维护跨组件一致性。
    /// </summary>
    public void DoMove(Move move)
    {
        if (move is null)
            throw new ArgumentNullException(nameof(move));

        if (!move.CanDo(this))
            throw new InvalidOperationException($"无法执行移动：{move}。");

        if (_historyIndex >= _historyMoves.Length)
            throw new InvalidOperationException("移动历史已满，无法继续执行移动。");

        move.Do(this);
        _historyMoves[_historyIndex++] = move;
    }

    public void UndoMove()
    {
        if (_historyIndex <= 0)
            throw new InvalidOperationException("没有可撤销的移动。");

        var undoIndex = _historyIndex - 1;
        var lastMove = _historyMoves[undoIndex];
        if (lastMove is null)
            throw new InvalidOperationException("移动历史状态异常，无法撤销。");

        lastMove.Undo(this);
        _historyMoves[undoIndex] = null;
        _historyIndex = undoIndex;
    }

    public void UnDoMove()
    {
        UndoMove();
    }

    private LevelCore(
        LevelRuleSpec ruleSpec,
        TileMappingTable mapping,
        Pasture pasture,
        StagingArea stagingArea,
        Corral corral,
        Move?[] historyMoves,
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
            (Move?[])_historyMoves.Clone(),
            _historyIndex
            );
    }

    public override string ToString()
    {
        return ToString(multiline: false);
    }

    public string ToString(bool multiline)
    {
        if (multiline)
            return ToMultilineString();

        return ToSingleLineString();
    }

    private string ToSingleLineString()
    {
        return $"LevelCore(" +
               $"Tiles={Mapping.TileCount}, " +
               $"Size(rcz)={Mapping.MaxRow}x{Mapping.MaxCol}x{Mapping.MaxLayer}, " +
               $"Rule=match{RuleSpec.MatchRequireCount}/slot{RuleSpec.SlotCapacity}/{RuleSpec.LockRuleType}, " +
               $"Pasture={Pasture}, " +
               $"StagingArea={StagingArea}, " +
               $"Corral={Corral.ToString(RuleSpec.MatchRequireCount)})";
    }

    private string ToMultilineString()
    {
        return "LevelCore(\n" +
               $"  Tiles={Mapping.TileCount},\n" +
               $"  Size(rcz)={Mapping.MaxRow}x{Mapping.MaxCol}x{Mapping.MaxLayer},\n" +
               $"  Rule=match{RuleSpec.MatchRequireCount}/slot{RuleSpec.SlotCapacity}/{RuleSpec.LockRuleType},\n" +
               $"  Pasture={Pasture},\n" +
               $"  StagingArea={StagingArea},\n" +
               $"  Corral={Corral.ToString(RuleSpec.MatchRequireCount)}\n" +
               ")";
    }
}

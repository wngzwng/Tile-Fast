using System.Text;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Moves;
using Tile.Core.Core.Zones;
using Tile.Core.Core.Utils;

namespace Tile.Core.Core;

public sealed class LevelCore
{
    private static class TextProtocol
    {
        public const char LayerSeparator = ';';
        public const char RowSeparator = '.';
        public const char ColumnSeparator = ',';
        public const char SuitSeparator = ':';
    }

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

            var (row, col, Layer) = PositionPacker.UnpackRcz(position);
            maxRow = Math.Max(row, maxRow);
            maxCol = Math.Max(col, maxCol);
            maxLayer = Math.Max(Layer, maxLayer);
        }

        var (dRow, dCol, dLayer) = PositionPacker.UnpackRcz(Tile.DefaultVolume);
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

    /// <summary>
    /// 解析关卡字符串得到 LevelCore。
    ///
    /// 字符串格式：
    /// <c>position[:suit]</c>
    ///
    /// position 格式：
    /// <c>zrc[,c][.rc[,c]][;zrc[,c][.rc[,c]]]</c>
    /// </summary>
    public static LevelCore Deserialize(
        string str,
        LevelRuleSpec ruleSpec)
    {
        #region 归一化输入

        if (str is null)
            throw new ArgumentNullException(nameof(str));

        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        var normalized = str.Trim('\"', '\n', '\r', '\t', ' ', '●', '○');
        var segments = normalized.Split(TextProtocol.SuitSeparator);

        if (segments.Length > 2)
            throw new ArgumentException("关卡字符串不合法，':' 分割的段超过 2 段。", nameof(str));

        #endregion

        #region 解析位置

        List<int> positions = [];
        HashSet<int> positionSet = [];
        var boardStr = segments[0];

        if (!string.IsNullOrEmpty(boardStr))
        {
            var layerStrings = boardStr.Split(
                TextProtocol.LayerSeparator,
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var layerStr in layerStrings)
            {
                if (layerStr.Length < 3)
                    throw new InvalidOperationException("关卡字符串不合法，层信息不完整。");

                var layer = Base62CharCodec.CharToIndex(layerStr[0]);
                var rowsPart = layerStr[1..];
                var rowStrings = rowsPart.Split(
                    TextProtocol.RowSeparator,
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var rowStr in rowStrings)
                {
                    if (rowStr.Length < 2)
                        throw new InvalidOperationException("关卡字符串不合法，行信息不完整。");

                    var row = Base62CharCodec.CharToIndex(rowStr[0]);
                    var columnsPart = rowStr[1..];

                    if (string.IsNullOrEmpty(columnsPart))
                        throw new InvalidOperationException("关卡字符串不合法，列信息不能为空。");

                    if (columnsPart[0] == TextProtocol.ColumnSeparator)
                        throw new InvalidOperationException("关卡字符串不合法，列信息不能以 ',' 开头。");

                    if (columnsPart[^1] == TextProtocol.ColumnSeparator)
                        throw new InvalidOperationException("关卡字符串不合法，列信息不能以 ',' 结尾。");

                    var compactColumnCount = 0;

                    for (var i = 0; i < columnsPart.Length; i++)
                    {
                        var ch = columnsPart[i];

                        if (ch == TextProtocol.ColumnSeparator)
                        {
                            if (i + 1 >= columnsPart.Length || columnsPart[i + 1] == TextProtocol.ColumnSeparator)
                                throw new InvalidOperationException("关卡字符串不合法，列分隔格式错误。");

                            continue;
                        }

                        _ = Base62CharCodec.CharToIndex(ch);
                        compactColumnCount++;
                    }

                    if (compactColumnCount * 2 - 1 != columnsPart.Length)
                        throw new InvalidOperationException("关卡字符串不合法，列分隔格式错误。");

                    foreach (var columnChar in columnsPart)
                    {
                        if (columnChar == TextProtocol.ColumnSeparator)
                            continue;

                        var column = Base62CharCodec.CharToIndex(columnChar);
                        var position = PositionPacker.PackXyz(column, row, layer);

                        if (!positionSet.Add(position))
                        {
                            throw new InvalidOperationException(
                                $"关卡字符串不合法，棋子位置重复：column={column}, row={row}, layer={layer}。");
                        }

                        positions.Add(position);
                    }
                }
            }
        }

        #endregion

        #region 解析花色

        var tileCount = positions.Count;
        var suits = new int[tileCount];

        Array.Fill(suits, Tile.SuitUnspecified);

        if (segments.Length >= 2 && !string.IsNullOrEmpty(segments[1]) && segments[1].Length == tileCount)
        {
            var suitStr = segments[1];

            for (var i = 0; i < suitStr.Length; i++)
                suits[i] = Base62CharCodec.CharToIndex(suitStr[i]);
        }

        #endregion

        #region 构建关卡

        var positionArray = positions.ToArray();

        return new LevelCore(
            positionArray.AsSpan(),
            ruleSpec,
            suits.AsSpan());

        #endregion
    }

    /// <summary>
    /// 序列化 LevelCore。
    /// 输出顺序固定为：
    /// layer asc -> row asc -> column asc。
    /// </summary>
    public string Serialize()
    {
        #region 收集并排序棋子

        var present = Pasture.PresentTiles;
        var tileCount = present.Count();

        var positions = new int[tileCount];
        var suits = new int[tileCount];

        var write = 0;

        foreach (var tileIndex in present)
        {
            positions[write] = Mapping.GetPosition(tileIndex);
            suits[write] = Mapping.GetSuit(tileIndex);
            write++;
        }

        Array.Sort(
            positions,
            suits,
            Comparer<int>.Create((left, right) =>
            {
                var layerCompare = PositionPacker.Z(left).CompareTo(PositionPacker.Z(right));

                if (layerCompare != 0)
                    return layerCompare;

                var rowCompare = PositionPacker.Y(left).CompareTo(PositionPacker.Y(right));

                if (rowCompare != 0)
                    return rowCompare;

                return PositionPacker.X(left).CompareTo(PositionPacker.X(right));
            }));

        var builder = new StringBuilder(tileCount * 6 + 1 + tileCount);

        #endregion

        #region 写入位置

        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];

            var column = PositionPacker.X(position);
            var row = PositionPacker.Y(position);
            var layer = PositionPacker.Z(position);

            if (i == 0)
            {
                builder.Append(Base62CharCodec.GetCharFromInt(layer));
                builder.Append(Base62CharCodec.GetCharFromInt(row));
                builder.Append(Base62CharCodec.GetCharFromInt(column));
                continue;
            }

            var previous = positions[i - 1];

            var previousColumn = PositionPacker.X(previous);
            var previousRow = PositionPacker.Y(previous);
            var previousLayer = PositionPacker.Z(previous);

            if (layer != previousLayer)
            {
                builder.Append(TextProtocol.LayerSeparator);
                builder.Append(Base62CharCodec.GetCharFromInt(layer));
                builder.Append(Base62CharCodec.GetCharFromInt(row));
                builder.Append(Base62CharCodec.GetCharFromInt(column));
            }
            else if (row != previousRow)
            {
                builder.Append(TextProtocol.RowSeparator);
                builder.Append(Base62CharCodec.GetCharFromInt(row));
                builder.Append(Base62CharCodec.GetCharFromInt(column));
            }
            else if (column != previousColumn)
            {
                builder.Append(TextProtocol.ColumnSeparator);
                builder.Append(Base62CharCodec.GetCharFromInt(column));
            }
        }

        #endregion

        #region 写入花色

        builder.Append(TextProtocol.SuitSeparator);

        foreach (var suit in suits)
            builder.Append(Base62CharCodec.GetCharFromInt(suit));

        return builder.ToString();

        #endregion
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

    /// <summary>
    /// 判断当前关卡是否已经完成：牧场与卡槽均为空，且所有棋子已进入回收区。
    /// </summary>
    public bool IsFinish()
    {
        return Pasture.IsEmpty &&
               StagingArea.IsEmpty &&
               Corral.Count == Mapping.TileCount;
    }

    public void Reset()
    {
        Pasture.Reset();
        StagingArea.Reset();
        Corral.Reset();

        Array.Clear(_historyMoves);
        _historyIndex = 0;
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

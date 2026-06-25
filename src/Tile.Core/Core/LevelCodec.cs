using System.Text;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Core;

public static class LevelCodec
{
    private const char LayerSeparator = ';';
    private const char RowSeparator = '.';
    private const char ColumnSeparator = ',';
    private const char SuitSeparator = ':';

    /// <summary>
    /// 解析关卡字符串得到 LevelCore。
    ///
    /// 字符串格式：
    /// <c>position[:suit]</c>
    ///
    /// position 格式：
    /// <c>zrc[,c][.rc[,c]][;zrc[,c][.rc[,c]]]</c>
    ///
    /// 其中：
    /// <list type="bullet">
    /// <item><description><c>z</c> 表示 layer</description></item>
    /// <item><description><c>r</c> 表示 row</description></item>
    /// <item><description><c>c</c> 表示 column</description></item>
    /// </list>
    ///
    /// 分隔符：
    /// <list type="bullet">
    /// <item><description><c>;</c> 分隔 layer</description></item>
    /// <item><description><c>.</c> 分隔 row</description></item>
    /// <item><description><c>,</c> 分隔同一 row 下的 column</description></item>
    /// <item><description><c>:</c> 分隔 position 段和 suit 段</description></item>
    /// </list>
    ///
    /// 示例：
    /// <c>001,2.102;211:abcde</c>
    /// </summary>
    public static LevelCore Deserialize(
        this string str,
        LevelRuleSpec ruleSpec)
    {
        if (str is null)
            throw new ArgumentNullException(nameof(str));

        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        var normalized = NormalizeLevelString(str);
        var segments = normalized.Split(SuitSeparator);

        if (segments.Length > 2)
            throw new ArgumentException("关卡字符串不合法，':' 分割的段超过 2 段。", nameof(str));

        var positions = ParsePositions(segments[0]);
        var suits = ParseSuits(segments, positions.Count);

        var positionArray = positions.ToArray();

        return LevelCore.Create(
            positionArray.AsSpan(),
            ruleSpec,
            suits.AsSpan());
    }

    public static bool TryDeserialize(
        this string str,
        LevelRuleSpec ruleSpec,
        out LevelCore? levelCore)
    {
        try
        {
            levelCore = Deserialize(str, ruleSpec);
            return true;
        }
        catch
        {
            levelCore = null;
            return false;
        }
    }

    /// <summary>
    /// 序列化 LevelCore。
    /// 输出顺序固定为：
    /// layer asc -> row asc -> column asc。
    /// </summary>
    public static string Serialize(this LevelCore level)
    {
        if (level is null)
            throw new ArgumentNullException(nameof(level));

        var mapping = level.Mapping;
        var present = level.Pasture.PresentTiles;
        var tileCount = present.Count();

        var positions = new int[tileCount];
        var suits = new int[tileCount];

        var write = 0;

        foreach (var tileIndex in present)
        {
            positions[write] = mapping.GetPosition(tileIndex);
            suits[write] = mapping.GetSuit(tileIndex);
            write++;
        }

        Array.Sort(
            positions,
            suits,
            Comparer<int>.Create(ComparePosition));

        var builder = new StringBuilder(tileCount * 6 + 1 + tileCount);

        SerializePositions(builder, positions);
        builder.Append(SuitSeparator);
        SerializeSuits(builder, suits);

        return builder.ToString();
    }

    private static string NormalizeLevelString(string str)
    {
        return str.Trim('\"', '\n', '\r', '\t', ' ', '●', '○');
    }

    private static List<int> ParsePositions(string boardStr)
    {
        if (string.IsNullOrEmpty(boardStr))
            return [];

        List<int> positions = [];
        HashSet<int> positionSet = [];

        var layerStrings = boardStr.Split(
            LayerSeparator,
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var layerStr in layerStrings)
        {
            // 最短 layer 信息形如：zrc
            if (layerStr.Length < 3)
                throw new InvalidOperationException("关卡字符串不合法，层信息不完整。");

            var layer = layerStr[0].CharToIndex();
            var rowsPart = layerStr[1..];

            var rowStrings = rowsPart.Split(
                RowSeparator,
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var rowStr in rowStrings)
            {
                // 最短 row 信息形如：rc
                if (rowStr.Length < 2)
                    throw new InvalidOperationException("关卡字符串不合法，行信息不完整。");

                var row = rowStr[0].CharToIndex();
                var columnsPart = rowStr[1..];

                ValidateColumnsPart(columnsPart);

                var compactColumns = columnsPart.Replace(
                    ColumnSeparator.ToString(),
                    string.Empty);

                foreach (var columnChar in compactColumns)
                {
                    var column = columnChar.CharToIndex();
                    var position = (column, row, layer).PackXyz();

                    if (!positionSet.Add(position))
                    {
                        throw new InvalidOperationException(
                            $"关卡字符串不合法，棋子位置重复：column={column}, row={row}, layer={layer}。");
                    }

                    positions.Add(position);
                }
            }
        }

        return positions;
    }

    private static void ValidateColumnsPart(string columnsPart)
    {
        if (string.IsNullOrEmpty(columnsPart))
            throw new InvalidOperationException("关卡字符串不合法，列信息不能为空。");

        if (columnsPart[0] == ColumnSeparator)
            throw new InvalidOperationException("关卡字符串不合法，列信息不能以 ',' 开头。");

        if (columnsPart[^1] == ColumnSeparator)
            throw new InvalidOperationException("关卡字符串不合法，列信息不能以 ',' 结尾。");

        for (var i = 0; i < columnsPart.Length; i++)
        {
            var ch = columnsPart[i];

            if (ch == ColumnSeparator)
            {
                if (i + 1 >= columnsPart.Length || columnsPart[i + 1] == ColumnSeparator)
                    throw new InvalidOperationException("关卡字符串不合法，列分隔格式错误。");

                continue;
            }

            _ = ch.CharToIndex();
        }

        var compactLength = columnsPart.Count(ch => ch != ColumnSeparator);

        // n 个 column 字符，合法格式长度必须是：
        // n + (n - 1) = 2n - 1
        //
        // 合法：
        // c
        // c,c
        // c,c,c
        //
        // 非法：
        // cc
        // c,,c
        // ,c
        // c,
        if (compactLength * 2 - 1 != columnsPart.Length)
            throw new InvalidOperationException("关卡字符串不合法，列分隔格式错误。");
    }

    private static int[] ParseSuits(
        string[] segments,
        int tileCount)
    {
        if (segments.Length < 2 || string.IsNullOrEmpty(segments[1]))
            return CreateDefaultSuits(tileCount);

        var suitStr = segments[1];

        // 保持旧逻辑：
        // suit 数量与 tile 数量不一致时，不报错，直接使用默认 suit。
        if (suitStr.Length != tileCount)
            return CreateDefaultSuits(tileCount);

        var suits = new int[tileCount];

        for (var i = 0; i < suitStr.Length; i++)
            suits[i] = suitStr[i].CharToIndex();

        return suits;
    }

    private static int[] CreateDefaultSuits(int tileCount)
    {
        var suits = new int[tileCount];

        Array.Fill(suits, Tile.SuitUnspecified);

        return suits;
    }

    private static void SerializePositions(
        StringBuilder builder,
        int[] positions)
    {
        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];

            var column = position.X();
            var row = position.Y();
            var layer = position.Z();

            if (i == 0)
            {
                builder.Append(layer.GetCharFromInt());
                builder.Append(row.GetCharFromInt());
                builder.Append(column.GetCharFromInt());
                continue;
            }

            var previous = positions[i - 1];

            var previousColumn = previous.X();
            var previousRow = previous.Y();
            var previousLayer = previous.Z();

            if (layer != previousLayer)
            {
                builder.Append(LayerSeparator);
                builder.Append(layer.GetCharFromInt());
                builder.Append(row.GetCharFromInt());
                builder.Append(column.GetCharFromInt());
            }
            else if (row != previousRow)
            {
                builder.Append(RowSeparator);
                builder.Append(row.GetCharFromInt());
                builder.Append(column.GetCharFromInt());
            }
            else if (column != previousColumn)
            {
                builder.Append(ColumnSeparator);
                builder.Append(column.GetCharFromInt());
            }
        }
    }

    private static void SerializeSuits(
        StringBuilder builder,
        int[] suits)
    {
        foreach (var suit in suits)
            builder.Append(suit.GetCharFromInt());
    }

    private static int ComparePosition(int left, int right)
    {
        var layerCompare = left.Z().CompareTo(right.Z());

        if (layerCompare != 0)
            return layerCompare;

        var rowCompare = left.Y().CompareTo(right.Y());

        if (rowCompare != 0)
            return rowCompare;

        return left.X().CompareTo(right.X());
    }
}

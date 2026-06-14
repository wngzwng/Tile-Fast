using System.Numerics;
using System.Text;
using Tile.Core.Core.Moves;
using Tile.Core.Core.Types;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Core;

/// <summary>
/// 表示关卡核心对象。
/// 当前先聚焦静态核心数据：
/// 1. Tile 本体数组；
/// 2. 花色到 TileIndex 的索引；
/// 3. Move 历史。
/// </summary>
public partial class LevelCore
{
    #region 关卡静态基础

    /// <summary>
    /// 当前关卡持有的全部 Tile。
    /// 约定按 Tile.Index 直接存放，也就是 <c>_tiles[i].Index == i</c>。
    /// </summary>
    protected readonly Tile[] _tiles;

    /// <summary>
    /// 花色到 TileIndex 数组的映射表。
    /// 例如：
    /// <c>_tileIndexArrayAtSuit[suit]</c> 表示该花色下全部 TileIndex。
    /// </summary>
    protected int[][] _tileIndexArrayAtSuit = [];

    /// <summary>
    /// 花色到 Tile 数量的映射表。
    /// 例如：
    /// <c>_tileCountAtSuit[suit]</c> 表示该花色下有多少张 Tile。
    /// </summary>
    protected int[] _tileCountAtSuit = [];

    /// <summary>
    /// 当前关卡中实际参与映射的花色数量。
    /// 这里表示“出现过的花色种类数”，不是最大花色编号。
    /// </summary>
    protected int _suitCount;

    /// <summary>
    /// 当前关卡中出现过的最大花色编号。
    /// 如果尚未初始化，可约定为 <c>-1</c>。
    /// </summary>
    protected int _maxSuitIndex = -1;

    /// <summary>
    /// 当前关卡的 Tile 总数。
    /// </summary>
    public int TotalCount => _tiles.Length;

    /// <summary>
    /// 返回当前关卡中实际参与映射的花色数量。
    /// </summary>
    public int SuitCount => _suitCount;

    /// <summary>
    /// 返回当前关卡中出现过的最大花色编号。
    /// 若为 <c>-1</c>，表示当前还没有可用花色数据。
    /// </summary>
    public int MaxSuitIndex => _maxSuitIndex;

    #endregion

    #region 稳定参数


  /// <summary>
    /// 触发一次配对 / 消除所需的数量。
    /// 当前三消模型下通常为 3。
    /// </summary>
    public int MatchRequireCount { get; protected init; }

    /// <summary>
    /// StagingArea 的容量上限。
    /// </summary>
    public int SlotCapacity { get; protected init; }


    /// <summary>
    /// 当前关卡使用的锁定规则类型。
    /// </summary>
    public LockRuleTypeEnum LockRuleType { get; protected init; }

    #endregion

    #region 历史移动

    /// <summary>
    /// Move 执行历史。
    /// 用于撤销、回放和调试。
    /// </summary>
    protected readonly List<Move> _historyMoves = [];

    /// <summary>
    /// 对外只读暴露 Move 历史。
    /// </summary>
    public IReadOnlyList<Move> HistoryMoves => _historyMoves;

    #endregion

    #region 查询方法

    /// <summary>
    /// 对外返回指定花色对应的全部 TileIndex。
    /// 如果花色越界，抛出异常；
    /// 如果花色合法但不存在 Tile，则返回空数组。
    /// </summary>
    public ReadOnlySpan<int> GetTileIndexesAtSuit(int suit)
    {
        if ((uint)suit >= (uint)_tileIndexArrayAtSuit.Length)
            throw new ArgumentOutOfRangeException(nameof(suit), $"suit 越界：suit={suit}。");

        return _tileIndexArrayAtSuit[suit];
    }

    /// <summary>
    /// 对外返回指定花色对应的 Tile 数量。
    /// 如果花色越界，抛出异常。
    /// </summary>
    public int GetTileCountAtSuit(int suit)
    {
        if ((uint)suit >= (uint)_tileCountAtSuit.Length)
            throw new ArgumentOutOfRangeException(nameof(suit), $"suit 越界：suit={suit}。");

        return _tileCountAtSuit[suit];
    }

    #endregion

    protected TileMappingTable _tileMappingTable = null!;

    public int ToIndex(int position)
    {
        return _tileMappingTable.GetTileIndexAtPosition(position);
    }


    private LevelCore(int matchRequireCount, int slotCapacity, LockRuleTypeEnum lockRuleType, Span<int> tileBitSpan)
    {
        MatchRequireCount = matchRequireCount;
        SlotCapacity = slotCapacity;
        LockRuleType = lockRuleType;
        
        _maxSuitIndex = -1;
        ulong suitIndexBit = 0;
        for (int i = 0; i < tileBitSpan.Length; i++)
        {
            // 统计花色分布
            int suit = tileBitSpan[i].GetTileSuitFromBit();
            suitIndexBit |= 1UL << suit;
            _tileCountAtSuit[suit]++;

            _maxSuitIndex = Math.Max(_maxSuitIndex, suit); 
        }
        _suitCount = BitOperations.PopCount(suitIndexBit);
    

        while(suitIndexBit != 0)
        {
            int suit = BitOperations.TrailingZeroCount(suitIndexBit);
            suitIndexBit &= suitIndexBit - 1;

            _tileIndexArrayAtSuit[suit] = new int[_tileCountAtSuit[suit]];
        }


        var recordTileCountAtSuitSpan = (stackalloc int[_maxSuitIndex + 1]);
        _tiles = new Tile[tileBitSpan.Length];
        for (int i = 0; i < tileBitSpan.Length; i++)        
        {
            _tiles[i] = tileBitSpan[i].ToTile(i);
            int suit = _tiles[i].Suit;

            var tileIndexArrayAtSuit = _tileIndexArrayAtSuit[suit];
            tileIndexArrayAtSuit[recordTileCountAtSuitSpan[suit]++] = i;
        }
        _tileMappingTable = new TileMappingTable(_tiles);

        // Pasture = new Pasture(this);
        // StagingArea = new StagingArea(this);
        // Corral = new Corral(this);

    }


    public string Serialize()
    {
        var builder = new StringBuilder(_tiles.Length * 4);

        const int Unspecified = -1;

        const char LayerSeparator = ';';
        const char RowSeparator = '.';
        const char ColumnSeparator = ',';
        const char SuitSeparator = ':';

        var lastLayer = Unspecified;
        var lastRow = Unspecified;

        // 位置
        for (var i = 0; i < _tiles.Length; i++)
        {
            var (row, col, layer) = _tiles[i].Position.UnpackRCZ();

            var isNewLayer = layer != lastLayer;
            var isNewRow = isNewLayer || row != lastRow;

            if (isNewLayer)
            {
                if (lastLayer != Unspecified)
                    builder.Append(LayerSeparator);

                builder.Append(((int)layer).GetCharFromInt());
                builder.Append(((int)row).GetCharFromInt());
                builder.Append(((int)col).GetCharFromInt());

                lastLayer = layer;
                lastRow = row;

                continue;
            }

            if (isNewRow)
            {
                builder.Append(RowSeparator);
                builder.Append(((int)row).GetCharFromInt());
                builder.Append(((int)col).GetCharFromInt());

                lastRow = row;

                continue;
            }

            builder.Append(ColumnSeparator);
            builder.Append(((int)col).GetCharFromInt());
        }

        // 花色
        if (_tiles.Length > 0 && _tiles[0].Suit != Tile.SuitUnspecified)
        {
            builder.Append(SuitSeparator);

            for (var i = 0; i < _tiles.Length; i++)
                builder.Append(_tiles[i].Suit.GetCharFromInt());
        }

        return builder.ToString();
    }

}

using Tile.Core.Common.BitSet;
using Tile.Core.Core.Mapping;
using Tile.Core.Core.Types;
using Tile.Core.ExtensionTools;

namespace Tile.Core.Core.Zones;

public sealed class Pasture
{
    private readonly TileMappingTable _mapping;
    private readonly LockRuleTypeEnum _lockRule;

    private readonly ulong[] _present;
    private readonly ulong[] _visible;
    private readonly ulong[] _selectable;

    public TileIndexSet PresentTiles => TileIndexSet.Wrap(_present);
    public TileIndexSet VisibleTiles => TileIndexSet.Wrap(_visible);
    public TileIndexSet SelectableTiles => TileIndexSet.Wrap(_selectable);

    public bool IsEmpty => PresentTiles.IsEmpty();

    public Pasture(
        TileMappingTable mapping,
        LevelRuleSpec ruleSpec)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        _lockRule = ruleSpec.LockRuleType;

        var wordCount = mapping.WordCount;

        _present = new ulong[wordCount];
        _visible = new ulong[wordCount];
        _selectable = new ulong[wordCount];

        Initialize();
    }

    public bool IsPresent(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return BitSetOperations.Get(_present, tileIndex);
    }

    public bool IsVisible(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return BitSetOperations.Get(_visible, tileIndex);
    }

    public bool IsSelectable(int tileIndex)
    {
        ValidateTileIndex(tileIndex);
        return BitSetOperations.Get(_selectable, tileIndex);
    }

    public void Remove(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (!IsPresent(tileIndex))
            throw new InvalidOperationException($"Tile {tileIndex} is not present.");

        BitSetOperations.Clear(_present, tileIndex);

        RefreshAfterRemove(tileIndex);
    }

    public void Add(int tileIndex)
    {
        ValidateTileIndex(tileIndex);

        if (IsPresent(tileIndex))
            throw new InvalidOperationException($"Tile {tileIndex} is already present.");

        BitSetOperations.Set(_present, tileIndex);
        RefreshAfterRemove(tileIndex);
    }

    private void RefreshAfterRemove(int tileIndex)
    {
        // 根据 _lockRule、_mapping、_present 刷新 visible / selectable。
        // 获取该棋子受影响的棋子
        Span<ulong> affectedTileBits = stackalloc ulong[_mapping.WordCount];
        _mapping.GetAffectedTileBits(tileIndex).CopyTo(affectedTileBits);
        // 只要目前在盘面的棋子
        affectedTileBits.AndWith(_present);

        // 受影响的棋子排除可选和可见，重新评估
        _selectable.AsSpan().AndNotWith(affectedTileBits);
        _visible.AsSpan().AndNotWith(affectedTileBits);


        foreach (int affectdTileIndex in TileIndexSet.Wrap(affectedTileBits))
        {
            var exposeArea = GetExposedArea(affectdTileIndex);
            switch (exposeArea)
            {
                case 0: break;
                case > 0 and < 4: BitSetOperations.Set(_visible, tileIndex); break;
                case 4: BitSetOperations.Set(_selectable, tileIndex); break;
                default: break;
            }
        }
    }


    private int GetExposedArea(int tileIndex)
    {
        // 收集上方依赖的棋子
        var tile = _mapping.GetTile(tileIndex);
        var (x, y, z) = tile.Position.UnpackXyz();
        var topZ = tile.TopZ;
        Span<int> positons = stackalloc int[]
        {
            (x + 0, y + 0, topZ).PackXyz(),
            (x + 1, y + 0, topZ).PackXyz(),
            (x + 0, y + 1, topZ).PackXyz(),
            (x + 1, y + 1, topZ).PackXyz(),
        };

        var exposeArea = 0;
        foreach(int position in positons)
        {
            for(int layer = topZ + 1; layer < _mapping.MaxLayer; layer++)
            {
                var nextPositon = position.WithZ(layer);

                if (_mapping.TryGetTileIndexAtPosition(nextPositon, out int nextTileIndex)
                && IsPresent(nextTileIndex)
                )
                {
                    exposeArea += 1;
                    break;
                }
            }
            
        }
        return exposeArea;
    }

    private void Initialize()
    {
        // 初始化 present / visible / selectable。
    }

    private void ValidateTileIndex(int tileIndex)
    {
        if ((uint)tileIndex >= (uint)_mapping.TileCount)
            throw new ArgumentOutOfRangeException(nameof(tileIndex));
    }

    private Pasture(
        TileMappingTable mapping,
        LockRuleTypeEnum lockRule,
        ulong[] present,
        ulong[] visible,
        ulong[] selectable)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _lockRule = lockRule;
        _present = present ?? throw new ArgumentNullException(nameof(present));
        _visible = visible ?? throw new ArgumentNullException(nameof(visible));
        _selectable = selectable ?? throw new ArgumentNullException(nameof(selectable));
    }


     public Pasture Clone()
    {
        return new Pasture(
            _mapping,
            _lockRule,
            (ulong[])_present.Clone(),
            (ulong[])_visible.Clone(),
            (ulong[])_selectable.Clone());
    }

}

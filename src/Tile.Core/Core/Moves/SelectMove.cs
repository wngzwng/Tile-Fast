namespace Tile.Core.Core.Moves;
public class SelectMove : Move
{

    private bool _matched = false;
    private int[]? _matchedTileIds = null;

    private SelectMove(int tileIndex)
    {
        TileIndex = tileIndex;
    }
    
    public override bool CanDo(LevelCore level)
    {
        return level.Pasture.IsSelectable(TileIndex);
    }

    public override void Do(LevelCore level)
    {
        _matched = false;
        _matchedTileIds = null;


        level.Pasture.Remove(TileIndex);
        level.StagingArea.Add(TileIndex);

        int suit = level.Mapping.GetSuit(TileIndex);
        if (level.StagingArea.TryMatch(suit, out var matchedTileIds))
        {
           // 拿到对应的移除的tiles，从 stagingAera 拿出，放到 corral中
            _matched = true;
            _matchedTileIds = matchedTileIds;

            // 卡槽中消除的部分进入 corral
            for (int i = 0; i < matchedTileIds.Length; i++)
            {
                level.Corral.Add(matchedTileIds[i]);
            }
        }

    }

    public override void Undo(LevelCore level)
    {
        if(_matched)
        {
            // corrl 倒出几位
            level.Corral.Remove(_matchedTileIds.Length + 1);

            for (int i = 0; i < _matchedTileIds.Length; i++)
            {
                level.StagingArea.Add(_matchedTileIds[i]);    
            }
          
            _matched = false;
            _matchedTileIds = null;
        }

        level.StagingArea.Remove(TileIndex);
        level.Pasture.Add(TileIndex);
    }
}
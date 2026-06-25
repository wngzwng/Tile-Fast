namespace Tile.Core.Core.Moves;

/// <summary>
/// 从 Pasture 选择一张棋子进入 StagingArea 的移动。
/// </summary>
public sealed class SelectMove : Move
{
    #region Fields

    private bool _hasMatch;
    private int[]? _matchedTileIndexes;

    #endregion

    #region Construction

    public SelectMove(int tileIndex)
    {
        TileIndex = tileIndex;
    }

    #endregion

    #region Move Checks

    public override bool CanDo(LevelCore level)
    {
        return level.Pasture.IsSelectable(TileIndex);
    }

    private void EnsureCanDo(LevelCore level)
    {
        if (CanDo(level))
            return;

        throw new InvalidOperationException(
            $"无法选择棋子 {TileIndex}：该棋子当前不可选。");
    }

    #endregion

    #region Apply

    public override void Do(LevelCore level)
    {
        EnsureCanDo(level);
        ResetRuntimeState();

        MoveTileToStagingArea(level);
        TryResolveMatch(level);
    }

    private void MoveTileToStagingArea(LevelCore level)
    {
        level.Pasture.Lift(TileIndex);
        level.StagingArea.Enter(TileIndex);
    }

    private void TryResolveMatch(LevelCore level)
    {
        var suit = level.Mapping.GetSuit(TileIndex);
        if (level.StagingArea.TryMatch(suit, out var matchedTileIds))
        {
            _hasMatch = true;
            _matchedTileIndexes = matchedTileIds;

            MoveMatchedTilesToCorral(level, matchedTileIds);
        }
    }

    private static void MoveMatchedTilesToCorral(
        LevelCore level,
        ReadOnlySpan<int> matchedTileIndexes)
    {
        for (var i = 0; i < matchedTileIndexes.Length; i++)
            level.Corral.Push(matchedTileIndexes[i]);
    }

    #endregion

    #region Undo

    public override void Undo(LevelCore level)
    {
        UndoMatchIfNeeded(level);

        level.StagingArea.Leave(TileIndex);
        level.Pasture.Place(TileIndex);
    }

    private void UndoMatchIfNeeded(LevelCore level)
    {
        if (!_hasMatch || _matchedTileIndexes is null)
            return;

        // 假设 Corral 栈顶正好是本次 Do 推入的匹配棋子。
        level.Corral.DropMany(_matchedTileIndexes.Length);

        for (var i = 0; i < _matchedTileIndexes.Length; i++)
            level.StagingArea.Enter(_matchedTileIndexes[i]);

        ResetRuntimeState();
    }

    #endregion

    #region Formatting

    public override string ToString()
    {
        if (!_hasMatch || _matchedTileIndexes is null)
            return $"SelectMove(TileIndex={TileIndex}, HasMatch=False)";

        return $"SelectMove(TileIndex={TileIndex}, HasMatch=True, Matched=[{string.Join(", ", _matchedTileIndexes)}])";
    }

    public string ToString(LevelCore level)
    {
        var suit = level.Mapping.GetSuit(TileIndex);

        if (!_hasMatch || _matchedTileIndexes is null)
            return $"SelectMove(TileIndex={TileIndex}, Suit={suit}, HasMatch=False)";

        return $"SelectMove(TileIndex={TileIndex}, Suit={suit}, HasMatch=True, Matched=[{string.Join(", ", _matchedTileIndexes)}])";
    }

    #endregion

    #region Runtime State

    private void ResetRuntimeState()
    {
        _hasMatch = false;
        _matchedTileIndexes = null;
    }

    #endregion
}

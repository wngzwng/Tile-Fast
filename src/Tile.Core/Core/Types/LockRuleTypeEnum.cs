namespace Tile.Core.Core.Types;

public enum LockRuleTypeEnum 
{
    // 麻将左右锁定模式，棋子上方无遮挡，且左侧或右侧至少有一个方向有空隙，方可点选
    Classic = 0,
    // Tile 模式，棋子上方无遮挡即可点选
    Tile = 1,
}
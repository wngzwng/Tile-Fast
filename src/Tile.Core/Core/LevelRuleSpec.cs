using Tile.Core.Core.Types;

namespace Tile.Core.Core;

public class LevelRuleSpec
{
    public int MatchRequireCount { get; }
    public int SlotCapacity { get; }
    public LockRuleTypeEnum LockRuleType { get; }


    private LevelRuleSpec(
        int matchRequireCount, 
        int slotCapacity, 
        LockRuleTypeEnum lockRuleType)
    {
        MatchRequireCount = matchRequireCount;
        SlotCapacity = slotCapacity;
        LockRuleType = lockRuleType;
    }

    public static LevelRuleSpec PairClassic = new (
        matchRequireCount: 2, 
        slotCapacity: 4, 
        lockRuleType: LockRuleTypeEnum.Classic);

    public static LevelRuleSpec TripleTile = new (
        matchRequireCount: 3, 
        slotCapacity: 7,
        lockRuleType: LockRuleTypeEnum.Tile);


    public static LevelRuleSpec Create(
        int matchRequireCount, 
        int slotCapacity, 
        LockRuleTypeEnum lockRuleType)
    {
        if (matchRequireCount < 2)
            throw new ArgumentOutOfRangeException(nameof(matchRequireCount), "MatchRequireCount 必须至少为 2。");
        if (slotCapacity < matchRequireCount)
            throw new ArgumentOutOfRangeException(nameof(slotCapacity), "SlotCapacity 必须至少与 MatchRequireCount 相等。");
            
        return new LevelRuleSpec(matchRequireCount, slotCapacity, lockRuleType);
    }
    
}

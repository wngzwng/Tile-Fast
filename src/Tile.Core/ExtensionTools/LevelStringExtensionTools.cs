using Tile.Core.Core;
using Tile.Core.Core.Types;

namespace Tile.Core.ExtensionTools;

public static class LevelStringExtensionTools
{
    public static LevelCore ToLevel(this string str, LevelRuleSpec ruleSpec)
    {
        return LevelCore.Deserialize(str, ruleSpec);
    }

    public static LevelCore ToLevel(
        this string str,
        int matchRequireCount,
        int slotCapacity,
        LockRuleTypeEnum lockRuleType)
    {
        var ruleSpec = LevelRuleSpec.Create(
            matchRequireCount,
            slotCapacity,
            lockRuleType);

        return LevelCore.Deserialize(str, ruleSpec);
    }
}


LevelRuleSpec 与 LevelTextExtensions 设计文档
1. 目标
关卡文本构建 LevelCore 时，需要同时提供关卡文本和规则参数：
var level = puzzle.ToLevel(LevelRuleSpec.PairClassic);

其中：
puzzle
    关卡文本

LevelRuleSpec
    关卡规则规格

ToLevel(...)
    将关卡文本转换成 LevelCore

LevelRuleSpec 的目标是把构建 LevelCore 所需的规则参数收敛到一个明确对象中。
LevelTextExtensions 的目标是提供简洁的字符串扩展入口。

2. 命名约定
最终命名：
类型名：LockRuleTypeEnum
属性名：LockRule
TOML 字段：lock_rule


3. LevelRuleSpec
LevelRuleSpec 表示一份关卡规则规格。
它包含：
MatchRequireCount
    几个相同 tile 可以消除

SlotCapacity
    卡槽容量

LockRule
    锁定规则

它不负责解析关卡文本，也不负责执行模拟。
推荐代码：
namespace ThreeTile.Core.Levels;

public sealed class LevelRuleSpec
{
    public int MatchRequireCount { get; }

    public int SlotCapacity { get; }

    public LockRuleTypeEnum LockRule { get; }

    private LevelRuleSpec(
        int matchRequireCount,
        int slotCapacity,
        LockRuleTypeEnum lockRule)
    {
        MatchRequireCount = matchRequireCount;
        SlotCapacity = slotCapacity;
        LockRule = lockRule;
    }

    public static LevelRuleSpec PairClassic { get; } = new(
        matchRequireCount: 2,
        slotCapacity: 4,
        lockRule: LockRuleTypeEnum.Classic);

    public static LevelRuleSpec TripleTile { get; } = new(
        matchRequireCount: 3,
        slotCapacity: 7,
        lockRule: LockRuleTypeEnum.Tile);

    public static LevelRuleSpec Create(
        int matchRequireCount,
        int slotCapacity,
        LockRuleTypeEnum lockRule)
    {
        if (matchRequireCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchRequireCount));

        if (slotCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotCapacity));

        return new LevelRuleSpec(
            matchRequireCount,
            slotCapacity,
            lockRule);
    }
}


4. 静态预设
当前稳定预设只有两个：
LevelRuleSpec.PairClassic
LevelRuleSpec.TripleTile

PairClassic
public static LevelRuleSpec PairClassic { get; } = new(
    matchRequireCount: 2,
    slotCapacity: 4,
    lockRule: LockRuleTypeEnum.Classic);

表示：
二消
卡槽容量 4
Classic 锁定规则

TripleTile
public static LevelRuleSpec TripleTile { get; } = new(
    matchRequireCount: 3,
    slotCapacity: 7,
    lockRule: LockRuleTypeEnum.Tile);

表示：
三消
卡槽容量 7
Tile 锁定规则


5. 动态规则
非稳定预设通过 Create(...) 构建：
var ruleSpec = LevelRuleSpec.Create(
    matchRequireCount: 2,
    slotCapacity: 5,
    lockRule: LockRuleTypeEnum.Tile);

这样可以区分：
PairClassic / TripleTile
    稳定规则规格

Create(...)
    动态规则规格


6. LevelTextExtensions
LevelTextExtensions 提供字符串到 LevelCore 的简洁入口：
var level = puzzle.ToLevel(LevelRuleSpec.PairClassic);

推荐代码：
namespace ThreeTile.Core.Levels;

public static class LevelTextExtensions
{
    public static LevelCore ToLevel(
        this string levelText,
        LevelRuleSpec ruleSpec)
    {
        return LevelCoreBuilder.Build(levelText, ruleSpec);
    }
}

LevelTextExtensions 只负责转发，不承载复杂构建逻辑。

7. 构建入口
LevelTextExtensions 建议转交给正式构建入口：
namespace ThreeTile.Core.Levels;

public static class LevelCoreBuilder
{
    public static LevelCore Build(
        string levelText,
        LevelRuleSpec ruleSpec)
    {
        if (string.IsNullOrWhiteSpace(levelText))
            throw new ArgumentException("Level text is empty.", nameof(levelText));

        if (ruleSpec is null)
            throw new ArgumentNullException(nameof(ruleSpec));

        return LevelCore.Deserialize(
            levelText,
            matchRequireCount: ruleSpec.MatchRequireCount,
            slotCapacity: ruleSpec.SlotCapacity,
            lockRule: ruleSpec.LockRule);
    }
}

如果项目中已有其他构建方式，可以替换 LevelCore.Deserialize(...)。

8. 使用方式
使用稳定预设：
var level = puzzle.ToLevel(LevelRuleSpec.PairClassic);

var level = puzzle.ToLevel(LevelRuleSpec.TripleTile);

使用动态规则：
var ruleSpec = LevelRuleSpec.Create(
    matchRequireCount: 2,
    slotCapacity: 5,
    lockRule: LockRuleTypeEnum.Tile);

var level = puzzle.ToLevel(ruleSpec);


9. 与 CLI / TOML 的边界
LevelRuleSpec 可以由 CLI / TOML 配置转换而来，但它本身不依赖 CLI / TOML。
推荐流程：
TOML / CLI
    ↓
Command Options
    ↓
LevelRuleSpec
    ↓
LevelTextExtensions.ToLevel(...)
    ↓
LevelCore

例如 TOML：
[rules.PairClassic]
match_require_count = 2
slot_capacity = 4
lock_rule = "Classic"

转换成：
var ruleSpec = LevelRuleSpec.Create(
    matchRequireCount: options.MatchRequireCount,
    slotCapacity: options.SlotCapacity,
    lockRule: options.LockRule);

如果 CLI 指定稳定预设：
--level-rule-spec PairClassic

则映射为：
var ruleSpec = LevelRuleSpec.PairClassic;

这部分边界的重点是：
CLI / TOML
    负责描述外部配置

Command Options
    负责承接配置

LevelRuleSpec
    负责进入 Core 层后的规则规格


10. 与 tile-eval.toml 的字段对应
如果使用 tile-eval.toml 格式：
[rules.PairClassic]
pair_count = 2
slot_capacity = 4
lock_rule = "Classic"

那么字段对应关系可以是：
pair_count
    -> LevelRuleSpec.MatchRequireCount

slot_capacity
    -> LevelRuleSpec.SlotCapacity

lock_rule
    -> LevelRuleSpec.LockRule

TOML 字段使用：
lock_rule = "Classic"


11. 职责边界
LevelRuleSpec
    规则规格

LevelTextExtensions
    简洁调用入口

LevelCoreBuilder
    实际构建 LevelCore

核心关系：
levelText
    +
LevelRuleSpec
    ↓
ToLevel(...)
    ↓
LevelCore


12. 文件位置
推荐放在 Levels 目录下：
src/Tile.Core/
└── Levels/
    ├── LevelCore.cs
    ├── LevelRuleSpec.cs
    ├── LevelTextExtensions.cs
    └── LevelCoreBuilder.cs

LevelTextExtensions.ToLevel(...) 与 LevelCore 构建关系很近，放在 Levels 下更容易查找。

13. 总结
LevelRuleSpec 用来表达：
一个关卡文本应该按照什么规则规格构建成 LevelCore。

LevelTextExtensions 用来提供：
var level = puzzle.ToLevel(LevelRuleSpec.PairClassic);

这比反复传递零散参数更稳定：
var level = LevelCore.Deserialize(
    puzzle,
    matchRequireCount: 2,
    slotCapacity: 4,
    lockRule: LockRuleTypeEnum.Classic);

最终命名：
LockRuleTypeEnum
LockRule
lock_rule

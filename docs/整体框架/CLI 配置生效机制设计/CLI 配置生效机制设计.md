CLI 配置生效机制设计
1. 目标
CLI 命令的参数来源可能有多个：
1. 默认配置
2. 用户传入的配置文件
3. 命令行参数覆写
为了避免参数来源混乱，运行逻辑不应该直接关心参数来自哪里，而应该统一使用最终合并后的 生效配置。
最终目标是：
默认配置 + 传入配置 + 命令行覆写 => 生效配置 => 运行

命令内部只从生效配置中读取自己关心的参数。

2. 核心原则
2.1 默认值由默认配置提供
所有参数都应该有统一的默认配置入口。
不推荐把默认值散落在命令处理逻辑中。
不推荐：
var pairCount = options.PairCount ?? 2;
var slotCapacity = options.SlotCapacity ?? 4;

推荐：
var defaultConfig = TileEvalDefaultConfig.Create();

也就是说，默认值属于配置系统，不属于运行逻辑。

2.2 配置文件只负责覆盖默认配置
配置文件不是完整配置的唯一来源。
配置文件可以只写自己关心的部分参数，没写的参数继续使用默认配置。
例如：
{
  "slotCapacity": 4,
  "lockedRule": "Classic",
  "slotType": "Normal"
}

最终没有写到的参数，仍然来自默认配置。

2.3 命令行参数优先级最高
命令行参数用于临时覆写。
优先级规则固定为：
命令行参数 > 配置文件 > 默认配置

例如：
./ThreeTile.CLI tile-eval \
  --config ./tile-eval.json \
  --count 10

这里 count 以命令行传入的 10 为准。

2.4 命令运行只依赖生效配置
命令内部不应该判断参数来源。
命令内部只关心：
EffectiveConfig 中最终是什么值。

也就是说：
参数来源归配置系统处理；
业务运行只依赖最终配置。


3. 配置合并流程
推荐流程：
DefaultConfig
    ↓
FileConfig 覆写
    ↓
CliOverride 覆写
    ↓
EffectiveConfig
    ↓
Validate
    ↓
Run

可以抽象为：
DefaultConfig + FileConfig + CliOverride => EffectiveConfig


4. 配置对象设计
建议区分两类配置对象。

4.1 生效配置
生效配置是最终用于运行的完整配置。
例如：
public sealed class TileEvalConfig
{
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }

    public int PairCount { get; set; }
    public int SlotCapacity { get; set; }

    public string LockedRule { get; set; } = "";
    public string SlotType { get; set; } = "";

    public int? Count { get; set; }

    public int MaxSolveTime { get; set; }
    public int MaxSuccessTime { get; set; }
}

这个对象代表最终生效参数。
运行逻辑只接收这个对象。

4.2 覆写配置
配置文件和命令行参数都可以看成覆写配置。
覆写配置中的字段可以为空。
空表示“不覆盖”。
例如：
public sealed class TileEvalConfigOverride
{
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }

    public int? PairCount { get; set; }
    public int? SlotCapacity { get; set; }

    public string? LockedRule { get; set; }
    public string? SlotType { get; set; }

    public int? Count { get; set; }

    public int? MaxSolveTime { get; set; }
    public int? MaxSuccessTime { get; set; }
}

这两个对象语义不同：
TileEvalConfig         表示最终完整配置
TileEvalConfigOverride 表示部分覆写配置

不要把这两个概念混在一起。

5. 默认配置
默认配置应该集中定义。
例如：
public static class TileEvalDefaultConfig
{
    public static TileEvalConfig Create()
    {
        return new TileEvalConfig
        {
            Input = null,
            Output = null,
            Error = null,

            PairCount = 2,
            SlotCapacity = 4,

            LockedRule = "Classic",
            SlotType = "Normal",

            Count = null,

            MaxSolveTime = 1000,
            MaxSuccessTime = 200
        };
    }
}

这样默认值只有一个入口。
后续修改默认行为时，只需要改这一处。

6. 配置合并
合并逻辑应该独立，不要散落在命令执行代码里。
示例：
public static class TileEvalConfigMerger
{
    public static TileEvalConfig Merge(
        TileEvalConfig defaultConfig,
        TileEvalConfigOverride? fileConfig,
        TileEvalConfigOverride? cliOverride)
    {
        var result = Clone(defaultConfig);

        Apply(result, fileConfig);
        Apply(result, cliOverride);

        return result;
    }

    private static void Apply(TileEvalConfig target, TileEvalConfigOverride? source)
    {
        if (source is null)
            return;

        if (source.Input is not null)
            target.Input = source.Input;

        if (source.Output is not null)
            target.Output = source.Output;

        if (source.Error is not null)
            target.Error = source.Error;

        if (source.PairCount.HasValue)
            target.PairCount = source.PairCount.Value;

        if (source.SlotCapacity.HasValue)
            target.SlotCapacity = source.SlotCapacity.Value;

        if (source.LockedRule is not null)
            target.LockedRule = source.LockedRule;

        if (source.SlotType is not null)
            target.SlotType = source.SlotType;

        if (source.Count.HasValue)
            target.Count = source.Count.Value;

        if (source.MaxSolveTime.HasValue)
            target.MaxSolveTime = source.MaxSolveTime.Value;

        if (source.MaxSuccessTime.HasValue)
            target.MaxSuccessTime = source.MaxSuccessTime.Value;
    }

    private static TileEvalConfig Clone(TileEvalConfig source)
    {
        return new TileEvalConfig
        {
            Input = source.Input,
            Output = source.Output,
            Error = source.Error,

            PairCount = source.PairCount,
            SlotCapacity = source.SlotCapacity,

            LockedRule = source.LockedRule,
            SlotType = source.SlotType,

            Count = source.Count,

            MaxSolveTime = source.MaxSolveTime,
            MaxSuccessTime = source.MaxSuccessTime
        };
    }
}

注意：不要直接写：
var result = defaultConfig;

因为如果配置对象是引用类型，这会修改默认配置本体。
应该 Clone 一份后再覆盖。

7. 必要参数校验
必要参数校验应该发生在生效配置生成之后。
不要在命令行解析阶段判断：
命令行是否传了 --input

而应该判断：
生效配置中是否有 Input

因为 Input 可能来自配置文件，也可能来自命令行。
示例：
public static class TileEvalConfigValidator
{
    public static void Validate(TileEvalConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Input))
            throw new ArgumentException("Missing required option: input");

        if (string.IsNullOrWhiteSpace(config.Output))
            throw new ArgumentException("Missing required option: output");

        if (string.IsNullOrWhiteSpace(config.Error))
            throw new ArgumentException("Missing required option: error");

        if (config.PairCount <= 0)
            throw new ArgumentException("pairCount must be greater than 0");

        if (config.SlotCapacity <= 0)
            throw new ArgumentException("slotCapacity must be greater than 0");

        if (config.MaxSolveTime <= 0)
            throw new ArgumentException("maxSolveTime must be greater than 0");

        if (config.MaxSuccessTime <= 0)
            throw new ArgumentException("maxSuccessTime must be greater than 0");
    }
}


8. 命令执行方式
命令处理器只接收生效配置。
示例：
public async Task<int> HandleAsync(TileEvalConfig config)
{
    TileEvalConfigValidator.Validate(config);

    PrintEffectiveConfig(config);

    await RunTileEvalAsync(
        input: config.Input!,
        output: config.Output!,
        error: config.Error!,
        pairCount: config.PairCount,
        slotCapacity: config.SlotCapacity,
        lockedRule: config.LockedRule,
        slotType: config.SlotType,
        count: config.Count,
        maxSolveTime: config.MaxSolveTime,
        maxSuccessTime: config.MaxSuccessTime
    );

    return 0;
}

命令内部不要再出现：
options.PairCount ?? 2

也不要出现：
cli.PairCount ?? fileConfig.PairCount ?? 2

这些逻辑都应该在生成生效配置阶段完成。

9. 推荐命令形式
9.1 只使用默认配置和命令行必要参数
./ThreeTile.CLI tile-eval \
  --input /Users/admin/Desktop/ThreeTile/Tile2-3_4槽/testcsv.csv \
  --output ./result.csv \
  --error ./error.csv


9.2 命令行临时覆写
./ThreeTile.CLI tile-eval \
  --input /Users/admin/Desktop/ThreeTile/Tile2-3_4槽/testcsv.csv \
  --output ./result.csv \
  --error ./error.csv \
  --count 10


9.3 配置文件 + 命令行覆写
./ThreeTile.CLI tile-eval \
  --config ./tile-eval.json \
  --input /Users/admin/Desktop/ThreeTile/Tile2-3_4槽/testcsv.csv \
  --output ./result.csv \
  --error ./error.csv \
  --count 10

其中：
默认配置提供基础参数
配置文件覆盖默认配置
命令行参数覆盖配置文件
最终得到生效配置


10. 建议支持的辅助参数
10.1 --print-config
只打印生效配置，不运行。
适合排查参数合并结果。
./ThreeTile.CLI tile-eval \
  --config ./tile-eval.json \
  --count 10 \
  --print-config


10.2 --dry-run
打印生效配置，执行校验，但不真正运行。
适合批量任务运行前检查。
./ThreeTile.CLI tile-eval \
  --config ./tile-eval.json \
  --count 10 \
  --dry-run


11. 生效配置打印
建议每次运行前打印生效配置。
例如：
Effective tile-eval config:

Input:          /Users/admin/Desktop/ThreeTile/Tile2-3_4槽/testcsv.csv
Output:         ./result.csv
Error:          ./error.csv
PairCount:      2
SlotCapacity:   4
LockedRule:     Classic
SlotType:       Normal
Count:          10
MaxSolveTime:   1000
MaxSuccessTime: 200

这样可以快速确认当前运行到底使用了哪些参数。
如果需要进一步排查，也可以打印参数来源：
PairCount:      2        default
SlotCapacity:   4        default
LockedRule:     Classic  default
SlotType:       Normal   config
Count:          10       command line

来源追踪不是第一阶段必须做，但后续可以扩展。

12. 最终结论
推荐固定为以下架构：
所有参数都有统一配置入口；
默认值由默认配置提供；
配置文件只负责覆盖默认配置；
命令行只负责覆盖配置文件；
命令执行阶段只读取生效配置。

完整流程：
DefaultConfig
    +
FileConfig
    +
CliOverride
    ↓
EffectiveConfig
    ↓
Validate
    ↓
Print / DryRun
    ↓
Run

这套结构可以让 CLI 参数管理保持稳定、清晰、可扩展。
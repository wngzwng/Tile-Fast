ThreeTile CLI 配置系统设计文档
1. 目标
ThreeTile CLI 需要支持多个命令。
不同命令有不同参数，不同运行场景也可能需要不同配置。
配置系统的目标不是把所有参数都塞进配置文件，而是：
配置文件只写大部分情况下稳定、值得复用、容易敲错的参数。

命令行仍然负责本次运行中经常变化的参数，例如：
input
output
error
count

最终设计目标：
命令默认配置
  +
TOML 中该命令的 preset 配置
  +
TOML 中该命令的 profile 配置
  +
命令行参数覆写
  ↓
生效配置
  ↓
运行


2. 基础选择
2.1 配置文件格式
使用：
TOML

原因：
TOML 更像配置文件
支持分组
可读性好
比 JSON 更适合手写
比 YAML 更不容易因为缩进出错

配置文件名：
threetile.toml


2.2 TOML 解析库
C# 侧使用：
Tomlyn

安装：
dotnet add package Tomlyn

Tomlyn 在项目中的职责：
1. 读取 threetile.toml
2. 映射到 C# 配置对象
3. 导出默认配置为 TOML
4. 后续如果需要保留注释，可以使用语法树能力


3. 核心原则
3.1 TOML 不是完整配置
threetile.toml 不需要描述完整世界。
它只描述用户想改的部分。
也就是说：
写了就覆盖
没写就使用命令默认配置

例如：
[commands.tile-eval.presets.Pair]
maxSolveTime = 2000

这表示只覆盖 maxSolveTime。
其他参数继续使用 tile-eval 命令自己的默认配置。

3.2 每个命令只读取自己关心的配置
tile-eval 只读取：
[commands.tile-eval.presets.xxx]
[commands.tile-eval.profiles.xxx]

它不关心：
[commands.csv-merge.xxx]

csv-merge 也不关心 tile-eval。
这样多个命令的配置不会互相污染。

3.3 配置文件管稳定规则，命令行管本次任务
配置文件适合放：
大部分情况下不会变
但又不想写死在代码里
并且希望按项目 / 场景复用的参数

命令行适合放：
本次运行经常变化的参数
调试参数
临时覆写参数

对于 tile-eval，可以这样分类：
参数
建议位置
原因
pairCount
preset
规则预设，通常稳定
slotCapacity
preset
规则预设，通常稳定
lockedRule
preset
规则预设，通常稳定
slotType
preset
规则预设，通常稳定
maxSolveTime
preset / profile
算法策略，通常稳定
maxSuccessTime
preset / profile
实验参数，可能按场景变化
softMaxTemperature
profile
实验参数，常用于分批实验
input
命令行 / profile
输入数据，经常变化
output
命令行 / profile
输出路径，经常变化
error
命令行 / profile
错误输出，经常变化
count
命令行
调试参数，临时性强

4. 配置层级
最终配置来源分为四层：
命令内置默认配置
  ↓
preset 配置
  ↓
profile 配置
  ↓
命令行覆写

优先级：
命令行参数 > profile 配置 > preset 配置 > 命令内置默认配置

解释：
命令内置默认配置：
  命令自己提供的基础默认值。

preset：
  规则预设，例如 Pair、Triple。

profile：
  运行场景，例如 local-test、pika-t025。

命令行参数：
  本次运行临时覆写，优先级最高。


5. preset 与 profile 的区别
5.1 preset：规则预设
preset 表示一套稳定规则。
例如：
Pair   二合规则
Triple 三合规则

它回答的是：
这个命令按什么规则跑？

示例：
[commands.tile-eval.presets.Pair]
pairCount = 2
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"

[commands.tile-eval.presets.Triple]
pairCount = 3
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"


5.2 profile：运行场景
profile 表示一个具体运行场景。
例如：
local-test
pika-t025
pika-t06

它回答的是：
这一次常用场景要覆写哪些参数？

示例：
[commands.tile-eval.profiles.local-test]
input = "/Users/admin/Desktop/ThreeTile/Tile2-3_4槽/testcsv.csv"
output = "./result.csv"
error = "./error.csv"
count = 10

[commands.tile-eval.profiles.pika-t025]
softMaxTemperature = 0.25
maxSuccessTime = 1000


6. 推荐 TOML 结构
version = 1

# ==============================
# tile-eval: 规则预设
# ==============================

[commands.tile-eval.presets.Pair]
pairCount = 2
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200

[commands.tile-eval.presets.Triple]
pairCount = 3
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200


# ==============================
# tile-eval: 运行场景
# ==============================

[commands.tile-eval.profiles.local-test]
input = "/Users/admin/Desktop/ThreeTile/Tile2-3_4槽/testcsv.csv"
output = "./result.csv"
error = "./error.csv"
count = 10

[commands.tile-eval.profiles.pika-t025]
softMaxTemperature = 0.25
maxSuccessTime = 1000

[commands.tile-eval.profiles.pika-t06]
softMaxTemperature = 0.6
maxSuccessTime = 1000

[commands.tile-eval.profiles.pika-t09]
softMaxTemperature = 0.9
maxSuccessTime = 1000


# ==============================
# csv-merge: 示例
# ==============================

[commands.csv-merge.profiles.pika]
inputDir = "./Result"
output = "./online-check-pika-new.csv"

注意：
每个块都可以不完整。
只写需要覆盖的参数。


7. 运行示例
7.1 使用 Pair 规则运行
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --input ./Origin/a.csv \
  --output ./Result/a.result.csv \
  --error ./Error/a.error.csv

生效顺序：
TileEvalConfig 默认值
  +
commands.tile-eval.presets.Pair
  +
命令行参数


7.2 使用 Triple 规则运行
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Triple \
  --input ./Origin/a.csv \
  --output ./Result/a.result.csv \
  --error ./Error/a.error.csv


7.3 Pair + local-test 场景
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --profile local-test

生效顺序：
TileEvalConfig 默认值
  +
commands.tile-eval.presets.Pair
  +
commands.tile-eval.profiles.local-test


7.4 Pair + pika-t025 场景 + 命令行输入输出
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --profile pika-t025 \
  --input ./Origin \
  --output ./Result \
  --error ./Error

生效顺序：
TileEvalConfig 默认值
  +
commands.tile-eval.presets.Pair
  +
commands.tile-eval.profiles.pika-t025
  +
命令行参数


7.5 命令行临时覆写
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --profile pika-t025 \
  --input ./Origin \
  --output ./Result \
  --error ./Error \
  --max-solve-time 2000

最终 maxSolveTime 以命令行传入的 2000 为准。

8. 命名规范
8.1 TOML 字段名
TOML 中使用 camelCase：
pairCount = 2
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200
softMaxTemperature = 0.25


8.2 C# 属性名
C# 中使用 PascalCase：
PairCount
SlotCapacity
LockedRule
SlotType
MaxSolveTime
MaxSuccessTime
SoftMaxTemperature


8.3 命令行参数名
命令行中使用 kebab-case：
--pair-count
--slot-capacity
--locked-rule
--slot-type
--max-solve-time
--max-success-time
--soft-max-temperature

对应关系：
TOML:    slotCapacity
C#:      SlotCapacity
CLI:     --slot-capacity


9. C# 设计
9.1 推荐目录结构
不要为每个命令拆一堆文件。
推荐：
ThreeTile.CLI
├── Config
│   ├── TomlConfigLoader.cs
│   ├── TomlConfigApplier.cs
│   ├── TomlConfigExporter.cs
│   └── Commands
│       ├── TileEvalConfig.cs
│       ├── CsvMergeConfig.cs
│       └── OtherCommandConfig.cs

原则：
通用加载 / 应用 / 导出逻辑放 Config 根目录。
每个命令只维护自己的 Config 文件。
必要时复杂命令再单独增加 Binder / Validator。


9.2 TileEvalConfig
public sealed class TileEvalConfig
{
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }

    public int PairCount { get; set; } = 2;
    public int SlotCapacity { get; set; } = 4;

    public string LockedRule { get; set; } = "Classic";
    public string SlotType { get; set; } = "Normal";

    public int? Count { get; set; }

    public int MaxSolveTime { get; set; } = 1000;
    public int MaxSuccessTime { get; set; } = 200;

    public double? SoftMaxTemperature { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Input))
            throw new ArgumentException("Missing required option: input");

        if (string.IsNullOrWhiteSpace(Output))
            throw new ArgumentException("Missing required option: output");

        if (string.IsNullOrWhiteSpace(Error))
            throw new ArgumentException("Missing required option: error");

        if (PairCount <= 0)
            throw new ArgumentException("pairCount must be greater than 0");

        if (SlotCapacity <= 0)
            throw new ArgumentException("slotCapacity must be greater than 0");

        if (MaxSolveTime <= 0)
            throw new ArgumentException("maxSolveTime must be greater than 0");

        if (MaxSuccessTime <= 0)
            throw new ArgumentException("maxSuccessTime must be greater than 0");
    }
}

这个类承担：
字段定义
默认值
最终生效配置
本命令校验


10. 配置应用流程
伪代码：
var config = new TileEvalConfig();

TomlConfigApplier.ApplyCommandPreset(
    config,
    tomlPath,
    commandName: "tile-eval",
    presetName: presetName);

TomlConfigApplier.ApplyCommandProfile(
    config,
    tomlPath,
    commandName: "tile-eval",
    profileName: profileName);

TileEvalCliOptions.ApplyTo(config);

config.Validate();

await RunAsync(config);

完整逻辑：
new TileEvalConfig()
  ↓
Apply commands.tile-eval.presets.<preset>
  ↓
Apply commands.tile-eval.profiles.<profile>
  ↓
Apply CLI options
  ↓
Validate
  ↓
Run


11. 命令行参数对象
命令行参数对象应该表示“用户传了什么”。
因此很多字段应该是可空的。
示例：
public sealed class TileEvalCliOptions
{
    public string? Config { get; set; }
    public string? Preset { get; set; }
    public string? Profile { get; set; }

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

    public double? SoftMaxTemperature { get; set; }

    public void ApplyTo(TileEvalConfig config)
    {
        if (Input is not null)
            config.Input = Input;

        if (Output is not null)
            config.Output = Output;

        if (Error is not null)
            config.Error = Error;

        if (PairCount.HasValue)
            config.PairCount = PairCount.Value;

        if (SlotCapacity.HasValue)
            config.SlotCapacity = SlotCapacity.Value;

        if (LockedRule is not null)
            config.LockedRule = LockedRule;

        if (SlotType is not null)
            config.SlotType = SlotType;

        if (Count.HasValue)
            config.Count = Count.Value;

        if (MaxSolveTime.HasValue)
            config.MaxSolveTime = MaxSolveTime.Value;

        if (MaxSuccessTime.HasValue)
            config.MaxSuccessTime = MaxSuccessTime.Value;

        if (SoftMaxTemperature.HasValue)
            config.SoftMaxTemperature = SoftMaxTemperature.Value;
    }
}

注意：
CLI Options 不是最终配置。
CLI Options 是命令行覆写。


12. TOML 读取策略
不建议一开始把整个 threetile.toml 映射成一个巨大的强类型对象。
更推荐：
Tomlyn 读取整个 TOML
  ↓
按 commandName 找 commands.<command>
  ↓
按 presetName 找 presets.<preset>
  ↓
按 profileName 找 profiles.<profile>
  ↓
只应用当前命令关心的字段

原因：
命令会越来越多。
每个命令参数不同。
不应该让一个巨大 ThreeTileConfig 承担所有命令结构。

更好的边界是：
TOML 是项目级配置文件。
命令 Config 是强类型运行配置。
配置应用器负责把 TOML 局部块覆盖到命令 Config。


13. 默认配置导出
13.1 导出某个命令的默认配置
./ThreeTile.CLI tile-eval config export

输出：
[commands.tile-eval.presets.Pair]
pairCount = 2
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200

[commands.tile-eval.presets.Triple]
pairCount = 3
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200


13.2 初始化项目配置文件
./ThreeTile.CLI config init

生成：
threetile.toml

内容示例：
version = 1

[commands.tile-eval.presets.Pair]
pairCount = 2
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200

[commands.tile-eval.presets.Triple]
pairCount = 3
slotCapacity = 4
lockedRule = "Classic"
slotType = "Normal"
maxSolveTime = 1000
maxSuccessTime = 200

用户可以按需添加：
[commands.tile-eval.profiles.local-test]
input = "./test.csv"
output = "./result.csv"
error = "./error.csv"
count = 10


14. 生效配置打印
建议每次运行前支持打印生效配置。
命令：
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --profile pika-t025 \
  --input ./Origin \
  --output ./Result \
  --error ./Error \
  --print-config

输出示例：
Effective tile-eval config:

Input:              ./Origin
Output:             ./Result
Error:              ./Error
PairCount:          2
SlotCapacity:       4
LockedRule:         Classic
SlotType:           Normal
Count:              <null>
MaxSolveTime:       1000
MaxSuccessTime:     1000
SoftMaxTemperature: 0.25

如果需要更强排查能力，后续可以增加来源追踪：
PairCount:          2        preset: Pair
SlotCapacity:       4        preset: Pair
MaxSuccessTime:     1000     profile: pika-t025
Input:              ./Origin command line

来源追踪不是第一阶段必须做。

15. dry-run
建议支持：
--dry-run

含义：
生成生效配置
执行校验
打印配置
不真正运行

示例：
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --profile pika-t025 \
  --input ./Origin \
  --output ./Result \
  --error ./Error \
  --dry-run

适合批量任务运行前检查。

16. 推荐命令参数
tile-eval 推荐支持：
--config
--preset
--profile

--input
--output
--error

--pair-count
--slot-capacity
--locked-rule
--slot-type

--count

--max-solve-time
--max-success-time
--soft-max-temperature

--print-config
--dry-run

其中：
--config        指定 threetile.toml
--preset        指定规则预设，例如 Pair / Triple
--profile       指定运行场景，例如 local-test / pika-t025
--print-config  打印生效配置
--dry-run       只校验和打印，不运行


17. 第一版实现边界
第一版不要过度设计。
推荐第一版只实现：
1. Tomlyn 读取 threetile.toml
2. 支持 commands.<command>.presets.<preset>
3. 支持 commands.<command>.profiles.<profile>
4. 支持命令行覆写
5. 生成生效配置
6. Validate
7. PrintConfig
8. DryRun

暂时不做：
1. 来源追踪
2. 注释保留
3. 自动修改原 TOML
4. 复杂继承
5. 多 profile 叠加

后续如果真的需要，再加。

18. 最终结论
ThreeTile CLI 配置系统最终固定为：
配置文件格式：TOML
解析库：Tomlyn
配置文件名：threetile.toml
配置方式：按需配置 / 局部覆写
配置入口：commands.<command>
规则预设：commands.<command>.presets.<preset>
运行场景：commands.<command>.profiles.<profile>
命令执行：只读取生效配置

核心流程：
命令默认配置
  +
commands.<command>.presets.<preset>
  +
commands.<command>.profiles.<profile>
  +
命令行参数覆写
  ↓
生效配置
  ↓
校验
  ↓
打印 / dry-run
  ↓
运行

一句话总结：
每个命令自己定义默认配置；
threetile.toml 只写需要覆盖的部分；
Pair / Triple 作为 tile-eval 的规则预设；
profile 作为具体运行场景；
命令行参数永远拥有最高优先级。

tile-eval 指标输出配置设计文档
1. 目标
tile-eval 在模拟运行过程中会产生两类结果：
SolveInfo：单次模拟 / 单局求解结果
LevelInfo：多次模拟 / 聚合统计结果

当模拟运行很多次时，例如 1000 次，指标数量可能很多，不同场景下需要输出的指标也不同。
因此需要一个独立的指标输出配置文件，用来控制：
1. 哪些指标参与输出
2. 输出指标的顺序
3. 指标按什么分组计算

该配置文件只负责指标输出，不负责 tile-eval 的运行参数。

2. 配置文件
推荐文件名：
tile-eval.metrics.toml

配置格式：
TOML


3. 核心原则
指标配置分成两部分：
groups
outputs

核心职责：
groups 管“怎么算”
outputs.order 管“怎么排”

也就是：
groups：
  定义指标属于哪个计算组。
  一个 group 对应一组计算逻辑。

outputs：
  定义哪些输出域启用。
  定义最终输出指标顺序。


4. 输出域
当前支持两个输出域：
solve
level

含义：
solve：
  单次模拟输出。
  对应 SolveInfo。

level：
  多次模拟聚合输出。
  对应 LevelInfo。

配置结构：
[outputs.solve]
enabled = false
order = []

[outputs.level]
enabled = true
order = []

说明：
enabled：
  是否启用该输出域。

order：
  该输出域最终输出指标顺序。


5. 指标命名规范
指标名建议使用：
<scope>.<metricName>

其中：
scope：
  solve 或 level

metricName：
  具体指标名

示例：
solve.failed
solve.totalTiles
solve.score

level.totalRuns
level.failRate
level.avgScore

这样可以避免单局指标和聚合指标重名。

6. groups 结构
groups 用于描述指标的计算分组。
结构：
[groups.<scope>.<groupName>]
metrics = []

例如：
[groups.solve.base]
metrics = []

[groups.level.result]
metrics = []

含义：
scope：
  solve 或 level

groupName：
  指标分组名称

metrics：
  这个分组下包含哪些指标


7. groups 与 outputs 的关系
最终输出由 outputs.<scope>.order 控制。
指标分组由 groups.<scope>.<groupName>.metrics 控制。
它们的关系是：
groups 管指标属于哪个计算组
outputs.order 管最终输出顺序

同一个指标应该：
1. 出现在某个 groups.<scope>.<groupName>.metrics 中
2. 出现在 outputs.<scope>.order 中

这样它既知道：
怎么算

也知道：
排在哪里


8. 注释控制
TOML 配置中，可以通过注释控制是否启用某个指标。
例如：
[groups.level.result]
metrics = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  # "level.avgScore",
  # "level.avgNewDifficultyScore",
]

被注释掉的指标不会进入该分组。
同样，输出顺序中也可以注释：
[outputs.level]
enabled = true

order = [
  "level.successCount",
  "level.failCount",
  "level.failRate",
  # "level.avgScore",
]


9. 完整结构模板
version = 1
command = "tile-eval"


# =========================================================
# 输出配置
# outputs 只关心：
# 1. 哪个输出域启用
# 2. 最终输出顺序
# =========================================================

[outputs.solve]
enabled = false

order = [
  # "solve.xxx",
  # "solve.xxx",
  # "solve.xxx",
]


[outputs.level]
enabled = true

order = [
  # "level.xxx",
  # "level.xxx",
  # "level.xxx",
]


# =========================================================
# SolveInfo 指标分组
# groups.solve.* 只关心：
# 指标属于哪个单局计算组
# =========================================================

[groups.solve.base]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.tags]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.result]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.smooth]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.swap]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.hardSwap]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.slotPressure]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.dead]
metrics = [
  # "solve.xxx",
]

[groups.solve.colorHold]
metrics = [
  # "solve.xxx",
  # "solve.xxx",
]

[groups.solve.curves]
metrics = [
  # "solve.xxx",
]


# =========================================================
# LevelInfo 指标分组
# groups.level.* 只关心：
# 指标属于哪个聚合计算组
# =========================================================

[groups.level.base]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.result]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.failPosition]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.relive]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.smooth]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.swap]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.hardSwap]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.slotPressure]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.dead]
metrics = [
  # "level.xxx",
]

[groups.level.colorHold]
metrics = [
  # "level.xxx",
  # "level.xxx",
]

[groups.level.curves]
metrics = [
  # "level.xxx",
]


10. 简洁骨架
如果不需要注释说明，可以使用简洁版：
version = 1
command = "tile-eval"

[outputs.solve]
enabled = false
order = []

[outputs.level]
enabled = true
order = []


[groups.solve.base]
metrics = []

[groups.solve.tags]
metrics = []

[groups.solve.result]
metrics = []

[groups.solve.smooth]
metrics = []

[groups.solve.swap]
metrics = []

[groups.solve.hardSwap]
metrics = []

[groups.solve.slotPressure]
metrics = []

[groups.solve.dead]
metrics = []

[groups.solve.colorHold]
metrics = []

[groups.solve.curves]
metrics = []


[groups.level.base]
metrics = []

[groups.level.result]
metrics = []

[groups.level.failPosition]
metrics = []

[groups.level.relive]
metrics = []

[groups.level.smooth]
metrics = []

[groups.level.swap]
metrics = []

[groups.level.hardSwap]
metrics = []

[groups.level.slotPressure]
metrics = []

[groups.level.dead]
metrics = []

[groups.level.colorHold]
metrics = []

[groups.level.curves]
metrics = []


11. 推荐读取流程
程序读取 tile-eval.metrics.toml 后，按照以下步骤处理：
1. 读取 outputs.solve / outputs.level
2. 判断哪些输出域 enabled = true
3. 读取对应 scope 下的 outputs.<scope>.order
4. 读取 groups.<scope>.*
5. 建立 metricName -> groupName 的映射
6. 根据 outputs.<scope>.order 得到最终输出列顺序
7. 根据 order 中的指标反推需要执行哪些 group
8. 执行对应 group 的计算逻辑
9. 按 order 输出结果


12. 校验规则
建议读取后做基础校验：
1. command 必须等于 tile-eval
2. enabled = true 的输出域，order 可以为空，但应允许
3. outputs.<scope>.order 中的指标名必须属于对应 scope
4. groups.<scope>.*.metrics 中的指标名必须属于对应 scope
5. 同一个指标不应出现在同一个 scope 的多个 group 中
6. outputs.<scope>.order 中重复指标应报错
7. 未知指标名应报错


13. C# 对象结构建议
public sealed class MetricsConfig
{
    public int Version { get; set; } = 1;

    public string Command { get; set; } = "";

    public Dictionary<string, MetricsOutputConfig> Outputs { get; set; } = new();

    public Dictionary<string, Dictionary<string, MetricsGroupConfig>> Groups { get; set; } = new();
}

public sealed class MetricsOutputConfig
{
    public bool Enabled { get; set; }

    public List<string> Order { get; set; } = new();
}

public sealed class MetricsGroupConfig
{
    public List<string> Metrics { get; set; } = new();
}

对应 TOML：
[outputs.level]
enabled = true
order = []

[groups.level.result]
metrics = []

映射关系：
Outputs["level"]
Groups["level"]["result"]


14. 命令行参数
tile-eval 可以增加参数：
--metrics-config ./tile-eval.metrics.toml

示例：
./ThreeTile.CLI tile-eval \
  --config ./threetile.toml \
  --preset Pair \
  --profile pika-t025 \
  --input ./Origin \
  --output ./Result \
  --error ./Error \
  --metrics-config ./tile-eval.metrics.toml


15. 最终结论
tile-eval.metrics.toml 只负责指标输出配置。
它不负责运行参数。
核心结构固定为：
outputs：
  管输出域是否启用
  管最终输出顺序

groups：
  管指标计算分组

最终一句话：
groups 管“怎么算”，outputs.order 管“怎么排”。


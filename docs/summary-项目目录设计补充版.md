# Tile-Fast / ThreeTile 项目目录设计补充版

> 本文用于先确定项目目录设计，重点说明：
>
> * `src/Tile.Core/`
> * `src/Tile.Services/`
> * `src/Tile.CLI/`
> * `src/Tile.Tooling/`
>
> 这些项目分别应该包含哪些内容、边界怎么划分、哪些内容不建议放进去。

---

## 1. 总体目标

`Tile-Fast` 是一个面向 Tile 类关卡的 C# 高性能求解、模拟、评估与命令行工具项目。

项目采用多 C# 项目组成一个解决方案：

```text
Tile-Fast/
├── Tile.sln
├── src/
│   ├── Tile.Core/
│   ├── Tile.Services/
│   ├── Tile.CLI/
│   └── Tile.Tooling/
├── tests/
├── benchmarks/
├── docs/
├── samples/
└── scripts/
```

核心分层原则：

```text
Tile.CLI
   ↓
Tile.Services
   ↓
Tile.Core
```

同时补充一个共享工具层：

```text
Tile.Tooling
  ↙     ↓      ↘
Core  Services  CLI
```

也就是：

* `Tile.Core`：稳定核心层，尽量小，尽量高性能。
* `Tile.Services`：业务服务层，负责把 Core 能力组织成完整任务。
* `Tile.CLI`：命令行入口层，负责配置、命令、输出、流程接线。
* `Tile.Tooling`：共享工具层，放 Core / Services / CLI 都可能复用的通用工具能力。

一句话总结：

* `Core` 提供领域核心能力。
* `Services` 编排业务任务。
* `CLI` 处理命令行配置与输出。
* `Tooling` 提供跨项目复用的通用工具。

---

## 2. 为什么要拆成四个项目

如果不拆层，项目很容易出现下面几种混乱：

* 配置读取逻辑直接混进核心算法。
* CLI 命令代码直接操作底层结构。
* 指标、批处理、输出顺序和模拟逻辑缠在一起。
* Core 被业务试验代码污染，后面越来越难复用。
* CSV、进度条、文件格式支持散落在 CLI 和 Services 各处，后面很难统一。

拆成四层的好处是：

* `Tile.Core` 可以稳定沉淀高性能结构和公共能力。
* `Tile.Services` 可以大胆迭代 finder / scorer / metrics / simulation 这些业务模块。
* `Tile.CLI` 可以自由调整 TOML、参数、命令组织、日志和输出，而不会影响核心能力。
* `Tile.Tooling` 可以统一承接 CSV、NPY、Progress 这类跨项目共享工具，避免变成 CLI 私有杂项。

---

## 3. src 项目总览

| 项目 | 类型 | 定位 | 依赖 |
| --- | --- | --- | --- |
| `Tile.Core` | Class Library | 稳定核心层 | 不依赖其他 Tile 项目 |
| `Tile.Services` | Class Library | 业务服务层 | 依赖 `Tile.Core`，必要时依赖 `Tile.Tooling` |
| `Tile.CLI` | Console App | 命令行入口层 | 依赖 `Tile.Services`，必要时依赖 `Tile.Core`、`Tile.Tooling` |
| `Tile.Tooling` | Class Library | 共享工具层 | 不依赖业务层，尽量保持通用 |

推荐依赖方向：

```text
Tile.CLI
   ↓
Tile.Services
   ↓
Tile.Core

Tile.Tooling
  ↙     ↓      ↘
Core  Services  CLI
```

推荐约束：

```text
Tile.Core     不应该引用 Tile.Services / Tile.CLI
Tile.Services 不应该引用 Tile.CLI
Tile.Tooling 不应该依赖具体业务模块
```

补充理解：

* `Tile.Core` 要尽量做到未来可以被 CLI、测试、Godot 编辑器、其他服务共同复用。
* `Tile.Services` 是“面向用例”的一层，不一定稳定，但要保持边界清晰。
* `Tile.CLI` 是最靠外的一层，允许和 TOML、Console、路径、命令参数打交道。
* `Tile.Tooling` 是共享工具层，只放跨项目复用的通用工具，不放领域核心、不放业务编排、不放 CLI 专属逻辑。

---

## 4. Tile.Core

## 4.1 定位

`Tile.Core` 是整个项目最底层、最稳定、最强调性能的一层。

它应该回答的是：

* Tile 是什么？
* Level 是什么？
* 静态映射和动态状态怎么表达？
* 邻居、遮挡、可见性如何高性能判断？
* 卡槽状态如何维护？
* 位集、数学、基础模拟抽象如何组织？

这一层的关键词是：

* 稳定
* 低分配
* 可复用
* 少依赖
* 不关心外部配置和输出

## 4.2 `Tile.Core` 应该包含哪些内容

建议放入 `Tile.Core` 的内容：

* `Tile`、`TilePosition`、`TileVolume`、`TileSuit` 这类基础领域结构
* `LevelCore`、`TileMappingTable`、`RegionBounds`、`RegionIndex` 这类关卡静态结构
* `StagingArea` 这类热路径卡槽状态结构
* `Pasture`、邻居收集、遮挡判断、可见性判断等空间结构
* `BitSetOperations`、位图遍历器、位集工具
* `MathKit`、Softmax、WeightedChoice、距离计算、平均值等数学工具
* 模拟相关的基础接口或结果抽象，例如 `SimulationContext`、`SimulationRunResult`
* 纯分析能力，例如 DAG、解锁路径、簇分析、残留结构分析

这些内容的共同特征通常是：

* 不依赖 TOML / JSON / CSV
* 不依赖控制台输出
* 不依赖路径和文件系统流程
* 不依赖具体命令
* 不依赖某一种 scorer / finder / profile

## 4.3 `Tile.Core` 不应该包含哪些内容

不建议放进 `Tile.Core` 的内容：

* 命令行参数解析
* TOML 配置读取和合并
* CSV 业务输出
* Console 日志和进度打印
* `tile-eval` 命令编排逻辑
* 大文件分片与结果合并流程
* PiKa / Tokiki 这类具体业务评分器
* 面向某个业务场景的 finder / policy 组合器
* 只为某个 CLI 命令服务的 DTO 或 Request

因为这些内容都带有明显的“外部适配”或“业务试验”属性，不够稳定。

## 4.4 `Tile.Core` 推荐子目录

```text
Tile.Core/
├── Tile.Core.csproj
├── Core/
│   ├── Utils/
│   ├── Moves/
│   ├── Types/
│   ├── Zones/
│   ├── Tile.cs
│   ├── LevelCore.cs
│   └── TileMappingTable.cs
├── Analysis/
├── Simulation/
└── Common/
```

各目录建议如下：

| 目录 | 建议内容 | 说明 |
| --- | --- | --- |
| `Core/` | 最核心的领域结构和静态结构 | 放 Tile、Level、Move、Zone、基础编码和静态映射 |
| `Analysis/` | 稳定、可复用、和具体策略无关的纯分析能力 | 例如 DAG、解锁路径、簇分析、残留结构分析 |
| `Simulation/` | 业务无关的模拟抽象与通用流程 | 可以放基础接口、通用运行流程、观察器接口，但不放具体业务策略组装 |
| `Common/` | 通用底层工具 | 放 `MathKit`、`BitSetOperations`、通用小型共享结构 |

`Core/` 建议包含：

* `Utils/`
* `Moves/`
* `Types/`
* `Zones/`
* `Tile.cs`
* `LevelCore.cs`
* `TileMappingTable.cs`

其中可以进一步理解为：

* `Utils/`：例如 `xyz.pack`、`charToIndex`、`indexToChar`、`charPairToIndex`
* `Moves/`：例如 `Move.cs`、`SelectMove.cs`
* `Types/`：例如 `LockRuleTypeEnum.cs`、`DirTypeEnum.cs`
* `Zones/`：例如 `Corral.cs`、`Pasture.cs`、`StagingArea.cs`

这里的原则是：

* 只要还属于“关卡本体怎么表达、状态怎么表达、静态关系怎么表达”，就优先放在 `Core/`
* 不要因为文件数量增长，就把边界本来一致的内容硬拆成很多顶层目录

## 4.5 `Tile.Core` 的判断标准

一个类适合放进 `Tile.Core`，通常尽量满足下面几个问题：

| 判断问题 | 应该回答 |
| --- | --- |
| 它是否稳定？ | 是 |
| 它是否会被多个业务模块复用？ | 是 |
| 它是否不依赖具体命令和输出？ | 是 |
| 它是否不依赖 TOML / CSV / Console？ | 是 |
| 它是否偏基础结构或基础能力？ | 是 |
| 它是否适合未来被 Godot 编辑器或其他入口复用？ | 是 |

如果答案大部分是“否”，那它通常不适合进 `Tile.Core`。

## 4.6 `Tile.Core` 放置示例

适合放在 `Tile.Core`：

* `Tile`
* `LevelCore`
* `TileMappingTable`
* `RegionIndex`
* `TileFaceCollector`
* `StagingArea`
* `BitSetOperations`
* `MathKit`
* `UnlockPathFinder`
* `MaxClusterAnalyzer`
* 业务无关的 `SimulationRunner`
* `ISimulationObserver`

不适合放在 `Tile.Core`：

* `TileEvalService`
* `PiKaScorer`
* `TokikiScorer`
* `MetricExportService`
* `BatchRunner`
* `ConfigLoader`
* `CsvResultWriter`

---

## 5. Tile.Services

## 5.1 定位

`Tile.Services` 是业务服务层。

这一层的任务不是发明基础结构，而是把 `Tile.Core` 中的能力组合起来，形成可以直接执行的业务能力。

它主要回答的是：

* 如何执行一次 `tile-eval`？
* 如何组织 finder、scorer、policy、metrics？
* 如何执行批量模拟、批处理和结果聚合？
* 如何把一次运行所需的模块装配成一个完整任务？

这一层允许变化，也应该允许变化。

如果某个模块还处在快速试验期，优先放在 `Tile.Services`，等稳定之后再考虑是否下沉到 `Tile.Core`。

## 5.2 `Tile.Services` 应该包含哪些内容

建议放入 `Tile.Services` 的内容：

* `TileEvalService` 这种面向完整用例的服务入口
* 求解流程编排
* finder / scorer / policy 的具体实现
* 行为组构造与选择策略
* 运行时指标计算与聚合组织
* 批量执行、分片、合并结果
* 面向业务的请求对象、结果对象、服务参数对象
* 模拟器 Builder、组合检查、模块装配逻辑

换句话说，`Tile.Services` 负责“把能力拼成任务”。

## 5.3 `Tile.Services` 不应该包含哪些内容

不建议放进 `Tile.Services` 的内容：

* `Program.cs`
* 命令行参数定义与解析
* `threetile.toml` 的读取与合并
* `tile-eval.metrics.toml` 的读取与输出排序控制
* Console 打印、进度条展示、日志格式化
* 纯基础位运算工具
* 纯数学工具
* 只和某个交互入口相关的输出代码

这些内容都更适合放在 `Tile.CLI` 或 `Tile.Tooling`。

## 5.4 `Tile.Services` 推荐子目录

```text
Tile.Services/
├── Tile.Services.csproj
├── Analysis/
├── Metrics/
├── Scorers/
├── Finders/
└── Simulation/
```

各目录建议如下：

| 目录 | 建议内容 | 说明 |
| --- | --- | --- |
| `Analysis/` | 面向业务场景的分析编排 | 依赖 `Tile.Core.Analysis`，补上与策略、流程、输出有关的分析组织 |
| `Metrics/` | 指标结果结构、指标计算器、指标绑定逻辑 | 面向业务结果的指标体系 |
| `Scorers/` | `PiKaScorer`、`TokikiScorer`、Feature Scorer | 评分逻辑 |
| `Finders/` | 候选查找器、路径查找器、行为组查找器 | 候选生成逻辑 |
| `Simulation/` | 业务相关模拟组装与任务执行入口 | 放策略组装、观察器适配器、指标适配器、Builder、tile-eval 主流程编排 |

这里尤其要强调两层 `Analysis` 和两层 `Simulation` 的边界：

`Tile.Core/Analysis`：

* 放稳定、可复用、和具体策略无关的纯分析能力
* 这些能力一旦成立，不会因为 scorer、finder、policy 的变化而消失

`Tile.Services/Analysis`：

* 放面向具体业务场景的分析编排
* 可以组合多个 Core 分析能力，并把结果接到某个服务流程上

`Tile.Core/Simulation`：

* 放业务无关的模拟抽象与通用主流程
* 例如基础上下文、运行结果、观察器接口、稳定的通用运行器

`Tile.Services/Simulation`：

* 放业务相关的模拟组装
* 例如 finder / scorer / policy 的组合、指标观察器适配、Builder、tile-eval 运行入口

## 5.5 `Tile.Services` 的判断标准

一个类适合放进 `Tile.Services`，通常满足：

| 判断问题 | 应该回答 |
| --- | --- |
| 它是否在组合多个 Core 能力？ | 是 |
| 它是否属于业务流程，而不是基础结构？ | 是 |
| 它是否和 tile-eval / finder / scorer / metrics / simulation 强相关？ | 是 |
| 它是否未来可能继续调整？ | 是 |
| 它是否不该和命令行解析耦合？ | 是 |

## 5.6 `Tile.Services` 放置示例

适合放在 `Tile.Services`：

* `TileEvalService`
* `TileEvalBatchService`
* `PiKaScorer`
* `TokikiScorer`
* `FseFinder`
* `BehaviourGroupBuilder`
* `SolvePolicy`
* `MetricCalculator`
* `ResultMerger`
* 指标观察器适配器
* Simulation Builder

不适合放在 `Tile.Services`：

* `Program.cs`
* `ConfigLoader`
* `CliArgumentBinder`
* `CsvResultWriter`
* `ConsoleReporter`
* `BitSetOperations`
* `MathKit`

---

## 6. Tile.CLI

## 6.1 定位

`Tile.CLI` 是命令行入口层，也是整个项目最靠外的一层。

它不负责核心算法本身，而是负责把“用户输入”变成“服务调用”。

它主要回答的是：

* 命令行怎么定义？
* 参数怎么解析？
* `threetile.toml` 怎么读取？
* preset / profile / CLI 覆写怎么合并？
* 指标配置怎么读取？
* 输出结果按什么顺序写 CSV？
* 控制台日志、错误码、进度如何展示？

## 6.2 `Tile.CLI` 应该包含哪些内容

建议放入 `Tile.CLI` 的内容：

* `Program.cs`
* 命令定义与参数解析
* `threetile.toml` 配置读取
* `tile-eval.metrics.toml` 配置读取
* 默认配置与配置合并
* 请求对象构造
* 调用 `Tile.Services`
* 控制台输出、错误输出、日志输出
* 错误码转换与命令退出码处理

这层是典型的“适配层”。

## 6.3 `Tile.CLI` 不应该包含哪些内容

不建议放进 `Tile.CLI` 的内容：

* `Tile`
* `LevelCore`
* `Pasture`
* `StagingArea`
* `BitSetOperations`
* `MathKit`
* `PiKaScorer`
* `TokikiScorer`
* `FseFinder`
* 指标计算核心
* 求解主流程
* 通用 CSV 原子操作
* 通用进度模型

这些内容要么是核心结构，要么是业务服务，要么是共享工具，不能反向塞进 CLI。

## 6.4 `Tile.CLI` 推荐子目录

```text
Tile.CLI/
├── Tile.CLI.csproj
├── Program.cs
├── Commands/
├── Config/
├── Output/
└── Errors/
```

各目录建议如下：

| 目录 | 建议内容 | 说明 |
| --- | --- | --- |
| `Program.cs` | CLI 启动入口 | 尽量保持薄，只做注册和启动 |
| `Commands/` | `TileEvalCommand`、`MergeCommand`、`ExportMetricsCommand` | 每个命令单独组织 |
| `Config/` | `ConfigLoader`、`ConfigMerger`、默认配置、TOML DTO | 配置系统都在这里 |
| `Output/` | Console 输出、命令层结果展示、CLI 专属输出整理 | 这里不放通用 CSV 原子能力 |
| `Errors/` | 错误码、异常转义、用户提示 | CLI 错误处理层 |

## 6.5 `Tile.CLI` 的判断标准

一个类适合放进 `Tile.CLI`，通常满足：

| 判断问题 | 应该回答 |
| --- | --- |
| 它是否只服务命令行入口？ | 是 |
| 它是否和参数解析有关？ | 是 |
| 它是否和配置读取有关？ | 是 |
| 它是否和控制台输出或退出码有关？ | 是 |
| 它是否不需要被 Core / Services / Godot 复用？ | 是 |

## 6.6 `Tile.CLI` 放置示例

适合放在 `Tile.CLI`：

* `Program.cs`
* `TileEvalCommand`
* `MergeCommand`
* `ConfigLoader`
* `ConfigMerger`
* `MetricConfigLoader`
* `ConsoleReporter`
* `ProgressStyleSelector`

不适合放在 `Tile.CLI`：

* `LevelCore`
* `TileMappingTable`
* `StagingArea`
* `MathKit`
* `BitSetOperations`
* `PiKaScorer`
* `FseFinder`
* `TileEvalService`
* `CsvOperations`
* `IProgressSink`

---

## 7. Tile.Tooling

## 7.1 定位

`Tile.Tooling` 表示被 `Tile.Core`、`Tile.Services`、`Tile.CLI` 都可能复用的通用工具层。

它不是“命令行工具项目”，而是“共享工具项目”。

它负责：

* 通用文件格式支持
* 通用表格处理能力
* 通用进度模型与进度展示包装
* 可复用的工具型脚本支撑能力

它不负责：

* 领域核心结构
* scorer / finder / simulation 这类业务编排
* CLI 参数解析
* 某个命令专属的业务列语义

## 7.2 `Tile.Tooling` 推荐子目录

```text
Tile.Tooling/
├── Tile.Tooling.csproj
├── Csv/
├── Npy/
├── Progress/
└── Scripts/
```

各目录建议如下：

| 目录 | 建议内容 | 说明 |
| --- | --- | --- |
| `Csv/` | 通用 CSV 封装与表格操作 | 基于 `CsvHelper` 提供统一 API，不重写 CSV 解析器 |
| `Npy/` | `.npy` 文件读写与数组转换 | 面向离线数据交换和工具处理 |
| `Progress/` | 通用进度模型与展示包装 | 可以包装现有 C# 进度条库 |
| `Scripts/` | 工具型脚本支撑代码 | 放脚本支持逻辑，不等于仓库根目录 shell 脚本本体 |

## 7.3 `Tile.Tooling` 的判断标准

一个类适合放进 `Tile.Tooling`，通常满足：

| 判断问题 | 应该回答 |
| --- | --- |
| 它是否不是领域核心能力？ | 是 |
| 它是否不是业务编排逻辑？ | 是 |
| 它是否不是 CLI 专属适配？ | 是 |
| 它是否可能被 Core / Services / CLI 共同复用？ | 是 |

## 7.4 `Csv/` 的职责

`Tile.Tooling/Csv` 是通用表格处理能力层。

它负责：

* CSV 的读取与写入
* CSV 的拆分与合并
* 列筛选
* 只输出指定列
* 列排序
* 索引列添加
* 索引列名设置
* 初始编号设置
* 表格序列化
* 列映射

它不负责：

* `tile-eval` 默认输出哪些列
* 某个命令专属的列定义
* CLI 参数解析
* 业务流程编排

### 7.4.1 `Csv/` 的设计原则

`Csv/` 设计上需要满足：

1. 正交性好，提供原子功能，可组合得到完整流程。
2. API 使用便捷。
3. 不自己重写 CSV 解析器，底层统一封装 `CsvHelper`。

也就是：

* `CsvHelper` 负责底层 CSV 解析与写出
* `Tile.Tooling/Csv` 负责项目内统一 API、表格模型、列操作和组合能力
* 上层项目只依赖封装后的统一接口，不直接到处散落 `CsvHelper` 细节

### 7.4.2 `Csv/` 的边界

适合放在 `Csv/`：

* `CsvReader`
* `CsvWriter`
* `CsvTable`
* `CsvColumnMap`
* `SelectColumns`
* `OrderColumns`
* `MergeTables`
* `SplitTable`
* `AddIndexColumn`

不适合放在 `Csv/`：

* `TileEvalDefaultColumns`
* `TileEvalCsvLayout`
* `TileEvalCommandCsvExporter`

这些更适合放在 `Tile.CLI` 或 `Tile.Services` 的业务层。

## 7.5 `Progress/` 的职责

`Tile.Tooling/Progress` 是通用进度反馈层。

它负责：

* 提供多种进度条表现方式
* 提供统一进度接口
* 提供批任务进度模型
* 对现有 C# 进度条库做包装

它的设计目标是：

* `Services` 只发进度事件
* `CLI` 决定怎么展示
* `Tooling.Progress` 负责统一进度模型和展示封装

## 7.6 `Tile.Tooling` 的提醒

`Tile.Tooling` 很容易慢慢变成“杂物箱”，所以要明确约束：

* 只放跨项目复用的通用工具能力
* 不放领域核心
* 不放业务编排
* 不放 CLI 专属逻辑

---

## 8. 四层之间如何理解

可以把四层理解成下面四句话：

### 8.1 `Tile.Core`

“这个项目最底层的事实和能力是什么？”

它提供：

* 基础结构
* 基础分析
* 基础工具
* 业务无关的基础模拟抽象

### 8.2 `Tile.Services`

“如何把这些能力组织成一个具体业务任务？”

它提供：

* 业务流程
* finder / scorer / policy 组合
* 指标和模拟组织
* 完整任务执行入口

### 8.3 `Tile.CLI`

“用户怎样通过命令行真正跑起来？”

它提供：

* 参数
* 配置
* 命令
* 控制台输出
* 错误码

### 8.4 `Tile.Tooling`

“有哪些通用工具能力值得抽出来给所有层复用？”

它提供：

* 通用表格工具
* 通用文件格式支持
* 通用进度工具
* 工具型脚本支撑

---

## 9. 最终推荐目录

```text
Tile-Fast/
├── Tile.sln
├── README.md
├── docs/
│   ├── summary.md
│   └── summary-项目目录设计补充版.md
│
├── src/
│   ├── Tile.Core/
│   │   ├── Tile.Core.csproj
│   │   ├── Core/
│   │   ├── Analysis/
│   │   ├── Simulation/
│   │   └── Common/
│   │
│   ├── Tile.Services/
│   │   ├── Tile.Services.csproj
│   │   ├── Analysis/
│   │   ├── Metrics/
│   │   ├── Scorers/
│   │   ├── Finders/
│   │   └── Simulation/
│   │
│   ├── Tile.CLI/
│   │   ├── Tile.CLI.csproj
│   │   ├── Program.cs
│   │   ├── Commands/
│   │   ├── Config/
│   │   ├── Output/
│   │   └── Errors/
│   │
│   └── Tile.Tooling/
│       ├── Tile.Tooling.csproj
│       ├── Csv/
│       ├── Npy/
│       ├── Progress/
│       └── Scripts/
│
├── tests/
├── benchmarks/
├── samples/
└── scripts/
```

---

## 10. 当前建议的落地原则

为了后续不反复搬目录，建议先按下面原则落地：

* 只要是稳定的结构、静态映射、位集、数学、空间判定、业务无关模拟抽象，优先放 `Tile.Core`。
* 只要是 finder、scorer、policy、metrics、业务相关 simulation 组装，优先放 `Tile.Services`。
* 只要是 TOML、参数、命令、控制台输出、错误码，优先放 `Tile.CLI`。
* 只要是 CSV、NPY、Progress、脚本支撑这类三层都可能复用的工具，优先放 `Tile.Tooling`。
* 如果一个模块还拿不准是否稳定，先放 `Tile.Services`，后续成熟了再下沉。
* `Tile.Core` 宁可保守一点，也不要过早塞入强业务内容。
* `Tile.Tooling` 宁可克制一点，也不要变成杂物箱。

---

## 11. 一句话版结论

`src/Tile.Core/` 放稳定核心能力。  
`src/Tile.Services/` 放完整业务编排。  
`src/Tile.CLI/` 放命令行配置与输出适配。  
`src/Tile.Tooling/` 放跨项目复用的通用工具能力。

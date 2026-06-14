# Tile-Fast 项目结构设计

## 1. 总体目标

`Tile-Fast` 是一个面向 Tile 类关卡的 C# 高性能求解、模拟、评估与命令行工具项目。

项目采用多 C# 项目组成一个解决方案：

```text
Tile-Fast/
├── Tile.sln
├── src/
│   ├── Tile.Core/
│   ├── Tile.Services/
│   └── Tile.CLI/
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

也就是：

* `Tile.Core`：稳定核心层，尽量小，尽量高性能。
* `Tile.Services`：业务服务层，承载会频繁变化的求解、评分、模拟、指标、批处理逻辑。
* `Tile.CLI`：命令行入口层，只负责配置、命令、输出与服务组合。

---

## 2. 顶层目录

| 目录 / 文件       | 职责      | 说明                                                         |
| ------------- | ------- | ---------------------------------------------------------- |
| `Tile.sln`    | C# 解决方案 | 管理 `Tile.Core`、`Tile.Services`、`Tile.CLI`、测试与 benchmark 项目 |
| `README.md`   | 项目入口说明  | 介绍项目用途、快速启动、常用命令                                           |
| `docs/`       | 文档目录    | 架构、核心模块、服务层、模拟、指标、Pasture 等设计文档                            |
| `src/`        | 源码目录    | 放所有正式 C# 项目                                                |
| `tests/`      | 单元测试目录  | 按项目一一对应测试                                                  |
| `benchmarks/` | 性能测试目录  | BenchmarkDotNet 或其他性能测试代码                                  |
| `samples/`    | 示例目录    | 示例配置、示例关卡、示例输入                                             |
| `scripts/`    | 脚本目录    | 批处理、合并结果、归档、服务器运行脚本                                        |

---

## 3. src 项目总览

| 项目              | 类型            | 定位     | 依赖                                   |
| --------------- | ------------- | ------ | ------------------------------------ |
| `Tile.Core`     | Class Library | 稳定核心层  | 不依赖其他 Tile 项目                        |
| `Tile.Services` | Class Library | 业务服务层  | 依赖 `Tile.Core`                       |
| `Tile.CLI`      | Console App   | 命令行入口层 | 依赖 `Tile.Services`，必要时依赖 `Tile.Core` |

推荐依赖方向：

```text
Tile.CLI
   ↓
Tile.Services
   ↓
Tile.Core
```

禁止反向依赖：

```text
Tile.Core     不应该引用 Tile.Services / Tile.CLI
Tile.Services 不应该引用 Tile.CLI
```

---

# 4. Tile.Core

## 4.1 定位

`Tile.Core` 是整个项目的稳定核心层。

它应该包含：

* 关卡基础结构
* Tile 基础数据结构
* 空间与遮挡判定
* 卡槽状态
* 位集工具
* 数学工具
* 模拟基础接口与基础结果结构

它不应该包含：

* 具体命令行逻辑
* TOML / JSON 配置读取
* CSV 批处理
* PiKa / Tokiki 这类强业务评分器
* tile-eval 命令逻辑
* 服务器脚本逻辑
* UI / Godot 编辑器逻辑

## 4.2 Tile.Core 目录表

| 目录            | 建议内容                                                              | 是否稳定 | 说明                           |
| ------------- | ----------------------------------------------------------------- | ---: | ---------------------------- |
| `Tiles/`      | `Tile`、`TilePosition`、`TileVolume`、`TileColor`                    |    高 | Tile 的基础表达，尽量稳定              |
| `Levels/`     | `LevelCore`、`TileStaticMap`、`LevelBounds`                         |    高 | 关卡核心数据、索引映射、静态结构             |
| `Staging/`    | `StagingArea`、卡槽基础规则                                              |    高 | 热路径，尽量零 GC，避免 Hash/Dict/List |
| `Pasture/`    | `Pasture`、`RegionGeometry`、邻居/遮挡/可见性判定                            |   中高 | 空间判定核心，后续稳定后应沉淀在 Core        |
| `BitSets/`    | `BitOps`、`BitSet64`、`UInt64BitIterator`                           |    高 | 位运算、位集遍历、集合操作                |
| `Math/`       | `MathKit`、`Softmax`、`WeightedChoice`、`Sigmoid`、平均值                |    高 | 纯数学工具，不携带业务概念                |
| `Simulation/` | `SimulationContext`、`SimulationBatchResult`、`ISimulationObserver` |    中 | 只放模拟基础抽象，不放具体策略              |
| `Common/` 可选  | 通用枚举、Guard、Result 类型                                              |    中 | 只有确实通用时再放，不要变成杂物箱            |

## 4.3 Tile.Core 判断标准

一个类适合放进 `Tile.Core`，需要尽量满足：

| 判断问题                                 | 应该回答 |
| ------------------------------------ | ---- |
| 它是否稳定？                               | 是    |
| 它是否多个业务都会用？                          | 是    |
| 它是否和具体 scorer / finder / command 无关？ | 是    |
| 它是否是热路径的一部分？                         | 通常是  |
| 它是否可以不依赖配置文件和 IO？                    | 是    |
| 它是否适合被 Godot 编辑器直接复用？                | 是    |

## 4.4 Tile.Core 不建议放的内容

| 内容                | 原因      | 建议放置                                        |
| ----------------- | ------- | ------------------------------------------- |
| `TileEvalService` | 强业务服务   | `Tile.Services/TileEval/`                   |
| `PiKaScorer`      | 业务评分器   | `Tile.Services/Scorers/`                    |
| `TokikiScorer`    | 业务评分器   | `Tile.Services/Scorers/`                    |
| `MetricBinder`    | 服务层聚合逻辑 | `Tile.Services/Metrics/`                    |
| `BatchRunner`     | 批处理业务   | `Tile.Services/Batch/`                      |
| `ConfigLoader`    | 配置读取    | `Tile.CLI/Config/`                          |
| `CsvWriter`       | 输出实现    | `Tile.CLI/Output/` 或 `Tile.Services/Batch/` |

---

# 5. Tile.Services

## 5.1 定位

`Tile.Services` 是业务服务层。

它负责组合 `Tile.Core` 的基础能力，形成具体业务能力：

* tile-eval 评估
* 求解策略
* 行为生成
* FSE 搜索
* scorer 评分
* finder 候选查找
* metrics 指标计算
* 批处理和分片执行
* 结果聚合

这一层允许变化。

如果某个模块还在快速迭代，优先放在 `Tile.Services`，不要过早沉淀到 `Tile.Core`。

## 5.2 Tile.Services 目录表

| 目录                     | 建议内容                                                 | 稳定性 | 说明                        |
| ---------------------- | ---------------------------------------------------- | --: | ------------------------- |
| `TileEval/`            | `TileEvalService`、`TileEvalOptions`、`TileEvalResult` |   中 | tile-eval 主业务入口           |
| `Solving/`             | 求解流程、策略组合、行为选择                                       |  中低 | 业务变化较多，适合放 Services       |
| `Solving/Behaviours/`  | `Behaviour`、`BehaviourGroup`、行为构造                    |  中低 | 候选行为表达，可能频繁调整             |
| `Solving/Policies/`    | `ISolvePolicy`、softmax 选择策略、剪枝策略                     |  中低 | 策略层，容易变化                  |
| `Solving/Analysers/`   | `FailRateAnalyser`、`LevelInfoAnalyser`、可解性分析器        |  中低 | 业务分析逻辑                    |
| `Fse/`                 | `FseFinder`、`FseContext`、`FsePick`、`FseBuffers`      |   中 | 如果未来稳定，可考虑部分下沉 Core       |
| `Metrics/`             | `MetricBag`、`MetricBinder`、指标名称、指标计算器                |   中 | 指标体系和业务输出强相关              |
| `Metrics/Calculators/` | 单个指标计算器                                              |  中低 | 每个指标独立、纯计算、便于增删           |
| `Scorers/`             | `PiKaScorer`、`TokikiScorer`、特征评分器                    |  中低 | 评分业务变化快                   |
| `Finders/`             | 候选查找器、路径查找器、簇查找器                                     |  中低 | 候选生成策略变化快                 |
| `Batch/`               | `BatchRunner`、`ShardRunner`、`ResultMerger`           |   中 | 批量评估、分片、合并结果              |
| `IO/` 可选               | CSV 读取、结果文件写入                                        |   中 | 如果 CLI 和 Service 都需要，可放这里 |

## 5.3 Tile.Services 判断标准

一个类适合放进 `Tile.Services`，通常满足：

| 判断问题                                           | 应该回答 |
| ---------------------------------------------- | ---- |
| 它是否会经常改？                                       | 是    |
| 它是否组合多个 Core 能力？                               | 是    |
| 它是否和 tile-eval / scorer / finder / metrics 有关？ | 是    |
| 它是否属于业务流程，而不是基础数据结构？                           | 是    |
| 它是否可能被 CLI 和 Godot 编辑器共同调用？                    | 是    |

## 5.4 推荐子结构

```text
Tile.Services/
├── TileEval/
│   ├── TileEvalService.cs
│   ├── TileEvalOptions.cs
│   └── TileEvalResult.cs
│
├── Solving/
│   ├── Behaviours/
│   ├── Policies/
│   ├── Analysers/
│   └── Collectors/
│
├── Fse/
│   ├── FseFinder.cs
│   ├── FseContext.cs
│   ├── FsePick.cs
│   └── FseBuffers.cs
│
├── Metrics/
│   ├── MetricBag.cs
│   ├── TileEvalMetricNames.cs
│   ├── TileEvalMetricBinder.cs
│   └── Calculators/
│
├── Scorers/
│   ├── IScorer.cs
│   ├── PiKaScorer.cs
│   └── TokikiScorer.cs
│
├── Finders/
│   ├── IBehaviourFinder.cs
│   ├── FseBehaviourFinder.cs
│   └── UnlockPathFinder.cs
│
└── Batch/
    ├── BatchRunner.cs
    ├── ShardRunner.cs
    └── ResultMerger.cs
```

## 5.5 Tile.Services 不建议放的内容

| 内容            | 原因      | 建议放置                 |
| ------------- | ------- | -------------------- |
| `Program.cs`  | CLI 入口  | `Tile.CLI/`          |
| 命令行参数解析       | CLI 责任  | `Tile.CLI/Commands/` |
| TOML 配置加载     | CLI 责任  | `Tile.CLI/Config/`   |
| 纯位运算工具        | 太基础     | `Tile.Core/BitSets/` |
| 纯数学工具         | 太基础     | `Tile.Core/Math/`    |
| Godot 场景 / 脚本 | 编辑器项目责任 | Godot 项目仓库           |

---

# 6. Tile.CLI

## 6.1 定位

`Tile.CLI` 是命令行入口层。

它负责：

* 解析命令行参数
* 加载配置文件
* 合并默认配置、配置文件、命令行覆写
* 创建 Services
* 调用业务服务
* 打印进度
* 输出 CSV / 日志 / 结果
* 处理错误码

它不负责：

* 具体求解算法
* 具体评分逻辑
* 具体候选生成算法
* 具体 Pasture / Staging / BitSet 实现
* 指标计算细节

## 6.2 Tile.CLI 目录表

| 目录 / 文件           | 建议内容                                                   | 稳定性 | 说明                         |
| ----------------- | ------------------------------------------------------ | --: | -------------------------- |
| `Program.cs`      | CLI 程序入口                                               |   中 | 只做 Host / Command 注册，不写重业务 |
| `Commands/`       | `TileEvalCommand`、`MergeCommand`、`ExportConfigCommand` |   中 | 每个命令一个文件                   |
| `Config/`         | `ConfigLoader`、`TileEvalConfig`、默认配置、配置合并              |   中 | 配置读取与命令行覆写                 |
| `Output/`         | `ConsoleReporter`、`CsvResultWriter`、`ProgressReporter` |   中 | 控制台、CSV、日志输出               |
| `Errors/` 可选      | 错误码、异常转换、错误打印                                          |   中 | 如果 CLI 错误处理变复杂，可以单独拆       |
| `Composition/` 可选 | Service 创建、依赖装配                                        |   中 | 如果 Program.cs 变胖，可拆出来      |

## 6.3 Tile.CLI 判断标准

一个类适合放进 `Tile.CLI`，通常满足：

| 判断问题                 | 应该回答 |
| -------------------- | ---- |
| 它是否只服务命令行？           | 是    |
| 它是否和参数解析有关？          | 是    |
| 它是否和配置文件加载有关？        | 是    |
| 它是否和控制台输出有关？         | 是    |
| 它是否可以不被 Godot 编辑器复用？ | 是    |

## 6.4 推荐子结构

```text
Tile.CLI/
├── Tile.CLI.csproj
├── Program.cs
│
├── Commands/
│   ├── TileEvalCommand.cs
│   ├── MergeCommand.cs
│   └── ExportConfigCommand.cs
│
├── Config/
│   ├── TileConfig.cs
│   ├── TileEvalConfig.cs
│   ├── ConfigLoader.cs
│   ├── ConfigMerger.cs
│   └── DefaultConfigProvider.cs
│
├── Output/
│   ├── ConsoleReporter.cs
│   ├── CsvResultWriter.cs
│   ├── ProgressReporter.cs
│   └── RunSummaryWriter.cs
│
└── Composition/
    └── ServiceFactory.cs
```

## 6.5 Tile.CLI 不建议放的内容

| 内容                 | 原因     | 建议放置                                            |
| ------------------ | ------ | ----------------------------------------------- |
| `Pasture`          | 核心空间判定 | `Tile.Core/Pasture/`                            |
| `StagingArea`      | 核心卡槽状态 | `Tile.Core/Staging/`                            |
| `PiKaScorer`       | 业务评分器  | `Tile.Services/Scorers/`                        |
| `FseFinder`        | 业务搜索器  | `Tile.Services/Fse/` 或 `Tile.Services/Finders/` |
| `MetricCalculator` | 指标计算   | `Tile.Services/Metrics/Calculators/`            |
| `BatchRunner`      | 批处理执行  | `Tile.Services/Batch/`                          |

---

# 7. tests 目录

测试项目按源码项目一一对应：

```text
tests/
├── Tile.Core.Tests/
├── Tile.Services.Tests/
└── Tile.CLI.Tests/
```

| 测试项目                  | 测试对象            | 重点                            |
| --------------------- | --------------- | ----------------------------- |
| `Tile.Core.Tests`     | `Tile.Core`     | 数据结构、位运算、卡槽、Pasture、数学工具      |
| `Tile.Services.Tests` | `Tile.Services` | 求解策略、FSE、Scorer、Metrics、Batch |
| `Tile.CLI.Tests`      | `Tile.CLI`      | 参数解析、配置合并、命令执行、输出格式           |

推荐原则：

* Core 测试最多、最稳定。
* Services 测试覆盖业务场景。
* CLI 测试只测入口行为，不重复测算法细节。

---

# 8. benchmarks 目录

性能测试单独放：

```text
benchmarks/
└── Tile.Benchmarks/
```

建议包含：

| Benchmark               | 关注点                          |
| ----------------------- | ---------------------------- |
| `BitSetBenchmarks`      | 位集遍历、集合操作、NextSetBit         |
| `StagingAreaBenchmarks` | Add / Remove / Match / Clone |
| `PastureBenchmarks`     | 可见性刷新、邻居查找、遮挡判定              |
| `FseBenchmarks`         | 候选搜索、pick 组合、buffer 复用       |
| `SimulationBenchmarks`  | 单局模拟、批量模拟、分配情况               |
| `ScorerBenchmarks`      | scorer 评分热路径                 |

Benchmark 只回答性能问题，不替代单元测试。

---

# 9. samples 目录

```text
samples/
├── configs/
└── levels/
```

| 目录                 | 内容         |
| ------------------ | ---------- |
| `samples/configs/` | 示例 TOML 配置 |
| `samples/levels/`  | 示例关卡输入     |

示例配置建议包含：

```text
samples/configs/
├── tile-eval.pair.sample.toml
├── tile-eval.triple.sample.toml
└── batch.sample.toml
```

---

# 10. scripts 目录

```text
scripts/
├── run-tile-eval.sh
├── merge-result.sh
└── archive-result.sh
```

| 脚本                  | 作用                        |
| ------------------- | ------------------------- |
| `run-tile-eval.sh`  | 批量运行 tile-eval            |
| `merge-result.sh`   | 合并 Result 目录下的 CSV        |
| `archive-result.sh` | 打包归档 Result / Error / Log |

脚本只负责操作流程，不应该隐藏核心业务逻辑。

---

# 11. 命名约定

## 11.1 解决方案与项目名

| 类型       | 名称                    |
| -------- | --------------------- |
| 仓库 / 根目录 | `Tile-Fast`           |
| 解决方案     | `Tile.sln`            |
| 核心项目     | `Tile.Core`           |
| 服务项目     | `Tile.Services`       |
| 命令行项目    | `Tile.CLI`            |
| 核心测试     | `Tile.Core.Tests`     |
| 服务测试     | `Tile.Services.Tests` |
| CLI 测试   | `Tile.CLI.Tests`      |
| 性能测试     | `Tile.Benchmarks`     |

## 11.2 命名空间

建议命名空间与项目名保持一致：

```csharp
namespace Tile.Core;
namespace Tile.Services;
namespace Tile.CLI;
```

子模块命名：

```csharp
namespace Tile.Core.Pasture;
namespace Tile.Core.Staging;
namespace Tile.Services.TileEval;
namespace Tile.Services.Metrics;
namespace Tile.CLI.Commands;
```

---

# 12. 最终推荐目录

```text
Tile-Fast/
├── Tile.sln
├── README.md
├── docs/
│   ├── Architecture.md
│   ├── Core.md
│   ├── Services.md
│   ├── Simulation.md
│   ├── Metrics.md
│   └── Pasture.md
│
├── src/
│   ├── Tile.Core/
│   │   ├── Tile.Core.csproj
│   │   ├── Tiles/
│   │   ├── Levels/
│   │   ├── Staging/
│   │   ├── Pasture/
│   │   ├── BitSets/
│   │   ├── Math/
│   │   └── Simulation/
│   │
│   ├── Tile.Services/
│   │   ├── Tile.Services.csproj
│   │   ├── TileEval/
│   │   ├── Solving/
│   │   ├── Fse/
│   │   ├── Metrics/
│   │   ├── Scorers/
│   │   ├── Finders/
│   │   └── Batch/
│   │
│   └── Tile.CLI/
│       ├── Tile.CLI.csproj
│       ├── Program.cs
│       ├── Commands/
│       ├── Config/
│       └── Output/
│
├── tests/
│   ├── Tile.Core.Tests/
│   ├── Tile.Services.Tests/
│   └── Tile.CLI.Tests/
│
├── benchmarks/
│   └── Tile.Benchmarks/
│
├── samples/
│   ├── configs/
│   └── levels/
│
└── scripts/
    ├── run-tile-eval.sh
    ├── merge-result.sh
    └── archive-result.sh
```

---

# 13. 当前阶段建议

当前阶段不要拆太碎。

优先固定这三个项目：

```text
Tile.Core
Tile.Services
Tile.CLI
```

之后再根据稳定程度调整：

| 情况               | 操作                          |
| ---------------- | --------------------------- |
| Services 中某块长期稳定 | 可以下沉到 Core                  |
| Core 中某块频繁变化     | 上移到 Services                |
| CLI 中业务逻辑变多      | 抽到 Services                 |
| Godot 编辑器也需要调用   | 优先放 Core 或 Services，不要放 CLI |
| 性能热点需要验证         | 加到 Benchmarks               |

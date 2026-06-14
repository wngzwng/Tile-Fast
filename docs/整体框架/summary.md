下面是整个会话的简版总结，只保留最终方向，不展开细节。
1. CLI 配置体系
最终确定：
默认配置 + TOML 配置 + CLI 参数覆写 => 生效配置

配置文件使用：
TOML
Tomlyn

主配置文件：
threetile.toml

核心原则：
配置文件只写稳定、常用、需要复用的参数；
命令行参数负责本次临时变化；
CLI 层负责读取配置、合并配置、生成生效配置。

tile-eval 里：
preset：
  Pair / Triple 这种规则预设

profile：
  local-test / pika-t025 这种运行场景


2. 项目分层
最终倾向拆成三个项目：
ThreeTile.Core
ThreeTile.Services
ThreeTile.CLI

依赖方向：
ThreeTile.CLI
      ↓
ThreeTile.Services
      ↓
ThreeTile.Core

职责：
Core：
  提供核心能力。

Services：
  编排完整任务。

CLI：
  处理配置、命令、输入输出。


3. ThreeTile.Core 的职责
Core 项目里放真正的业务能力：
Core：
  关卡核心结构。

Common：
  通用底层工具。

Analysis：
  复杂分析能力。

Metrics：
  指标定义与指标计算器。

Simulation：
  模拟器能力。

Difficulty：
  难度计算能力。

Core 不关心：
TOML
命令行
CSV
文件路径
大文件分片
控制台输出


4. Simulation 模拟体系
模拟器是组合出来的：
候选组寻找方式
+
候选组打分方式
+
选择策略
+
规则环境
= 具体模拟器

候选组寻找和打分分离：
CandidateFinding：
  FSE、解锁路径、组合寻找方式等。

CandidateScoring：
  PiKa、Tokiki、Feature 等打分方式。

Strategies：
  选择策略。

Builders：
  构建具体模拟器，并检查组合兼容性。

Simulation 负责：
给定 finder、scorer、总模拟次数、最大成功次数、指标器；
执行模拟；
收集指标依赖；
调用指标器；
返回结果。

Simulation 不负责：
哪些指标输出；
指标输出顺序；
TOML 配置；
CSV 写入。


5. 指标体系
最终确定：
Metrics 层只有指标。

每组指标由三件套组成：
1. 指标名映射
2. 指标结果结构体
3. 指标计算器

例如：
LevelResultMetrics.cs
  ├── LevelResultMetricNames
  ├── LevelResultMetrics
  └── LevelResultMetricCalculator

原则：
谁计算，谁声明指标名；
不要建巨大指标名总表；
指标结构体提供 ToString 和 ToDict；
指标计算器尽量无状态。

暂时不知道怎么分类的指标：
misc

例如：
LevelMiscMetrics.cs
SolveMiscMetrics.cs


6. 指标配置文件
指标输出配置单独成文件：
tile-eval.metrics.toml

核心结构：
groups：
  管怎么算，指标属于哪个计算组。

outputs.order：
  管怎么排，最终输出顺序。

但这个配置属于：
CLI 层

不属于 Core / Metrics / Simulation。
CLI 负责：
读取 tile-eval.metrics.toml；
决定启用哪些指标器；
决定输出哪些指标；
决定输出顺序；
写 CSV。


7. 指标配置导出
因为每个指标计算器都声明：
Scope
Group
MetricNames

所以可以通过命令自动导出指标配置模板：
./ThreeTile.CLI tile-eval metrics export \
  --output ./tile-eval.metrics.toml

导出依据来自：
指标计算器本身

用户拿到后可以：
注释不需要的指标；
调整 outputs.order；
保留自己需要的 group。


8. Services 层
Services 是用例编排层。
主要包括：
TileEvalService：
  执行一次完整 tile-eval。

TileEvalBatchService：
  大文件分片、计算、合并。

MetricCatalog / MetricExportService：
  汇总可用指标器，辅助导出指标配置。

大文件分片、计算、合并属于：
Services 层

不是 Core，也不是 Simulation。

9. CLI 层
CLI 负责所有外部适配：
命令行参数
TOML 配置
配置合并
文件路径
CSV 输出
控制台日志
指标输出顺序
指标配置导出

CLI 不应该直接塞满业务编排细节，而是：
读取配置
组装 Request
调用 Services
输出结果


10. Core 基础工具归类
基础工具最终分为三块：
Core/Encoding：
  业务编码工具。

Common/Bits：
  位集合 / 位运算工具。

Common/Math：
  数学工具。

目录大致是：
Core/Encoding
  PositionPacker
  PositionExtensions
  TileCharCodec
  TileCharExtensions

Common/Bits
  BitOps32
  BitOps64
  UInt32BitIterator
  UInt64BitIterator

Common/Math
  MathKit
  MathKit.Softmax
  MathKit.WeightedChoice
  MathKit.Distance
  MathKitExtensions


11. MathKit 设计
数学工具统一叫：
MathKit

不用：
MathOps
MathUtil
MathHelper
NumericGuard

MathKit 用 partial 文件组织：
MathKit.cs
MathKit.Softmax.cs
MathKit.WeightedChoice.cs
MathKit.Distance.cs
MathKitExtensions.cs

支持两种调用：
scores.Softmax().WeightedChoice(random);

以及高性能版本：
scores
    .Softmax(weights, temperature: 0.6)
    .WeightedChoice(random);

其中：
Softmax 可以写入外部 destination；
Span 可以链式传递；
但不能返回方法内部创建的 stackalloc。


12. 最终总原则
整个设计的最终核心是：
Core 提供能力；
Simulation 完成计算；
Services 编排任务；
CLI 处理配置和输入输出。

再浓缩一点：
Metrics 只定义指标；单个指标与聚合指标一起，
名字映射，结构体定义，指标计算
Simulation 只负责跑模拟和调用指标器；
手动做 指标到模拟的的薄适配器
Services 负责任务级编排；
CLI 负责配置、筛选和输出。


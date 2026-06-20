# 模型二 47 特征轨迹采集工具(TrajectoryFeatures)

把每道题的多条轨迹(每条每步 47 个特征)存成 tar(一题一 tar),供模型二 47 维模型评估新题。纯 .NET 标准库、零第三方依赖:把 `tools/csharp/TrajectoryCollection/` 下 4 个 `.cs`(`TrajectoryFeatures` / `NpyWriter` / `UstarTarWriter` / `ByteBuffer`)并入你的采集项目即可(命名空间 `G42.TrajectoryCollection`)。手写 `.npy` + USTAR、不挑 .NET 版本;net8.0 已验证,产物与 numpy / GNU tar 字节级互通。

## 快速用法

每道题 new 一个实例,边采集边喂,最后存:

```csharp
var c = new TrajectoryFeatures(puzzleId, outputPath);
foreach (var traj in trajectoriesOfThisPuzzle)
{
    foreach (var step in traj.Steps)
    {
        var f = new StepFeatures();
        f.candidate_layer = ...;   // fill all 47 fields by name
        // ... the other 46 ...
        c.AddStepFeatures(in f);
    }
    c.NewTrajectory();   // end one trajectory
}
c.SaveTar();             // flush this puzzle's tar
```

## API

- `new TrajectoryFeatures(string puzzleId, string outputPath, int maxTrajectoriesPerPuzzle = int.MaxValue, bool gzip = false)`
- `void AddStepFeatures(in StepFeatures step)`:追加当前轨迹的一步。
- `void NewTrajectory()`:结束当前轨迹(序列化成一个 `.npy` entry);步数为 0 抛异常。
- `void SaveTar()`:写出该题 tar(临时文件 + 原子 rename);每个实例只调一次。

## StepFeatures 的 47 个字段

`StepFeatures` 是个 struct,47 个 `public float` 字段,**按字段名填即可、不用关心顺序**(内部按固定列序写盘)。字段名与含义见 `TrajectoryFeatures.cs`(每个字段带 `col` 与 `#编号` 注释)。

**重要:这 47 个字段名必须和模型二一致、不要改名/改顺序**——它们是同事↔模型二的对齐契约(对应模型二 `feature_subset.py` 的 SUB47 白名单,由 schema 派生)。

## 输出

- 一题一 tar:`{outputPath}/{bucket}/{puzzleId}.tar`,`bucket` = puzzleId 稳定哈希 % 1000(三位,分桶避免单目录文件过多)。
- tar 内每条轨迹一个 entry `0.npy` / `1.npy` / ...:`[steps, 47]` float32、C-order、小端。无 json。
- Python 侧 `numpy.load` / `tarfile` 可直接读。

## 配置

- `maxTrajectoriesPerPuzzle`:每题只存前 N 条轨迹(默认不限;减量 / 测试时调小)。
- `gzip`:默认 `false`;`true` 则输出 `.tar.gz`(省空间,但写入耗 CPU、会和特征计算抢核)。

## 注意事项

- **并发**:每道题用一个独立实例,无共享状态、线程安全;多题并行请在你这边开线程(本工具内部不开线程)。
- **puzzleId 须为 ASCII**(你们的 puzzle_id 是大整数、天然满足):分桶哈希按字节计算,非 ASCII id 的桶路径不保证跨语言一致。
- **fail-fast**:特征含 NaN/Inf、空轨迹 `NewTrajectory`、漏调 `NewTrajectory` 就 `SaveTar`、`SaveTar` 后再用、0 条轨迹 `SaveTar`——都会抛带定位信息的异常(跑长任务时宁可崩、不要写脏数据)。
- 详细设计见 `docs/plans/2026-06-18-model2-47feature-csharp-collector-design.md`。

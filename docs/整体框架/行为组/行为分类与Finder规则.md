# 行为分类与 Finder 规则

本文整理 `BehaviourCandidateFinder` 的行为生成规则。

## 行为分类

### 简单消除

当前盘面下，某花色的原始可选棋子数量，加上卡槽内该花色已有数量，能够满足一组消除时，生成简单消除行为。

等价条件：

```text
clearNeedCount = MatchRequireCount - SlotMap[suit]
(OriginSelectableBits & Mapping.GetTileBitsBySuit(suit)).Count >= clearNeedCount
```

简单消除只由原始可选棋子构成，`VisibleCount = 0`。

### 困难消除

困难消除由原始可选棋子和原始可见棋子共同构成。

约束：

```text
1. 行为中至少包含一张原始可选棋子。
2. 其余需要解锁的棋子必须在原始盘面可见。
3. 解锁这些可见棋子的成本只能来自当前消除组内部，不引入其他花色或其他额外成本。
4. 记录被纳入行为的原始可见、非原始可选棋子数量，即 VisibleCount。
```

实现上，困难消除通过 F/S/E 三组进行 DFS 展开：

```text
F = Fixed
    已确认纳入当前行为链路的棋子。

S = Selectable
    当前可直接补入行为的同花色棋子。

E = Expanded
    上一次展开后新得到的同花色候选。
```

选取规则：

```text
固定组全部选取。
展开组至少选取一张。
剩余数量从可选组补足。

FixedCount + ExpandedCount + SelectableCount >= ClearNeedCount
```

推进规则：

```text
只要 FixedCount + 1 < ClearNeedCount，就允许继续从 Expanded 中选择一张向下展开。

展开某张 e 后：
new F = F + e
new S = S + (E - e)
new E = 移除 F + e 后变为 selectable 的同花色原始可见棋子
```

如果 `new E` 为空，则该路径停止展开。

### 翻牌

没有被任何消除行为覆盖的原始可选棋子，会生成翻牌行为。

翻牌条件：

```text
1. 当前卡槽仍有可用容量。
2. 该 tile 属于 OriginSelectableGroup。
3. 该 tile 未出现在任何 EasyClear / HardClear 行为中。
```

## Finder 数据口径

FSE 搜索器位于：

```text
src/Tile.Core/Simulation/Finders/FseFinder.cs
```

它只依赖 `LevelCore`，通过 `Func<Behaviour>` / `Action<Behaviour>` 回调输出行为。

Simulation 使用的适配层位于：

```text
src/Tile.Core/Simulation/Finders/BehaviourCandidateFinder.cs
```

它只负责把 FSE 生成的 `Behaviour` 放入 Simulation 候选集合，不承载 FSE 搜索规则。

主要输入：

```text
SlotMap[suit]
    当前卡槽内各花色数量。

AvailableCapacity
    当前卡槽剩余容量。

OriginSelectableGroup
    当前盘面的原始可选棋子集合。

SelectableSuitBits
    当前盘面可选棋子的花色集合，用于决定哪些花色进入 FSE 搜索。

OriginSelectableBits
    当前盘面的原始可选棋子 bitset。

Mapping.GetTileBitsBySuit(suit)
    指定花色在当前关卡中的全量棋子 bitset。

MatchRequireCount
    一组消除需要的棋子数量。

clearNeedCount[suit]
    MatchRequireCount - SlotMap[suit]。
```

某个花色的原始可选组不在 `FseBoard` 中缓存，而是在进入单色搜索时现场计算：

```text
OriginSelectableBits & Mapping.GetTileBitsBySuit(suit)
```

剪枝：

```text
clearNeedCount <= 0 跳过。
clearNeedCount > AvailableCapacity 跳过。
```

当前实现只处理普通卡槽语义：行为最终会转成按顺序执行的 `SelectMove` 集合。

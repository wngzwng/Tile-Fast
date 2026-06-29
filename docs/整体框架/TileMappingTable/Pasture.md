# Pasture 盘面状态设计

## 1. 定位

`Pasture` 表示关卡中仍留在盘面上的 Tile 区域。

它不负责保存 Tile 的静态空间事实，也不负责求解策略。它只维护运行时动态状态：

```text
present     当前仍在场的棋子
visible     当前可见的棋子
selectable  当前可选的棋子
```

对应代码中的三组 bitset：

```csharp
private readonly ulong[] _present;
private readonly ulong[] _visible;
private readonly ulong[] _selectable;
```

## 2. 职责边界

`Pasture` 应该负责：

- 维护在场状态。
- 维护可见状态。
- 维护可选状态。
- 在棋子被拿起或放回后刷新受影响的棋子。
- 提供 `IsPresent` / `IsVisible` / `IsSelectable` 查询。

`Pasture` 不应该负责：

- Tile 静态位置映射。
- position 到 regionId 的转换。
- 邻居面坐标收集。
- 上方依赖闭包的预计算。
- 求解策略、评分、批处理。

这些几何与静态映射能力应沉到 `TileMappingTable` 或稳定的静态几何工具中。

## 3. 公开语义

### 3.1 状态查询

```csharp
public TileIndexSet PresentTiles { get; }

public TileIndexSet VisibleTiles { get; }

public TileIndexSet SelectableTiles { get; }

public bool IsPresent(int tileIndex);

public bool IsVisible(int tileIndex);

public bool IsSelectable(int tileIndex);
```

### 3.2 状态动作

推荐使用：

```csharp
public void Lift(int tileIndex);

public void Place(int tileIndex);
```

语义：

```text
Lift   从 Pasture 上拿起一张棋子，使其不再 present。
Place  把棋子放回 Pasture，使其重新 present。
```

这两个方法只表达 Tile 与 Pasture 的动态归属变化。

典型流程：

```csharp
level.Pasture.Lift(tileIndex);
level.StagingArea.Enter(tileIndex);

// Undo
level.StagingArea.Leave(tileIndex);
level.Pasture.Place(tileIndex);
```

## 4. 可见与可选规则

`Pasture` 的核心判断基于暴露面积。

当前 Tile 默认体积为：

```text
dx = 2
dy = 2
dz = 1
```

因此顶面有 4 个单位面积。

### 4.1 Tile 模式

Tile 模式只看上方遮挡。

规则：

```text
暴露面积 = 0  -> 不可见，不可选
暴露面积 > 0  -> 可见
暴露面积 = 4  -> 可选
```

也就是：

```csharp
visible = exposedArea > 0;
selectable = exposedArea == 4;
```

注意：这里的 `exposedArea` 应表示未被上方 Tile 遮挡的顶面面积，而不是被遮挡面积。

### 4.2 Classic 模式

Classic 模式在 Tile 模式基础上增加左右锁定规则。

规则：

```text
Classic 可选 =
    Tile 模式可选
    &&
    左右直接邻居至少空一个
```

也就是：

```csharp
selectable =
    exposedArea == 4
    &&
    (leftNeighborEmpty || rightNeighborEmpty);
```

Classic 模式下：

- 只要左右两侧都存在直接邻居，则不可选。
- 左侧为空或右侧为空，且顶面完全暴露，则可选。

## 5. 刷新流程

### 5.1 全量重建

`Pasture` 初始化或重置时，应全量构建：

```text
1. _present 设置为所有 Tile。
2. 清空 _visible。
3. 清空 _selectable。
4. 遍历所有 present Tile。
5. 根据规则重新计算 visible / selectable。
```

建议方法：

```csharp
private void Rebuild();

private void RefreshOne(int tileIndex);
```

### 5.2 局部刷新

`Lift(tileIndex)` 或 `Place(tileIndex)` 后，不需要全量刷新。

推荐流程：

```text
1. 修改 _present。
2. 收集受影响的 Tile。
3. 从 _visible / _selectable 中清除这些 Tile。
4. 对每个仍 present 的受影响 Tile 重新计算状态。
```

建议方法：

```csharp
private void RefreshAffected(int tileIndex);
```

受影响集合由 `Pasture` 基于当前 `_present`、上下遮挡关系和锁定规则即时收集。

## 6. Pasture 的当前在场邻居查询

### 6.1 分层原则

邻居关系现在收敛到 `Pasture` 内部计算：

```text
TileMappingTable  只提供 position -> tileIndex 等静态空间索引
Pasture           基于静态索引和 _present 计算当前在场邻居
```

`TileMappingTable` 不知道 `_present`，也不再提供静态邻居候选 API。

`Pasture` 在查询邻居时直接完成两件事：

```text
1. 按方向推导需要检查的空间坐标。
2. 通过 TileMappingTable 的位置索引查命中的 Tile。
3. 结合 _present / ignoredTileBits 过滤掉当前不在场的 Tile。
```

一句话：

```text
TileMappingTable 回答：这个坐标上是谁？
Pasture 回答：这个方向上现在还有没有有效邻居？
```

这样可以避免在 `TileMappingTable` 中沉淀一套只服务当前盘面语义的邻居 API，也避免静态候选和动态在场过滤在两个对象之间来回传递。

### 6.2 当前在场邻居定义

给定一个 Tile 和一个方向，当前在场邻居查询的语义是：

```text
1. 取 Tile 在指定方向上的面坐标组。
2. 将这一组面坐标整体向指定方向推进一格。
3. 查询这些坐标上命中的 Tile。
4. 去重。
5. 只保留当前有效在场的 Tile。
```

其中“有效在场”由 `Pasture` 判断：

```csharp
private bool IsEffectivelyPresent(
    int tileIndex,
    ReadOnlySpan<ulong> ignoredTileBits);
```

它同时服务真实盘面查询和模拟查询：

```text
真实盘面：只看 _present。
模拟盘面：看 _present，并额外排除 ignoredTileBits。
```

例如左邻居：

```text
取 Tile 左侧面
向 x - 方向推进一格
查找命中的 Tile
```

右邻居：

```text
取 Tile 右侧面
向 x + 方向推进一格
查找命中的 Tile
```

上方邻居：

```text
取 Tile 顶面
向 z + 方向推进一格
查找命中的 Tile
```

### 6.3 Pasture 推荐 API

`Pasture` 提供当前在场邻居查询：

```csharp
public bool HasPresentNeighbor(
    int tileIndex,
    NeighborDir dir);

public int GetPresentNeighbors(
    int tileIndex,
    NeighborDir dir,
    Span<int> buffer);
```

其中核心方法是 `GetPresentNeighbors`。

语义：

```text
把指定方向上当前仍有效在场的邻居去重后写入 buffer。
返回写入数量。
```

约束：

- 不分配 `List`。
- 不返回 `IEnumerable`。
- 不在热路径使用 LINQ。
- 必须判断 `_present`。
- 不判断 `Visible`。
- 不判断 `Selectable`。
- 默认 Tile 体积下，单方向邻居数量通常很小，适合 `stackalloc`。

### 6.4 GetPresentNeighbors 实现思路

不要使用 `EnumerateNeighborFacePositions` / `yield return` 这类枚举器式实现。

推荐在 `Pasture` 内分成两步：

```text
GetNeighborCandidates  负责按方向查坐标命中的候选 Tile
GetPresentNeighbors   负责用 IsEffectivelyPresent 过滤候选
```

示意：

```csharp
private int GetPresentNeighbors(
    int tileIndex,
    NeighborDir dir,
    ReadOnlySpan<ulong> ignoredTileBits,
    Span<int> buffer)
{
    ValidateTileIndex(tileIndex);
    ValidateIgnoredTileBits(ignoredTileBits);

    Span<int> candidates = stackalloc int[4];

    var candidateCount = GetNeighborCandidates(
        tileIndex,
        dir,
        candidates);

    var count = 0;

    for (var i = 0; i < candidateCount; i++)
    {
        var candidate = candidates[i];

        if (!IsEffectivelyPresent(candidate, ignoredTileBits))
            continue;

        buffer[count++] = candidate;
    }

    return count;
}
```

候选收集仍然按方向展开，并通过 `_mapping.TryGetTileIndexAtPosition` 查询坐标命中：

```csharp
private int GetNeighborCandidates(
    int tileIndex,
    NeighborDir dir,
    Span<int> buffer)
{
    // 按 dir 展开 Left / Right / Front / Back / Up / Down。
    // 每个坐标通过 _mapping.TryGetTileIndexAtPosition(position, out var tileIndex) 查询。
    // 命中后去重写入 buffer。

    return count;
}
```

方向面规则：

```text
Left   x = x0 - 1
Right  x = x0 + dx
Front  y = y0 - 1
Back   y = y0 + dy
Up     z = z0 + dz
Down   z = z0 - 1
```

每个方向遍历对应外侧面：

```text
Left / Right  遍历 y, z
Front / Back  遍历 x, z
Up / Down     遍历 x, y
```

去重建议使用小范围线性扫描：

```csharp
private static bool Contains(
    ReadOnlySpan<int> values,
    int count,
    int value)
{
    for (var i = 0; i < count; i++)
    {
        if (values[i] == value)
            return true;
    }

    return false;
}
```

默认 Tile 单方向面点最多 4 个，线性去重比 `HashSet` 更适合热路径。

Classic 左右判断只需要“有没有”：

```csharp
var leftBlocked = HasPresentNeighbor(tileIndex, NeighborDir.Left);
var rightBlocked = HasPresentNeighbor(tileIndex, NeighborDir.Right);
```

## 7. 上方依赖收集

### 7.1 直接上方依赖

直接上方依赖指：

```text
当前局面下，某个 Tile 顶面上方直接压着它的 present Tile。
```

这里必须结合 `_present`，所以最终结果应由 `Pasture` 计算。

`Pasture` 可以通过当前在场邻居查询获得直接上方依赖：

```csharp
GetPresentNeighbors(tileIndex, NeighborDir.Up, buffer);
```

模拟视角下则使用带 `ignoredTileBits` 的内部重载：

```csharp
GetPresentNeighbors(tileIndex, NeighborDir.Up, ignoredTileBits, buffer);
```

### 7.2 上方依赖闭包

上方依赖闭包是动态概念，不应该由 `TileMappingTable` 提供最终结果。

原因：

```text
已经被 Lift 的 Tile 不再参与遮挡链。
闭包必须基于当前 _present。
```

正确归属：

```text
TileMappingTable  提供坐标到 Tile 的静态索引
Pasture           基于 _present 计算动态上方依赖闭包
```

推荐 API 放在 `Pasture`：

```csharp
public void CollectPresentUpperClosure(
    int tileIndex,
    Span<ulong> resultBits);
```

语义：

```text
1. 从 tileIndex 的当前在场上邻居开始。
2. 只收集 present Tile。
3. 对收集到的上邻居继续向上扩展。
4. 直到没有新的 present 上邻居。
```

用途：

- 判断某个 Tile 的上层阻塞结构。
- 评估解锁路径。
- 局部刷新时扩大影响范围。
- 后续做 DAG / 残留结构分析。

伪代码：

```csharp
public void CollectPresentUpperClosure(
    int tileIndex,
    Span<ulong> resultBits)
{
    resultBits.Clear();

    Span<int> stack = stackalloc int[_mapping.TileCount];
    var stackCount = 0;

    PushPresentUpperNeighbors(tileIndex, stack, ref stackCount, resultBits);

    while (stackCount > 0)
    {
        var current = stack[--stackCount];
        PushPresentUpperNeighbors(current, stack, ref stackCount, resultBits);
    }
}
```

`PushPresentUpperNeighbors` 内部调用：

```csharp
GetPresentNeighbors(tileIndex, NeighborDir.Up, candidates);
```

当前在场过滤由 `GetPresentNeighbors` 内部完成。

## 8. 推荐内部结构

`Pasture` 推荐整理为：

```text
Pasture
├── 状态
│   ├── _present
│   ├── _visible
│   └── _selectable
│
├── 动作
│   ├── Lift(tileIndex)
│   └── Place(tileIndex)
│
├── 查询
│   ├── IsPresent(tileIndex)
│   ├── IsVisible(tileIndex)
│   └── IsSelectable(tileIndex)
│
├── 刷新
│   ├── Rebuild()
│   ├── RefreshAffected(tileIndex)
│   └── RefreshOne(tileIndex)
│
└── 判定
    ├── GetExposedArea(tileIndex)
    ├── IsTileRuleSelectable(tileIndex)
    ├── IsClassicRuleSelectable(tileIndex)
    ├── HasFreeHorizontalSide(tileIndex)
    ├── HasPresentNeighbor(tileIndex, dir)
    ├── GetPresentNeighbors(tileIndex, dir, buffer)
    └── CollectPresentUpperClosure(tileIndex, resultBits)
```

## 9. 当前实现需要注意

当前 `Pasture` 实现里有几处需要后续修正：

- `Initialize()` 仍为空，导致 `_present` / `_visible` / `_selectable` 初始状态没有建立。
- `RefreshAfterRemove()` 被 `Lift` 和 `Place` 共用时，命名不够准确，应改为 `RefreshAffected()`。
- 当前 `GetExposedArea()` 的计数语义需要确认，避免把“遮挡面积”误当成“暴露面积”。
- 刷新 affected tile 时，应更新被影响的 tile，而不是误写触发变化的 tile。
- `_lockRule` 应参与可选判定，尤其是 Classic 的左右邻居规则。

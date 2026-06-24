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

`TileMappingTable.GetAffectedTileBits(tileIndex)` 可以作为受影响集合的基础来源。

## 6. TileMappingTable 的静态邻居查询

### 6.1 分层原则

邻居关系要分成两层：

```text
TileMappingTable  静态邻居候选：空间布局上相邻的是谁
Pasture           动态在场邻居：当前局面下仍 present 的邻居是谁
```

`TileMappingTable` 不应该知道 `_present`，所以它不能回答“当前有没有邻居”。

它只能回答：

```text
按关卡静态空间布局，这个方向上有哪些邻居候选。
```

`Pasture` 再结合 `_present` 过滤：

```text
这些候选邻居里，哪些当前仍然在场。
```

一句话：

```text
TileMappingTable 回答：空间上是谁？
Pasture 回答：现在还算不算？
```

### 6.2 静态邻居定义

给定一个 Tile 和一个方向，邻居查询的语义是：

```text
1. 取 Tile 在指定方向上的面坐标组。
2. 将这一组面坐标整体向指定方向推进一格。
3. 查询这些坐标上命中的 Tile。
4. 去重后得到指定方向的邻居集合。
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

### 6.3 TileMappingTable 推荐 API

`TileMappingTable` 提供静态邻居候选查询：

```csharp
public bool HasNeighbor(
    int tileIndex,
    NeighborDir dir);

public int GetNeighborCount(
    int tileIndex,
    NeighborDir dir);

public int GetNeighbors(
    int tileIndex,
    NeighborDir dir,
    Span<int> buffer);
```

其中核心方法是 `GetNeighbors`。

语义：

```text
把指定方向上的静态邻居候选去重后写入 buffer。
返回写入数量。
```

约束：

- 不分配 `List`。
- 不返回 `IEnumerable`。
- 不在热路径使用 LINQ。
- 不判断 `Present`。
- 不判断 `Visible`。
- 不判断 `Selectable`。
- 默认 Tile 体积下，单方向邻居数量通常很小，适合 `stackalloc`。

### 6.4 GetNeighbors 实现思路

不要使用 `EnumerateNeighborFacePositions` / `yield return` 这类枚举器式实现。

推荐直接在 `GetNeighbors` 内按方向展开循环：

```csharp
public int GetNeighbors(
    int tileIndex,
    NeighborDir dir,
    Span<int> buffer)
{
    ValidateTileIndex(tileIndex);

    var tile = _tiles[tileIndex];
    var (x0, y0, z0) = tile.Position.UnpackXyz();
    var (dx, dy, dz) = tile.Volume.UnpackXyz();

    var write = 0;

    void TryAddAt(int x, int y, int z)
    {
        if (!IsInside(x, y, z))
            return;

        var position = (x, y, z).PackXyz();

        if (!TryGetTileIndexAtPosition(position, out var neighborIndex))
            return;

        if (neighborIndex == tileIndex)
            return;

        if (Contains(buffer, write, neighborIndex))
            return;

        if (write >= buffer.Length)
            throw new ArgumentException("邻居缓冲区长度不足。", nameof(buffer));

        buffer[write++] = neighborIndex;
    }

    // 按 dir 展开 Left / Right / Front / Back / Up / Down。

    return write;
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

### 6.5 Pasture 的动态邻居过滤

`Pasture` 基于 `TileMappingTable.GetNeighbors` 提供当前在场邻居查询：

```csharp
public bool HasPresentNeighbor(
    int tileIndex,
    NeighborDir dir);

public int GetPresentNeighborCount(
    int tileIndex,
    NeighborDir dir);

public int GetPresentNeighbors(
    int tileIndex,
    NeighborDir dir,
    Span<int> buffer);
```

实现关系：

```csharp
public int GetPresentNeighbors(
    int tileIndex,
    NeighborDir dir,
    Span<int> buffer)
{
    Span<int> candidates = stackalloc int[4];

    var candidateCount = _mapping.GetNeighbors(
        tileIndex,
        dir,
        candidates);

    var write = 0;

    for (var i = 0; i < candidateCount; i++)
    {
        var candidate = candidates[i];

        if (IsPresent(candidate))
            buffer[write++] = candidate;
    }

    return write;
}
```

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

`TileMappingTable` 可以提供静态上邻居候选：

```csharp
_mapping.GetNeighbors(tileIndex, NeighborDir.Up, buffer);
```

但 `Pasture` 必须过滤：

```csharp
if (IsPresent(candidate))
{
    // candidate 才是当前局面下真实存在的上方依赖。
}
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
TileMappingTable  提供静态上邻居候选
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
_mapping.GetNeighbors(tileIndex, NeighborDir.Up, candidates);
```

然后用 `IsPresent` 过滤。

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
    ├── GetPresentNeighborCount(tileIndex, dir)
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

# Behaviour 设计：性能与简单 API 的折中方案

## 1. 目标

`Behaviour` 表示一次候选行为，例如：

* 需要先选择哪些 tile 进入卡槽
* 会触发哪些 tile 匹配
* 当前行为属于什么类型
* 当前行为对应什么颜色

原始设计通常会写成 class：

```csharp
public sealed class Behaviour
{
    public BehaviourKind Kind { get; }
    public int Color { get; }

    public int[] SelectIds { get; }
    public int[] MatchIds { get; }

    public int Count => SelectIds.Length + MatchIds.Length;
}
```

这个写法简单，但在热路径中会产生较多对象和数组分配。

本设计的目标是：

```text
1. 外部 API 仍然简单
2. Behaviour 本身轻量、可复制
3. 内部使用 ArrayPool 降低分配
4. Dispose 只集中在一层
5. 不把 ArrayPool / offset / buffer 管理暴露给业务代码
```

---

# 2. 核心设计

采用两层结构：

```text
Behaviour
    readonly struct
    只读行为视图
    不拥有内存
    不实现 IDisposable

BehaviourList
    sealed class
    拥有池化数组
    负责 ArrayPool 租借和归还
    实现 IDisposable
```

核心原则：

```text
BehaviourList 拥有内存
Behaviour 只是视图
```

这样可以避免让单个 `Behaviour` 直接持有数组池资源，从而避免结构体复制导致重复 Dispose 的问题。

---

# 3. 为什么不推荐 Behaviour 自己 Dispose

不要这样设计：

```csharp
public readonly struct Behaviour : IDisposable
{
    private readonly int[] _selectIds;

    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_selectIds);
    }
}
```

原因是 `struct` 会被复制。

例如：

```csharp
var b1 = CreateBehaviour();
var b2 = b1;

b1.Dispose();
b2.Dispose();
```

这会导致同一个数组被归还两次，污染 `ArrayPool`。

下面这些场景也可能产生复制：

```csharp
UseBehaviour(behaviour);

list.Add(behaviour);

return behaviour;
```

即使是 `readonly struct`，也只是字段不可变，不代表不会复制。

所以结论是：

```text
Behaviour 不应该直接拥有 ArrayPool buffer。
Dispose 应该放在 BehaviourList 这一层。
```

---

# 4. Behaviour 结构

`Behaviour` 是一个只读视图。

它保存：

* 行为类型
* 颜色
* select ids 在共享 buffer 中的位置
* match ids 在共享 buffer 中的位置

```csharp
public readonly struct Behaviour
{
    private readonly int[] _idBuffer;

    private readonly int _selectStart;
    private readonly int _selectCount;

    private readonly int _matchStart;
    private readonly int _matchCount;

    public BehaviourKind Kind { get; }
    public int Color { get; }

    public int SelectCount => _selectCount;
    public int MatchCount => _matchCount;
    public int Count => _selectCount + _matchCount;

    public ReadOnlySpan<int> SelectIds
        => _idBuffer.AsSpan(_selectStart, _selectCount);

    public ReadOnlySpan<int> MatchIds
        => _idBuffer.AsSpan(_matchStart, _matchCount);

    internal Behaviour(
        BehaviourKind kind,
        int color,
        int[] idBuffer,
        int selectStart,
        int selectCount,
        int matchStart,
        int matchCount)
    {
        Kind = kind;
        Color = color;

        _idBuffer = idBuffer;

        _selectStart = selectStart;
        _selectCount = selectCount;

        _matchStart = matchStart;
        _matchCount = matchCount;
    }
}
```

外部使用时仍然很简单：

```csharp
foreach (var id in behaviour.SelectIds)
{
    // select tile
}

foreach (var id in behaviour.MatchIds)
{
    // match tile
}
```

---

# 5. BehaviourList 结构

`BehaviourList` 负责统一管理一批 `Behaviour`。

它内部持有两个池化数组：

```text
Behaviour[] _behaviours
int[] _idBuffer
```

其中：

```text
_behaviours
    存放 Behaviour 结构体

_idBuffer
    存放所有 SelectIds / MatchIds
```

完整实现：

```csharp
using System;
using System.Buffers;

public sealed class BehaviourList : IDisposable
{
    private Behaviour[] _behaviours;
    private int[] _idBuffer;

    private int _count;
    private int _idCount;
    private bool _disposed;

    private BehaviourList(int behaviourCapacity, int idCapacity)
    {
        _behaviours = ArrayPool<Behaviour>.Shared.Rent(behaviourCapacity);
        _idBuffer = ArrayPool<int>.Shared.Rent(idCapacity);
    }

    public static BehaviourList Rent(
        int capacity = 32,
        int idCapacity = 256)
    {
        return new BehaviourList(capacity, idCapacity);
    }

    public int Count => _count;

    public ReadOnlySpan<Behaviour> AsSpan()
    {
        ThrowIfDisposed();
        return _behaviours.AsSpan(0, _count);
    }

    public void Add(
        BehaviourKind kind,
        int color,
        ReadOnlySpan<int> selectIds,
        ReadOnlySpan<int> matchIds)
    {
        ThrowIfDisposed();

        EnsureBehaviourCapacity(_count + 1);
        EnsureIdCapacity(_idCount + selectIds.Length + matchIds.Length);

        int selectStart = _idCount;
        selectIds.CopyTo(_idBuffer.AsSpan(selectStart));
        _idCount += selectIds.Length;

        int matchStart = _idCount;
        matchIds.CopyTo(_idBuffer.AsSpan(matchStart));
        _idCount += matchIds.Length;

        _behaviours[_count++] = new Behaviour(
            kind,
            color,
            _idBuffer,
            selectStart,
            selectIds.Length,
            matchStart,
            matchIds.Length);
    }

    public void Clear()
    {
        ThrowIfDisposed();

        _count = 0;
        _idCount = 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        ArrayPool<Behaviour>.Shared.Return(_behaviours, clearArray: false);
        ArrayPool<int>.Shared.Return(_idBuffer, clearArray: false);

        _behaviours = Array.Empty<Behaviour>();
        _idBuffer = Array.Empty<int>();

        _count = 0;
        _idCount = 0;
    }

    private void EnsureBehaviourCapacity(int required)
    {
        if (required <= _behaviours.Length)
            return;

        var newBuffer = ArrayPool<Behaviour>.Shared.Rent(required * 2);

        _behaviours
            .AsSpan(0, _count)
            .CopyTo(newBuffer);

        ArrayPool<Behaviour>.Shared.Return(_behaviours, clearArray: false);

        _behaviours = newBuffer;
    }

    private void EnsureIdCapacity(int required)
    {
        if (required <= _idBuffer.Length)
            return;

        var newBuffer = ArrayPool<int>.Shared.Rent(required * 2);

        _idBuffer
            .AsSpan(0, _idCount)
            .CopyTo(newBuffer);

        ArrayPool<int>.Shared.Return(_idBuffer, clearArray: false);

        _idBuffer = newBuffer;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BehaviourList));
    }
}
```

---

# 6. 使用方式

业务层只需要这样写：

```csharp
using var behaviours = BehaviourList.Rent(
    capacity: 32,
    idCapacity: 256);

behaviours.Add(
    BehaviourKind.SelectThenMatch,
    color: 3,
    selectIds: stackalloc[] { 12, 18 },
    matchIds: stackalloc[] { 7 });

foreach (ref readonly var behaviour in behaviours.AsSpan())
{
    HandleBehaviour(in behaviour);
}
```

处理函数：

```csharp
private static void HandleBehaviour(in Behaviour behaviour)
{
    foreach (var id in behaviour.SelectIds)
    {
        // 执行 Select
    }

    foreach (var id in behaviour.MatchIds)
    {
        // 执行 Match
    }
}
```

---

# 7. Clear 与 Dispose 的区别

## Clear

`Clear()` 只重置计数，不归还数组。

适合一轮搜索中复用：

```csharp
behaviours.Clear();
```

效果：

```text
_count = 0
_idCount = 0
```

数组仍然留在 `BehaviourList` 内部，下次继续写入。

## Dispose

`Dispose()` 归还数组到 `ArrayPool`。

适合整个生命周期结束时调用：

```csharp
using var behaviours = BehaviourList.Rent();
```

作用域结束后会自动调用：

```csharp
behaviours.Dispose();
```

---

# 8. 生命周期规则

使用者只需要遵守一个规则：

```text
不要在 BehaviourList Dispose 之后继续使用 Behaviour。
```

因为 `Behaviour` 内部只是指向 `BehaviourList` 管理的共享 buffer。

错误示例：

```csharp
Behaviour saved;

using (var behaviours = BehaviourList.Rent())
{
    behaviours.Add(...);
    saved = behaviours.AsSpan()[0];
}

// 错误：behaviours 已经 Dispose
Console.WriteLine(saved.Count);
```

正确示例：

```csharp
using var behaviours = BehaviourList.Rent();

behaviours.Add(...);

foreach (ref readonly var behaviour in behaviours.AsSpan())
{
    HandleBehaviour(in behaviour);
}
```

---

# 9. 适合的使用场景

这个设计适合：

```text
一轮搜索生成一批 Behaviour
一批 Behaviour 被评分
通过 Softmax / WeightedChoice 选择其中一个
本轮结束后 Clear 或 Dispose
```

典型流程：

```csharp
using var behaviours = BehaviourList.Rent();

finder.FillBehaviours(context, behaviours);

ReadOnlySpan<Behaviour> span = behaviours.AsSpan();

int selectedIndex = scorer.Choose(span, random);

Behaviour selected = span[selectedIndex];

ApplyBehaviour(in selected);
```

---

# 10. Finder 接口建议

推荐让 finder 写入 `BehaviourList`，而不是返回 `List<Behaviour>`。

```csharp
public interface IBehaviourFinder
{
    void FillBehaviours(
        in BehaviourFindContext context,
        BehaviourList output);
}
```

实现示例：

```csharp
public sealed class FseBehaviourFinder : IBehaviourFinder
{
    public void FillBehaviours(
        in BehaviourFindContext context,
        BehaviourList output)
    {
        output.Clear();

        // 搜索候选行为
        // output.Add(kind, color, selectIds, matchIds);
    }
}
```

这样可以避免：

```text
new List<Behaviour>()
new Behaviour()
new int[]
```

---

# 11. Scorer 接口建议

Scorer 只读取，不持有。

```csharp
public interface IBehaviourScorer
{
    double Evaluate(
        in LevelCore level,
        in Behaviour behaviour);
}
```

使用：

```csharp
for (int i = 0; i < behaviours.Count; i++)
{
    ref readonly var behaviour = ref behaviours.AsSpan()[i];

    scores[i] = scorer.Evaluate(
        in level,
        in behaviour);
}
```

---

# 12. 为什么这个方案简单

外部只接触三个概念：

```text
Behaviour
BehaviourList
using var
```

业务层不需要知道：

```text
ArrayPool
Rent
Return
offset
idBuffer
扩容
重复 Dispose
```

对业务代码来说，它仍然像普通集合一样使用：

```csharp
using var behaviours = BehaviourList.Rent();

behaviours.Add(...);

foreach (var behaviour in behaviours.AsSpan())
{
    ...
}
```

复杂度被压在 `BehaviourList` 内部。

---

# 13. 性能收益

相比原始 class 方案：

```csharp
public sealed class Behaviour
{
    public int[] SelectIds { get; }
    public int[] MatchIds { get; }
}
```

新方案减少了：

```text
1. Behaviour 对象分配
2. SelectIds 数组分配
3. MatchIds 数组分配
4. List<Behaviour> 扩容分配
5. GC 压力
```

同时保留了：

```text
1. 简单的 SelectIds / MatchIds 访问方式
2. 明确的生命周期
3. 可控的内存复用
```

---

# 14. 命名建议

推荐命名：

```text
Behaviour
    单个行为，只读视图

BehaviourList
    一批 Behaviour，拥有内存

IBehaviourFinder
    负责生成候选行为

IBehaviourScorer
    负责给行为打分
```

如果后续想更强调池化语义，也可以叫：

```text
BehaviourBuffer
BehaviourBatch
BehaviourArena
```

但从易懂角度看，`BehaviourList` 最适合当前阶段。

---

# 15. 最终结论

推荐方案：

```csharp
public readonly struct Behaviour
public sealed class BehaviourList : IDisposable
```

不要推荐：

```csharp
public readonly struct Behaviour : IDisposable
```

核心原因：

```text
Behaviour 是值类型，会复制。
让它自己 Dispose 容易导致重复归还 ArrayPool buffer。
```

最终心智模型：

```text
BehaviourList 负责生命周期
Behaviour 负责表达行为
业务层只管 using var + Add + foreach
```

这是目前比较好的平衡：

```text
性能足够好
API 足够简单
ArrayPool 不暴露
Dispose 不分散
热路径对象分配少
```

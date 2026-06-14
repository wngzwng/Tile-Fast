# ThreeTile BitSetOperations 与 Extension 设计文档

## 1. 目标

`Common/BitSet` 目录用于统一管理 ThreeTile 项目中的位图集合能力。

`BitSetOperations` 的定位是：

```text
System.Numerics.BitOperations 的多 word / Span bitset 扩展版
```

也就是说：

```text
BitOperations：
  面向单个 uint / ulong / nuint 的底层位操作。

BitSetOperations：
  面向 ReadOnlySpan<ulong> / Span<ulong>
  以及 ReadOnlySpan<uint> / Span<uint>
  的多 word 位图操作。
```

当前需要覆盖：

1. bit 长度到 word 长度换算
2. 单 bit 读取
3. 单 bit 设置
4. 单 bit 清除
5. 清空整个位图
6. 并集
7. 交集
8. 差集
9. 异或
10. 取反
11. 判断是否为空
12. 判断是否存在任意置位 bit
13. 判断两个 bitset 是否有交集
14. 判断一个 bitset 是否完整包含另一个 bitset
15. 统计置位 bit 数量
16. 查找第一个置位 bit
17. 查找下一个置位 bit
18. 零 GC 枚举所有置位 bit
19. 左移
20. 右移
21. Extension 链式调用体验

后续可以继续补充：

```text
区域读写：
  Read(bits, start, length, out value)
  Write(bits, start, length, value)

更严格的 bitLength 版本：
  NotWith(bits, bitLength)
  LeftShift(bits, count, bitLength)
  RightShift(bits, count, bitLength)
  HasAllSet(bits, bitLength)
```

核心目标：

```text
高性能
零 GC
正交性好
高内聚
API 命名尽量贴近官方
调用体验足够顺手
```

最终希望可以写出：

```csharp
source.CopyTo(output);
BitSetOperations.AndNotWith(output, mask);
```

也可以写成：

```csharp
source.CopyTo(output);
output.AndNotWith(mask);
```

含义：

```text
output = source & ~mask
```

---

## 2. 目录结构

推荐目录：

```text
ThreeTile.Core
└── Common
    └── BitSet
        ├── BitSetOperations.cs
        ├── BitSetExtensions.cs
        ├── UInt64BitIterator.cs
        └── UInt32BitIterator.cs
```

说明：

```text
BitSetOperations.cs：
  BitSet 核心工具类。

BitSetExtensions.cs：
  链式调用扩展方法。
  只转发到 BitSetOperations，不承载核心逻辑。

UInt64BitIterator.cs：
  ulong 位图的零 GC 置位遍历器。

UInt32BitIterator.cs：
  uint 位图的零 GC 置位遍历器。
```

第一阶段也可以先放一个文件：

```text
ThreeTile.Core
└── Common
    └── BitSet
        └── BitSetOperations.cs
```

等 API 稳定后再拆分。

---

## 3. 核心原则

## 3.1 BitSetOperations 是多 word BitOperations

`System.Numerics.BitOperations` 主要处理单个整数 word。

例如：

```csharp
BitOperations.PopCount(value);
BitOperations.TrailingZeroCount(value);
BitOperations.LeadingZeroCount(value);
BitOperations.RotateLeft(value, offset);
BitOperations.RotateRight(value, offset);
```

`BitSetOperations` 则处理多 word 位图：

```csharp
BitSetOperations.PopCount(bits);
BitSetOperations.FindFirstSet(bits);
BitSetOperations.FindNextSet(bits, start);
BitSetOperations.LeftShift(bits, count);
BitSetOperations.RightShift(bits, count);
```

也就是：

```text
单 word：
  ulong value

多 word：
  Span<ulong> bits
  ReadOnlySpan<ulong> bits
```

---

## 3.2 BitSetOperations 放核心逻辑

核心实现统一放在：

```csharp
BitSetOperations
```

例如：

```csharp
BitSetOperations.Get(bits, bit);
BitSetOperations.Set(bits, bit);
BitSetOperations.Clear(bits, bit);
BitSetOperations.ClearAll(bits);

BitSetOperations.OrWith(a, b);
BitSetOperations.AndWith(a, b);
BitSetOperations.AndNotWith(a, b);
BitSetOperations.XorWith(a, b);

BitSetOperations.IsEmpty(bits);
BitSetOperations.HasAnySet(bits);
BitSetOperations.ContainsAll(superset, subset);
BitSetOperations.Overlaps(a, b);

BitSetOperations.PopCount(bits);
BitSetOperations.FindFirstSet(bits);
BitSetOperations.FindNextSet(bits, start);
BitSetOperations.EnumerateSetBits(bits);
```

---

## 3.3 Extension 只负责调用体验

链式调用放在：

```csharp
BitSetExtensions
```

Extension 不承载核心逻辑，只转发给 `BitSetOperations`。

推荐：

```csharp
public static Span<ulong> AndNotWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> mask)
{
    BitSetOperations.AndNotWith(bits, mask);
    return bits;
}
```

不推荐：

```csharp
public static Span<ulong> AndNotWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> mask)
{
    // 大段核心逻辑
}
```

---

## 3.4 高性能调用优先使用 Span

核心 API 使用：

```csharp
Span<T>
ReadOnlySpan<T>
```

例如：

```csharp
BitSetOperations.Get(bits, bit);
BitSetOperations.Set(bits, bit);
BitSetOperations.Clear(bits, bit);
BitSetOperations.ClearAll(bits);

BitSetOperations.OrWith(a, b);
BitSetOperations.AndWith(a, b);
BitSetOperations.AndNotWith(a, b);
BitSetOperations.XorWith(a, b);
```

这样可以支持：

```text
ulong[]
uint[]
ArraySegment
stackalloc Span
复用缓冲区
```

---

## 3.5 不返回新集合

不推荐：

```csharp
ulong[] result = BitSetOperations.AndNot(a, b);
```

原因：

```text
会分配新数组。
热路径容易产生 GC 压力。
```

推荐：

```csharp
a.CopyTo(output);
BitSetOperations.AndNotWith(output, b);
```

或者使用 Extension：

```csharp
a.CopyTo(output);
output.AndNotWith(b);
```

特点：

```text
output 由调用者提供。
BitSetOperations 只负责原地修改。
分配行为由调用方显式控制。
```

---

## 3.6 不使用 LINQ / IEnumerable

不推荐：

```csharp
foreach (var bit in BitSetOperations.Enumerate(bits))
{
}
```

如果返回 `IEnumerable<int>`，会引入额外抽象，也可能产生分配。

推荐：

```csharp
var it = BitSetOperations.EnumerateSetBits(bits);

while (it.MoveNext(out var bit))
{
    // use bit
}
```

Extension 也只返回同一个零 GC iterator：

```csharp
var it = bits.EnumerateSetBits();

while (it.MoveNext(out var bit))
{
    // use bit
}
```

---

## 3.7 集合操作原地修改左侧参数

集合操作统一使用：

```text
OrWith
AndWith
AndNotWith
XorWith
NotWith
```

其中 `With` 表示：

```text
原地修改左侧参数
```

例如：

```csharp
BitSetOperations.AndNotWith(output, mask);
```

含义是：

```text
output &= ~mask
```

Extension 版本：

```csharp
output.AndNotWith(mask);
```

同样表示：

```text
output &= ~mask
```

---

## 4. 命名约定

## 4.1 类名

使用：

```csharp
BitSetOperations
```

不使用：

```text
BitOps
BitUtil
BitHelper
BitSetHelper
BitMapHelper
```

原因：

```text
BitSetOperations 更正式。
BitSetOperations 和 System.Numerics.BitOperations 风格一致。
BitSetOperations 表达这是 bitset 的 operations。
BitOps 太短，稳定性和正式感不足。
```

---

## 4.2 参考官方命名

优先参考：

```text
System.Numerics.BitOperations
System.Collections.BitArray
```

其中：

```text
PopCount：
  直接对齐 BitOperations.PopCount / BitArray.PopCount。

LeftShift / RightShift：
  对齐 BitArray.LeftShift / BitArray.RightShift。

Get / Set：
  对齐 BitArray.Get / BitArray.Set。

Or / And / Xor：
  参考 BitArray.Or / BitArray.And / BitArray.Xor。
  但 BitSetOperations 是静态 Span API，所以保留 With 后缀表示原地修改。
```

---

## 4.3 核心命名表

| 能力            | 命名                 |
| ------------- | ------------------ |
| 单 bit 读取      | `Get`              |
| 单 bit 设置      | `Set`              |
| 单 bit 清除      | `Clear`            |
| 清空整个位图        | `ClearAll`         |
| 判断是否为空        | `IsEmpty`          |
| 判断是否存在置位 bit  | `HasAnySet`        |
| 统计置位数量        | `PopCount`         |
| 查找第一个置位 bit   | `FindFirstSet`     |
| 查找下一个置位 bit   | `FindNextSet`      |
| 零 GC 枚举置位 bit | `EnumerateSetBits` |
| 判断完整包含        | `ContainsAll`      |
| 判断是否有交集       | `Overlaps`         |
| 并集            | `OrWith`           |
| 交集            | `AndWith`          |
| 差集            | `AndNotWith`       |
| 异或            | `XorWith`          |
| 取反            | `NotWith`          |
| 左移            | `LeftShift`        |
| 右移            | `RightShift`       |

---

## 5. Word 类型选择

## 5.1 主力类型：ulong

主力版本使用：

```csharp
ulong
```

原因：

```text
ulong 是 64-bit word。
单个 word 可以存 64 个 bit。
整体 word 数更少。
```

例如 bit 总数为 `160`：

```text
ulong[] 需要 3 个 word
uint[]  需要 5 个 word
```

所以主路径优先使用：

```csharp
ulong[] bits = new ulong[BitSetOperations.BitLenToU64Len(bitCount)];
```

---

## 5.2 可选类型：uint

`uint` 版本用于需要 32-bit word 的场景。

例如：

```text
兼容外部数据格式
某些算法明确按 32-bit word 处理
```

如果没有明确需求，主路径可以先只使用 `ulong`。

---

## 5.3 不提供 int / long 版本

不建议提供：

```csharp
int
long
```

原因：

```text
bitset word 更适合使用无符号整数。
int / long 是有符号整数，语义不如 uint / ulong 清晰。
```

最终建议：

```text
主版本：ulong
可选版本：uint
不提供：int
不提供：long
```

---

## 6. BitSet 基础表示

## 6.1 ulong 位图

```csharp
ulong[] bits = new ulong[BitSetOperations.BitLenToU64Len(bitCount)];
```

bit 到 word 的映射：

```text
wordIndex = bit >> 6
bitOffset = bit & 63
bitMask   = 1UL << bitOffset
```

例如：

```text
bit 0   -> bits[0] 的第 0 位
bit 63  -> bits[0] 的第 63 位
bit 64  -> bits[1] 的第 0 位
bit 127 -> bits[1] 的第 63 位
```

---

## 6.2 uint 位图

```csharp
uint[] bits = new uint[BitSetOperations.BitLenToU32Len(bitCount)];
```

bit 到 word 的映射：

```text
wordIndex = bit >> 5
bitOffset = bit & 31
bitMask   = 1U << bitOffset
```

---

## 7. API 分层

`BitSetOperations` 分成七类能力：

1. 长度换算
2. 单 bit 操作
3. 整段 bitset 操作
4. 判断关系
5. 统计与查找
6. 零 GC 迭代
7. 整体移位

对应 API：

```text
Length：
  BitLenToU64Len
  BitLenToU32Len

Single bit：
  Get
  Set
  Clear
  ClearAll

Set operations：
  OrWith
  AndWith
  AndNotWith
  XorWith
  NotWith

Predicates：
  IsEmpty
  HasAnySet
  ContainsAll
  Overlaps

Count / find：
  PopCount
  FindFirstSet
  FindNextSet

Iteration：
  EnumerateSetBits

Shift：
  LeftShift
  RightShift
```

`BitSetExtensions` 只提供同名转发方法，方便链式使用。

---

# 8. BitSetOperations 核心 API

## 8.1 长度换算

### BitLenToU64Len

```csharp
public static int BitLenToU64Len(int bitLength)
{
    Debug.Assert(bitLength >= 0);
    return (int)(((uint)bitLength + 63u) >> 6);
}
```

说明：

```text
每个 ulong 存 64 个 bit。
所以向上取整除以 64。
```

---

### BitLenToU32Len

```csharp
public static int BitLenToU32Len(int bitLength)
{
    Debug.Assert(bitLength >= 0);
    return (int)(((uint)bitLength + 31u) >> 5);
}
```

说明：

```text
每个 uint 存 32 个 bit。
所以向上取整除以 32。
```

---

## 8.2 单 bit 操作

### Get

```csharp
public static bool Get(ReadOnlySpan<ulong> bits, int bit)
{
    return (bits[bit >> 6] & (1UL << (bit & 63))) != 0;
}

public static bool Get(ReadOnlySpan<uint> bits, int bit)
{
    return (bits[bit >> 5] & (1U << (bit & 31))) != 0;
}
```

---

### Set

```csharp
public static void Set(Span<ulong> bits, int bit)
{
    bits[bit >> 6] |= 1UL << (bit & 63);
}

public static void Set(Span<uint> bits, int bit)
{
    bits[bit >> 5] |= 1U << (bit & 31);
}
```

---

### Clear

```csharp
public static void Clear(Span<ulong> bits, int bit)
{
    bits[bit >> 6] &= ~(1UL << (bit & 63));
}

public static void Clear(Span<uint> bits, int bit)
{
    bits[bit >> 5] &= ~(1U << (bit & 31));
}
```

---

### ClearAll

```csharp
public static void ClearAll(Span<ulong> bits)
{
    bits.Clear();
}

public static void ClearAll(Span<uint> bits)
{
    bits.Clear();
}
```

---

## 8.3 集合操作

### OrWith

```csharp
public static bool OrWith(Span<ulong> a, ReadOnlySpan<ulong> b)
{
#if DEBUG
    if (a.Length != b.Length)
        throw new ArgumentException("BitSet size mismatch.");
#endif

    bool changed = false;

    for (int i = 0; i < a.Length; i++)
    {
        ulong before = a[i];
        ulong after = before | b[i];

        a[i] = after;
        changed |= before != after;
    }

    return changed;
}
```

---

### AndWith

```csharp
public static bool AndWith(Span<ulong> a, ReadOnlySpan<ulong> b)
{
#if DEBUG
    if (a.Length != b.Length)
        throw new ArgumentException("BitSet size mismatch.");
#endif

    bool changed = false;

    for (int i = 0; i < a.Length; i++)
    {
        ulong before = a[i];
        ulong after = before & b[i];

        a[i] = after;
        changed |= before != after;
    }

    return changed;
}
```

---

### AndNotWith

```csharp
public static bool AndNotWith(Span<ulong> a, ReadOnlySpan<ulong> b)
{
#if DEBUG
    if (a.Length != b.Length)
        throw new ArgumentException("BitSet size mismatch.");
#endif

    bool changed = false;

    for (int i = 0; i < a.Length; i++)
    {
        ulong before = a[i];
        ulong after = before & ~b[i];

        a[i] = after;
        changed |= before != after;
    }

    return changed;
}
```

---

### XorWith

```csharp
public static bool XorWith(Span<ulong> a, ReadOnlySpan<ulong> b)
{
#if DEBUG
    if (a.Length != b.Length)
        throw new ArgumentException("BitSet size mismatch.");
#endif

    bool changed = false;

    for (int i = 0; i < a.Length; i++)
    {
        ulong before = a[i];
        ulong after = before ^ b[i];

        a[i] = after;
        changed |= before != after;
    }

    return changed;
}
```

---

### NotWith

```csharp
public static void NotWith(Span<ulong> bits)
{
    for (int i = 0; i < bits.Length; i++)
    {
        bits[i] = ~bits[i];
    }
}
```

注意：

```text
NotWith 会翻转整个 word。
如果最后一个 word 存在 bitLength 之外的无效尾部 bit，
这些尾部 bit 也会被翻转成 1。

如果业务需要严格 bitLength 语义，
后续可以增加 NotWith(bits, bitLength) 版本。
```

---

## 8.4 判断类 API

### IsEmpty

作用：

```text
判断 bitset 是否没有任何置位 bit。
```

```csharp
public static bool IsEmpty(ReadOnlySpan<ulong> bits)
{
    for (int i = 0; i < bits.Length; i++)
    {
        if (bits[i] != 0)
            return false;
    }

    return true;
}
```

---

### HasAnySet

作用：

```text
判断 bitset 中是否存在任意置位 bit。
```

```csharp
public static bool HasAnySet(ReadOnlySpan<ulong> bits)
{
    return !IsEmpty(bits);
}
```

说明：

```text
IsEmpty 和 HasAnySet 都保留。
IsEmpty 表达空集合判断。
HasAnySet 对齐 BitArray.HasAnySet 的风格。
```

---

### ContainsAll

作用：

```text
判断 superset 是否包含 subset 的所有 bit。
```

等价关系：

```text
subset ⊆ superset
```

底层判断：

```text
(subset & ~superset) == 0
```

实现：

```csharp
public static bool ContainsAll(
    ReadOnlySpan<ulong> superset,
    ReadOnlySpan<ulong> subset)
{
#if DEBUG
    if (superset.Length != subset.Length)
        throw new ArgumentException("BitSet size mismatch.");
#endif

    for (int i = 0; i < superset.Length; i++)
    {
        if ((subset[i] & ~superset[i]) != 0)
            return false;
    }

    return true;
}
```

---

### Overlaps

作用：

```text
判断两个 bitset 是否有交集。
```

底层判断：

```text
(a & b) != 0
```

实现：

```csharp
public static bool Overlaps(
    ReadOnlySpan<ulong> a,
    ReadOnlySpan<ulong> b)
{
#if DEBUG
    if (a.Length != b.Length)
        throw new ArgumentException("BitSet size mismatch.");
#endif

    for (int i = 0; i < a.Length; i++)
    {
        if ((a[i] & b[i]) != 0)
            return true;
    }

    return false;
}
```

---

## 8.5 统计与查找

### PopCount

```csharp
public static int PopCount(ReadOnlySpan<ulong> bits)
{
    int count = 0;

    for (int i = 0; i < bits.Length; i++)
    {
        count += BitOperations.PopCount(bits[i]);
    }

    return count;
}
```

---

### FindFirstSet

```csharp
public static int FindFirstSet(ReadOnlySpan<ulong> bits)
{
    return FindNextSet(bits, 0);
}
```

---

### FindNextSet

```csharp
public static int FindNextSet(ReadOnlySpan<ulong> bits, int start)
{
    int wi = start >> 6;

    if (wi >= bits.Length)
        return -1;

    ulong word = bits[wi] & (~0UL << (start & 63));

    while (true)
    {
        if (word != 0)
        {
            return (wi << 6) + BitOperations.TrailingZeroCount(word);
        }

        wi++;

        if (wi >= bits.Length)
            return -1;

        word = bits[wi];
    }
}
```

---

## 8.6 零 GC 迭代

### EnumerateSetBits

```csharp
public static UInt64BitIterator EnumerateSetBits(ReadOnlySpan<ulong> bits)
{
    return new UInt64BitIterator(bits);
}
```

---

### UInt64BitIterator

```csharp
public ref struct UInt64BitIterator
{
    private readonly ReadOnlySpan<ulong> _bits;
    private int _nextStart;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UInt64BitIterator(ReadOnlySpan<ulong> bits)
    {
        _bits = bits;
        _nextStart = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(out int bit)
    {
        bit = BitSetOperations.FindNextSet(_bits, _nextStart);

        if (bit < 0)
            return false;

        _nextStart = bit + 1;
        return true;
    }
}
```

---

### UInt32BitIterator

```csharp
public ref struct UInt32BitIterator
{
    private readonly ReadOnlySpan<uint> _bits;
    private int _nextStart;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UInt32BitIterator(ReadOnlySpan<uint> bits)
    {
        _bits = bits;
        _nextStart = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(out int bit)
    {
        bit = BitSetOperations.FindNextSet(_bits, _nextStart);

        if (bit < 0)
            return false;

        _nextStart = bit + 1;
        return true;
    }
}
```

---

## 8.7 移位

### LeftShift

```csharp
public static void LeftShift(Span<ulong> bits, int count)
{
    if (count < 0)
        throw new ArgumentOutOfRangeException(nameof(count));

    if (count == 0 || bits.Length == 0)
        return;

    int wordShift = count >> 6;
    int bitShift = count & 63;

    for (int i = bits.Length - 1; i >= 0; i--)
    {
        int src = i - wordShift;

        ulong value = 0UL;

        if (src >= 0)
        {
            value = bits[src] << bitShift;

            if (bitShift != 0 && src - 1 >= 0)
            {
                value |= bits[src - 1] >> (64 - bitShift);
            }
        }

        bits[i] = value;
    }
}
```

---

### RightShift

```csharp
public static void RightShift(Span<ulong> bits, int count)
{
    if (count < 0)
        throw new ArgumentOutOfRangeException(nameof(count));

    if (count == 0 || bits.Length == 0)
        return;

    int wordShift = count >> 6;
    int bitShift = count & 63;

    for (int i = 0; i < bits.Length; i++)
    {
        int src = i + wordShift;

        ulong value = 0UL;

        if (src < bits.Length)
        {
            value = bits[src] >> bitShift;

            if (bitShift != 0 && src + 1 < bits.Length)
            {
                value |= bits[src + 1] << (64 - bitShift);
            }
        }

        bits[i] = value;
    }
}
```

---

## 9. BitSetExtensions

## 9.1 作用

`BitSetExtensions` 用于提高调用体验。

它只做一件事：

```text
把扩展方法转发给 BitSetOperations。
```

Extension 不应该承载核心逻辑。

---

## 9.2 链式规则

对会修改 bitset 的方法，返回原来的 `Span<T>`：

```csharp
public static Span<ulong> AndNotWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> mask)
{
    BitSetOperations.AndNotWith(bits, mask);
    return bits;
}
```

这样可以写：

```csharp
output
    .ClearAll()
    .OrWith(source)
    .AndNotWith(mask);
```

但需要注意：

```text
链式调用适合短链。
热路径里如果觉得不清楚，仍然可以直接调用 BitSetOperations。
```

---

## 9.3 BitSetExtensions.cs

```csharp
namespace ThreeTile.Core.Common.BitSet;

public static class BitSetExtensions
{
    // ==============================
    // ulong single bit
    // ==============================

    public static bool Get(this ReadOnlySpan<ulong> bits, int bit)
    {
        return BitSetOperations.Get(bits, bit);
    }

    public static Span<ulong> Set(this Span<ulong> bits, int bit)
    {
        BitSetOperations.Set(bits, bit);
        return bits;
    }

    public static Span<ulong> Clear(this Span<ulong> bits, int bit)
    {
        BitSetOperations.Clear(bits, bit);
        return bits;
    }

    public static Span<ulong> ClearAll(this Span<ulong> bits)
    {
        BitSetOperations.ClearAll(bits);
        return bits;
    }

    // ==============================
    // ulong set ops
    // ==============================

    public static Span<ulong> OrWith(
        this Span<ulong> bits,
        ReadOnlySpan<ulong> other)
    {
        BitSetOperations.OrWith(bits, other);
        return bits;
    }

    public static Span<ulong> AndWith(
        this Span<ulong> bits,
        ReadOnlySpan<ulong> other)
    {
        BitSetOperations.AndWith(bits, other);
        return bits;
    }

    public static Span<ulong> AndNotWith(
        this Span<ulong> bits,
        ReadOnlySpan<ulong> other)
    {
        BitSetOperations.AndNotWith(bits, other);
        return bits;
    }

    public static Span<ulong> XorWith(
        this Span<ulong> bits,
        ReadOnlySpan<ulong> other)
    {
        BitSetOperations.XorWith(bits, other);
        return bits;
    }

    public static Span<ulong> NotWith(this Span<ulong> bits)
    {
        BitSetOperations.NotWith(bits);
        return bits;
    }

    // ==============================
    // ulong predicates
    // ==============================

    public static bool IsEmpty(this ReadOnlySpan<ulong> bits)
    {
        return BitSetOperations.IsEmpty(bits);
    }

    public static bool HasAnySet(this ReadOnlySpan<ulong> bits)
    {
        return BitSetOperations.HasAnySet(bits);
    }

    public static bool ContainsAll(
        this ReadOnlySpan<ulong> superset,
        ReadOnlySpan<ulong> subset)
    {
        return BitSetOperations.ContainsAll(superset, subset);
    }

    public static bool Overlaps(
        this ReadOnlySpan<ulong> bits,
        ReadOnlySpan<ulong> other)
    {
        return BitSetOperations.Overlaps(bits, other);
    }

    // ==============================
    // ulong count / find
    // ==============================

    public static int PopCount(this ReadOnlySpan<ulong> bits)
    {
        return BitSetOperations.PopCount(bits);
    }

    public static int FindFirstSet(this ReadOnlySpan<ulong> bits)
    {
        return BitSetOperations.FindFirstSet(bits);
    }

    public static int FindNextSet(
        this ReadOnlySpan<ulong> bits,
        int start)
    {
        return BitSetOperations.FindNextSet(bits, start);
    }

    public static UInt64BitIterator EnumerateSetBits(
        this ReadOnlySpan<ulong> bits)
    {
        return BitSetOperations.EnumerateSetBits(bits);
    }

    // ==============================
    // ulong shift
    // ==============================

    public static Span<ulong> LeftShift(
        this Span<ulong> bits,
        int count)
    {
        BitSetOperations.LeftShift(bits, count);
        return bits;
    }

    public static Span<ulong> RightShift(
        this Span<ulong> bits,
        int count)
    {
        BitSetOperations.RightShift(bits, count);
        return bits;
    }

    // ==============================
    // uint single bit
    // ==============================

    public static bool Get(this ReadOnlySpan<uint> bits, int bit)
    {
        return BitSetOperations.Get(bits, bit);
    }

    public static Span<uint> Set(this Span<uint> bits, int bit)
    {
        BitSetOperations.Set(bits, bit);
        return bits;
    }

    public static Span<uint> Clear(this Span<uint> bits, int bit)
    {
        BitSetOperations.Clear(bits, bit);
        return bits;
    }

    public static Span<uint> ClearAll(this Span<uint> bits)
    {
        BitSetOperations.ClearAll(bits);
        return bits;
    }

    // ==============================
    // uint set ops
    // ==============================

    public static Span<uint> OrWith(
        this Span<uint> bits,
        ReadOnlySpan<uint> other)
    {
        BitSetOperations.OrWith(bits, other);
        return bits;
    }

    public static Span<uint> AndWith(
        this Span<uint> bits,
        ReadOnlySpan<uint> other)
    {
        BitSetOperations.AndWith(bits, other);
        return bits;
    }

    public static Span<uint> AndNotWith(
        this Span<uint> bits,
        ReadOnlySpan<uint> other)
    {
        BitSetOperations.AndNotWith(bits, other);
        return bits;
    }

    public static Span<uint> XorWith(
        this Span<uint> bits,
        ReadOnlySpan<uint> other)
    {
        BitSetOperations.XorWith(bits, other);
        return bits;
    }

    public static Span<uint> NotWith(this Span<uint> bits)
    {
        BitSetOperations.NotWith(bits);
        return bits;
    }

    // ==============================
    // uint predicates
    // ==============================

    public static bool IsEmpty(this ReadOnlySpan<uint> bits)
    {
        return BitSetOperations.IsEmpty(bits);
    }

    public static bool HasAnySet(this ReadOnlySpan<uint> bits)
    {
        return BitSetOperations.HasAnySet(bits);
    }

    public static bool ContainsAll(
        this ReadOnlySpan<uint> superset,
        ReadOnlySpan<uint> subset)
    {
        return BitSetOperations.ContainsAll(superset, subset);
    }

    public static bool Overlaps(
        this ReadOnlySpan<uint> bits,
        ReadOnlySpan<uint> other)
    {
        return BitSetOperations.Overlaps(bits, other);
    }

    // ==============================
    // uint count / find
    // ==============================

    public static int PopCount(this ReadOnlySpan<uint> bits)
    {
        return BitSetOperations.PopCount(bits);
    }

    public static int FindFirstSet(this ReadOnlySpan<uint> bits)
    {
        return BitSetOperations.FindFirstSet(bits);
    }

    public static int FindNextSet(
        this ReadOnlySpan<uint> bits,
        int start)
    {
        return BitSetOperations.FindNextSet(bits, start);
    }

    public static UInt32BitIterator EnumerateSetBits(
        this ReadOnlySpan<uint> bits)
    {
        return BitSetOperations.EnumerateSetBits(bits);
    }

    // ==============================
    // uint shift
    // ==============================

    public static Span<uint> LeftShift(
        this Span<uint> bits,
        int count)
    {
        BitSetOperations.LeftShift(bits, count);
        return bits;
    }

    public static Span<uint> RightShift(
        this Span<uint> bits,
        int count)
    {
        BitSetOperations.RightShift(bits, count);
        return bits;
    }
}
```

---

## 10. 使用方式

## 10.1 创建 ulong bitset

```csharp
int bitCount = 160;

ulong[] presentBits = new ulong[BitSetOperations.BitLenToU64Len(bitCount)];
ulong[] visibleBits = new ulong[BitSetOperations.BitLenToU64Len(bitCount)];
ulong[] selectableBits = new ulong[BitSetOperations.BitLenToU64Len(bitCount)];
ulong[] outputBits = new ulong[BitSetOperations.BitLenToU64Len(bitCount)];
```

---

## 10.2 静态方法调用

```csharp
BitSetOperations.Set(visibleBits, 12);

if (BitSetOperations.Get(visibleBits, 12))
{
    // bit 12 exists
}

BitSetOperations.Clear(visibleBits, 12);
```

---

## 10.3 Extension 调用

```csharp
visibleBits.AsSpan()
    .Set(12)
    .Clear(12);
```

数组要进入 `Span` 链式调用时，建议显式写：

```csharp
visibleBits.AsSpan()
```

这样语义更明确。

---

## 10.4 ClearAll

```csharp
BitSetOperations.ClearAll(outputBits);
```

或者：

```csharp
outputBits.AsSpan().ClearAll();
```

---

## 10.5 OrWith

```csharp
visibleBits.CopyTo(outputBits);
BitSetOperations.OrWith(outputBits, selectableBits);
```

或者：

```csharp
visibleBits.CopyTo(outputBits);

outputBits.AsSpan()
    .OrWith(selectableBits);
```

含义：

```text
outputBits = visibleBits | selectableBits
```

---

## 10.6 AndWith

```csharp
visibleBits.CopyTo(outputBits);
BitSetOperations.AndWith(outputBits, selectableBits);
```

或者：

```csharp
visibleBits.CopyTo(outputBits);

outputBits.AsSpan()
    .AndWith(selectableBits);
```

含义：

```text
outputBits = visibleBits & selectableBits
```

---

## 10.7 AndNotWith

```csharp
visibleBits.CopyTo(outputBits);
BitSetOperations.AndNotWith(outputBits, selectableBits);
```

或者：

```csharp
visibleBits.CopyTo(outputBits);

outputBits.AsSpan()
    .AndNotWith(selectableBits);
```

含义：

```text
outputBits = visibleBits & ~selectableBits
```

---

## 10.8 XorWith

```csharp
beforeBits.CopyTo(outputBits);
BitSetOperations.XorWith(outputBits, afterBits);
```

或者：

```csharp
beforeBits.CopyTo(outputBits);

outputBits.AsSpan()
    .XorWith(afterBits);
```

含义：

```text
outputBits = beforeBits ^ afterBits
```

---

## 10.9 NotWith

```csharp
sourceBits.CopyTo(outputBits);
BitSetOperations.NotWith(outputBits);
```

或者：

```csharp
sourceBits.CopyTo(outputBits);

outputBits.AsSpan()
    .NotWith();
```

含义：

```text
outputBits = ~sourceBits
```

---

## 10.10 IsEmpty / HasAnySet

```csharp
if (BitSetOperations.IsEmpty(outputBits))
{
    // no set bit
}
```

```csharp
if (BitSetOperations.HasAnySet(outputBits))
{
    // has at least one set bit
}
```

Extension：

```csharp
if (outputBits.AsSpan().IsEmpty())
{
}
```

```csharp
if (outputBits.AsSpan().HasAnySet())
{
}
```

---

## 10.11 ContainsAll

```csharp
if (BitSetOperations.ContainsAll(visibleBits, requiredBits))
{
    // visibleBits contains all requiredBits
}
```

含义：

```text
requiredBits ⊆ visibleBits
```

Extension：

```csharp
if (visibleBits.AsSpan().ContainsAll(requiredBits))
{
}
```

---

## 10.12 Overlaps

```csharp
if (BitSetOperations.Overlaps(a, b))
{
    // a and b have intersection
}
```

含义：

```text
(a & b) != 0
```

Extension：

```csharp
if (a.AsSpan().Overlaps(b))
{
}
```

---

## 10.13 PopCount

```csharp
int count = BitSetOperations.PopCount(selectableBits);
```

Extension：

```csharp
int count = selectableBits.AsSpan().PopCount();
```

---

## 10.14 FindFirstSet

```csharp
int first = BitSetOperations.FindFirstSet(selectableBits);

if (first >= 0)
{
    // found
}
```

Extension：

```csharp
int first = selectableBits.AsSpan().FindFirstSet();
```

---

## 10.15 FindNextSet

```csharp
int next = BitSetOperations.FindNextSet(selectableBits, start);
```

Extension：

```csharp
int next = selectableBits.AsSpan().FindNextSet(start);
```

---

## 10.16 EnumerateSetBits

```csharp
var it = BitSetOperations.EnumerateSetBits(selectableBits);

while (it.MoveNext(out var bit))
{
    // use bit
}
```

Extension：

```csharp
var it = selectableBits.AsSpan().EnumerateSetBits();

while (it.MoveNext(out var bit))
{
    // use bit
}
```

---

## 10.17 LeftShift / RightShift

```csharp
BitSetOperations.LeftShift(bits, 3);
BitSetOperations.RightShift(bits, 3);
```

Extension：

```csharp
bits.AsSpan()
    .LeftShift(3)
    .RightShift(3);
```

含义：

```text
bits <<= 3
bits >>= 3
```

---

## 10.18 业务中推荐使用方式

业务层只组合 `BitSetOperations`，不要把业务语义下沉到 `BitSetOperations`。

例如：

```csharp
public void FillInvisibleBits(Span<ulong> output)
{
    PresentBits.CopyTo(output);
    output.AndNotWith(VisibleBits);
}
```

含义：

```text
InvisibleBits = PresentBits - VisibleBits
```

再例如：

```csharp
public void FillChangedBits(
    ReadOnlySpan<ulong> before,
    ReadOnlySpan<ulong> after,
    Span<ulong> output)
{
    before.CopyTo(output);
    output.XorWith(after);
}
```

含义：

```text
ChangedBits = BeforeBits ^ AfterBits
```

推荐业务层自己表达语义，底层只保持原子能力。

---

## 11. 是否需要 Extension

需要，但要保持轻量。

推荐：

```text
BitSetOperations：
  核心实现。

BitSetExtensions：
  调用体验。
  只做转发。
```

Extension 的主要价值是：

```text
提高链式使用体验。
减少重复写 BitSetOperations。
让 output.AndNotWith(mask) 这类表达更自然。
```

但是 Extension 不应该替代核心 API。

核心 API 仍然以 `BitSetOperations` 为准。

---

## 12. 是否需要简单版返回新数组

暂时不建议。

不推荐：

```csharp
ulong[] result = BitSetOperations.AndNot(a, b);
```

原因：

```text
BitSet 是热路径工具。
返回新数组容易鼓励分配。
```

如果需要调试或非热路径使用，可以由调用方自己写：

```csharp
var result = new ulong[a.Length];

a.CopyTo(result);
result.AsSpan().AndNotWith(b);
```

这样分配行为非常明确。

---

## 13. 是否需要 int / long 版本

暂时不需要。

最终建议：

```text
主版本：ulong
可选版本：uint
不提供：int
不提供：long
```

原因：

```text
int / long 是有符号整数。
bitset word 更适合无符号类型。
int / long 版本会增加 API 面积，但没有明显收益。
```

---

## 14. Debug 检查策略

集合操作需要检查长度一致。

推荐只在 `DEBUG` 下检查：

```csharp
#if DEBUG
if (a.Length != b.Length)
    throw new ArgumentException("BitSet size mismatch.");
#endif
```

原因：

```text
开发期可以尽早发现错误。
Release 热路径减少额外分支。
```

如果后续发现线上也需要强校验，可以再改成 Release 也检查。

第一阶段建议：

```text
DEBUG 检查长度。
Release 假设调用方保证长度正确。
```

---

## 15. 最终 API 约定

## 15.1 BitSetOperations ulong API

```csharp
public static int BitLenToU64Len(int bitLength);

public static bool Get(ReadOnlySpan<ulong> bits, int bit);
public static void Set(Span<ulong> bits, int bit);
public static void Clear(Span<ulong> bits, int bit);
public static void ClearAll(Span<ulong> bits);

public static bool OrWith(Span<ulong> a, ReadOnlySpan<ulong> b);
public static bool AndWith(Span<ulong> a, ReadOnlySpan<ulong> b);
public static bool AndNotWith(Span<ulong> a, ReadOnlySpan<ulong> b);
public static bool XorWith(Span<ulong> a, ReadOnlySpan<ulong> b);
public static void NotWith(Span<ulong> bits);

public static bool IsEmpty(ReadOnlySpan<ulong> bits);
public static bool HasAnySet(ReadOnlySpan<ulong> bits);

public static bool ContainsAll(
    ReadOnlySpan<ulong> superset,
    ReadOnlySpan<ulong> subset);

public static bool Overlaps(
    ReadOnlySpan<ulong> a,
    ReadOnlySpan<ulong> b);

public static int PopCount(ReadOnlySpan<ulong> bits);
public static int FindFirstSet(ReadOnlySpan<ulong> bits);

public static int FindNextSet(
    ReadOnlySpan<ulong> bits,
    int start);

public static UInt64BitIterator EnumerateSetBits(
    ReadOnlySpan<ulong> bits);

public static void LeftShift(Span<ulong> bits, int count);
public static void RightShift(Span<ulong> bits, int count);
```

---

## 15.2 BitSetOperations uint API

```csharp
public static int BitLenToU32Len(int bitLength);

public static bool Get(ReadOnlySpan<uint> bits, int bit);
public static void Set(Span<uint> bits, int bit);
public static void Clear(Span<uint> bits, int bit);
public static void ClearAll(Span<uint> bits);

public static bool OrWith(Span<uint> a, ReadOnlySpan<uint> b);
public static bool AndWith(Span<uint> a, ReadOnlySpan<uint> b);
public static bool AndNotWith(Span<uint> a, ReadOnlySpan<uint> b);
public static bool XorWith(Span<uint> a, ReadOnlySpan<uint> b);
public static void NotWith(Span<uint> bits);

public static bool IsEmpty(ReadOnlySpan<uint> bits);
public static bool HasAnySet(ReadOnlySpan<uint> bits);

public static bool ContainsAll(
    ReadOnlySpan<uint> superset,
    ReadOnlySpan<uint> subset);

public static bool Overlaps(
    ReadOnlySpan<uint> a,
    ReadOnlySpan<uint> b);

public static int PopCount(ReadOnlySpan<uint> bits);
public static int FindFirstSet(ReadOnlySpan<uint> bits);

public static int FindNextSet(
    ReadOnlySpan<uint> bits,
    int start);

public static UInt32BitIterator EnumerateSetBits(
    ReadOnlySpan<uint> bits);

public static void LeftShift(Span<uint> bits, int count);
public static void RightShift(Span<uint> bits, int count);
```

---

## 15.3 BitSetExtensions API

```csharp
public static bool Get(this ReadOnlySpan<ulong> bits, int bit);
public static Span<ulong> Set(this Span<ulong> bits, int bit);
public static Span<ulong> Clear(this Span<ulong> bits, int bit);
public static Span<ulong> ClearAll(this Span<ulong> bits);

public static Span<ulong> OrWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> other);

public static Span<ulong> AndWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> other);

public static Span<ulong> AndNotWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> other);

public static Span<ulong> XorWith(
    this Span<ulong> bits,
    ReadOnlySpan<ulong> other);

public static Span<ulong> NotWith(this Span<ulong> bits);

public static bool IsEmpty(this ReadOnlySpan<ulong> bits);
public static bool HasAnySet(this ReadOnlySpan<ulong> bits);

public static bool ContainsAll(
    this ReadOnlySpan<ulong> superset,
    ReadOnlySpan<ulong> subset);

public static bool Overlaps(
    this ReadOnlySpan<ulong> bits,
    ReadOnlySpan<ulong> other);

public static int PopCount(this ReadOnlySpan<ulong> bits);
public static int FindFirstSet(this ReadOnlySpan<ulong> bits);

public static int FindNextSet(
    this ReadOnlySpan<ulong> bits,
    int start);

public static UInt64BitIterator EnumerateSetBits(
    this ReadOnlySpan<ulong> bits);

public static Span<ulong> LeftShift(
    this Span<ulong> bits,
    int count);

public static Span<ulong> RightShift(
    this Span<ulong> bits,
    int count);
```

`uint` 版本同理。

---

## 16. 原命名到新命名的迁移

| 原命名                    | 新命名                        |
| ---------------------- | -------------------------- |
| `BitOps`               | `BitSetOperations`         |
| `Contains(bits, bit)`  | `Get(bits, bit)`           |
| `Set(bits, bit)`       | `Set(bits, bit)`           |
| `Reset(bits, bit)`     | `Clear(bits, bit)`         |
| `Clear(bits)`          | `ClearAll(bits)`           |
| `CountOnes(bits)`      | `PopCount(bits)`           |
| `NextOne(bits, start)` | `FindNextSet(bits, start)` |
| `Ones(bits)`           | `EnumerateSetBits(bits)`   |
| `IsEmpty(bits)`        | 保留                         |
| `OrWith(a, b)`         | 保留                         |
| `AndWith(a, b)`        | 保留                         |
| `AndNotWith(a, b)`     | 保留                         |
| `XorWith(a, b)`        | 保留                         |

---

## 17. 最终结论

最终目录：

```text
ThreeTile.Core
└── Common
    └── BitSet
        ├── BitSetOperations.cs
        ├── BitSetExtensions.cs
        ├── UInt64BitIterator.cs
        └── UInt32BitIterator.cs
```

最终定位：

```text
BitSetOperations 是 BitOperations 的多 word / Span bitset 扩展版。
```

最终原则：

```text
BitSetOperations 负责 bitset 核心实现。
BitSetExtensions 负责链式调用体验。

BitSetOperations 只处理 Span<ulong> / ReadOnlySpan<ulong>。
BitSetOperations 可选支持 Span<uint> / ReadOnlySpan<uint>。
BitSetOperations 不提供 int / long 版本。

BitSetOperations 不关心业务对象。
BitSetOperations 不返回新集合。
BitSetOperations 不使用 LINQ。
BitSetOperations 不使用 IEnumerable。

调用方负责准备 output。
BitSetOperations 负责高性能原子操作。
业务层负责组合语义。
```

最终核心 API：

```text
Get
Set
Clear
ClearAll

OrWith
AndWith
AndNotWith
XorWith
NotWith

IsEmpty
HasAnySet
ContainsAll
Overlaps

PopCount
FindFirstSet
FindNextSet
EnumerateSetBits

LeftShift
RightShift
```

核心理解：

```text
BitSet：
  用一段整数数组表示一组 bit。

BitSetOperations：
  对 bitset 做多 word 高性能原子操作。

BitSetExtensions：
  提供链式调用体验。

PopCount：
  统计 set bit 数量，对齐官方命名。

FindFirstSet / FindNextSet：
  在多 word bitset 中查找置位 bit。

EnumerateSetBits：
  零 GC 遍历所有置位 bit index。

OrWith：
  a |= b。

AndWith：
  a &= b。

AndNotWith：
  a &= ~b。

XorWith：
  a ^= b。

NotWith：
  a = ~a。

LeftShift / RightShift：
  多 word 整体位移。
```

最推荐调用：

```csharp
source.CopyTo(output);
output.AsSpan().AndNotWith(mask);
```

高性能点：

```text
输入使用 ReadOnlySpan<T>
输出使用 Span<T>
调用者复用 output
内部使用 word 级 for 循环
使用 BitOperations.PopCount
使用 BitOperations.TrailingZeroCount
避免每次 new ulong[]
避免 LINQ
避免 IEnumerable
```

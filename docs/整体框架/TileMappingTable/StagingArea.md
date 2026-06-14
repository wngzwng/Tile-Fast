ThreeTile StagingArea 优化设计文档
1. 背景与目标
StagingArea，也就是卡槽 / 暂存槽，负责维护：
已被选入，但尚未消除的 tile

它位于求解热路径中，会被高频读写：
scorer
FSE
analyser
每一步行为评估
颜色分布查询
末尾牌查询
容量判断
匹配判断

因此它必须尽量简单、稳定、可预测。
优化目标：
零 Hash
零 Dict
零 List 分配
热路径只使用 int[] / Span / 计数数组
Clone / Reset 退化为 memcpy / memclear
对外行为与旧实现等价

StagingArea 不负责：
匹配策略选择
自动消除策略
Move 生成
Move 评分
关卡整体状态管理

它只负责维护卡槽内部状态。

2. 设计前提
ThreeTile 的卡槽有三个关键事实：
1. tileIndex 有界，通常 <= TotalCount
2. color / suit 是小调色板，PaletteSize = maxColor + 1，通常 <= 32
3. 卡槽容量很小，Capacity 通常 <= 10

因此可以彻底去掉：
HashSet
Dictionary
List
LINQ

改成数组结构：
槽内 tile:
  int[Capacity]

颜色数量:
  int[PaletteSize]

颜色出现顺序:
  int[PaletteSize] + count

末尾牌 / 分布查询:
  对小数组线性扫描

卡槽容量极小，所以这里的线性扫描是常数级，缓存友好，通常远快于哈希结构。

3. 数据布局
public sealed class StagingArea
{
    private readonly TileStaticMap _map;
    private readonly LevelCore _parent;

    // 槽内 tile，按插入顺序排列，长度为最大容量
    private readonly int[] _slotTiles;
    private int _count;

    // color -> count
    private readonly int[] _colorCount;

    // 当前出现过的颜色，按首次出现顺序排列
    private readonly int[] _colorSeq;
    private int _colorSeqCount;

    public int Capacity { get; private set; }

    public int MatchRequireCount => _parent.MatchRequireCount;

    public int TileCount => _count;

    public int UsedCapacity => _count;

    public int AvailableCapacity => Capacity - _count;

    public bool IsFull => _count >= Capacity;

    public bool IsEmpty => _count == 0;

    public StagingArea(LevelCore parent, TileStaticMap map)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _map = map ?? throw new ArgumentNullException(nameof(map));

        Capacity = parent.SlotCapacity;

        _slotTiles = new int[Capacity];
        _colorCount = new int[map.PaletteSize];
        _colorSeq = new int[map.PaletteSize];
    }
}

字段说明：
_slotTiles:
  当前卡槽里的 tileIndex，按进入卡槽的顺序排列。

_count:
  当前卡槽中 tile 数量。

_colorCount:
  每种颜色当前在卡槽中的数量。

_colorSeq:
  当前卡槽中出现过的颜色，按颜色首次出现顺序排列。

_colorSeqCount:
  当前活跃颜色数量。

Capacity:
  当前卡槽容量。


4. 核心不变量
所有写操作都必须维护以下不变量。
INV1:
  _count == 所有 _colorCount[c] 之和

INV2:
  _colorSeq[0.._colorSeqCount) 恰好是 _colorCount[c] > 0 的颜色集合

INV3:
  _colorSeq 中没有重复颜色

INV4:
  _colorSeq 顺序等于各颜色当前首次出现的先后顺序

INV5:
  _slotTiles[0.._count) 按 tile 进入卡槽的先后顺序排列

INV6:
  0 <= _count <= Capacity

测试时应该把这些不变量作为断言。

5. 核心 API
5.1 AddTile
public void AddTile(int tileIndex)
{
    if (_count >= Capacity)
    {
        throw new InvalidOperationException(
            $"StagingArea full. Capacity={Capacity}, Count={_count}");
    }

    // 去重：卡槽极小，线性扫描即可。
    // 如果上层严格保证不会重复加入，可以删除这段。
    for (int i = 0; i < _count; i++)
    {
        if (_slotTiles[i] == tileIndex)
            return;
    }

    int color = _map.Color(tileIndex);

    _slotTiles[_count++] = tileIndex;

    if (_colorCount[color]++ == 0)
    {
        _colorSeq[_colorSeqCount++] = color;
    }
}

语义：
1. 检查容量
2. 可选去重
3. tileIndex 追加到 _slotTiles 尾部
4. colorCount[color] 自增
5. 如果该颜色首次出现，则追加到 _colorSeq 尾部


5.2 RemoveTile
public void RemoveTile(int tileIndex)
{
    int pos = IndexOf(tileIndex);

    if (pos < 0)
        return;

    for (int i = pos; i < _count - 1; i++)
    {
        _slotTiles[i] = _slotTiles[i + 1];
    }

    _count--;

    int color = _map.Color(tileIndex);

    if (--_colorCount[color] == 0)
    {
        RemoveFromColorSeq(color);
    }
}

辅助方法：
private int IndexOf(int tileIndex)
{
    for (int i = 0; i < _count; i++)
    {
        if (_slotTiles[i] == tileIndex)
            return i;
    }

    return -1;
}

语义：
1. 找到 tileIndex 在卡槽中的位置
2. 左移紧凑，保持插入序
3. 更新颜色计数
4. 如果颜色数量归零，从 _colorSeq 中删除该颜色


6. 末尾牌查询
旧语义通常是：
lastColor = GetColors()[^1]
lastTile = GetTilesOfColor(lastColor)[^1]

新实现中：
_colorSeq[^1] 表示最后出现的颜色
LastTileOfColor(color) 倒扫 _slotTiles，得到该颜色最后进入卡槽的 tile

6.1 查询某颜色最后一张牌
public int LastTileOfColor(int color)
{
    for (int i = _count - 1; i >= 0; i--)
    {
        int tileIndex = _slotTiles[i];

        if (_map.Color(tileIndex) == color)
            return tileIndex;
    }

    return -1;
}

6.2 查询卡槽末尾牌
public int LastTile()
{
    if (_colorSeqCount == 0)
        return -1;

    int lastColor = _colorSeq[_colorSeqCount - 1];

    return LastTileOfColor(lastColor);
}


7. 颜色分布查询
7.1 当前活跃颜色
public ReadOnlySpan<int> ActiveColors
    => _colorSeq.AsSpan(0, _colorSeqCount);

语义等价旧实现：
GetColors()

但是不分配数组。

7.2 颜色计数表
public ReadOnlySpan<int> ColorCounts => _colorCount;

按 color 下标访问：
int count = stagingArea.ColorCounts[color];


7.3 查询某颜色数量
public int GetColorCount(int color)
{
    return _colorCount[color];
}


7.4 判断某颜色是否可匹配
public bool CanMatch(int color)
{
    return _colorCount[color] >= MatchRequireCount;
}


7.5 填充分布到调用方 buffer
public void FillColorCounts(Span<int> destination)
{
    for (int i = 0; i < _colorSeqCount; i++)
    {
        int color = _colorSeq[i];
        destination[color] = _colorCount[color];
    }
}

注意：
FillColorCounts 只写活跃颜色。
如果 destination 复用，调用方需要在外部 Clear。

如果希望函数内部保证完整清理，可以改成：
public void FillColorCountsCleared(Span<int> destination)
{
    destination.Clear();

    for (int i = 0; i < _colorSeqCount; i++)
    {
        int color = _colorSeq[i];
        destination[color] = _colorCount[color];
    }
}

热路径建议由调用方负责清理，避免重复清空。

8. 删除某颜色的所有 tile
public int RemoveColor(int color, Span<int> removedBuffer)
{
    if (_colorCount[color] == 0)
        return 0;

    int write = 0;
    int removed = 0;

    for (int i = 0; i < _count; i++)
    {
        int tileIndex = _slotTiles[i];

        if (_map.Color(tileIndex) == color)
        {
            removedBuffer[removed++] = tileIndex;
        }
        else
        {
            _slotTiles[write++] = tileIndex;
        }
    }

    _count = write;
    _colorCount[color] = 0;

    RemoveFromColorSeq(color);

    return removed;
}

语义：
1. 扫描 _slotTiles
2. 属于目标颜色的 tile 写入 removedBuffer
3. 其他 tile 左移保留
4. 更新 _count
5. 清空 colorCount[color]
6. 从 _colorSeq 删除该颜色


9. 匹配相关 API
9.1 PeekMatch
查看如果匹配某颜色，会取出哪些 tile。
不修改状态。
public int PeekMatch(int color, Span<int> buffer)
{
    if (_colorCount[color] < MatchRequireCount)
        return 0;

    int need = MatchRequireCount;
    int got = 0;

    for (int i = 0; i < _count && got < need; i++)
    {
        int tileIndex = _slotTiles[i];

        if (_map.Color(tileIndex) == color)
        {
            buffer[got++] = tileIndex;
        }
    }

    return got;
}

语义：
按插入序取该颜色前 MatchRequireCount 张 tile。


9.2 TryMatch
执行一次匹配，移除该颜色前 MatchRequireCount 张 tile。
public int TryMatch(int color, Span<int> matchedBuffer)
{
    int matchedCount = PeekMatch(color, matchedBuffer);

    if (matchedCount == 0)
        return 0;

    int need = MatchRequireCount;
    int removed = 0;
    int write = 0;

    for (int i = 0; i < _count; i++)
    {
        int tileIndex = _slotTiles[i];

        if (removed < need && _map.Color(tileIndex) == color)
        {
            removed++;
        }
        else
        {
            _slotTiles[write++] = tileIndex;
        }
    }

    _count = write;
    _colorCount[color] -= need;

    if (_colorCount[color] == 0)
    {
        RemoveFromColorSeq(color);
    }

    return need;
}

语义等价旧逻辑：
group.RemoveRange(0, MatchRequireCount)

也就是按该颜色 tile 的插入序移除前 N 张。

10. 内部辅助方法
10.1 RemoveFromColorSeq
private void RemoveFromColorSeq(int color)
{
    for (int i = 0; i < _colorSeqCount; i++)
    {
        if (_colorSeq[i] != color)
            continue;

        for (int j = i; j < _colorSeqCount - 1; j++)
        {
            _colorSeq[j] = _colorSeq[j + 1];
        }

        _colorSeqCount--;
        return;
    }
}

语义：
从活跃颜色序列中移除指定颜色。
保持其他颜色的相对顺序不变。


11. Reset / CopyFrom / SetCapacity
11.1 Reset
public void Reset()
{
    _count = 0;
    _colorSeqCount = 0;

    Array.Clear(_colorCount, 0, _colorCount.Length);

    Capacity = _parent.SlotCapacity;
}

注意：
_slotTiles 不需要清空。
因为有效范围只由 _count 决定。


11.2 CopyFrom
CopyFrom 是 Clone 的零分配形式。
public void CopyFrom(StagingArea src)
{
    Capacity = src.Capacity;
    _count = src._count;
    _colorSeqCount = src._colorSeqCount;

    Array.Copy(src._slotTiles, _slotTiles, src._count);
    Array.Copy(src._colorCount, _colorCount, _colorCount.Length);
    Array.Copy(src._colorSeq, _colorSeq, src._colorSeqCount);
}

语义：
复制槽内 tile
复制颜色计数
复制活跃颜色序列
不创建新对象


11.3 SetCapacity
public void SetCapacity(int capacity)
{
    if (capacity < _count)
    {
        throw new InvalidOperationException(
            $"Cannot set capacity to {capacity}, current count is {_count}.");
    }

    Capacity = capacity;
}

注意：
如果 capacity 可能超过 _slotTiles.Length，
则 _slotTiles 必须按最大可能容量预分配。

热路径不建议在 SetCapacity 中 Array.Resize。

推荐策略：
初始化时按最大可能容量分配。
运行时只修改 Capacity 值。


12. 复杂度分析
操作
复杂度
说明
GetColorCount
O(1)
数组读
CanMatch
O(1)
数组读
AddTile
O(slot)
去重扫描；去掉去重则 O(1)
RemoveTile
O(slot)
定位 + 左移
RemoveColor
O(slot)
一次紧凑扫描
PeekMatch
O(slot)
按插入序扫描
TryMatch
O(slot)
一次紧凑扫描
LastTile
O(slot)
倒扫
LastTileOfColor
O(slot)
倒扫
FillColorCounts
O(activeColorCount)
只遍历活跃颜色
Reset
O(palette)
清空颜色计数
CopyFrom
O(slot + palette)
数组复制
其中：
slot = 卡槽容量，通常是个位数
palette = 颜色数量，通常 <= 32

所以这些操作在实际运行中都是很小的常数操作。

13. 旧接口迁移对照
旧接口 / 旧用法
新接口 / 新用法
说明
int[] GetColors()
ReadOnlySpan<int> ActiveColors
零分配
Dictionary<int, int> GetColorCountMap()
ActiveColors + GetColorCount
不再创建字典
GetTilesOfColor(color)
扫 _slotTiles 或新增填充版
热路径建议填充到 Span
末尾牌三步查询
LastTile()
一次调用
HashSet.Contains(tile)
IndexOf(tile) >= 0
卡槽小，扫描足够
Clone(newParent)
CopyFrom(src)
复用对象，数组复制
Clear()
Reset()
清空计数状态
如果必须兼容旧接口，可以临时保留旧方法：
旧接口内部基于新底层结构按需构造 int[] / Dictionary。

但这些兼容方法只允许在非热路径使用。

14. 正确性与旧语义对齐
14.1 末尾牌语义
旧语义：
lastColor = GetColors()[^1]
lastTile = GetTilesOfColor(lastColor)[^1]

新语义：
lastColor = _colorSeq[_colorSeqCount - 1]
lastTile = LastTileOfColor(lastColor)

二者等价。

14.2 颜色清空后再次出现
当某颜色数量归零时：
RemoveFromColorSeq(color)

会把该颜色从活跃颜色序列中删除。
如果之后再次 AddTile 该颜色：
_colorCount[color] 从 0 变 1
该颜色会重新追加到 _colorSeq 尾部

这与旧 List 行为一致。

14.3 插入序保持
以下操作都必须保持 _slotTiles 插入序：
RemoveTile
RemoveColor
TryMatch

实现方式是：
扫描 + write 指针左移紧凑


14.4 TryMatch 语义
TryMatch 移除的是：
该颜色按插入序排列的前 MatchRequireCount 张 tile

等价旧逻辑：
group.RemoveRange(0, MatchRequireCount)


15. 测试与对拍建议
建议保留旧版 StagingArea 作为 oracle，新旧实现并行执行同一组随机操作。
随机操作包括：
AddTile
RemoveTile
RemoveColor
PeekMatch
TryMatch
Reset
CopyFrom
SetCapacity

每一步断言：
TileCount 相等
每个 color 的 GetColorCount 相等
ActiveColors 序列等于旧 GetColors() 序列
LastTile() 结果一致
PeekMatch 返回内容一致
TryMatch 返回内容一致
RemoveColor 返回内容一致

同时校验不变量：
INV1: _count == sum(_colorCount)
INV2: _colorSeq 恰好包含所有 count > 0 的颜色
INV3: _colorSeq 无重复
INV4: _colorSeq 顺序正确
INV5: _slotTiles 插入序正确
INV6: 0 <= _count <= Capacity

随机几千步全部通过后，可以认为新实现是旧实现的等价高性能替换。

16. 推荐最终结构
ThreeTile.Core
├── StagingArea.cs
├── StagingAreaDebugValidator.cs
├── TileStaticMap.cs
└── Docs
    └── StagingArea.md

其中：
StagingArea:
  生产代码，只保留高性能数据结构和核心 API。

StagingAreaDebugValidator:
  DEBUG / TEST 环境下校验 INV1..INV6。

TileStaticMap:
  提供 tileIndex -> color 等静态事实。

Docs/StagingArea.md:
  当前设计文档。


17. 最终总结
StagingArea 的优化核心是：
用数组表达卡槽
用颜色计数数组表达分布
用颜色序列数组表达颜色出现顺序
用 Span 输出临时结果
用 CopyFrom 替代 Clone 分配
用 Reset 清空计数状态

最终热路径中不再依赖：
HashSet
Dictionary
List
LINQ
ToArray

一句话：
StagingArea 本质是一个容量极小、颜色有限、tileIndex 有界的状态容器。
它最适合用 int[] + count 数组实现，而不是 Hash / Dict。

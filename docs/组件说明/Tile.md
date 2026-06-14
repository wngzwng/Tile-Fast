# Tile

## 1. 定位

`Tile` 表示单张牌的必要静态事实。

它只负责描述：

* 这张牌是谁
* 这张牌是什么花色
* 这张牌当前在什么位置
* 这张牌占据多大体积
* 这张牌当前属于哪个区域

它不负责：

* 全局索引映射
* Suit -> indexes 之类的聚合关系
* RegionId -> TileIndex 的查询
* 可见 / 可选 / 锁定等动态状态

也就是：

* `Tile` 是单张牌的静态事实
* `TileMappingTable` 是全局静态映射
* `BitSet / LevelState` 是动态状态

---

## 2. 推荐字段

`Tile` 建议保存：

* `Index`
* `Suit`
* `Position`
* `Volume`
* `Zone`

建议主构造参数只保留：

* `index`
* `position`

其余字段初始值建议为：

* `Suit = SuitUnspecified`
* `Volume = DefaultVolume`
* `Zone = TileZone.Unspecified`

### 2.1 Index

关卡内唯一编号，使用 0-based。

也就是：

* 第一张牌的 `Index = 0`
* 第二张牌的 `Index = 1`

### 2.2 Suit

牌的花色编号，使用 0-based。

如果暂时未指定，可以使用：

* `SuitUnspecified = -1`

### 2.3 Position

Tile 的最小空间坐标，使用 packed xyz 表示。

语义：

* `Position = packed(x, y, z)`

这里的 `x / y / z` 也可以理解成：

* `Col / Row / Z`

即：

* `Col` 等价于 `X`
* `Row` 等价于 `Y`

### 2.4 Volume

Tile 的体积，使用 packed xyz 表示。

语义：

* `Volume = packed(dx, dy, dz)`

默认体积建议为：

* `(2, 2, 1)`

### 2.5 Zone

Tile 当前所在区域。

建议的区域枚举：

* `Unspecified`
* `StagingArea`
* `Corral`
* `Pasture`

默认值建议为：

* `TileZone.Unspecified`

---

## 3. Position / Volume 的 packed 约定

当前采用 `PackedXyzExtensions` 统一处理 packed xyz。

命名空间：

```csharp
namespace ThreeTile.Core.ExtensionTools;
```

布局约定：

```text
X | Y | Z | Reserved
```

每个分量占 8 bit。

也就是：

* `XShift = 0`
* `YShift = 8`
* `ZShift = 16`

读取方式：

* `value.X()`
* `value.Y()`
* `value.Z()`

别名读取方式：

* `value.Col()`
* `value.Row()`

改写方式：

* `value.WithX(x)`
* `value.WithY(y)`
* `value.WithZ(z)`

别名改写方式：

* `value.WithCol(col)`
* `value.WithRow(row)`

字符串展示：

* `value.ToXyzString()`

---

## 4. TopZ

`Tile` 可以提供一个便捷属性：

* `TopZ`

公式：

```text
topZ = z0 + dz - 1
```

其中：

* `z0` 来自 `Position`
* `dz` 来自 `Volume`

这个值对遮挡判断很常用，所以放在 `Tile` 上是合理的。

---

## 5. 行为约定

`Tile` 建议提供的行为：

* `SetIndex(int index)`
* `SetSuit(int suit)`
* `SetPasturePosition(int position)`
* `SetZone(TileZone zone)`
* `GetPositionXyz()`
* `GetVolumeXyz()`

其中：

### 5.1 SetPasturePosition

更新 Tile 的空间位置：

* `Position = position`

约定：

* 这里只更新 `Position`
* 不强制同步修改 `Zone`

### 5.2 SetZone

只更新 `Zone`。

---

## 6. 不建议塞进 Tile 的内容

不建议放进 `Tile`：

* Suit -> TileIndexes
* RegionId -> TileIndex 查询
* Position -> RegionId 表
* 所有占用坐标数组
* 可见 / 可选 / 锁定等动态状态
* 复杂邻居查找逻辑

这些更适合放在：

* `TileMappingTable`
* `LevelState / BitSet`
* 邻居 / 空间分析工具

---

## 7. 当前实现落点

当前代码落点：

* [src/Tile.Core/Core/Tile.cs](/Users/wngzwng/projects/godot/Tile-Fast/src/Tile.Core/Core/Tile.cs)
* [src/Tile.Core/Extensions/PackedXyzExtensions.cs](/Users/wngzwng/projects/godot/Tile-Fast/src/Tile.Core/Extensions/PackedXyzExtensions.cs)

这两个文件对应的是：

* `Tile` 本体
* packed xyz 的基础扩展工具

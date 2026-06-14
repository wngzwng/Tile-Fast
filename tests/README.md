# tests

测试目录按源码项目一一对应组织。

当前结构：

```text
tests/
└── Tile.Core.Tests/
    └── Common/
        ├── BitSet/
        └── Math/
```

约定：

* 测试目录尽量与源码目录结构保持一致，方便定位。
* `Tile.Core/Common/Math` 对应 `tests/Tile.Core.Tests/Common/Math`
* `Tile.Core/Common/BitSet` 对应 `tests/Tile.Core.Tests/Common/BitSet`

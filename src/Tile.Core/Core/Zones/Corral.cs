    namespace ThreeTile.Core.WngZwng.Core.Zones;

    /// <summary>
    /// Corral（围栏）
    /// ------------------------------------------------------------
    /// 已完成收集的 Tile 集合（LIFO）。
    ///
    /// 特性：
    /// - 顺序：Stack（用于回滚）
    /// - 存在性：HashSet（O(1)）
    /// - 颜色计数：Dictionary
    ///
    /// 不负责：
    /// - 几何 / Move / 策略
    /// </summary>
    public sealed class Corral
    {
        private readonly Stack<int> _stack = new();
        private readonly HashSet<int> _set = new();
        private readonly Dictionary<int, int> _colorCount = new();

        public Corral(LevelCore level)
        {
            Parent = level ?? throw new ArgumentNullException(nameof(level));
        }

        public LevelCore Parent { get; }
        
        public int TileCount => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;

        // =========================================================
        // Core Ops
        // =========================================================

        public void Reset()
        {
            _stack.Clear();
            _set.Clear();
            _colorCount.Clear();
        }

        public Corral Clone(LevelCore newParent)
        {
            var clone = new Corral(newParent);

            foreach (var idx in _stack.Reverse())
            {
                clone._stack.Push(idx);
                clone._set.Add(idx);
            }

            foreach (var (color, count) in _colorCount)
                clone._colorCount[color] = count;

            return clone;
        }

        // =========================================================
        // Add / Accept
        // =========================================================

        public void Accept(IEnumerable<int> tileIndexes)
        {
            foreach (var idx in tileIndexes)
                Add(idx);
        }

        public void Add(int tileIndex)
        {
            if (!_set.Add(tileIndex))
                return;

            if (!Parent.TryGetTileByIndex(tileIndex, out var tile))
            {
                _set.Remove(tileIndex);
                return;
            }

            _stack.Push(tileIndex);

            _colorCount[tile.Color] = _colorCount.TryGetValue(tile.Color, out var c)
                ? c + 1
                : 1;
        }

        // =========================================================
        // Retrieve (LIFO)
        // =========================================================

        public bool TryPop(out int tileIndex)
        {
            if (_stack.Count == 0)
            {
                tileIndex = default;
                return false;
            }

            tileIndex = _stack.Pop();
            _set.Remove(tileIndex);

            if (Parent.TryGetTileByIndex(tileIndex, out var tile))
            {
                var next = _colorCount[tile.Color] - 1;
                if (next == 0)
                    _colorCount.Remove(tile.Color);
                else
                    _colorCount[tile.Color] = next;
            }

            return true;
        }

        public int[] PopMany(int count)
        {
    #if DEBUG
            if (count > _stack.Count)
                throw new ArgumentOutOfRangeException(nameof(count));
    #endif

            var result = new int[count];

            for (int i = 0; i < count; i++)
            {
                TryPop(out var idx);
                result[i] = idx;
            }

            return result;
        }

        public bool TryPeek(out int tileIndex)
        {
            if (_stack.Count == 0)
            {
                tileIndex = default;
                return false;
            }

            tileIndex = _stack.Peek();
            return true;
        }

        // =========================================================
        // Query
        // =========================================================

        public bool Contains(int tileIndex) => _set.Contains(tileIndex);

        public int GetColorCount(int color)
            => _colorCount.TryGetValue(color, out var c) ? c : 0;

        public bool HasCompletedColor(int color, int required)
            => GetColorCount(color) >= required;

        public bool IsAllCollected()
            => _stack.Count >= Parent.TotalCount;

        // =========================================================
        // Snapshot（非核心路径）
        // =========================================================

        public int[] ToArrayOrdered()
            => _stack.Reverse().ToArray();

        public IReadOnlyDictionary<int, int> ColorCounter => _colorCount;

        public IReadOnlyDictionary<int, IReadOnlyList<int>> GetByColor()
        {
            var result = new Dictionary<int, List<int>>();

            foreach (var idx in _stack.Reverse())
            {
                if (!Parent.TryGetTileByIndex(idx, out var tile))
                    continue;

                if (!result.TryGetValue(tile.Color, out var list))
                    result[tile.Color] = list = new List<int>();

                list.Add(idx);
            }

            return result.ToDictionary(k => k.Key, v => (IReadOnlyList<int>)v.Value);
        }
    }
namespace Tile.Core.Metrices;

/// <summary>
/// 指标容器。
/// 第一版支持 int、double、bool、string、List&lt;int&gt;、List&lt;double&gt;。
/// </summary>
public sealed class MetricBag
{
    private readonly Dictionary<string, int> _ints = new();
    private readonly Dictionary<string, double> _doubles = new();
    private readonly Dictionary<string, bool> _bools = new();
    private readonly Dictionary<string, string> _strings = new();

    private readonly Dictionary<string, List<int>> _intLists = new();
    private readonly Dictionary<string, List<double>> _doubleLists = new();

    #region int

    public void Set(MetricKey<int> key, int value)
    {
        _ints[key.Name] = value;
    }

    public void Add(MetricKey<int> key, int value)
    {
        if (_ints.TryGetValue(key.Name, out var oldValue))
            _ints[key.Name] = oldValue + value;
        else
            _ints[key.Name] = value;
    }

    public bool TryRead(MetricKey<int> key, out int value)
    {
        return _ints.TryGetValue(key.Name, out value);
    }

    public int GetOrDefault(MetricKey<int> key, int defaultValue = default)
    {
        return _ints.TryGetValue(key.Name, out var value)
            ? value
            : defaultValue;
    }

    #endregion

    #region double

    public void Set(MetricKey<double> key, double value)
    {
        _doubles[key.Name] = value;
    }

    public void Add(MetricKey<double> key, double value)
    {
        if (_doubles.TryGetValue(key.Name, out var oldValue))
            _doubles[key.Name] = oldValue + value;
        else
            _doubles[key.Name] = value;
    }

    public bool TryRead(MetricKey<double> key, out double value)
    {
        return _doubles.TryGetValue(key.Name, out value);
    }

    public double GetOrDefault(MetricKey<double> key, double defaultValue = default)
    {
        return _doubles.TryGetValue(key.Name, out var value)
            ? value
            : defaultValue;
    }

    #endregion

    #region bool

    public void Set(MetricKey<bool> key, bool value)
    {
        _bools[key.Name] = value;
    }

    public bool TryRead(MetricKey<bool> key, out bool value)
    {
        return _bools.TryGetValue(key.Name, out value);
    }

    public bool GetOrDefault(MetricKey<bool> key, bool defaultValue = default)
    {
        return _bools.TryGetValue(key.Name, out var value)
            ? value
            : defaultValue;
    }

    #endregion

    #region string

    public void Set(MetricKey<string> key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _strings[key.Name] = value;
    }

    public bool TryRead(MetricKey<string> key, out string value)
    {
        if (_strings.TryGetValue(key.Name, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public string GetOrDefault(MetricKey<string> key, string defaultValue = "")
    {
        return _strings.TryGetValue(key.Name, out var value)
            ? value
            : defaultValue;
    }

    #endregion

    #region List<int>

    public void Set(MetricKey<List<int>> key, List<int> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _intLists[key.Name] = value;
    }

    public void Append(MetricKey<List<int>> key, int value)
    {
        if (!_intLists.TryGetValue(key.Name, out var list))
        {
            list = new List<int>();
            _intLists[key.Name] = list;
        }

        list.Add(value);
    }

    public bool TryRead(
        MetricKey<List<int>> key,
        out IReadOnlyList<int> value)
    {
        if (_intLists.TryGetValue(key.Name, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<int>();
        return false;
    }

    public IReadOnlyList<int> GetOrEmpty(MetricKey<List<int>> key)
    {
        return _intLists.TryGetValue(key.Name, out var list)
            ? list
            : Array.Empty<int>();
    }

    #endregion

    #region List<double>

    public void Set(MetricKey<List<double>> key, List<double> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _doubleLists[key.Name] = value;
    }

    public void Append(MetricKey<List<double>> key, double value)
    {
        if (!_doubleLists.TryGetValue(key.Name, out var list))
        {
            list = new List<double>();
            _doubleLists[key.Name] = list;
        }

        list.Add(value);
    }

    public bool TryRead(
        MetricKey<List<double>> key,
        out IReadOnlyList<double> value)
    {
        if (_doubleLists.TryGetValue(key.Name, out var list))
        {
            value = list;
            return true;
        }

        value = Array.Empty<double>();
        return false;
    }

    public IReadOnlyList<double> GetOrEmpty(MetricKey<List<double>> key)
    {
        return _doubleLists.TryGetValue(key.Name, out var list)
            ? list
            : Array.Empty<double>();
    }

    #endregion

    #region 清理

    /// <summary>
    /// 彻底清空指标结构。
    /// </summary>
    public void Clear()
    {
        _ints.Clear();
        _doubles.Clear();
        _bools.Clear();
        _strings.Clear();

        _intLists.Clear();
        _doubleLists.Clear();
    }

    /// <summary>
    /// 清空当前指标值，但保留 List 容器和 List 内部容量。
    /// </summary>
    public void ResetValues()
    {
        _ints.Clear();
        _doubles.Clear();
        _bools.Clear();
        _strings.Clear();

        foreach (var pair in _intLists)
            pair.Value.Clear();

        foreach (var pair in _doubleLists)
            pair.Value.Clear();
    }

    #endregion
}

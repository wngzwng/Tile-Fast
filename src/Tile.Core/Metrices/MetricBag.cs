namespace Tile.Core.Metrices;

using System.Collections;
using System.Globalization;

/// <summary>
/// 指标容器。调用侧使用 MetricKey&lt;TValue&gt; 保持强类型，内部按类型分仓存储。
/// </summary>
public sealed class MetricBag
{
    private readonly Dictionary<string, int> _ints = new();
    private readonly Dictionary<string, double> _doubles = new();
    private readonly Dictionary<string, bool> _bools = new();
    private readonly Dictionary<string, string> _strings = new();

    private readonly Dictionary<string, List<int>> _intLists = new();
    private readonly Dictionary<string, List<double>> _doubleLists = new();
    private readonly Dictionary<string, object> _objects = new();
    private readonly Dictionary<string, List<object>> _objectLists = new();

    #region 写入

    public void Set<TValue>(MetricKey<TValue> key, TValue value)
    {
        var valueType = typeof(TValue);

        ArgumentNullException.ThrowIfNull(value);

        if (valueType == typeof(int))
        {
            _ints[key.Name] = (int)(object)value;
            return;
        }

        if (valueType == typeof(double))
        {
            _doubles[key.Name] = (double)(object)value;
            return;
        }

        if (valueType == typeof(bool))
        {
            _bools[key.Name] = (bool)(object)value;
            return;
        }

        if (valueType == typeof(string))
        {
            _strings[key.Name] = (string)(object)value;
            return;
        }

        if (valueType == typeof(List<int>))
        {
            _intLists[key.Name] = (List<int>)(object)value;
            return;
        }

        if (valueType == typeof(List<double>))
        {
            _doubleLists[key.Name] = (List<double>)(object)value;
            return;
        }

        if (IsListType(valueType) && value is IEnumerable items)
        {
            _objectLists[key.Name] = CopyToObjectList(items);
            return;
        }

        _objects[key.Name] = value;
    }

    public void Add(MetricKey<int> key, int value)
    {
        if (_ints.TryGetValue(key.Name, out var oldValue))
            _ints[key.Name] = oldValue + value;
        else
            _ints[key.Name] = value;
    }

    public void Add(MetricKey<double> key, double value)
    {
        if (_doubles.TryGetValue(key.Name, out var oldValue))
            _doubles[key.Name] = oldValue + value;
        else
            _doubles[key.Name] = value;
    }

    public void Append<TValue>(MetricKey<List<TValue>> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (typeof(TValue) == typeof(int))
        {
            if (!_intLists.TryGetValue(key.Name, out var list))
            {
                list = new List<int>();
                _intLists[key.Name] = list;
            }

            list.Add((int)(object)value);
            return;
        }

        if (typeof(TValue) == typeof(double))
        {
            if (!_doubleLists.TryGetValue(key.Name, out var list))
            {
                list = new List<double>();
                _doubleLists[key.Name] = list;
            }

            list.Add((double)(object)value);
            return;
        }

        if (!_objectLists.TryGetValue(key.Name, out var objectList))
        {
            objectList = new List<object>();
            _objectLists[key.Name] = objectList;
        }

        objectList.Add(value);
    }

    #endregion

    #region 读取

    public bool TryRead<TValue>(MetricKey<TValue> key, out TValue? value)
    {
        var valueType = typeof(TValue);

        if (valueType == typeof(int))
            return TryReadTyped(_ints, key.Name, out value);

        if (valueType == typeof(double))
            return TryReadTyped(_doubles, key.Name, out value);

        if (valueType == typeof(bool))
            return TryReadTyped(_bools, key.Name, out value);

        if (valueType == typeof(string))
        {
            if (!_strings.TryGetValue(key.Name, out var stringValue))
            {
                value = (TValue)(object)string.Empty;
                return false;
            }

            value = (TValue)(object)stringValue;
            return true;
        }

        if (valueType == typeof(List<int>))
            return TryReadTyped(_intLists, key.Name, out value);

        if (valueType == typeof(List<double>))
            return TryReadTyped(_doubleLists, key.Name, out value);

        if (IsListType(valueType))
            return TryReadObjectList(key.Name, out value);

        if (_objects.TryGetValue(key.Name, out var found) &&
            found is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    public TValue? GetOrDefault<TValue>(
        MetricKey<TValue> key,
        TValue? defaultValue = default)
    {
        return TryRead(key, out TValue? value)
            ? value
            : defaultValue;
    }

    public IReadOnlyList<TValue> GetOrEmpty<TValue>(
        MetricKey<List<TValue>> key)
    {
        if (typeof(TValue) == typeof(int) &&
            _intLists.TryGetValue(key.Name, out var intList))
            return (IReadOnlyList<TValue>)(object)intList;

        if (typeof(TValue) == typeof(double) &&
            _doubleLists.TryGetValue(key.Name, out var doubleList))
            return (IReadOnlyList<TValue>)(object)doubleList;

        return TryReadObjectList<List<TValue>>(key.Name, out var list)
            ? list!
            : Array.Empty<TValue>();
    }

    #endregion

    #region 通用读取

    public bool Contains(IMetricKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var valueType = key.ValueType;

        if (valueType == typeof(int))
            return _ints.ContainsKey(key.Name);

        if (valueType == typeof(double))
            return _doubles.ContainsKey(key.Name);

        if (valueType == typeof(bool))
            return _bools.ContainsKey(key.Name);

        if (valueType == typeof(string))
            return _strings.ContainsKey(key.Name);

        if (valueType == typeof(List<int>))
            return _intLists.ContainsKey(key.Name);

        if (valueType == typeof(List<double>))
            return _doubleLists.ContainsKey(key.Name);

        return IsListType(valueType)
            ? _objectLists.ContainsKey(key.Name)
            : _objects.ContainsKey(key.Name);
    }

    public bool TryReadText(IMetricKey key, out string value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var valueType = key.ValueType;

        if (valueType == typeof(int) &&
            _ints.TryGetValue(key.Name, out var intValue))
        {
            value = intValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (valueType == typeof(double) &&
            _doubles.TryGetValue(key.Name, out var doubleValue))
        {
            value = doubleValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (valueType == typeof(bool) &&
            _bools.TryGetValue(key.Name, out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        if (valueType == typeof(string) &&
            _strings.TryGetValue(key.Name, out var stringValue))
        {
            value = stringValue;
            return true;
        }

        if (valueType == typeof(List<int>) &&
            _intLists.TryGetValue(key.Name, out var intListValue))
        {
            value = string.Join(
                "|",
                intListValue.Select(item => item.ToString(CultureInfo.InvariantCulture)));
            return true;
        }

        if (valueType == typeof(List<double>) &&
            _doubleLists.TryGetValue(key.Name, out var doubleListValue))
        {
            value = string.Join(
                "|",
                doubleListValue.Select(item => item.ToString(CultureInfo.InvariantCulture)));
            return true;
        }

        if (IsListType(valueType) &&
            _objectLists.TryGetValue(key.Name, out var objectListValue))
        {
            value = string.Join("|", objectListValue.Select(FormatTextValue));
            return true;
        }

        if (_objects.TryGetValue(key.Name, out var objectValue))
        {
            value = FormatTextValue(objectValue);
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryReadObject(IMetricKey key, out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var valueType = key.ValueType;

        if (valueType == typeof(int) &&
            _ints.TryGetValue(key.Name, out var intValue))
        {
            value = intValue;
            return true;
        }

        if (valueType == typeof(double) &&
            _doubles.TryGetValue(key.Name, out var doubleValue))
        {
            value = doubleValue;
            return true;
        }

        if (valueType == typeof(bool) &&
            _bools.TryGetValue(key.Name, out var boolValue))
        {
            value = boolValue;
            return true;
        }

        if (valueType == typeof(string) &&
            _strings.TryGetValue(key.Name, out var stringValue))
        {
            value = stringValue;
            return true;
        }

        if (valueType == typeof(List<int>) &&
            _intLists.TryGetValue(key.Name, out var intListValue))
        {
            value = intListValue;
            return true;
        }

        if (valueType == typeof(List<double>) &&
            _doubleLists.TryGetValue(key.Name, out var doubleListValue))
        {
            value = doubleListValue;
            return true;
        }

        if (IsListType(valueType) &&
            _objectLists.TryGetValue(key.Name, out var objectListValue))
        {
            value = objectListValue;
            return true;
        }

        return _objects.TryGetValue(key.Name, out value);
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
        _objects.Clear();

        _intLists.Clear();
        _doubleLists.Clear();
        _objectLists.Clear();
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
        _objects.Clear();

        foreach (var pair in _intLists)
            pair.Value.Clear();

        foreach (var pair in _doubleLists)
            pair.Value.Clear();

        foreach (var pair in _objectLists)
            pair.Value.Clear();
    }

    #endregion

    private static bool TryReadTyped<TStored, TValue>(
        Dictionary<string, TStored> values,
        string name,
        out TValue? value)
    {
        if (values.TryGetValue(name, out var found) &&
            found is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    private bool TryReadObjectList<TValue>(
        string name,
        out TValue? value)
    {
        if (!_objectLists.TryGetValue(name, out var list) ||
            !IsListType(typeof(TValue)))
        {
            value = default;
            return false;
        }

        var converted = (IList)Activator.CreateInstance(typeof(TValue))!;
        var itemType = typeof(TValue).GetGenericArguments()[0];

        foreach (var item in list)
        {
            if (!itemType.IsInstanceOfType(item))
            {
                value = default;
                return false;
            }

            converted.Add(item);
        }

        value = (TValue)converted;
        return true;
    }

    private static List<object> CopyToObjectList(IEnumerable items)
    {
        var list = new List<object>();

        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            list.Add(item);
        }

        return list;
    }

    private static string FormatTextValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static bool IsListType(Type valueType)
    {
        return valueType.IsGenericType &&
               valueType.GetGenericTypeDefinition() == typeof(List<>);
    }
}

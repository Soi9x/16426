// Port từ server/internal/Map.go
// Các cấu trúc dữ liệu an toàn luồng: SafeMap, SafeSet, SafeOrderedMap.

using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeLanServer.Server.Internal;

/// <summary>
/// SafeMap: Bản đồ an toàn luồng với copy-on-write.
/// Tương đương SafeMap[K, V] trong Go.
/// Sử dụng lock cho ghi và atomic pointer cho đọc.
/// </summary>
public class SafeMap<K, V> where K : notnull
{
    private readonly object _lock = new();
    internal readonly Dictionary<K, V> _writeOnly;
    internal volatile IReadOnlyDictionary<K, V> _readOnly;

    public SafeMap()
    {
        _writeOnly = new Dictionary<K, V>();
        _readOnly = new Dictionary<K, V>(_writeOnly);
    }

    /// <summary>
    /// Tải giá trị theo key.
    /// </summary>
    public bool TryLoad(K key, out V value)
    {
        return _readOnly.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Load hoặc Store bằng hàm sinh giá trị nếu chưa tồn tại.
    /// </summary>
    public V GetOrStore(K key, Func<V> valueFn)
    {
        lock (_lock)
        {
            if (_readOnly.TryGetValue(key, out var existing))
                return existing;
            var value = valueFn();
            _writeOnly[key] = value;
            UpdateReadOnly();
            return value;
        }
    }

    /// <summary>
    /// Duyệt qua tất cả giá trị (sync enumerable).
    /// </summary>
    public IEnumerable<V> Values()
    {
        return _readOnly.Values.ToList();
    }

    /// <summary>
    /// Duyệt qua tất cả cặp key-value (sync enumerable).
    /// </summary>
    public IEnumerable<(K key, V value)> Iter()
    {
        return _readOnly.ToList().Select(kv => (kv.Key, kv.Value));
    }

    /// <summary>
    /// Xóa key khỏi map.
    /// </summary>
    public void Delete(K key)
    {
        lock (_lock)
        {
            _writeOnly.Remove(key);
            UpdateReadOnly();
        }
    }

    /// <summary>
    /// Lưu và xóa đồng thời.
    /// </summary>
    public void StoreAndDelete(K storeKey, V storeValue, K deleteKey)
    {
        lock (_lock)
        {
            _writeOnly[storeKey] = storeValue;
            _writeOnly.Remove(deleteKey);
            UpdateReadOnly();
        }
    }

    /// <summary>
    /// Lưu giá trị với tùy chọn kiểm tra thay thế.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <param name="value">Giá trị mới.</param>
    /// <param name="replace">Hàm kiểm tra có thay thế giá trị cũ không. Null = luôn thay thế.</param>
    /// <returns>(giáTriDuocLuu, daTonTai)</returns>
    public (V stored, bool exists) Store(K key, V value, Func<V, bool>? replace = null)
    {
        replace ??= _ => true;
        lock (_lock)
        {
            if (!_writeOnly.TryGetValue(key, out var stored) || replace(stored))
            {
                stored = value;
                _writeOnly[key] = stored;
                UpdateReadOnly();
            }
            return (stored, _writeOnly.ContainsKey(key));
        }
    }

    /// <summary>
    /// So sánh và xóa nếu khớp.
    /// </summary>
    public bool CompareAndDelete(K key, Func<V, bool>? compareFunc = null)
    {
        compareFunc ??= _ => false;
        lock (_lock)
        {
            if (_writeOnly.TryGetValue(key, out var current) && compareFunc(current))
            {
                _writeOnly.Remove(key);
                UpdateReadOnly();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Load hoặc Store bằng hàm sinh giá trị nếu chưa tồn tại.
    /// </summary>
    public (V actual, bool loaded) LoadOrStoreFn(K key, Func<V> valueFn)
    {
        lock (_lock)
        {
            if (_writeOnly.TryGetValue(key, out var actual))
            {
                return (actual, true);
            }
            var value = valueFn();
            _writeOnly[key] = value;
            UpdateReadOnly();
            return (value, false);
        }
    }

    /// <summary>
    /// Duyệt qua tất cả giá trị (async enumerable).
    /// </summary>
    public async IAsyncEnumerable<V> ValuesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _readOnly.Values.ToList();
        foreach (var value in snapshot)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return value;
        }
    }

    /// <summary>
    /// Duyệt qua tất cả cặp key-value (async enumerable).
    /// </summary>
    public async IAsyncEnumerable<(K key, V value)> IterAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _readOnly.ToList();
        foreach (var (key, value) in snapshot)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return (key, value);
        }
    }

    /// <summary>
    /// Số lượng phần tử.
    /// </summary>
    public int Count => _readOnly.Count;

    private void UpdateReadOnly()
    {
        _readOnly = new Dictionary<K, V>(_writeOnly);
    }
}

/// <summary>
/// Custom JSON converter cho SafeMap để serialize giống Dictionary.
/// </summary>
public class SafeMapJsonConverter<K, V> : JsonConverter<SafeMap<K, V>> where K : notnull
{
    public override SafeMap<K, V>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<K, V>>(ref reader, options);
        if (dict == null) return null;
        var map = new SafeMap<K, V>();
        foreach (var (k, v) in dict)
        {
            map.Store(k, v);
        }
        return map;
    }

    public override void Write(Utf8JsonWriter writer, SafeMap<K, V> value, JsonSerializerOptions options)
    {
        // Note: cần lock để snapshot an toàn
        // Vì SafeMap dùng copy-on-write, snapshot là thread-safe
        var snapshot = value._readOnly; // Access internal - in production nên dùng method public
        JsonSerializer.Serialize(writer, snapshot, options);
    }
}

/// <summary>
/// SafeSet: Tập hợp an toàn luồng, xây dựng trên SafeMap.
/// </summary>
public class SafeSet<V> where V : notnull
{
    private readonly SafeMap<V, object> _map = new();
    private static readonly object _dummy = new();

    /// <summary>
    /// Xóa giá trị khỏi tập hợp. Trả về true nếu đã xóa.
    /// </summary>
    public bool Delete(V value)
    {
        return _map.CompareAndDelete(value, _ => true);
    }

    /// <summary>
    /// Thêm giá trị vào tập hợp. Trả về true nếu thêm thành công (chưa tồn tại).
    /// </summary>
    public bool Store(V value)
    {
        var (_, exists) = _map.Store(value, _dummy, _ => false);
        return !exists;
    }

    /// <summary>
    /// Số lượng phần tử.
    /// </summary>
    public int Count => _map.Count;

    /// <summary>
    /// Số lượng phần tử (alias cho Count).
    /// </summary>
    public int Len() => _map.Count;
}

/// <summary>
/// ReadOnlyOrderedMap: Bản đồ chỉ đọc có thứ tự key.
/// </summary>
public class ReadOnlyOrderedMap<K, V> where K : notnull
{
    internal readonly Dictionary<K, V> _internal;
    internal readonly List<K> _keys;

    public ReadOnlyOrderedMap(List<K> keyOrder, Dictionary<K, V> mapping)
    {
        _internal = mapping;
        _keys = keyOrder;
    }

    /// <summary>
    /// Tải giá trị theo key.
    /// </summary>
    public bool TryLoad(K key, out V value)
    {
        return _internal.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Tải giá trị theo key (alias cho TryLoad).
    /// </summary>
    public V? Load(K key)
    {
        if (_internal.TryGetValue(key, out var value))
            return value;
        return default;
    }

    /// <summary>
    /// Số lượng phần tử.
    /// </summary>
    public int Count => _keys.Count;

    /// <summary>
    /// Số lượng phần tử (alias cho Count - tương thích Go).
    /// </summary>
    public int Len() => _keys.Count;

    /// <summary>
    /// Duyệt qua các cặp key-value theo thứ tự.
    /// </summary>
    public IEnumerable<(K key, V value)> Iter()
    {
        foreach (var key in _keys)
        {
            yield return (key, _internal[key]);
        }
    }

    /// <summary>
    /// Duyệt qua các giá trị theo thứ tự.
    /// </summary>
    public IEnumerable<V> Values()
    {
        foreach (var key in _keys)
        {
            yield return _internal[key];
        }
    }

    /// <summary>
    /// Lấy danh sách keys.
    /// </summary>
    public IEnumerable<K> Keys()
    {
        return _keys.ToList();
    }
}

/// <summary>
/// SafeOrderedMap: Bản đồ an toàn luồng có thứ tự key.
/// Tương đương SafeOrderedMap[K, V] trong Go.
/// </summary>
public class SafeOrderedMap<K, V> where K : notnull
{
    private readonly object _lock = new();
    private readonly Dictionary<K, V> _internal = new();
    private readonly List<K> _keys = new();
    private readonly Dictionary<K, int> _keyMap = new();
    private volatile ReadOnlyOrderedMap<K, V>? _readOnly;

    public SafeOrderedMap()
    {
        UpdateReadOnly();
    }

    /// <summary>
    /// Tải giá trị theo key.
    /// </summary>
    public bool TryLoad(K key, out V value)
    {
        return _readOnly!.TryLoad(key, out value);
    }

    /// <summary>
    /// Số lượng phần tử.
    /// </summary>
    public int Count => _readOnly?.Count ?? 0;

    /// <summary>
    /// Lưu giá trị với tùy chọn kiểm tra thay thế.
    /// </summary>
    public (bool stored, V storedValue) Store(K key, V value, Func<V, bool>? replace = null)
    {
        lock (_lock)
        {
            return StoreInternal(key, value, replace);
        }
    }

    /// <summary>
    /// Xóa key khỏi map. Trả về true nếu đã tồn tại.
    /// </summary>
    public bool Delete(K key)
    {
        lock (_lock)
        {
            if (!_internal.ContainsKey(key)) return false;

            var index = _keyMap[key];
            _keyMap.Remove(key);
            _keys.RemoveAt(index);
            _internal.Remove(key);

            // Cập nhật lại index cho các key sau
            foreach (var k in _keyMap.Keys.ToList())
            {
                if (_keyMap[k] > index)
                {
                    _keyMap[k]--;
                }
            }

            UpdateReadOnly();
            return true;
        }
    }

    /// <summary>
    /// Duyệt và Store đồng thời (cho phép thao tác trước khi lưu).
    /// </summary>
    public (bool stored, V storedValue) IterAndStore(K key, V value, Func<V, bool>? replace, Action<int, IEnumerable<(K, V)>> fn)
    {
        lock (_lock)
        {
            fn(_keys.Count, Iter());
            return StoreInternal(key, value, replace);
        }
    }

    /// <summary>
    /// Lấy danh sách keys (async enumerable).
    /// </summary>
    public async IAsyncEnumerable<K> KeysAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _readOnly!._keys.ToList();
        foreach (var key in snapshot)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return key;
        }
    }

    /// <summary>
    /// Duyệt qua các cặp key-value.
    /// </summary>
    public IEnumerable<(K key, V value)> Iter()
    {
        var ro = _readOnly!;
        return ro.Iter();
    }

    /// <summary>
    /// Duyệt qua các giá trị.
    /// </summary>
    public IEnumerable<V> Values()
    {
        var ro = _readOnly!;
        return ro.Values();
    }

    /// <summary>
    /// Số lượng phần tử (alias cho Count).
    /// </summary>
    public int Len() => _readOnly?.Count ?? 0;

    /// <summary>
    /// Tải giá trị theo key (alias cho TryLoad).
    /// </summary>
    public V? Load(K key)
    {
        if (_readOnly!.TryLoad(key, out var value))
            return value;
        return default;
    }

    /// <summary>
    /// Lấy danh sách keys.
    /// </summary>
    public IEnumerable<K> Keys()
    {
        var ro = _readOnly!;
        return ro.Keys();
    }

    /// <summary>
    /// Lấy phần tử đầu tiên.
    /// </summary>
    public bool TryFirst(out K key, out V value)
    {
        var ro = _readOnly!;
        if (ro._keys.Count == 0)
        {
            key = default!;
            value = default!;
            return false;
        }
        key = ro._keys[0];
        value = ro._internal[key];
        return true;
    }

    private (bool stored, V storedValue) StoreInternal(K key, V value, Func<V, bool>? replace)
    {
        replace ??= _ => true;

        if (!_internal.TryGetValue(key, out var storedValue))
        {
            storedValue = value;
            _keyMap[key] = _keys.Count;
            _keys.Add(key);
            _internal[key] = value;
            UpdateReadOnly();
            return (true, storedValue);
        }
        else if (replace(storedValue))
        {
            storedValue = value;
            _internal[key] = storedValue;
            UpdateReadOnly();
            return (false, storedValue);
        }
        return (false, storedValue);
    }

    private void UpdateReadOnly()
    {
        var clonedInternal = new Dictionary<K, V>(_internal);
        var clonedKeys = new List<K>(_keys);
        _readOnly = new ReadOnlyOrderedMap<K, V>(clonedKeys, clonedInternal);
    }
}

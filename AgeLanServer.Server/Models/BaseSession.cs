namespace AgeLanServer.Server.Models;

/// <summary>
/// Lớp cơ sở quản lý phiên làm việc với thời gian hết hạn.
/// Sử dụng generic để hỗ trợ nhiều loại khóa và dữ liệu.
/// Tương đương BaseSessions trong Go (baseSession.go) với background sweeper tự động xóa session hết hạn.
/// </summary>
public class BaseSessions<K, T> where K : notnull
{
    private readonly Dictionary<K, SessionEntry<K, T>> _sessions = new();
    private readonly TimeSpan _duration;
    private readonly object _lock = new();
    private readonly object _sweeperLock = new();
    private bool _sweeperStarted;

    public BaseSessions(TimeSpan duration)
    {
        _duration = duration;
    }

    public SessionEntry<K, T> CreateSession(Func<K> idGenerator, T data)
    {
        lock (_lock)
        {
            var id = idGenerator();
            var entry = new SessionEntry<K, T>(id, data, _duration);
            _sessions[id] = entry;

            // Khởi động background sweeper khi session đầu tiên được tạo
            lock (_sweeperLock)
            {
                if (!_sweeperStarted)
                {
                    _sweeperStarted = true;
                    StartSweeper();
                }
            }

            return entry;
        }
    }

    public bool Get(K id, out SessionEntry<K, T> entry)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(id, out entry))
            {
                entry.ResetExpiry();
                return true;
            }
            return false;
        }
    }

    public void Delete(K id)
    {
        lock (_lock) _sessions.Remove(id);
    }

    public void ResetExpiryTimer(K id)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(id, out var entry))
                entry.ResetExpiry();
        }
    }

    public IEnumerable<SessionEntry<K, T>> Values()
    {
        lock (_lock)
            return _sessions.Values.ToList();
    }

    /// <summary>
    /// Chạy background task dọn dẹp các session hết hạn.
    /// Tương đương startSweeper() trong Go: chạy PeriodicTimer mỗi 60 giây,
    /// duyệt tất cả session, xóa những session đã hết hạn.
    /// </summary>
    private void StartSweeper()
    {
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            while (await timer.WaitForNextTickAsync())
            {
                var expiredIds = new List<K>();
                var now = DateTime.UtcNow;

                lock (_lock)
                {
                    foreach (var kvp in _sessions)
                    {
                        if (kvp.Value.IsExpiredAt(now))
                        {
                            expiredIds.Add(kvp.Key);
                        }
                    }

                    // Xóa các session hết hạn
                    foreach (var expiredId in expiredIds)
                    {
                        _sessions.Remove(expiredId);
                    }
                }
            }
        });
    }
}

/// <summary>
/// Một phiên làm việc với thời gian hết hạn.
/// </summary>
public class SessionEntry<K, T>
{
    public K Id { get; }
    private T _data;
    private DateTime _expiry;
    private readonly TimeSpan _duration;

    public SessionEntry(K id, T data, TimeSpan duration)
    {
        Id = id;
        _data = data;
        _duration = duration;
        _expiry = DateTime.UtcNow.Add(duration);
    }

    public T Data() => _data;
    public void ResetExpiry() => _expiry = DateTime.UtcNow.Add(_duration);
    public bool IsExpired => DateTime.UtcNow > _expiry;

    /// <summary>
    /// Kiểm tra hết hạn tại thời điểm cụ thể (dùng cho sweeper để tránh gọi DateTime.UtcNow nhiều lần).
    /// </summary>
    public bool IsExpiredAt(DateTime now) => now > _expiry;
}

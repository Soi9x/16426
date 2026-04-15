// Port từ server/internal/keyRWMutex.go
/// Mutex theo key: mỗi key có một RWMutex riêng.

namespace AgeLanServer.Server.Internal;

/// <summary>
/// KeyRWMutex: Cung cấp cơ chế lock theo key.
/// Mỗi key sẽ có một ReaderWriterLockSlim riêng.
/// Tương đương KeyRWMutex[K] trong Go.
/// </summary>
public class KeyRwMutex<K> where K : notnull
{
    private readonly ReaderWriterLockSlim _globalLock = new();
    private readonly Dictionary<K, ReaderWriterLockSlim> _mutexes = new();

    /// <summary>
    /// Khóa write cho key.
    /// </summary>
    public void Lock(K key)
    {
        GetOrCreateLock(key).EnterWriteLock();
    }

    /// <summary>
    /// Khóa read cho key.
    /// </summary>
    public void ReadLock(K key)
    {
        GetOrCreateLock(key).EnterReadLock();
    }

    /// <summary>
    /// Mở khóa write cho key.
    /// </summary>
    public void Unlock(K key)
    {
        _globalLock.EnterReadLock();
        try
        {
            if (_mutexes.TryGetValue(key, out var lockObj))
            {
                lockObj.ExitWriteLock();
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Mở khóa read cho key.
    /// </summary>
    public void ReadUnlock(K key)
    {
        _globalLock.EnterReadLock();
        try
        {
            if (_mutexes.TryGetValue(key, out var lockObj))
            {
                lockObj.ExitReadLock();
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Alias cho ReadLock (tương thích Go).
    /// </summary>
    public void RLock(K key) => ReadLock(key);

    /// <summary>
    /// Alias cho ReadUnlock (tương thích Go).
    /// </summary>
    public void RUnlock(K key) => ReadUnlock(key);

    /// <summary>
    /// Lấy hoặc tạo lock cho key.
    /// </summary>
    private ReaderWriterLockSlim GetOrCreateLock(K key)
    {
        _globalLock.EnterReadLock();
        try
        {
            if (_mutexes.TryGetValue(key, out var lockObj))
            {
                return lockObj;
            }
        }
        finally
        {
            _globalLock.ExitReadLock();
        }

        var newLock = new ReaderWriterLockSlim();
        _globalLock.EnterWriteLock();
        try
        {
            _mutexes[key] = newLock;
        }
        finally
        {
            _globalLock.ExitWriteLock();
        }
        return newLock;
    }
}

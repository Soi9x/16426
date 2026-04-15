// Port từ server/internal/rng.go
// Bộ sinh số ngẫu nhiên an toàn luồng.

namespace AgeLanServer.Server.Internal;

/// <summary>
/// RandRng: Bộ sinh số ngẫu nhiên an toàn luồng.
/// Tương đương RandReader trong Go.
/// </summary>
public sealed class RandRng
{
    private readonly Random _random;
    private readonly object _mutex = new();

    public RandRng(Random random)
    {
        _random = random;
    }

    /// <summary>
    /// Sinh số ngẫu nhiên trong khoảng [0, upperBound).
    /// Tương đương UintN trong Go.
    /// </summary>
    public uint NextUInt(uint upperBound)
    {
        lock (_mutex)
        {
            return (uint)_random.NextInt64(0, upperBound);
        }
    }

    /// <summary>
    /// Đọc byte ngẫu nhiên vào buffer (tương đương io.Reader interface).
    /// </summary>
    public int Read(byte[] buffer)
    {
        lock (_mutex)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)_random.Next(0, 256);
            }
            return buffer.Length;
        }
    }

    /// <summary>
    /// Thực hiện action với rng đã được lock.
    /// </summary>
    public void WithRng(Action<RandRng> action)
    {
        lock (_mutex)
        {
            action(this);
        }
    }
}

/// <summary>
/// Singleton RNG toàn cục.
/// </summary>
public static class Rng
{
    /// <summary>
    /// Instance RNG toàn cục.
    /// </summary>
    public static RandRng Instance { get; private set; } = new(new Random());

    /// <summary>
    /// Thực hiện action với RNG toàn cục.
    /// </summary>
    public static void WithRng(Action<RandRng> action)
    {
        Instance.WithRng(action);
    }

    /// <summary>
    /// Khởi tạo RNG với seed.
    /// </summary>
    public static void Initialize(ulong seed)
    {
        Instance = new RandRng(new Random((int)seed));
        // Lưu ý: System.Guid không hỗ trợ custom RNG như Go's uuid.SetRand.
        // Nếu cần UUID determinist, phải tự sinh bằng cách khác.
    }
}

using System.Collections.Concurrent;
using System.Threading.Channels;
using AgeLanServer.Server.Internal;

/// <summary>
/// Khóa phiên, được định nghĩa là chuỗi.
/// </summary>
using SessionKey = System.String;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện phiên đăng nhập (session).
/// </summary>
public interface ISession
{
    string Id { get; }
    int UserId { get; }
    ushort ClientLibVersion { get; }
    void AddMessage(object[] message);
    (uint ackNum, object[] messages) WaitForMessages(uint ackNum);
}

/// <summary>
/// Dữ liệu phiên đăng nhập.
/// Thời gian hết hạn: 5 phút.
/// </summary>
public class SessionData : ISession
{
    private static readonly char[] SessionLetters = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private static readonly TimeSpan SessionDuration = TimeSpan.FromMinutes(5);

    public string Id { get; private set; }
    public int UserId { get; private set; }
    public ushort ClientLibVersion { get; private set; }
    private readonly Channel<object[]> _messageChan;

    public SessionData(string id, int userId, ushort clientLibVersion)
    {
        Id = id;
        UserId = userId;
        ClientLibVersion = clientLibVersion;
        _messageChan = Channel.CreateBounded<object[]>(100);
    }

    /// <summary>
    /// Tạo ID phiên ngẫu nhiên độ dài 30 ký tự.
    /// </summary>
    public static string GenerateSessionId()
    {
        var sessionId = new char[30];
        var rng = new Random();
        for (int j = 0; j < sessionId.Length; j++)
            sessionId[j] = SessionLetters[rng.Next(SessionLetters.Length)];
        return new string(sessionId);
    }

    public void AddMessage(object[] message)
    {
        _messageChan.Writer.TryWrite(message);
    }

    public (uint, object[]) WaitForMessages(uint ackNum)
    {
        var results = new List<object[]>();
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(19));

        while (true)
        {
            try
            {
                var msg = _messageChan.Reader.ReadAsync(timer.Token).AsTask().Result;
                results.Add(msg);
                while (results.Count < 100)
                {
                    if (_messageChan.Reader.TryRead(out var nextMsg))
                    {
                        results.Add(nextMsg);
                    }
                    else
                    {
                        if (results.Count > 0) ackNum++;
                        return (ackNum, results.ToArray());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return (ackNum, results.ToArray());
            }
        }
    }
}

/// <summary>
/// Quản lý tập hợp các phiên đăng nhập.
/// </summary>
public interface ISessions
{
    string Create(int userId, ushort clientLibVersion);
    ISession? GetById(string id);
    ISession? GetByUserId(int userId);
    void Delete(string id);
    void ResetExpiry(string id);
    void Initialize();
}

/// <summary>
/// Lớp triển khai chính quản lý phiên đăng nhập.
/// Sử dụng BaseSessions để quản lý thời gian hết hạn.
/// </summary>
public class MainSessions : ISessions
{
    private BaseSessions<SessionKey, SessionData> _baseSessions = null!;

    public void Initialize()
    {
        _baseSessions = new BaseSessions<SessionKey, SessionData>(TimeSpan.FromMinutes(5));
    }

    public string Create(int userId, ushort clientLibVersion)
    {
        var newId = SessionData.GenerateSessionId();
        var sess = new SessionData(newId, userId, clientLibVersion);
        _baseSessions.CreateSession(() => newId, sess);
        return newId;
    }

    public ISession? GetById(string id)
    {
        if (_baseSessions.Get(id, out var baseSess))
            return baseSess.Data();
        return null;
    }

    public ISession? GetByUserId(int userId)
    {
        foreach (var sess in _baseSessions.Values())
        {
            if (sess.Data().UserId == userId)
                return sess.Data();
        }
        return null;
    }

    public void Delete(string id) => _baseSessions.Delete(id);
    public void ResetExpiry(string id) => _baseSessions.ResetExpiryTimer(id);
}

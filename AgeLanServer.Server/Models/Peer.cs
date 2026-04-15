namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện người ngang hàng (peer) trong một phòng chơi.
/// Đại diện cho một người chơi đã tham gia vào quảng cáo (lobby).
/// </summary>
public interface IPeer
{
    int AdvertisementId { get; }
    string AdvertisementIp { get; }
    int UserId { get; }
    int UserStatId { get; }
    int Party { get; }
    int Race { get; }
    int Team { get; }
    object[] Encode();
}

/// <summary>
/// Lớp triển khai chính của peer (người chơi trong lobby).
/// </summary>
public class MainPeer : IPeer
{
    public int AdvertisementId { get; private set; }
    public string AdvertisementIp { get; private set; } = null!;
    public int UserId { get; private set; }
    public int UserStatId { get; private set; }
    public int Party { get; private set; }
    public int Race { get; private set; }
    public int Team { get; private set; }

    private MainPeer(int advertisementId, string advertisementIp, int userId, int userStatId, int party, int race, int team)
    {
        AdvertisementId = advertisementId;
        AdvertisementIp = advertisementIp;
        UserId = userId;
        UserStatId = userStatId;
        Party = party;
        Race = race;
        Team = team;
    }

    /// <summary>
    /// Tạo mới một peer.
    /// </summary>
    public static IPeer New(int advertisementId, string advertisementIp, int userId, int userStatId, int party, int race, int team)
    {
        return new MainPeer(advertisementId, advertisementIp, userId, userStatId, party, race, team);
    }

    public object[] Encode()
    {
        return new object[]
        {
            UserId, UserStatId, Party, Race, Team
        };
    }
}

/// <summary>
/// Factory method để tạo peer.
/// </summary>
public static class Peer
{
    public static IPeer New(int advertisementId, string advertisementIp, int userId, int userStatId, int party, int race, int team)
    {
        return MainPeer.New(advertisementId, advertisementIp, userId, userStatId, party, race, team);
    }
}

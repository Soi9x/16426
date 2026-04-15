namespace AgeLanServer.Server.Models;

/// <summary>
/// Giao diện tin nhắn trong hệ thống.
/// Dùng để gửi thông điệp giữa người dùng trong lobby.
/// </summary>
public interface IMessage
{
    int AdvertisementId { get; }
    long Time { get; }
    bool Broadcast { get; }
    string Content { get; }
    byte Typ { get; }
    IUser Sender { get; }
    IEnumerable<IUser> Receivers { get; }
    object[] Encode();
}

/// <summary>
/// Lớp triển khai chính của tin nhắn.
/// </summary>
public class MainMessage : IMessage
{
    public int AdvertisementId { get; set; }
    public long Time { get; set; }
    public bool Broadcast { get; set; }
    public string Content { get; set; } = null!;
    public byte Typ { get; set; }
    public IUser Sender { get; set; } = null!;
    public IEnumerable<IUser> Receivers { get; set; } = null!;

    public object[] Encode()
    {
        var receiverIds = Receivers.Select(u => u.Id).ToArray();
        return new object[]
        {
            AdvertisementId,
            Time,
            Broadcast ? 1 : 0,
            Content,
            Typ,
            Sender.Id,
            Sender.Alias,
            receiverIds
        };
    }
}

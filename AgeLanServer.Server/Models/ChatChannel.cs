namespace AgeLanServer.Server.Models;

/// <summary>
/// Kênh chat trong game.
/// </summary>
public class MainChatChannel
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool Private { get; set; }
}

/// <summary>
/// Giao diện quản lý kênh chat.
/// </summary>
public interface IChatChannels
{
    void Initialize(Dictionary<string, MainChatChannel> chatChannels);
}

/// <summary>
/// Lớp triển khai chính quản lý kênh chat.
/// </summary>
public class MainChatChannels : IChatChannels
{
    private Dictionary<string, MainChatChannel> _channels = new();

    public void Initialize(Dictionary<string, MainChatChannel> chatChannels)
    {
        _channels = chatChannels;
    }
}

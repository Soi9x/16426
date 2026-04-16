using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Routes.Shared;

/// <summary>
/// Các DTO chung được sử dụng xuyên suốt các route handler.
/// Tương đương i.A (mảng động) và i.Json[T] trong bản Go.
/// </summary>

/// <summary>
/// Wrapper cho dữ liệu JSON dạng mảng từ query string (ví dụ: "[1,2,3]").
/// </summary>
public sealed class JsonArray<T>
{
    public List<T> Data { get; set; } = new();
}

/// <summary>
/// Yêu cầu chứa ID advertisement.
/// </summary>
public sealed class AdvertisementIdRequest
{
    public int AdvertisementId { get; set; }
}

/// <summary>
/// Yêu cầu cơ bản cho các thao tác advertisement.
/// </summary>
public class AdvertisementBaseRequest
{
    public int Id { get; set; }
    public int AppBinaryChecksum { get; set; }
    public int DataChecksum { get; set; }
    public string ModDllFile { get; set; } = string.Empty;
    public int ModDllChecksum { get; set; }
    public string ModName { get; set; } = string.Empty;
    public string ModVersion { get; set; } = string.Empty;
    public int Party { get; set; }
    public int Race { get; set; }
    public int Team { get; set; }
    public uint VersionFlags { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật advertisement.
/// </summary>
public sealed class AdvertisementUpdateRequest : AdvertisementBaseRequest
{
    public string Description { get; set; } = string.Empty;
    public int AutomatchPollId { get; set; }
    public string RelayRegion { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int HostId { get; set; }
    public bool Observable { get; set; }
    public uint ObserverDelay { get; set; }
    public string ObserverPassword { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Passworded { get; set; }
    public bool Visible { get; set; }
    public bool Joinable { get; set; }
    public byte MatchType { get; set; }
    public byte MaxPlayers { get; set; }
    public string Options { get; set; } = string.Empty;
    public string SlotInfo { get; set; } = string.Empty;
    public ulong PsnSessionId { get; set; }
    public sbyte State { get; set; }
}

/// <summary>
/// Yêu cầu host advertisement (tạo lobby mới).
/// </summary>
public sealed class AdvertisementHostRequest : AdvertisementBaseRequest
{
    public string Description { get; set; } = string.Empty;
    public int AutomatchPollId { get; set; }
    public string RelayRegion { get; set; } = string.Empty;
    public string MapName { get; set; } = string.Empty;
    public int HostId { get; set; }
    public bool Observable { get; set; }
    public uint ObserverDelay { get; set; }
    public string ObserverPassword { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Passworded { get; set; }
    public bool Visible { get; set; }
    public int StatGroup { get; set; }
    public bool Joinable { get; set; }
    public byte MatchType { get; set; }
    public byte MaxPlayers { get; set; }
    public string Options { get; set; } = string.Empty;
    public string SlotInfo { get; set; } = string.Empty;
    public ulong PsnSessionId { get; set; }
    public sbyte State { get; set; }
    public byte ServiceType { get; set; }
}

/// <summary>
/// Yêu cầu tìm kiếm advertisement.
/// </summary>
public sealed class SearchQuery
{
    public int AppBinaryChecksum { get; set; }
    public int DataChecksum { get; set; }
    public byte? MatchType { get; set; }
    public string ModDllFile { get; set; } = string.Empty;
    public int ModDllChecksum { get; set; }
    public string ModName { get; set; } = string.Empty;
    public string ModVersion { get; set; } = string.Empty;
    public uint VersionFlags { get; set; }
}

/// <summary>
/// Yêu cầu phân trang danh sách advertisement.
/// </summary>
public sealed class WanQuery
{
    public int Length { get; set; }
    public int Offset { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật tags.
/// </summary>
public sealed class TagRequest
{
    public JsonArray<string> NumericTagNames { get; set; } = new();
    public JsonArray<int> NumericTagValues { get; set; } = new();
    public JsonArray<string> StringTagNames { get; set; } = new();
    public JsonArray<string> StringTagValues { get; set; } = new();
}

/// <summary>
/// Yêu cầu cho thao tác party (peer).
/// </summary>
public sealed class PeerRequest
{
    public int MatchId { get; set; }
    public JsonArray<int> ProfileIds { get; set; } = new();
    public JsonArray<int> RaceIds { get; set; } = new();
    public JsonArray<int> TeamIds { get; set; } = new();
}

/// <summary>
/// Yêu cầu cho lời mời.
/// </summary>
public class InvitationRequest
{
    public int AdvertisementId { get; set; }
}

/// <summary>
/// Yêu cầu chat trong channel.
/// </summary>
public class ChatroomRequest
{
    public int ChatroomId { get; set; }
}

/// <summary>
/// Yêu cầu gửi tin nhắn.
/// </summary>
public class TextRequest
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu gửi whisper (tin nhắn riêng tư).
/// </summary>
public sealed class WhisperRequest : TextRequest
{
    public JsonArray<int> RecipientIds { get; set; } = new();
    public int RecipientId { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật presence.
/// </summary>
public sealed class SetPresenceRequest
{
    [BindAlias("presence_id")]
    public int PresenceId { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật thuộc tính presence.
/// </summary>
public sealed class SetPresencePropertyRequest
{
    [BindAlias("presencePropertyDef_id")]
    public int PresencePropertyId { get; set; }
    public string Value { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Yêu cầu kết bạn.
/// </summary>
public sealed class FriendRequest
{
    public int TargetProfileId { get; set; }
}

/// <summary>
/// Yêu cầu cho inventory.
/// </summary>
public sealed class InventoryRequest
{
    public JsonArray<int> ProfileIds { get; set; } = new();
}

/// <summary>
/// Yêu cầu thao tác item.
/// </summary>
public class ItemRequest
{
    public JsonArray<int> ItemIds { get; set; } = new();
}

/// <summary>
/// Yêu cầu detach item.
/// </summary>
public sealed class DetachItemsRequest : ItemRequest
{
    public JsonArray<int> LocationIds { get; set; } = new();
    public JsonArray<int> DurabilityCounts { get; set; } = new();
}

/// <summary>
/// Yêu cầu di chuyển item.
/// </summary>
public sealed class MoveItemRequest : ItemRequest
{
    public JsonArray<int> LocationIds { get; set; } = new();
    public JsonArray<int> PositionIds { get; set; } = new();
    public JsonArray<int> SlotIds { get; set; } = new();
}

/// <summary>
/// Yêu cầu cập nhật thuộc tính item.
/// </summary>
public sealed class UpdateItemAttributesRequest
{
    public JsonArray<JsonArray<string>> Keys { get; set; } = new();
    public JsonArray<JsonArray<string>> Values { get; set; } = new();
    public JsonArray<int> ItemIds { get; set; } = new();
    public JsonArray<int> XpGains { get; set; } = new();
}

/// <summary>
/// Yêu cầu loadout item.
/// </summary>
public sealed class ItemLoadoutRequest
{
    public int Id { get; set; }
}

/// <summary>
/// Yêu cầu tạo loadout item.
/// </summary>
public class CreateItemLoadoutRequest
{
    public List<int> ItemOrLocIds { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật loadout item.
/// </summary>
public sealed class UpdateItemLoadoutRequest : CreateItemLoadoutRequest
{
    public int Id { get; set; }
}

/// <summary>
/// Yêu cầu lấy URL file.
/// </summary>
public sealed class GetFileUrlRequest
{
    public JsonArray<string> Names { get; set; } = new();
}

/// <summary>
/// Yêu cầu tìm profile.
/// </summary>
public sealed class GetProfileNameRequest
{
    public JsonArray<int> ProfileIds { get; set; } = new();
}

/// <summary>
/// Yêu cầu tìm profile theo nền tảng.
/// </summary>
public sealed class FindProfilesByPlatformIdRequest
{
    public JsonArray<ulong> PlatformIds { get; set; } = new();
}

/// <summary>
/// Yêu cầu thuộc tính profile.
/// </summary>
public class ProfilePropertiesRequest
{
    public string PropertyId { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu lấy thuộc tính profile.
/// </summary>
public sealed class GetProfilePropertyRequest
{
    public string PropertyId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu thêm thuộc tính profile.
/// </summary>
public sealed class AddProfilePropertyRequest : ProfilePropertiesRequest
{
    public string PropertyValue { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu cập nhật avatar metadata.
/// </summary>
public sealed class SetAvatarMetadataRequest
{
    public string Metadata { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu login.
/// </summary>
public sealed class PlatformLoginRequest
{
    public string AccountType { get; set; } = string.Empty;
    public ulong PlatformUserId { get; set; }
    public string Alias { get; set; } = string.Empty;
    [BindAlias("title")]
    public string GameId { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public ushort ClientLibVersion { get; set; }
}

/// <summary>
/// Yêu cầu read session.
/// </summary>
public sealed class ReadSessionRequest
{
    public uint Ack { get; set; }
}

/// <summary>
/// Yêu cầu cập nhật avatar stats.
/// </summary>
public sealed class SetAvatarStatValuesRequest
{
    public JsonArray<int> AvatarStatIds { get; set; } = new();
    public JsonArray<long> Values { get; set; } = new();
    public JsonArray<int> UpdateTypes { get; set; } = new();
}

/// <summary>
/// Yêu cầu cho party match chat.
/// </summary>
public sealed class MatchChatRequest
{
    public byte MessageTypeId { get; set; }
    public int MatchId { get; set; }
    public bool Broadcast { get; set; }
    public string Message { get; set; } = string.Empty;
    public JsonArray<int> ToProfileIds { get; set; } = new();
    public int ToProfileId { get; set; }
}

/// <summary>
/// Yêu cầu cho lời mời mở rộng.
/// </summary>
public sealed class ExtendInvitationRequest : InvitationRequest
{
    public int UserId { get; set; }
    public string AdvertisementPassword { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu hủy lời mời.
/// </summary>
public sealed class CancelInvitationRequest : InvitationRequest
{
    public int UserId { get; set; }
}

/// <summary>
/// Yêu cầu phản hồi lời mời.
/// </summary>
public sealed class ReplyInvitationRequest : InvitationRequest
{
    public bool Accept { get; set; }
    public int InviterId { get; set; }
    public string InvitationId { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu cloud file.
/// </summary>
public sealed class CloudFileRequest
{
    public List<string> Names { get; set; } = new();
}

/// <summary>
/// Kết quả phân tích tham số peer.
/// </summary>
public sealed class PeerParametersResult
{
    public bool HasError { get; set; }
    public int AdvId { get; set; }
    public int Length { get; set; }
    public List<int> ProfileIds { get; set; } = new();
    public List<int> RaceIds { get; set; } = new();
    public List<int> TeamIds { get; set; } = new();
}

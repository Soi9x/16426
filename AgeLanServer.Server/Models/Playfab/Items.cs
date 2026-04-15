using AgeLanServer.Common;

namespace AgeLanServer.Server.Models.Playfab;

/// <summary>
/// Định dạng thời gian ISO 8601 dùng trong PlayFab.
/// </summary>
public static class PlayfabConstants
{
    public const string Iso8601Layout = "yyyy-MM-ddTHH:mm:ss.fffZ";
    public static string BaseDir => Path.Combine(AppConstants.ResourcesDir, "responses", AppConstants.GameAoM, "playfab");
    public const string StaticSuffix = "/static";
    public const string Branch = "public/production";
}

/// <summary>
/// ID thay thế của vật phẩm danh mục.
/// </summary>
public class CatalogItemAlternativeId
{
    public string Type { get; set; } = null!;
    public string Value { get; set; } = null!;
}

/// <summary>
/// Tiêu đề vật phẩm.
/// </summary>
public class CatalogItemTitle
{
    public string NEUTRAL { get; set; } = null!;
}

/// <summary>
/// Thực thể tạo vật phẩm.
/// </summary>
public class CatalogItemCreatorEntity
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string TypeString { get; set; } = null!;
}

/// <summary>
/// Vật phẩm trong danh mục (Catalog Item).
/// </summary>
public class CatalogItem
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public List<CatalogItemAlternativeId> AlternateIds { get; set; } = new();
    public string FriendlyId { get; set; } = null!;
    public CatalogItemTitle Title { get; set; } = new();
    public object Description { get; set; } = new();
    public object Keywords { get; set; } = new();
    public CatalogItemCreatorEntity CreatorEntity { get; set; } = new();
    public List<object> Platforms { get; set; } = new();
    public List<object> Tags { get; set; } = new();
    public string CreationDate { get; set; } = null!;
    public string LastModifiedDate { get; set; } = null!;
    public string StartDate { get; set; } = null!;
    public List<object> Contents { get; set; } = new();
    public List<object> Images { get; set; } = new();
    public List<object> ItemReferences { get; set; } = new();
    public List<object> DeepLinks { get; set; } = new();
    public object DisplayProperties { get; set; } = new();
}

/// <summary>
/// Vật phẩm trong kho (Inventory Item).
/// </summary>
public class InventoryItem
{
    public string Id { get; set; } = null!;
    public string StackId { get; set; } = null!;
    public object DisplayProperties { get; set; } = new();
    public int Amount { get; set; }
    public string Type { get; set; } = null!;
}

using System.Collections.Immutable;
using AgeLanServer.Common;
using AgeLanServer.Server.Internal;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Tùy chọn tài nguyên.
/// </summary>
public class ResourcesOpts
{
    public ImmutableHashSet<string>? KeyedFilenames { get; set; }
}

/// <summary>
/// Cấu trúc chữ ký cho tài nguyên được ký.
/// </summary>
public class Signature
{
    public string Value { get; set; } = null!;
}

/// <summary>
/// Giao diện tài nguyên game.
/// Quản lý các tệp phản hồi, dữ liệu đăng nhập, kênh chat, và đám mây.
/// </summary>
public interface IResources
{
    void Initialize(string gameId, ResourcesOpts? opts);
    void ReturnSignedAsset(string name, HttpResponse w, HttpRequest req, bool keyedResponse);
    object[] LoginData();
    Dictionary<string, MainChatChannel> ChatChannels();
    Dictionary<string, object[]> ArrayFiles();
    Dictionary<string, byte[]> SignedAssets();
    ICloudFiles CloudFiles { get; }
}

/// <summary>
/// Lớp triển khai chính quản lý tài nguyên game.
/// Đọc dữ liệu từ các thư mục cấu hình và phản hồi.
/// </summary>
public class MainResources : IResources
{
    public static string ResponsesFolder => Path.Combine(AppConstants.ResourcesDir, "responses");
    public static string UserDataFolder => Path.Combine(AppConstants.ResourcesDir, "userData");
    public static string EtcFolder => Path.Combine(AppConstants.ResourcesDir, "etc");
    public static string CloudFolder => Path.Combine(ResponsesFolder, "cloud");

    private ImmutableHashSet<string> _keyedFilenames = null!;
    private Dictionary<string, MainChatChannel> _chatChannels = new();
    private object[] _loginData = Array.Empty<object>();
    private Dictionary<string, object[]> _arrayFiles = new();
    private Dictionary<string, byte[]> _keyedFiles = new();
    private Dictionary<string, string> _nameToSignature = new();
    private ICloudFiles? _cloudFiles;

    public ICloudFiles CloudFiles => _cloudFiles ?? new EmptyCloudFiles();

    public void Initialize(string gameId, ResourcesOpts? opts)
    {
        opts ??= new ResourcesOpts();
        if (opts.KeyedFilenames == null || opts.KeyedFilenames.Count == 0)
        {
            opts.KeyedFilenames = ImmutableHashSet.Create("itemDefinitions.json");
        }

        _arrayFiles = new Dictionary<string, object[]>();
        _keyedFiles = new Dictionary<string, byte[]>();
        _nameToSignature = new Dictionary<string, string>();
        _keyedFilenames = opts.KeyedFilenames;

        InitializeUserData(gameId);
        InitializeLogin(gameId);
        InitializeChatChannels(gameId);
        InitializeResponses(gameId);
        InitializeCloud(gameId);
    }

    public object[] LoginData() => _loginData;
    public Dictionary<string, MainChatChannel> ChatChannels() => _chatChannels;
    public Dictionary<string, object[]> ArrayFiles() => _arrayFiles;
    public Dictionary<string, byte[]> SignedAssets() => _keyedFiles;

    private void InitializeChatChannels(string gameId)
    {
        var path = Path.Combine(AppConstants.ConfigsPath, gameId, "chatChannels.json");
        if (!File.Exists(path)) return;

        var data = File.ReadAllText(path);
        // JSON deserialization would go here
    }

    private void InitializeLogin(string gameId)
    {
        var path = Path.Combine(AppConstants.ConfigsPath, gameId, "login.json");
        if (!File.Exists(path)) return;

        var data = File.ReadAllText(path);
        // Parse login data - simplified
        _loginData = Array.Empty<object>();
    }

    private void InitializeResponses(string gameId)
    {
        var dir = Path.Combine(ResponsesFolder, gameId);
        if (!Directory.Exists(dir)) return;

        foreach (var entry in Directory.EnumerateFiles(dir))
        {
            var name = Path.GetFileName(entry);
            if (!name.EndsWith(".json")) continue;

            try
            {
                var data = File.ReadAllBytes(entry);
                if (_keyedFilenames.Contains(name))
                {
                    _keyedFiles[name] = data;
                    // Extract signature
                }
                else
                {
                    // Parse as array
                }
            }
            catch { }
        }
    }

    private void InitializeCloud(string gameId)
    {
        var cloudfiles = CloudFilesHelper.BuildCloudfilesIndex(
            Path.Combine(AppConstants.ConfigsPath, gameId),
            Path.Combine(CloudFolder, gameId));
        if (cloudfiles != null)
            _cloudFiles = cloudfiles;
    }

    private void InitializeUserData(string gameId)
    {
        var path = Path.Combine(UserDataFolder, gameId);
        Directory.CreateDirectory(path);
    }

    public void ReturnSignedAsset(string name, HttpResponse w, HttpRequest req, bool keyedResponse)
    {
        string? serverSignature = null;
        object? response = null;

        if (keyedResponse)
        {
            _keyedFiles.TryGetValue(name, out var keyedData);
            response = keyedData;
            _nameToSignature.TryGetValue(name, out serverSignature);
        }
        else
        {
            _arrayFiles.TryGetValue(name, out var arrayData);
            response = arrayData;
            if (arrayData != null && arrayData.Length > 0)
                serverSignature = arrayData[^1]?.ToString();
        }

        var querySig = req.Query["signature"].ToString();
        if (querySig != serverSignature)
        {
            if (keyedResponse && response is byte[] raw)
                w.Body.WriteAsync(raw);
            // else JSON response
            return;
        }

        // Return signed response
    }
}

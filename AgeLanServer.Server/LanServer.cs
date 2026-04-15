using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AgeLanServer.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AgeLanServer.Server;

/// <summary>
/// Server LAN chính - triển khai web server xử lý các API request của game.
/// Tương đương server/ trong bản Go gốc.
/// </summary>
public static class LanServer
{
    private static WebApplication? _app;
    private static readonly ConcurrentDictionary<string, Lobby> Lobbies = new();
    private static readonly ConcurrentDictionary<string, Player> Players = new();
    private static readonly ConcurrentDictionary<string, GameSession> Sessions = new();
    private static string _serverId = Guid.NewGuid().ToString("N")[..12];
    private static string _currentGame = string.Empty;

    /// <summary>
    /// Cấu hình server.
    /// </summary>
    public record ServerConfig
    {
        public int Port { get; init; } = 443;
        public string Host { get; init; } = "0.0.0.0";
        public string CertPath { get; init; } = string.Empty;
        public string KeyPath { get; init; } = string.Empty;
        public string GameId { get; init; } = string.Empty;
        public bool LogRequests { get; init; } = true;
        public string ResourcesPath { get; init; } = "resources";
        public string AuthenticationMode { get; init; } = "disabled";
    }

    /// <summary>
    /// Thông tin lobby.
    /// </summary>
    public record Lobby
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string HostPlayerId { get; init; } = string.Empty;
        public string GameId { get; init; } = string.Empty;
        public string Scenario { get; init; } = string.Empty;
        public string Map { get; init; } = string.Empty;
        public int MaxPlayers { get; init; } = 8;
        public List<string> PlayerIds { get; init; } = new();
        public bool IsPublic { get; init; } = true;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Thông tin người chơi.
    /// </summary>
    public record Player
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string LobbyId { get; init; } = string.Empty;
        public DateTime LastSeen { get; init; } = DateTime.UtcNow;
        public bool IsOnline => (DateTime.UtcNow - LastSeen) < TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Phiên chơi game.
    /// </summary>
    public record GameSession
    {
        public string Id { get; init; } = string.Empty;
        public string LobbyId { get; init; } = string.Empty;
        public List<string> PlayerIds { get; init; } = new();
        public DateTime StartedAt { get; init; } = DateTime.UtcNow;
        public string? RestoreData { get; init; } = string.Empty;
    }

    /// <summary>
    /// Khởi động và cấu hình server.
    /// </summary>
    public static async Task RunAsync(ServerConfig config, CancellationToken ct = default)
    {
        _currentGame = config.GameId;
        var builder = WebApplication.CreateBuilder();

        // Cấu hình HTTPS
        if (!string.IsNullOrEmpty(config.CertPath) && File.Exists(config.CertPath))
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Parse(config.Host), config.Port, listenOptions =>
                {
                    var cert = LoadCertificate(config.CertPath, config.KeyPath);
                    if (cert != null)
                        listenOptions.UseHttps(cert);
                });
            });
        }

        // Cấu hình Case-insensitive routing (Quan trọng cho game)
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteOptions>(options =>
        {
            options.LowercaseUrls = false; // Giữ nguyên case để khớp chính xác hơn
            options.LowercaseQueryStrings = false;
        });

        builder.Services.AddRouting();
        builder.Services.AddCors();

        _app = builder.Build();

        if (config.LogRequests)
        {
            _app.Use(async (context, next) =>
            {
                AppLogger.Info($"{context.Request.Method} {context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}");
                await next();
            });
        }

        _app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        // 1. Đăng ký các API endpoints gốc (Minimal)
        RegisterEndpoints(_app);

        // 2. Đăng ký các API endpoints từ RouteRegistrar (Toàn bộ logic game)
        AgeLanServer.Server.Routes.RouteRegistrar.RegisterGameRoutes(_app, _currentGame);
        AgeLanServer.Server.Routes.RouteRegistrar.RegisterGeneralRoutes(_app);

        // Header thông báo
        _app.Use(async (context, next) =>
        {
			context.Response.Headers["X-Server-Id"] = _serverId;
			context.Response.Headers["X-Announce-Version"] = AnnounceVersions.Latest.ToString();
            context.Response.Headers["X-Game-Title"] = _currentGame;
            await next();
        });

        var url = $"https://{config.Host}:{config.Port}";
        AppLogger.Info($"Server đang chạy tại {url}");
        AppLogger.Info($"Server ID: {_serverId}");
        AppLogger.Info($"Game: {_currentGame}");

        await _app.RunAsync(ct);
    }

    /// <summary>
    /// Tải chứng chỉ SSL từ file.
    /// </summary>
    private static X509Certificate2? LoadCertificate(string certPath, string keyPath)
    {
        var certDir = Path.GetDirectoryName(certPath) ?? ".";

        // Ưu tiên load PFX (Windows SChannel xử lý tốt nhất)
        var pfxPath = Path.Combine(certDir, "server.pfx");
        if (File.Exists(pfxPath))
        {
            try
            {
                var cert = new X509Certificate2(pfxPath, "");
                AppLogger.Info($"Đã tải chứng chỉ PFX thành công ({Path.GetFileName(pfxPath)})");
                return cert;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Không thể tải PFX: {ex.Message}");
            }
        }

        // Thử load cert + key chính từ PEM
        try
        {
            var certPem = File.ReadAllText(certPath);
            var cert = X509Certificate2.CreateFromPem(certPem);

            if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
            {
                var keyPem = File.ReadAllText(keyPath);
                using var rsa = RSA.Create();
                rsa.ImportFromPem(keyPem);
                cert = cert.CopyWithPrivateKey(rsa);
            }

            AppLogger.Info($"Đã tải chứng chỉ PEM thành công ({Path.GetFileName(certPath)})");
            return cert;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Không thể tải cert chính ({Path.GetFileName(certPath)}): {ex.Message}");
        }

        // Fallback: thử self-signed cert pair
        var selfSignedCert = Path.Combine(certDir, AppConstants.SelfSignedCert);
        var selfSignedKey = Path.Combine(certDir, AppConstants.SelfSignedKey);

        if (File.Exists(selfSignedCert) && File.Exists(selfSignedKey))
        {
            try
            {
                var certPem = File.ReadAllText(selfSignedCert);
                var keyPem = File.ReadAllText(selfSignedKey);
                var cert = X509Certificate2.CreateFromPem(certPem);

                using var rsa = RSA.Create();
                rsa.ImportFromPem(keyPem);
                cert = cert.CopyWithPrivateKey(rsa);

                AppLogger.Info("Đã tải self-signed certificate thành công");
                return cert;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Không thể tải self-signed cert: {ex.Message}");
            }
        }

        AppLogger.Warn("Không tìm thấy chứng chỉ hợp lệ nào. Server sẽ chạy không HTTPS.");
        return null;
    }

    /// <summary>
    /// Đăng ký tất cả API endpoints.
    /// </summary>
    private static void RegisterEndpoints(WebApplication app)
    {
        // === Health check & test endpoint ===
        app.MapGet("/test", () => Results.Ok(new
        {
            ServerId = _serverId,
            GameTitle = _currentGame,
            Version = "1.0",
            AnnounceVersion = AnnounceVersions.Latest
        }));

        // === API endpoints chính ===

        // Login endpoint
        app.MapPost("/api/login", async (HttpContext ctx, LoginRequest req) =>
        {
            var player = new Player
            {
                Id = req.PlayerId ?? Guid.NewGuid().ToString("N"),
                Name = req.PlayerName ?? "Player",
                LastSeen = DateTime.UtcNow
            };

            Players[player.Id] = player;

            var loginResponse = await LoadLoginConfigAsync();
            return Results.Ok(loginResponse);
        });

        // Player presence
        app.MapGet("/api/player/presence", () =>
        {
            var onlinePlayers = Players.Values
                .Where(p => p.IsOnline)
                .Select(p => new { p.Id, p.Name, Status = "online" })
                .ToList();

            return Results.Ok(new { Players = onlinePlayers });
        });

        // Update player presence (heartbeat)
        app.MapPost("/api/player/presence", async (HttpContext ctx, PresenceUpdate req) =>
        {
            if (Players.TryGetValue(req.PlayerId, out var player))
            {
                player = player with { LastSeen = DateTime.UtcNow };
                Players[req.PlayerId] = player;
            }
            return Results.Ok();
        });

        // === Lobby Management ===

        // Danh sách lobbies
        app.MapGet("/api/lobbies", () =>
        {
            var publicLobbies = Lobbies.Values
                .Where(l => l.IsPublic && l.GameId == _currentGame)
                .Select(l => new
                {
                    l.Id,
                    l.Name,
                    l.HostPlayerId,
                    PlayerCount = l.PlayerIds.Count,
                    l.MaxPlayers,
                    l.Map,
                    l.Scenario,
                    l.IsPublic
                })
                .ToList();

            return Results.Ok(new { Lobbies = publicLobbies });
        });

        // Tạo lobby mới
        app.MapPost("/api/lobbies", async (HttpContext ctx, CreateLobbyRequest req) =>
        {
            var lobby = new Lobby
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = req.Name ?? "Lobby",
                HostPlayerId = req.HostPlayerId,
                GameId = _currentGame,
                Scenario = req.Scenario ?? string.Empty,
                Map = req.Map ?? string.Empty,
                MaxPlayers = req.MaxPlayers > 0 ? req.MaxPlayers : 8,
                PlayerIds = new List<string> { req.HostPlayerId },
                IsPublic = req.IsPublic != false
            };

            Lobbies[lobby.Id] = lobby;

            // Cập nhật player
            if (Players.TryGetValue(req.HostPlayerId, out var player))
            {
                player = player with { LobbyId = lobby.Id, LastSeen = DateTime.UtcNow };
                Players[req.HostPlayerId] = player;
            }

            return Results.Created($"/api/lobbies/{lobby.Id}", lobby);
        });

        // Thông tin lobby
        app.MapGet("/api/lobbies/{lobbyId}", (string lobbyId) =>
        {
            if (!Lobbies.TryGetValue(lobbyId, out var lobby))
                return Results.NotFound();

            var players = lobby.PlayerIds
                .Select(id => Players.GetValueOrDefault(id))
                .Where(p => p != null)
                .Select(p => new { p!.Id, p.Name })
                .ToList();

            return Results.Ok(new { lobby, Players = players });
        });

        // Cập nhật lobby
        app.MapPut("/api/lobbies/{lobbyId}", (string lobbyId, UpdateLobbyRequest req) =>
        {
            if (!Lobbies.TryGetValue(lobbyId, out var lobby))
                return Results.NotFound();

            lobby = lobby with
            {
                Name = req.Name ?? lobby.Name,
                Map = req.Map ?? lobby.Map,
                Scenario = req.Scenario ?? lobby.Scenario,
                IsPublic = req.IsPublic ?? lobby.IsPublic,
                UpdatedAt = DateTime.UtcNow
            };

            Lobbies[lobbyId] = lobby;
            return Results.Ok(lobby);
        });

        // Xóa lobby
        app.MapDelete("/api/lobbies/{lobbyId}", (string lobbyId) =>
        {
            if (Lobbies.TryRemove(lobbyId, out var lobby))
            {
                // Giải phóng player khỏi lobby
                foreach (var playerId in lobby.PlayerIds)
                {
                    if (Players.TryGetValue(playerId, out var player))
                    {
                        player = player with { LobbyId = string.Empty };
                        Players[playerId] = player;
                    }
                }
                return Results.Ok();
            }
            return Results.NotFound();
        });

        // Tham gia lobby
        app.MapPost("/api/lobbies/{lobbyId}/join", (string lobbyId, JoinRequest req) =>
        {
            if (!Lobbies.TryGetValue(lobbyId, out var lobby))
                return Results.NotFound();

            if (lobby.PlayerIds.Count >= lobby.MaxPlayers)
                return Results.BadRequest(new { Error = "Lobby đầy" });

            if (!lobby.PlayerIds.Contains(req.PlayerId))
            {
                lobby.PlayerIds.Add(req.PlayerId);
                Lobbies[lobbyId] = lobby;
            }

            if (Players.TryGetValue(req.PlayerId, out var player))
            {
                player = player with { LobbyId = lobbyId, LastSeen = DateTime.UtcNow };
                Players[req.PlayerId] = player;
            }

            return Results.Ok();
        });

        // Rời lobby
        app.MapPost("/api/lobbies/{lobbyId}/leave", (string lobbyId, LeaveRequest req) =>
        {
            if (Lobbies.TryGetValue(lobbyId, out var lobby))
            {
                lobby.PlayerIds.Remove(req.PlayerId);
                Lobbies[lobbyId] = lobby;
            }

            if (Players.TryGetValue(req.PlayerId, out var player))
            {
                player = player with { LobbyId = string.Empty, LastSeen = DateTime.UtcNow };
                Players[req.PlayerId] = player;
            }

            return Results.Ok();
        });

        // Mời player vào lobby
        app.MapPost("/api/lobbies/{lobbyId}/invite", (string lobbyId, InviteRequest req) =>
        {
            if (!Lobbies.TryGetValue(lobbyId, out var lobby))
                return Results.NotFound();

            if (!Players.TryGetValue(req.TargetPlayerId, out var target))
                return Results.NotFound(new { Error = "Không tìm thấy người chơi" });

            // Gửi invitation (lưu vào bộ nhớ)
            AppLogger.Info($"Mời {target.Name} vào lobby {lobby.Name}");
            return Results.Ok();
        });

        // === Game Sessions ===

        // Bắt đầu session
        app.MapPost("/api/sessions", (CreateSessionRequest req) =>
        {
            var session = new GameSession
            {
                Id = Guid.NewGuid().ToString("N"),
                LobbyId = req.LobbyId,
                PlayerIds = req.PlayerIds ?? new(),
                StartedAt = DateTime.UtcNow
            };

            Sessions[session.Id] = session;
            return Results.Created($"/api/sessions/{session.Id}", session);
        });

        // Restore session
        app.MapGet("/api/sessions/{sessionId}/restore", (string sessionId) =>
        {
            if (!Sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound();

            return Results.Ok(new { session.RestoreData });
        });

        // === Static Resources ===

        // Achievements
        app.MapGet("/api/achievements", async () =>
        {
            var path = GetResourcePath("achievements.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { Achievements = Array.Empty<object>() });
        });

        // Leaderboards
        app.MapGet("/api/leaderboards", async () =>
        {
            var path = GetResourcePath("leaderboards.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { Leaderboards = Array.Empty<object>() });
        });

        // Item definitions
        app.MapGet("/api/items", async () =>
        {
            var path = GetResourcePath("itemDefinitions.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { Items = Array.Empty<object>() });
        });

        // Challenges
        app.MapGet("/api/challenges", async () =>
        {
            var path = GetResourcePath("challenges.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { Challenges = Array.Empty<object>() });
        });

        // Presence data
        app.MapGet("/api/presence", async () =>
        {
            var path = GetResourcePath("presenceData.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { PresenceData = Array.Empty<object>() });
        });

        // Automatch maps
        app.MapGet("/api/automatch/maps", async () =>
        {
            var path = GetResourcePath("automatchMaps.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { Maps = Array.Empty<object>() });
        });

        // Cloud files index
        app.MapGet("/api/cloud", async () =>
        {
            var path = GetResourcePath("cloudfilesIndex.json");
            if (File.Exists(path))
                return Results.Content(await File.ReadAllTextAsync(path), "application/json");
            return Results.Ok(new { Files = Array.Empty<object>() });
        });

        // === Shutdown endpoint ===
        app.MapPost("/shutdown", () =>
        {
            AppLogger.Info("Nhận yêu cầu dừng server");
            Environment.Exit(0);
            return Results.Ok();
        });
    }

    /// <summary>
    /// Tải cấu hình login từ file JSON.
    /// </summary>
    private static async Task<Dictionary<string, object>> LoadLoginConfigAsync()
    {
        var path = GetResourcePath("login.json");
        if (File.Exists(path))
        {
            var content = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(content)
                   ?? new Dictionary<string, object>();
        }

        return new Dictionary<string, object>
        {
            ["SessionKey"] = Guid.NewGuid().ToString("N"),
            ["ServerId"] = _serverId,
            ["GameTitle"] = _currentGame
        };
    }

    /// <summary>
    /// Lấy đường dẫn tới file resource cho game hiện tại.
    /// </summary>
    private static string GetResourcePath(string fileName)
    {
        var gameFolder = string.IsNullOrEmpty(_currentGame) ? "" : _currentGame;
        return Path.Combine("resources", "responses", gameFolder, fileName);
    }

    // === DTOs cho request/response ===

    public record LoginRequest(string? PlayerId, string? PlayerName);
    public record PresenceUpdate(string PlayerId);
    public record CreateLobbyRequest(string Name, string HostPlayerId, string? Scenario, string? Map, int MaxPlayers, bool? IsPublic);
    public record UpdateLobbyRequest(string? Name, string? Map, string? Scenario, bool? IsPublic);
    public record JoinRequest(string PlayerId);
    public record LeaveRequest(string PlayerId);
    public record InviteRequest(string TargetPlayerId);
    public record CreateSessionRequest(string LobbyId, List<string>? PlayerIds);
}

using System.Net;
using System.Net.Sockets;
using AgeLanServer.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AgeLanServer.Server.Internal;

internal sealed record BattleServerRuntimeInfo(
    string Region,
    string Name,
    string IPv4,
    int BsPort,
    int WebSocketPort,
    int OutOfBandPort);

internal static class BattleServerRuntime
{
    private const int DefaultBattleServerPort = 27012;
    private const int DefaultBattleServerWebSocketPort = 27112;
    private const int DefaultBattleServerOutOfBandPort = 27212;

    public static bool IsLanRegion(string? region)
    {
        return !string.IsNullOrWhiteSpace(region) && Guid.TryParse(region, out _);
    }

    public static bool RequiresDedicatedBattleServer(string gameId)
    {
        return gameId is GameIds.AgeOfEmpires4 or GameIds.AgeOfMythology;
    }

    public static bool HasReadyBattleServers(string gameId)
    {
        return LoadConfiguredBattleServers(gameId).Count > 0;
    }

    public static async Task<bool> WaitForReadyBattleServersAsync(
        string gameId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            if (HasReadyBattleServers(gameId))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return HasReadyBattleServers(gameId);
    }

    public static bool TryGetConfiguredBattleServer(string gameId, string? region, out BattleServerRuntimeInfo server)
    {
        server = null!;
        if (string.IsNullOrWhiteSpace(region))
        {
            return false;
        }

        var configured = LoadConfiguredBattleServers(gameId)
            .FirstOrDefault(s => string.Equals(s.Region, region, StringComparison.OrdinalIgnoreCase));

        if (configured is null)
        {
            return false;
        }

        server = configured;
        return true;
    }

    public static BattleServerRuntimeInfo CreateLanBattleServer(string gameId, string? region)
    {
        var template = LoadConfiguredBattleServers(gameId).FirstOrDefault();

        return new BattleServerRuntimeInfo(
            region ?? string.Empty,
            string.IsNullOrWhiteSpace(template?.Name) ? "localhost" : template.Name,
            "auto",
            template?.BsPort > 0 ? template.BsPort : DefaultBattleServerPort,
            template?.WebSocketPort > 0 ? template.WebSocketPort : DefaultBattleServerWebSocketPort,
            template?.OutOfBandPort > 0 ? template.OutOfBandPort : DefaultBattleServerOutOfBandPort);
    }

    public static object[] EncodeLoginServers(HttpContext ctx, string gameId)
    {
        var includeName = gameId != GameIds.AgeOfEmpires1;
        var includeOutOfBand = gameId != GameIds.AgeOfEmpires1;

        var configuredServers = LoadConfiguredBattleServers(gameId);
        if (configuredServers.Count == 0)
        {
            var fallback = new List<object>
            {
                string.Empty
            };

            if (includeName)
            {
                fallback.Add("localhost");
            }

            fallback.Add("127.0.0.1");
            fallback.Add(DefaultBattleServerPort);
            fallback.Add(DefaultBattleServerWebSocketPort);

            if (includeOutOfBand)
            {
                fallback.Add(DefaultBattleServerOutOfBandPort);
            }

            return new object[] { fallback.ToArray() };
        }

        var encodedServers = new List<object[]>(configuredServers.Count);
        foreach (var server in configuredServers)
        {
            var encoded = new List<object>
            {
                server.Region
            };

            if (includeName)
            {
                encoded.Add(string.IsNullOrWhiteSpace(server.Name) ? "localhost" : server.Name);
            }

            encoded.Add(ResolveIPv4(ctx, server.IPv4));
            encoded.Add(server.BsPort);
            encoded.Add(server.WebSocketPort);

            if (includeOutOfBand)
            {
                encoded.Add(server.OutOfBandPort > 0 ? server.OutOfBandPort : DefaultBattleServerOutOfBandPort);
            }

            encodedServers.Add(encoded.ToArray());
        }

        return encodedServers.Cast<object>().ToArray();
    }

    public static string ResolveIPv4(HttpContext ctx, string? configuredIp)
    {
        if (!string.Equals(configuredIp, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(configuredIp) ? "127.0.0.1" : configuredIp;
        }

        var remoteAddress = ctx.Connection.RemoteIpAddress;
        if (remoteAddress is not null && IPAddress.IsLoopback(remoteAddress))
        {
            return "127.0.0.1";
        }

        var localAddress = ctx.Features.Get<IHttpConnectionFeature>()?.LocalIpAddress ?? ctx.Connection.LocalIpAddress;
        if (localAddress is not null)
        {
            var localV4 = localAddress.MapToIPv4();
            if (!IPAddress.Any.Equals(localV4) && !IPAddress.None.Equals(localV4))
            {
                return localV4.ToString();
            }
        }

        try
        {
            var hostAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            var candidate = hostAddresses.FirstOrDefault(ip =>
                ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));

            if (candidate is not null)
            {
                return candidate.ToString();
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }

    private static List<BattleServerRuntimeInfo> LoadConfiguredBattleServers(string gameId)
    {
        var configs = BattleServerConfigManager.LoadConfigs(gameId, onlyValid: true);

        return configs
            .Where(c => c.BsPort > 0 && c.WebSocketPort > 0)
            .Select(c => new BattleServerRuntimeInfo(
                c.Region,
                string.IsNullOrWhiteSpace(c.Name) ? "localhost" : c.Name,
                string.IsNullOrWhiteSpace(c.IPv4) ? "auto" : c.IPv4,
                c.BsPort,
                c.WebSocketPort,
                c.OutOfBandPort > 0 ? c.OutOfBandPort : DefaultBattleServerOutOfBandPort))
            .ToList();
    }
}

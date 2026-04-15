using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgeLanServer.Common;
using AgeLanServer.Launcher.Internal.CmdUtils.Logger;
using AgeLanServer.LauncherCommon;
using AgeLanServer.ServerGenCert;

namespace AgeLanServer.Launcher.Internal.Server;

/// <summary>
/// Module quáº£n lÃ½ server - khá»Ÿi Ä‘á»™ng, táº¡o certificate, khÃ¡m phÃ¡ server qua UDP.
/// TÆ°Æ¡ng Ä‘Æ°Æ¡ng package server trong Go (server.go, ssl.go, announce.go).
/// </summary>
public static class ServerModule
{
    private const int LatencyMeasurementCount = 3;
    public const int AnnounceIdLength = 16;

    /// <summary>
    /// Äá»‹a chá»‰ IP Ä‘Ã£ Ä‘o latency.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng MesuredIpAddress trong server.go
    /// </summary>
    public record MeasuredIpAddress
    {
        public IPAddress Ip { get; init; } = IPAddress.Any;
        public TimeSpan Latency { get; init; }
    }

    /// <summary>
    /// Káº¿t quáº£ lá»c IP server.
    /// </summary>
    public record FilterServerIPsResult
    {
        public Guid ActualId { get; init; }
        public List<MeasuredIpAddress> MeasuredIpAddresses { get; init; } = new();
        public AnnounceMessageDataSupportedLatest? Data { get; init; }
    }

    /// <summary>
    /// Dá»¯ liá»‡u announce message phiÃªn báº£n má»›i nháº¥t Ä‘Æ°á»£c há»— trá»£.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng AnnounceMessageDataSupportedLatest trong announce.go
    /// </summary>
    public class AnnounceMessageDataSupportedLatest
    {
        [JsonPropertyName("game_title")]
        public string GameTitle { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// ThÃ´ng Ä‘iá»‡p announce tá»« server.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng AnnounceMessage trong announce.go
    /// </summary>
    public class AnnounceMessage
    {
        public HashSet<IPAddress> IpAddrs { get; set; } = new();
    }

    /// <summary>
    /// Khá»Ÿi Ä‘á»™ng server cá»¥c bá»™.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng StartServer trong server.go
    /// </summary>
    public static (int errorCode, string? ip) StartServerLocal(
        string gameTitle,
        string executablePath,
        List<string> args,
        bool stop,
        Guid id)
    {
        var showWindow = !stop;
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = string.Join(" ", args),
            UseShellExecute = false,
            CreateNoWindow = !showWindow,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            return (LauncherErrorCodes.InvalidGame + 22, null); // ErrServerStart
        }

        // Chá» server khá»Ÿi Ä‘á»™ng vÃ  láº¯ng nghe
        var timeout = TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (process.HasExited)
            {
                var errorOutput = process.StandardError.ReadToEnd();
                return (LauncherErrorCodes.InvalidGame + 22, null);
            }

            // Thá»­ káº¿t ná»‘i Ä‘áº¿n server
            // Prefer loopback first so hosts mapping matches local certificates.
            if (TryLanServerIP(id, gameTitle, IPAddress.Loopback, "", true, false, out _))
            {
                return (ErrorCodes.Success, IPAddress.Loopback.ToString());
            }

            // Fallback: probe IPv4 LAN interfaces.
            var localIPs = CommonUtilities.GetLocalIPv4Addresses();
            foreach (var localIp in localIPs)
            {
                if (TryLanServerIP(id, gameTitle, localIp, "", true, false, out var data))
                {
                    return (ErrorCodes.Success, localIp.ToString());
                }
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }

        // Timeout - kill server
        try
        {
            process.Kill();
        }
        catch { }
        finally
        {
            process.Dispose();
        }

        return (LauncherErrorCodes.InvalidGame + 22, null);
    }

    /// <summary>
    /// Táº¡o certificate pair cho server.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng GenerateServerCertificates trong server.go
    /// </summary>
    public static int GenerateServerCertificates(string serverExecutablePath, bool canTrustCertificate)
    {
        var certDir = Path.Combine(Path.GetDirectoryName(serverExecutablePath) ?? ".", "resources", "certificates");
        var certPath = Path.Combine(certDir, "cert.pem");
        var keyPath = Path.Combine(certDir, "key.pem");

        bool certExists = File.Exists(certPath) && File.Exists(keyPath);

        bool certSoonExpired = false;
        if (certExists)
        {
            try
            {
                var cert = new X509Certificate2(certPath);
                certSoonExpired = DateTime.UtcNow.AddHours(24) > cert.NotAfter;
            }
            catch
            {
                certSoonExpired = true;
            }
        }

        if (!certExists || certSoonExpired)
        {
            if (!canTrustCertificate)
            {
                LauncherLogger.Error(
                    "serverStart lÃ  true vÃ  canTrustCertificate lÃ  false. " +
                    "Certificate pair bá»‹ thiáº¿u hoáº·c sáº¯p háº¿t háº¡n. HÃ£y táº¡o certificate thá»§ cÃ´ng.");
                return LauncherErrorCodes.InvalidGame + 24; // ErrServerCertMissingExpired
            }

            if (!Directory.Exists(certDir))
            {
                LauncherLogger.Error(
                    "KhÃ´ng thá»ƒ tÃ¬m thÆ° má»¥c certificate cá»§a 'server'. " +
                    "Äáº£m báº£o cáº¥u trÃºc thÆ° má»¥c cá»§a 'server' Ä‘Ãºng.");
                return LauncherErrorCodes.InvalidGame + 25; // ErrServerCertDirectory
            }

            // Táº¡o self-signed certificate báº±ng SslCertificateGenerator
            // TÆ°Æ¡ng Ä‘Æ°Æ¡ng GenerateCertificatePairs trong server-gen-cert Go
            LauncherLogger.Info("Äang táº¡o certificate pair...");
            if (!SslCertificateGenerator.GenerateCertificatePairs(certDir))
            {
                LauncherLogger.Error("KhÃ´ng thá»ƒ táº¡o certificate pair. HÃ£y kiá»ƒm tra quyá»n ghi thÆ° má»¥c.");
                return LauncherErrorCodes.InvalidGame + 26; // ErrServerCertCreate
            }

            LauncherLogger.Info("ÄÃ£ táº¡o certificate pair thÃ nh cÃ´ng.");
        }

        return ErrorCodes.Success;
    }

    /// <summary>
    /// Láº¥y Ä‘Æ°á»ng dáº«n executable cá»§a server.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng GetExecutablePath trong server.go
    /// </summary>
    public static string GetExecutablePath(string executable)
    {
        if (executable == "auto")
        {
            return ExecutablePaths.FindExecutablePath(ExecutablePaths.Server) ?? string.Empty;
        }
        return executable;
    }

    /// <summary>
    /// Kiá»ƒm tra host cÃ³ trá» Ä‘áº¿n LAN server khÃ´ng.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng LanServerHost trong server.go
    /// </summary>
    public static bool LanServerHost(Guid id, string gameTitle, string host,
        bool insecureSkipVerify, X509Certificate2[]? rootCAs)
    {
        var ipAddrs = CommonUtilities.HostOrIpToIps(host);
        if (ipAddrs.Count == 0)
            return false;

        foreach (var ipAddr in ipAddrs)
        {
            if (!TryParseIP(ipAddr, out var ip))
                continue;

            if (!TryLanServerIP(id, gameTitle, ip, host, insecureSkipVerify, true, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Lá»c vÃ  Ä‘o latency cÃ¡c IP server.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng FilterServerIPs trong server.go
    /// </summary>
    public static FilterServerIPsResult FilterServerIPs(Guid id, string serverName,
        string gameTitle, HashSet<IPAddress> possibleIpAddrs)
    {
        var measuredIps = new List<MeasuredIpAddress>();
        var actualId = Guid.Empty;
        AnnounceMessageDataSupportedLatest? data = null;

        foreach (var ipAddr in possibleIpAddrs)
        {
            if (TryLanServerIP(id, gameTitle, ipAddr, serverName, true, false, out var checkResult))
            {
                measuredIps.Add(new MeasuredIpAddress
                {
                    Ip = ipAddr,
                    Latency = checkResult.Latency
                });

                if (actualId == Guid.Empty)
                {
                    actualId = checkResult.ServerId;
                }

                data ??= checkResult.Data;
            }
        }

        measuredIps.Sort((a, b) => a.Latency.CompareTo(b.Latency));
        return new FilterServerIPsResult
        {
            ActualId = actualId,
            MeasuredIpAddresses = measuredIps,
            Data = data
        };
    }

    /// <summary>
    /// Káº¿t quáº£ kiá»ƒm tra LAN server.
    /// </summary>
    private record LanServerCheckResult
    {
        public Guid ServerId { get; init; }
        public TimeSpan Latency { get; init; }
        public AnnounceMessageDataSupportedLatest? Data { get; init; }
    }

    /// <summary>
    /// Kiá»ƒm tra IP cÃ³ pháº£i lÃ  LAN server khÃ´ng.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng lanServerIP trong server.go
    /// </summary>
    private static bool TryLanServerIP(Guid id, string gameTitle, IPAddress ipAddr,
        string serverName, bool insecureSkipVerify, bool ignoreLatency,
        out LanServerCheckResult result)
    {
        result = new LanServerCheckResult();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) =>
                insecureSkipVerify || errors == System.Net.Security.SslPolicyErrors.None
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };

        // Äo latency
        if (!ignoreLatency)
        {
            TimeSpan latencyAccumulator = TimeSpan.Zero;
            int successCount = 0;

            for (int i = 0; i < LatencyMeasurementCount; i++)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var uri = $"https://{ipAddr}/test";
                    var req = new HttpRequestMessage(HttpMethod.Head, uri);
                    req.Headers.TryAddWithoutValidation("User-Agent", $"{AppConstants.Name}/1.0");
                    req.Headers.Host = serverName;
                    using var resp = client.SendAsync(req).Result;
                    if (resp.StatusCode != HttpStatusCode.OK)
                        continue;
                    successCount++;
                }
                catch
                {
                    continue;
                }
                finally
                {
                    sw.Stop();
                    latencyAccumulator += sw.Elapsed;
                }
            }

            if (successCount == 0)
                return false;

            result = result with { Latency = TimeSpan.FromTicks(latencyAccumulator.Ticks / Math.Max(successCount, 1)) };
        }

        // Láº¥y thÃ´ng tin server
        try
        {
            var uri2 = $"https://{ipAddr}/test";
            var req2 = new HttpRequestMessage(HttpMethod.Get, uri2);
            req2.Headers.Host = serverName;
            using var resp2 = client.SendAsync(req2).Result;

            if (resp2.StatusCode != HttpStatusCode.OK)
                return false;

            string? version = null;
            string? serverIdStr = null;

            if (resp2.Headers.TryGetValues(AppConstants.VersionHeader, out var versionHeaders))
                version = versionHeaders.FirstOrDefault();
            if (resp2.Headers.TryGetValues(AppConstants.IdHeader, out var idHeaders))
                serverIdStr = idHeaders.FirstOrDefault();

            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(serverIdStr))
                return false;

            if (!int.TryParse(version, out var versionInt) || versionInt > 2) // AnnounceVersionLatest = 2
                return false;

            if (!Guid.TryParse(serverIdStr, out var serverIdUuid))
                return false;

            if (id != Guid.Empty && id != serverIdUuid)
                return false;

            var content = resp2.Content.ReadAsStringAsync().Result;
            AnnounceMessageDataSupportedLatest? msgData = null;
            try
            {
                msgData = JsonSerializer.Deserialize<AnnounceMessageDataSupportedLatest>(content);
                if (msgData?.GameTitle != gameTitle)
                    return false;
            }
            catch
            {
                return false;
            }

            result = result with { ServerId = serverIdUuid, Data = msgData };
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// KhÃ¡m phÃ¡ vÃ  chá»n server tá»‘t nháº¥t.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng DiscoverServersAndSelectBestIpAddr trong server.go
    /// </summary>
    public static (Guid id, IPAddress? ip) DiscoverServersAndSelectBestIpAddr(
        string gameTitle,
        bool singleAutoSelect,
        HashSet<IPAddress> multicastGroups,
        HashSet<ushort> targetPorts)
    {
        var servers = new ConcurrentDictionary<Guid, AnnounceMessage>();

        LauncherLogger.Info("Äang tÃ¬m 'server', báº¡n cÃ³ thá»ƒ cáº§n cho phÃ©p 'launcher' trong firewall...");
        QueryServers(multicastGroups, targetPorts, servers);

        if (servers.IsEmpty)
            return (Guid.Empty, null);

        var processed = new List<(Guid id, IPAddress ip, TimeSpan latency, string description)>();

        foreach (var (serverId, announceMsg) in servers)
        {
            foreach (var ipAddr in announceMsg.IpAddrs)
            {
                var filterResult = FilterServerIPs(serverId, "", gameTitle, new HashSet<IPAddress> { ipAddr });
                if (filterResult.Data == null || filterResult.MeasuredIpAddresses.Count == 0)
                    continue;

                var best = filterResult.MeasuredIpAddresses[0];
                var description = $"{best.Ip} - {best.Latency.TotalMilliseconds:F0}ms ({filterResult.Data.Version})";

                processed.Add((filterResult.ActualId, best.Ip, best.Latency, description));
            }
        }

        processed.Sort((a, b) => a.latency.CompareTo(b.latency));

        if (processed.Count == 0)
            return (Guid.Empty, null);

        if (singleAutoSelect && processed.Count == 1)
        {
            LauncherLogger.Info("Tá»± Ä‘á»™ng chá»n server duy nháº¥t tÃ¬m tháº¥y.");
            return (processed[0].id, processed[0].ip);
        }

        // Hiá»ƒn thá»‹ danh sÃ¡ch server tÃ¬m tháº¥y
        LauncherLogger.Info("ÄÃ£ tÃ¬m tháº¥y cÃ¡c 'server':");
        for (int i = 0; i < processed.Count; i++)
        {
            LauncherLogger.Info($"{i + 1}. {processed[i].description}");
        }

        // Chá»n server Ä‘áº§u tiÃªn (tá»‘t nháº¥t)
        // Trong thá»±c táº¿, sáº½ yÃªu cáº§u ngÆ°á»i dÃ¹ng nháº­p sá»‘
        var selected = processed[0];
        LauncherLogger.Info($"Tá»± Ä‘á»™ng chá»n server: {selected.description}");
        return (selected.id, selected.ip);
    }

    /// <summary>
    /// KhÃ¡m phÃ¡ server qua UDP broadcast/multicast.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng QueryServers trong server.go
    /// </summary>
    public static void QueryServers(
        HashSet<IPAddress> multicastGroups,
        HashSet<ushort> targetPorts,
        ConcurrentDictionary<Guid, AnnounceMessage> servers)
    {
        var sourceToTargetAddrs = SourceToTargetUDPAddrs(multicastGroups, targetPorts);
        if (sourceToTargetAddrs.Count == 0)
            return;

        var connections = new List<(UdpClient conn, IPEndPoint target)>();
        var allConns = new HashSet<UdpClient>();

        foreach (var (source, targets) in sourceToTargetAddrs)
        {
            try
            {
                var conn = new UdpClient(source);
                allConns.Add(conn);

                foreach (var target in targets)
                {
                    connections.Add((conn, target));
                }
            }
            catch
            {
                // Bá» qua lá»—i táº¡o connection
            }
        }

        var headerBytes = System.Text.Encoding.UTF8.GetBytes(AppConstants.AnnounceHeader);
        var lockObj = new object();

        var tasks = new List<Task>();

        foreach (var (conn, target) in connections)
        {
            tasks.Add(Task.Run(async () =>
            {
                var packetBuffer = new byte[AppConstants.AnnounceHeader.Length + AnnounceIdLength];

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        await conn.SendAsync(headerBytes, headerBytes.Length, target);
                        conn.Client.ReceiveTimeout = 1000;

                        var received = await conn.ReceiveAsync();

                        if (received.Buffer.Length < packetBuffer.Length)
                            continue;

                        var header = System.Text.Encoding.UTF8.GetString(
                            received.Buffer, 0, AppConstants.AnnounceHeader.Length);
                        if (header != AppConstants.AnnounceHeader)
                            continue;

                        if (received.Buffer.Length >= AppConstants.AnnounceHeader.Length + AnnounceIdLength)
                        {
                            var idBytes = received.Buffer[AppConstants.AnnounceHeader.Length..(AppConstants.AnnounceHeader.Length + AnnounceIdLength)];
                            var parsedId = new Guid(idBytes);

                            lock (lockObj)
                            {
                                var server = servers.GetOrAdd(parsedId, _ => new AnnounceMessage());
                                server.IpAddrs.Add(received.RemoteEndPoint.Address);
                            }
                        }
                    }
                    catch
                    {
                        // Timeout hoáº·c lá»—i - thá»­ láº¡i
                    }

                    if (attempt < 2)
                        await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        foreach (var conn in allConns)
        {
            try { conn.Close(); } catch { }
            conn.Dispose();
        }
    }

    /// <summary>
    /// TÃ­nh toÃ¡n Ä‘á»‹a chá»‰ broadcast tá»« IP vÃ  subnet mask.
    /// </summary>
    private static IPAddress CalculateBroadcastIPv4(IPAddress ip, IPAddress mask)
    {
        var ipBytes = ip.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcast = new byte[4];

        for (int i = 0; i < 4; i++)
        {
            broadcast[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcast);
    }

    /// <summary>
    /// Táº¡o mapping tá»« source address Ä‘áº¿n target UDP addresses.
    /// </summary>
    private static Dictionary<IPEndPoint, List<IPEndPoint>> SourceToTargetUDPAddrs(
        HashSet<IPAddress> multicastGroups,
        HashSet<ushort> targetPorts)
    {
        var mapping = new Dictionary<IPEndPoint, List<IPEndPoint>>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            foreach (var iface in interfaces)
            {
                var ipProps = iface.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .ToList();

                foreach (var ipProp in ipProps)
                {
                    var sourceAddr = new IPEndPoint(ipProp.Address, 0);
                    mapping[sourceAddr] = new List<IPEndPoint>();

                    // Broadcast
                    if (iface.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        foreach (var port in targetPorts)
                        {
                            try
                            {
                                var broadcastIp = CalculateBroadcastIPv4(ipProp.Address, ipProp.IPv4Mask);
                                mapping[sourceAddr].Add(new IPEndPoint(broadcastIp, port));
                            }
                            catch { }
                        }
                    }

                    // Multicast
                    if (multicastGroups.Count > 0 && iface.SupportsMulticast)
                    {
                        foreach (var multicastGroup in multicastGroups)
                        {
                            foreach (var port in targetPorts)
                            {
                                mapping[sourceAddr].Add(new IPEndPoint(multicastGroup, port));
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // KhÃ´ng thá»ƒ láº¥y danh sÃ¡ch interface
        }

        return mapping;
    }

    /// <summary>
    /// Äá»c CA certificate tá»« server.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng ReadCACertificateFromServer trong ssl.go
    /// </summary>
    public static X509Certificate2? ReadCACertificateFromServer(string host)
    {
        var ips = CommonUtilities.HostOrIpToIps(host);
        var ip = ips.Count > 0 ? ips[0] : host;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };

        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://{ip}/cacert.pem");
            req.Headers.TryAddWithoutValidation("User-Agent", $"{AppConstants.Name}/1.0");
            req.Headers.Host = host;

            using var resp = client.SendAsync(req).Result;
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                AppLogger.Info($"ReadCACertificateFromServer status code: {resp.StatusCode}");
                return null;
            }

            var bodyBytes = resp.Content.ReadAsByteArrayAsync().Result;
            return new X509Certificate2(bodyBytes);
        }
        catch (Exception ex)
        {
            AppLogger.Info($"ReadCACertificateFromServer error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Kiá»ƒm tra káº¿t ná»‘i Ä‘áº¿n server.
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng CheckConnectionFromServer trong ssl.go
    /// </summary>
    public static bool CheckConnectionFromServer(string host, bool insecureSkipVerify, X509Certificate2[]? rootCAs)
    {
        var ips = CommonUtilities.HostOrIpToIps(host);
        var ip = ips.Count > 0 ? ips[0] : host;

        try
        {
            using var tcp = new TcpClient();
            var result = tcp.BeginConnect(ip, 443, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));

            if (!success)
            {
                tcp.Close();
                return false;
            }

            tcp.EndConnect(result);

            using var sslStream = new System.Net.Security.SslStream(
                tcp.GetStream(),
                false,
                (sender, certificate, chain, sslPolicyErrors) =>
                    insecureSkipVerify || sslPolicyErrors == System.Net.Security.SslPolicyErrors.None);

            sslStream.AuthenticateAsClient(host);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kiá»ƒm tra certificate cÃ³ sáº¯p háº¿t háº¡n khÃ´ng (trong 24h).
    /// TÆ°Æ¡ng Ä‘Æ°Æ¡ng CertificateSoonExpired trong ssl.go
    /// </summary>
    public static bool CertificateSoonExpired(string? certPath)
    {
        if (string.IsNullOrEmpty(certPath))
            return true;

        try
        {
            if (!File.Exists(certPath))
                return true;

            var cert = new X509Certificate2(certPath);
            return DateTime.UtcNow.AddHours(24) > cert.NotAfter;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Kiá»ƒm tra certificate object cÃ³ sáº¯p háº¿t háº¡n khÃ´ng.
    /// </summary>
    public static bool CertificateSoonExpired(X509Certificate2? cert)
    {
        if (cert == null)
            return true;

        return DateTime.UtcNow.AddHours(24) > cert.NotAfter;
    }

    private static bool TryParseIP(string ipString, out IPAddress ip)
    {
        return IPAddress.TryParse(ipString, out ip);
    }
}


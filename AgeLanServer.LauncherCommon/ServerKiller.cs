using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AgeLanServer.LauncherCommon.ServerKill;

/// <summary>
/// Trình dừng server battle.
/// Gửi yêu cầu shutdown qua HTTPS, sau đó kill process nếu cần.
/// Chuyển đổi từ Go package: launcher-common/serverKill/serverKill_windows.go, serverKill_other.go
/// </summary>
public static class ServerKiller
{
    /// <summary>
    /// Dừng server battle đang chạy.
    /// Trên Windows: gửi POST request đến /shutdown trên tất cả local IPs,
    /// sau đó chờ process tự tắt hoặc force kill.
    /// Trên Unix: chỉ force kill process.
    /// </summary>
    /// <param name="serverPath">Đường dẫn đến executable của server</param>
    /// <returns>Task hoàn thành khi server đã dừng</returns>
    public static async Task DoAsync(string serverPath)
    {
        if (OperatingSystem.IsWindows())
        {
            await DoWindowsAsync(serverPath);
        }
        else
        {
            DoUnix(serverPath);
        }
    }

    /// <summary>
    /// Thực hiện dừng server trên Windows.
    /// Gửi HTTPS POST đến /shutdown, sau đó kill process.
    /// </summary>
    private static async Task DoWindowsAsync(string serverPath)
    {
        // Gửi yêu cầu shutdown qua HTTPS
        await SendShutdownRequestAsync();

        // Tìm và kill process
        KillProcess(serverPath);
    }

    /// <summary>
    /// Gửi yêu cầu shutdown đến tất cả local IPs.
    /// </summary>
    private static async Task SendShutdownRequestAsync()
    {
        // Lấy tất cả local IPs
        var localIps = GetLocalIpAddresses();

        if (localIps.Count == 0)
        {
            // Không thể resolve local IPs, bỏ qua
            return;
        }

        // Cấu hình HttpClient bỏ qua validate certificate
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback =
            (HttpRequestMessage msg, X509Certificate2 cert, X509Chain chain, SslPolicyErrors errors) => true;

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(1)
        };

        foreach (var ip in localIps)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://{ip}/shutdown");
                request.Headers.UserAgent.ParseAdd("ageLANServer");

                var response = await client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Shutdown thành công
                    return;
                }
            }
            catch
            {
                // Bỏ qua lỗi và thử IP tiếp theo
            }
        }
    }

    /// <summary>
    /// Lấy danh sách địa chỉ IP cục bộ.
    /// </summary>
    private static List<IPAddress> GetLocalIpAddresses()
    {
        var addresses = new List<IPAddress>();

        // Lấy địa chỉ IPv4 và IPv6 từ tất cả network interfaces
        foreach (var networkInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            foreach (var ipProps in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (ipProps.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                    ipProps.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    addresses.Add(ipProps.Address);
                }
            }
        }

        return addresses;
    }

    /// <summary>
    /// Kill process trên Windows.
    /// Chờ process tự tắt trong 2 giây, nếu không thì force kill.
    /// </summary>
    private static void KillProcess(string serverPath)
    {
        string processName = Path.GetFileNameWithoutExtension(serverPath);

        // Tìm tất cả processes có tên phù hợp
        var processes = System.Diagnostics.Process.GetProcessesByName(processName);

        foreach (var proc in processes)
        {
            try
            {
                // Chờ process tự tắt trong 2 giây
                if (proc.WaitForExit(2000))
                {
                    continue;
                }

                // Force kill
                proc.Kill();
                proc.WaitForExit();
            }
            catch
            {
                // Bỏ qua nếu không thể kill
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    /// <summary>
    /// Thực hiện dừng server trên Unix (Linux/macOS).
    /// Chỉ force kill process.
    /// </summary>
    private static void DoUnix(string serverPath)
    {
        KillProcessUnix(serverPath);
    }

    /// <summary>
    /// Kill process trên Unix bằng SIGKILL.
    /// </summary>
    private static void KillProcessUnix(string serverPath)
    {
        string processName = Path.GetFileNameWithoutExtension(serverPath);

        var processes = System.Diagnostics.Process.GetProcessesByName(processName);

        foreach (var proc in processes)
        {
            try
            {
                proc.Kill(); // SIGKILL trên Unix
                proc.WaitForExit();
            }
            catch
            {
                // Bỏ qua nếu không thể kill
            }
            finally
            {
                proc.Dispose();
            }
        }
    }
}

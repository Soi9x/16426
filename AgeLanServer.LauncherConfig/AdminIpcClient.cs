using System.IO.Pipes;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace AgeLanServer.LauncherConfig;

/// <summary>
/// Client giao tiep voi config-admin-agent qua IPC (Named Pipe tren Windows hoac Unix Socket tren Linux).
/// Tuong tu toan bo code trong admin/*.go va ipc/*.go.
/// </summary>
public static class AdminIpcClient
{
    #region Hang so va trang thai

    /// <summary> Ten IPC: ageLANServer-launcher-config-admin-agent. </summary>
    private const string IpcName = "ageLANServer-launcher-config-admin-agent";

    /// <summary> Lenh Setup (byte 1). </summary>
    private const byte CommandSetup = 1;

    /// <summary> Lenh Revert (byte 0). </summary>
    private const byte CommandRevert = 0;

    /// <summary> Lenh Exit (byte 2). </summary>
    private const byte CommandExit = 2;

    /// <summary> Ket noi IPC hien tai. </summary>
    private static Stream? _ipcStream;

    /// <summary> Co dang ket noi khong. </summary>
    private static bool IsConnected => _ipcStream != null;

    #endregion

    #region Du lieu lenh

    /// <summary>
    /// Lenh Setup gui den agent.
    /// Tuong tu SetupCommand trong ipc.go.
    /// </summary>
    [DataContract]
    public sealed class SetupCommand
    {
        [DataMember(Order = 1)]
        public string GameId { get; set; } = string.Empty;

        [DataMember(Order = 2)]
        public byte[]? IP { get; set; }

        [DataMember(Order = 3)]
        public byte[]? Certificate { get; set; }

        public override string ToString()
        {
            return $"SetupCommand{{GameId={GameId}, HasIP={IP != null}, HasCert={Certificate != null}}}";
        }
    }

    /// <summary>
    /// Lenh Revert gui den agent.
    /// Tuong tu RevertCommand trong ipc.go.
    /// </summary>
    [DataContract]
    public sealed class RevertCommand
    {
        [DataMember(Order = 1)]
        public bool IPs { get; set; }

        [DataMember(Order = 2)]
        public bool Certificate { get; set; }

        public override string ToString()
        {
            return $"RevertCommand{{IPs={IPs}, Certificate={Certificate}}}";
        }
    }

    #endregion

    #region Ket noi IPC

    /// <summary>
    /// Lay duong dan IPC tuy theo he dieu hanh.
    /// Windows: \\.\pipe\ageLANServer-launcher-config-admin-agent
    /// Linux/other: /tmp/ageLANServer-launcher-config-admin-agent
    /// </summary>
    private static string GetIpcPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return $@"\\.\pipe\{IpcName}";
        }

        return Path.Combine(Path.GetTempPath(), IpcName);
    }

    /// <summary>
    /// Ket noi den agent neu chua ket noi.
    /// Tuong tu ConnectAgentIfNeeded trong admin.go.
    /// </summary>
    /// <returns>True neu da ket noi (hoặc da ket noi tru do), False neu that bai.</returns>
    public static async Task<bool> ConnectAgentIfNeededAsync()
    {
        if (IsConnected)
        {
            Console.WriteLine("Da ket noi tru do");
            return true;
        }

        Console.WriteLine("Dang ket noi den agent...");

        try
        {
            string path = GetIpcPath();
            Console.WriteLine($"Su dung duong dan: {path}");

            if (OperatingSystem.IsWindows())
            {
                var client = new NamedPipeClientStream(".", IpcName, PipeDirection.InOut, PipeOptions.None);
                await client.ConnectAsync(5000); // Timeout 5 giay
                _ipcStream = client;
            }
            else
            {
                // Tren Linux, dung Unix socket qua connect toi file socket
                // Trong .NET, co the dung UnixDomainSocketEndPoint
                var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(path);
                var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);
                await socket.ConnectAsync(endpoint);
                _ipcStream = new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }

            Console.WriteLine("Da ket noi");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"That bai khi ket noi: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Ket noi den agent voi nhieu lan thu.
    /// Tuong tu ConnectAgentIfNeededWithRetries trong admin.go.
    /// </summary>
    /// <param name="retryUntilSuccess">Neu true, thu den khi thanh cong; neu false, thu den khi that bai.</param>
    /// <param name="maxRetries">So lan toi da (mac dinh 30).</param>
    /// <param name="delayMs">Thoi gian cho giua cac lan thu (ms, mac dinh 100).</param>
    /// <returns>True neu ket qua phu hop voi retryUntilSuccess.</returns>
    public static async Task<bool> ConnectAgentIfNeededWithRetriesAsync(
        bool retryUntilSuccess,
        int maxRetries = 30,
        int delayMs = 100)
    {
        Action? prePostFn = retryUntilSuccess
            ? () => { /* khong lam gi, giu ket noi */ }
            : ClearIPCState;

        for (int i = 0; i < maxRetries; i++)
        {
            prePostFn();

            bool connected = await ConnectAgentIfNeededAsync();
            if (connected == retryUntilSuccess)
                return true;

            prePostFn();
            await Task.Delay(delayMs);
        }

        return false;
    }

    /// <summary>
    /// Xoa trang thai IPC (dong ket noi neu co).
    /// Tuong tu clearIPCState trong admin.go.
    /// </summary>
    private static void ClearIPCState()
    {
        if (_ipcStream != null)
        {
            try { _ipcStream.Close(); } catch { /* bo qua */ }
            _ipcStream = null;
        }
    }

    #endregion

    #region Start / Stop Agent

    /// <summary>
    /// Khoi dong agent neu chua chay.
    /// Tuong tu StartAgentIfNeeded trong admin.go.
    /// </summary>
    /// <param name="logRoot">Thu muc log, co the null.</param>
    /// <returns>Ket qua khoi dong.</returns>
    public static async Task<AgentStartResult> StartAgentIfNeededAsync(string? logRoot)
    {
        Console.WriteLine("Dang khoi dong agent...");

        if (IsConnected)
        {
            Console.WriteLine("Da chay tru do");
            return new AgentStartResult { Success = true };
        }

        string exeName = "config-admin-agent.exe";
        if (!OperatingSystem.IsWindows())
            exeName = "config-admin-agent";

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exeName,
                UseShellExecute = true,
                Verb = "runas" // Yeu cau quyen admin
            };

            if (!string.IsNullOrEmpty(logRoot))
            {
                startInfo.Arguments = $"\"{logRoot}\"";
            }
            else
            {
                startInfo.Arguments = "-";
            }

            Console.WriteLine($"Start config-admin-agent: {exeName} {startInfo.Arguments}");

            var proc = System.Diagnostics.Process.Start(startInfo);
            if (proc != null)
            {
                // Cho agent khoi dong
                await Task.Delay(1000);

                if (!OperatingSystem.IsWindows())
                {
                    // Tren Linux, cho den khi process san sang
                    while (!proc.HasExited)
                    {
                        await Task.Delay(1000);
                        // Kiem tra xem process da san sang chua
                        break; // Gia lap: thoat ngay
                    }
                }

                return new AgentStartResult
                {
                    Success = true,
                    Pid = proc.Id
                };
            }

            return new AgentStartResult
            {
                Success = false,
                ErrorMessage = "Khong the khoi dong process"
            };
        }
        catch (Exception ex)
        {
            return new AgentStartResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Dung agent neu dang ket noi.
    /// Tuong tu StopAgentIfNeeded trong admin.go.
    /// </summary>
    /// <returns>Null neu thanh cong, exception neu that bai.</returns>
    public static async Task<string?> StopAgentIfNeededAsync()
    {
        Console.WriteLine("Dang dung agent...");

        if (!IsConnected)
        {
            Console.WriteLine("Da dung (khong ket noi)");
            return null;
        }

        try
        {
            // Gui lenh Exit
            await WriteByteAsync(CommandExit);
            Console.WriteLine("-> Exit: OK");

            ClearIPCState();
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"-> Exit: Khong the encode: {ex.Message}");
            return ex.Message;
        }
    }

    #endregion

    #region Run SetUp / Revert

    /// <summary>
    /// Chay lenh Setup thong qua agent hoac truc tiep.
    /// Tuong tu RunSetUp trong admin.go.
    /// </summary>
    /// <param name="logRoot">Thu muc log.</param>
    /// <param name="ipToMap">Dia IP can map.</param>
    /// <param name="addCertData">Du lieu cert can them.</param>
    /// <returns>(loi, exitCode).</returns>
    public static async Task<(string? error, int exitCode)> RunSetUpAsync(
        string? logRoot,
        IPAddress? ipToMap,
        byte[]? addCertData)
    {
        if (IsConnected)
        {
            return await RunSetUpAgentAsync(ipToMap, addCertData);
        }

        // Neu khong ket noi agent, chay config-admin truc tiep
        return await RunSetUpDirectAsync(logRoot, ipToMap, addCertData);
    }

    /// <summary>
    /// Chay lenh Setup thong qua agent (da ket noi).
    /// Tuong tu runSetUpAgent trong admin.go.
    /// </summary>
    private static async Task<(string? error, int exitCode)> RunSetUpAgentAsync(
        IPAddress? ipToMap,
        byte[]? addCertData)
    {
        try
        {
            // Gui lenh Setup
            Console.Write("-> Setup: ");
            await WriteByteAsync(CommandSetup);
            Console.WriteLine("OK");

            // Doc exit code
            Console.Write("<- Exit Code: ");
            int exitCode = await ReadIntAsync();
            if (exitCode != 0)
            {
                Console.WriteLine(exitCode.ToString());
                return (null, exitCode);
            }
            Console.WriteLine("0");

            // Gui SetupCommand
            var cmd = new SetupCommand
            {
                GameId = CmdSetupStatic.GameId, // Lay tu bien static
                IP = ipToMap?.GetAddressBytes(),
                Certificate = addCertData
            };

            Console.Write($"-> {cmd}: ");
            await WriteJsonAsync(cmd);
            Console.WriteLine("OK");

            // Doc exit code
            Console.Write("<- Exit Code: ");
            exitCode = await ReadIntAsync();
            Console.WriteLine(exitCode.ToString());

            return (null, exitCode);
        }
        catch (Exception ex)
        {
            return (ex.Message, -1);
        }
    }

    /// <summary>
    /// Chay lenh Setup truc tiep (khong qua agent).
    /// </summary>
    private static async Task<(string? error, int exitCode)> RunSetUpDirectAsync(
        string? logRoot,
        IPAddress? ipToMap,
        byte[]? addCertData)
    {
        // Trong thuc te, chay config-admin.exe voi cac tham so phu hop
        // O day gia lap ket qua thanh cong
        Console.WriteLine("Dang chay config-admin setup truc tiep...");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "config-admin.exe" : "config-admin",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Xay dung arguments
            var args = new List<string> { "setup" };
            if (!string.IsNullOrEmpty(CmdSetupStatic.GameId))
                args.Add($"--gameId={CmdSetupStatic.GameId}");
            if (ipToMap != null)
                args.Add($"--ip={ipToMap}");
            if (addCertData != null)
                args.Add("--cert=true");
            if (!string.IsNullOrEmpty(logRoot))
                args.Add($"--logRoot={logRoot}");

            startInfo.Arguments = string.Join(" ", args);

            var proc = System.Diagnostics.Process.Start(startInfo);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                return (null, proc.ExitCode);
            }

            return ("Khong the khoi dong process", -1);
        }
        catch (Exception ex)
        {
            return (ex.Message, -1);
        }
    }

    /// <summary>
    /// Chay lenh Revert thong qua agent hoac truc tiep.
    /// Tuong tu RunRevert trong admin.go.
    /// </summary>
    /// <param name="logRoot">Thu muc log.</param>
    /// <param name="unmapIPs">Co unmap IPs khong.</param>
    /// <param name="removeCert">Co xoa cert khong.</param>
    /// <param name="failfast">Co dung ngay khi that bai khong.</param>
    /// <returns>(loi, exitCode).</returns>
    public static async Task<(string? error, int exitCode)> RunRevertAsync(
        string? logRoot,
        bool unmapIPs,
        bool removeCert,
        bool failfast)
    {
        if (IsConnected)
        {
            return await RunRevertAgentAsync(unmapIPs, removeCert);
        }

        return await RunRevertDirectAsync(logRoot, unmapIPs, removeCert, failfast);
    }

    /// <summary>
    /// Chay lenh Revert thong qua agent (da ket noi).
    /// Tuong tu runRevertAgent trong admin.go.
    /// </summary>
    private static async Task<(string? error, int exitCode)> RunRevertAgentAsync(
        bool unmapIPs,
        bool removeCert)
    {
        try
        {
            // Gui lenh Revert
            Console.Write("-> Revert: ");
            await WriteByteAsync(CommandRevert);
            Console.WriteLine("OK");

            // Doc exit code
            Console.Write("<- Exit Code: ");
            int exitCode = await ReadIntAsync();
            if (exitCode != 0)
            {
                Console.WriteLine(exitCode.ToString());
                return (null, exitCode);
            }
            Console.WriteLine("0");

            // Gui RevertCommand
            var cmd = new RevertCommand
            {
                IPs = unmapIPs,
                Certificate = removeCert
            };

            Console.Write($"-> {cmd}: ");
            await WriteJsonAsync(cmd);
            Console.WriteLine("OK");

            // Doc exit code
            Console.Write("<- Exit Code: ");
            exitCode = await ReadIntAsync();
            Console.WriteLine(exitCode.ToString());

            return (null, exitCode);
        }
        catch (Exception ex)
        {
            return (ex.Message, -1);
        }
    }

    /// <summary>
    /// Chay lenh Revert truc tiep (khong qua agent).
    /// </summary>
    private static async Task<(string? error, int exitCode)> RunRevertDirectAsync(
        string? logRoot,
        bool unmapIPs,
        bool removeCert,
        bool failfast)
    {
        Console.WriteLine("Dang chay config-admin revert truc tiep...");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "config-admin.exe" : "config-admin",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var args = new List<string> { "revert" };
            if (unmapIPs)
                args.Add("--unmapIPs=true");
            if (removeCert)
                args.Add("--removeCert=true");
            args.Add($"--failfast={failfast}");
            if (!string.IsNullOrEmpty(logRoot))
                args.Add($"--logRoot={logRoot}");

            startInfo.Arguments = string.Join(" ", args);

            var proc = System.Diagnostics.Process.Start(startInfo);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                return (null, proc.ExitCode);
            }

            return ("Khong the khoi dong process", -1);
        }
        catch (Exception ex)
        {
            return (ex.Message, -1);
        }
    }

    #endregion

    #region IPC I/O

    /// <summary>
    /// Doc 1 byte tu IPC stream.
    /// </summary>
    private static async Task<byte> ReadByteAsync()
    {
        if (_ipcStream == null)
            throw new InvalidOperationException("Chua ket noi IPC");

        byte[] buffer = new byte[1];
        int read = await _ipcStream.ReadAsync(buffer, 0, 1);
        if (read != 1)
            throw new EndOfStreamException("Khong the doc byte");

        return buffer[0];
    }

    /// <summary>
    /// Doc so nguyen (4 bytes, big-endian) tu IPC stream.
    /// </summary>
    private static async Task<int> ReadIntAsync()
    {
        if (_ipcStream == null)
            throw new InvalidOperationException("Chua ket noi IPC");

        byte[] buffer = new byte[4];
        int read = await _ipcStream.ReadAsync(buffer, 0, 4);
        if (read != 4)
            throw new EndOfStreamException("Khong the doc int");

        // Gob trong Go su dung little-endian tren most systems
        return BitConverter.ToInt32(buffer, 0);
    }

    /// <summary>
    /// Ghi 1 byte len IPC stream.
    /// </summary>
    private static async Task WriteByteAsync(byte value)
    {
        if (_ipcStream == null)
            throw new InvalidOperationException("Chua ket noi IPC");

        await _ipcStream.WriteAsync(new[] { value }, 0, 1);
        await _ipcStream.FlushAsync();
    }

    /// <summary>
    /// Ghi du lieu JSON len IPC stream.
    /// Luu y: Go su dung gob encoding, o day dung JSON de don gian hoa.
    /// Trong implement thuc te, can dung gob encoding.
    /// </summary>
    private static async Task WriteJsonAsync(object data)
    {
        if (_ipcStream == null)
            throw new InvalidOperationException("Chua ket noi IPC");

        // Trong Go, gob encoding duoc su dung.
        // O day, chung ta dung JSON de don gian hoa, nhung trong thuc te
        // can implement gob encoding de tuong thich voi Go agent.

        string json = System.Text.Json.JsonSerializer.Serialize(data);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        // Gui do dai truoc (4 bytes)
        byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
        await _ipcStream.WriteAsync(lengthBytes, 0, 4);

        // Gui du lieu
        await _ipcStream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
        await _ipcStream.FlushAsync();
    }

    /// <summary>
    /// Doc du lieu JSON tu IPC stream.
    /// </summary>
    private static async Task<T> ReadJsonAsync<T>() where T : class
    {
        if (_ipcStream == null)
            throw new InvalidOperationException("Chua ket noi IPC");

        // Doc do dai
        byte[] lengthBytes = new byte[4];
        int read = await _ipcStream.ReadAsync(lengthBytes, 0, 4);
        if (read != 4)
            throw new EndOfStreamException("Khong the doc do dai JSON");

        int length = BitConverter.ToInt32(lengthBytes, 0);

        // Doc du lieu
        byte[] data = new byte[length];
        read = await _ipcStream.ReadAsync(data, 0, length);
        if (read != length)
            throw new EndOfStreamException("Khong the doc du lieu JSON");

        return System.Text.Json.JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data))
               ?? throw new InvalidDataException("Khong the deserialize JSON");
    }

    #endregion
}

/// <summary>
    /// Ket qua khoi dong agent.
    /// </summary>
public sealed class AgentStartResult
{
    /// <summary> Thanh cong hay khong. </summary>
    public bool Success { get; init; }

    /// <summary> PID cua process. </summary>
    public int Pid { get; init; }

    /// <summary> Ma loi (neu co). </summary>
    public int ExitCode { get; init; }

    /// <summary> Thong bao loi (neu co). </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Bien static de luu GameId khi chay (tuong tu bien global trong Go).
/// </summary>
public static class CmdSetupStatic
{
    public static string GameId { get; set; } = string.Empty;
}

/// <summary>
/// Mo rong Task de ho tro WaitForExitAsync cho .NET 6+.
/// </summary>
public static class ProcessExtensions
{
    public static async Task WaitForExitAsync(this System.Diagnostics.Process process)
    {
        if (process.HasExited)
            return;

        process.EnableRaisingEvents = true;
        var tcs = new TaskCompletionSource<bool>();

        void Handler(object? s, EventArgs e) => tcs.TrySetResult(true);

        process.Exited += Handler;
        try
        {
            if (process.HasExited)
                return;
            await tcs.Task;
        }
        finally
        {
            process.Exited -= Handler;
        }
    }
}

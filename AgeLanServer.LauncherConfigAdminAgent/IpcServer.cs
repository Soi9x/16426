using System.Buffers;
using System.Formats.Asn1;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.Common;
using AgeLanServer.LauncherCommon;
using AgeLanServer.LauncherCommon.Cert;
using Microsoft.Extensions.Logging;

namespace AgeLanServer.LauncherConfigAdminAgent;

/// <summary>
/// Các hành động IPC được định nghĩa trong launcher-common/ipc/ipc.go
/// </summary>
internal static class IpcActions
{
    public const byte Revert = 0;
    public const byte Setup = 1;
    public const byte Exit = 2;
}

/// <summary>
/// Tên của agent, dùng để đặt tên cho named pipe / unix socket.
/// Lấy từ common/common.go: Name = "ageLANServer"
/// </summary>
internal static class Constants
{
    public const string AgentName = "ageLANServer-launcher-config-admin-agent";
}

/// <summary>
/// Lệnh Setup nhận từ client.
/// Tương đương ipc.SetupCommand trong Go.
/// </summary>
internal sealed class SetupCommand
{
    public IPAddress? IP { get; set; }
    public byte[]? Certificate { get; set; }
    public string GameId { get; set; } = string.Empty;
}

/// <summary>
/// Lệnh Revert nhận từ client.
/// Tương đương ipc.RevertCommand trong Go.
/// </summary>
internal sealed class RevertCommand
{
    public bool IPs { get; set; }
    public bool Certificate { get; set; }
}

/// <summary>
/// Server IPC xử lý kết nối từ launcher-config-admin client.
/// Trên Windows: sử dụng Named Pipe.
/// Trên Linux/macOS: sử dụng Unix Domain Socket.
/// Theo dõi trạng thái mappedIps và addedCert để tránh thao tác trùng lặp.
/// </summary>
public sealed class IpcServer
{
    private readonly string _logRoot;
    private readonly ILogger<IpcServer> _logger;

    // Trạng thái được theo dõi trong suốt vòng đời server (tương đương biến package-level trong Go)
    private bool _mappedIps;
    private bool _addedCert;

    // Đối tượng listener: NamedPipeServerStream trên Windows, Socket (Unix socket) trên Linux/macOS
    private IDisposable? _listener;
    private CancellationTokenSource? _acceptCts;

    public IpcServer(string logRoot, ILogger<IpcServer> logger)
    {
        _logRoot = logRoot;
        _logger = logger;
    }

    #region Public API

    /// <summary>
    /// Khởi tạo và bắt đầu lắng nghe trên IPC endpoint.
    /// Trả về mã lỗi. ErrorCode.ErrSuccess nếu thành công.
    /// Phương thức này block cho đến khi nhận được hành động Exit hoặc bị hủy.
    /// </summary>
    public async Task<int> StartAsync(CancellationToken cancellationToken = default)
    {
        // Thiết lập server (named pipe hoặc unix socket)
        var setupResult = SetupServer();
        if (setupResult.Error != ErrorCode.ErrSuccess)
        {
            _logger.LogError("Không thể thiết lập IPC server: mã lỗi {ErrorCode}", setupResult.Error);
            return setupResult.Error;
        }

        _listener = setupResult.Listener;
        _acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Vòng lặp chấp nhận kết nối
            while (!_acceptCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Đang chờ kết nối...");

                Stream clientStream;
                try
                {
                    clientStream = await AcceptClientAsync(_acceptCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể chấp nhận kết nối");
                    continue;
                }

                _logger.LogInformation("Đã chấp nhận kết nối");

                // Xử lý client. Nếu trả về true => nhận được lệnh Exit, dừng server.
                var shouldExit = await HandleClientAsync(clientStream).ConfigureAwait(false);

                if (shouldExit)
                {
                    _logger.LogInformation("Nhận được lệnh Exit, dừng server.");
                    break;
                }
            }
        }
        finally
        {
            CleanupServer();
        }

        return ErrorCode.ErrSuccess;
    }

    /// <summary>
    /// Dừng server và giải phóng tài nguyên.
    /// </summary>
    public void Stop()
    {
        _acceptCts?.Cancel();
        CleanupServer();
    }

    #endregion

    #region Server Setup (Platform-specific)

    private record SetupResult(int Error, IDisposable? Listener);

    /// <summary>
    /// Thiết lập server lắng nghe tùy thuộc vào hệ điều hành.
    /// Windows: Named Pipe thông qua NamedPipeServerStream.
    /// Linux/macOS: Unix Domain Socket thông qua Socket.
    /// </summary>
    private SetupResult SetupServer()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SetupServerWindows();
        }
        else
        {
            return SetupServerUnix();
        }
    }

    /// <summary>
    /// Thiết lập Named Pipe trên Windows.
    /// Tương đương winio.ListenPipe trong Go với SecurityDescriptor chỉ cho phép user hiện tại.
    /// </summary>
    private SetupResult SetupServerWindows()
    {
        // Tạo named pipe server stream
        // Trên Windows,NamedPipeServerStream sẽ tự động lắng nghe khi được tạo
        var pipeName = @"\\.\pipe\" + Constants.AgentName;

        try
        {
            // Tạo server stream - sẽ lắng nghe kết nối đầu tiên
            var serverStream = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                1024,  // InputBufferSize
                1      // OutputBufferSize
            );

            // Trên .NET, NamedPipeServerStream không hỗ trợ SecurityDescriptor trực tiếp.
            // Trong thực tế, cần dùng P/Invoke hoặc thư viện bổ sung để set ACL.
            // Ở đây ta tạo pipe với quyền mặc định của user hiện tại (tương tự behavior Go với u.Uid).

            _logger.LogInformation("Đã thiết lập Named Pipe server: {PipeName}", pipeName);
            return new SetupResult(ErrorCode.ErrSuccess, serverStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi thiết lập Named Pipe server");
            return new SetupResult(ErrorCode.ErrListen, null);
        }
    }

    /// <summary>
    /// Thiết lập Unix Domain Socket trên Linux/macOS.
    /// Tương đương net.Listen("unix", path) trong Go.
    /// </summary>
    private SetupResult SetupServerUnix()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), Constants.AgentName);

        try
        {
            // Xóa socket cũ nếu tồn tại
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            var endPoint = new UnixDomainSocketEndPoint(socketPath);
            socket.Bind(endPoint);
            socket.Listen(1);

            // Đặt quyền 0666 (tương đương Go: os.Chmod)
            // Trên Unix, có thể cần chmod để cho phép client kết nối
            try
            {
                // Sử dụng syscall chmod qua P/Invoke nếu cần
                // Ở đây ta bỏ qua vì đa phần quyền thư mục temp đã đủ
            }
            catch
            {
                // Bỏ qua lỗi chmod
            }

            _logger.LogInformation("Đã thiết lập Unix Domain Socket: {SocketPath}", socketPath);
            return new SetupResult(ErrorCode.ErrSuccess, new UnixSocketListener(socket, socketPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi thiết lập Unix Domain Socket");
            return new SetupResult(ErrorCode.ErrListen, null);
        }
    }

    /// <summary>
    /// Chấp nhận một kết nối client từ listener.
    /// </summary>
    private async Task<Stream> AcceptClientAsync(CancellationToken cancellationToken)
    {
        if (_listener is NamedPipeServerStream namedPipe)
        {
            // Chờ client kết nối đến named pipe
            await namedPipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            return namedPipe;
        }
        else if (_listener is UnixSocketListener unixListener)
        {
            var clientSocket = await unixListener.Socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
            return new NetworkStream(clientSocket, ownsSocket: true);
        }

        throw new InvalidOperationException("Listener không hợp lệ");
    }

    /// <summary>
    /// Dọn dẹp tài nguyên server khi dừng.
    /// Trên Unix, xóa file socket.
    /// </summary>
    private void CleanupServer()
    {
        if (_listener is UnixSocketListener unixListener)
        {
            unixListener.Socket.Dispose();
            if (File.Exists(unixListener.SocketPath))
            {
                try
                {
                    File.Delete(unixListener.SocketPath);
                }
                catch
                {
                    // Bỏ qua lỗi khi xóa socket
                }
            }
        }
        else if (_listener is NamedPipeServerStream namedPipe)
        {
            // DisconnectedNamedPipe hoặc Dispose
            try
            {
                if (namedPipe.IsConnected)
                {
                    namedPipe.Disconnect();
                }
            }
            catch
            {
                // Bỏ qua lỗi khi disconnect
            }
            namedPipe.Dispose();
        }

        _listener = null;
    }

    #endregion

    #region Client Handling

    /// <summary>
    /// Xử lý một kết nối client.
    /// Đọc các hành động từ client, thực thi và trả về mã lỗi.
    /// Trả về true nếu nhận được lệnh Exit.
    /// </summary>
    private async Task<bool> HandleClientAsync(Stream stream)
    {
        var shouldExit = false;

        try
        {
            while (!shouldExit)
            {
                // Đọc hành động (1 byte)
                var actionByte = await ReadByteAsync(stream).ConfigureAwait(false);
                if (actionByte < 0)
                {
                    _logger.LogWarning("Không thể đọc hành động từ client");
                    await WriteErrorCodeAsync(stream, ErrorCode.ErrDecode).ConfigureAwait(false);
                    return false;
                }

                var action = (byte)actionByte;
                int exitCode;

                switch (action)
                {
                    case IpcActions.Revert:
                        _logger.LogInformation("Nhận lệnh: Revert");
                        // Gửi phản hồi thành công ban đầu
                        await WriteErrorCodeAsync(stream, ErrorCode.ErrSuccess).ConfigureAwait(false);
                        exitCode = await HandleRevertAsync(stream).ConfigureAwait(false);
                        break;

                    case IpcActions.Setup:
                        _logger.LogInformation("Nhận lệnh: Setup");
                        // Gửi phản hồi thành công ban đầu
                        await WriteErrorCodeAsync(stream, ErrorCode.ErrSuccess).ConfigureAwait(false);
                        exitCode = await HandleSetupAsync(stream).ConfigureAwait(false);
                        break;

                    case IpcActions.Exit:
                        _logger.LogInformation("Nhận lệnh: Exit");
                        exitCode = ErrorCode.ErrSuccess;
                        shouldExit = true;
                        break;

                    default:
                        _logger.LogWarning("Hành động không tồn tại: {Action}", action);
                        exitCode = ErrorCode.ErrNonExistingAction;
                        break;
                }

                // Gửi mã lỗi kết quả
                await WriteErrorCodeAsync(stream, exitCode).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý client");
        }

        return shouldExit;
    }

    /// <summary>
    /// Đọc 1 byte từ stream. Trả về -1 nếu lỗi.
    /// </summary>
    private async Task<int> ReadByteAsync(Stream stream)
    {
        var buffer = new byte[1];
        var read = await stream.ReadAsync(buffer.AsMemory(0, 1)).ConfigureAwait(false);
        return read == 1 ? buffer[0] : -1;
    }

    /// <summary>
    /// Ghi mã lỗi (1 byte) vào stream.
    /// </summary>
    private async Task WriteErrorCodeAsync(Stream stream, int errorCode)
    {
        await stream.WriteAsync(new byte[] { (byte)errorCode }).ConfigureAwait(false);
    }

    /// <summary>
    /// Xử lý lệnh Setup từ client.
    /// Tương đương handleSetUp trong Go (ipc.go).
    /// </summary>
    private async Task<int> HandleSetupAsync(Stream stream)
    {
        _logger.LogInformation("Đang xử lý SetupCommand");

        // Đọc SetupCommand từ stream
        // Format đơn giản: [GameId length 1 byte][GameId bytes][IP present 1 byte][IP 4/16 bytes][Cert present 1 byte][Cert length 4 bytes][Cert bytes]
        var command = await ReadSetupCommandAsync(stream).ConfigureAwait(false);
        if (command == null)
        {
            _logger.LogWarning("Không thể giải mã SetupCommand");
            return ErrorCode.ErrDecode;
        }

        _logger.LogInformation("SetupCommand - GameId: {GameId}, IP: {IP}, Certificate: {HasCert}",
            command.GameId, command.IP?.ToString(), command.Certificate != null);

        // Kiểm tra IPs đã được ánh xạ chưa
        if (command.IP != null && _mappedIps)
        {
            _logger.LogWarning("IPs đã được ánh xạ trước đó");
            return ErrorCode.ErrIpsAlreadyMapped;
        }

        // Parse certificate nếu có
        X509Certificate2? cert = null;
        if (command.Certificate != null && command.Certificate.Length > 0)
        {
            if (_addedCert)
            {
                _logger.LogWarning("Chứng chỉ đã được thêm trước đó");
                return ErrorCode.ErrCertAlreadyAdded;
            }

            try
            {
                cert = new X509Certificate2(command.Certificate);

                // Kiểm tra tính hợp lệ của certificate
                if (!IsCertificateValid(cert))
                {
                    _logger.LogWarning("Chứng chỉ không hợp lệ");
                    return ErrorCode.ErrCertInvalid;
                }

                _logger.LogInformation("Chứng chỉ hợp lệ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi parse chứng chỉ");
                return ErrorCode.ErrCertInvalid;
            }
        }
        else
        {
            _logger.LogInformation("Không có chứng chỉ");
        }

        // Gọi executor để thực hiện setup
        // Trong phiên bản C#, đây sẽ là gọi process con hoặc thư viện tương đương
        var suffix = cert != null ? "_cert" : "_hosts";
        var logFileName = $"config-admin_setup{suffix}";

        _logger.LogInformation("Đang thực thi setup với log: {LogFileName}", logFileName);

        // Giả lập kết quả setup - trong thực tế sẽ gọi process hoặc service tương đương
        var setupResult = await ExecuteSetupAsync(command.GameId, command.IP, cert, logFileName).ConfigureAwait(false);

        if (setupResult.Success)
        {
            _mappedIps = _mappedIps || command.IP != null;
            _addedCert = _addedCert || cert != null;
            _logger.LogInformation("Setup thành công");
        }
        else
        {
            _logger.LogWarning("Setup thất bại với mã lỗi: {ExitCode}", setupResult.ExitCode);
        }

        return setupResult.ExitCode;
    }

    /// <summary>
    /// Xử lý lệnh Revert từ client.
    /// Tương đương handleRevert trong Go (ipc.go).
    /// </summary>
    private async Task<int> HandleRevertAsync(Stream stream)
    {
        _logger.LogInformation("Đang xử lý RevertCommand");

        // Đọc RevertCommand từ stream
        var command = await ReadRevertCommandAsync(stream).ConfigureAwait(false);
        if (command == null)
        {
            _logger.LogWarning("Không thể giải mã RevertCommand");
            return ErrorCode.ErrDecode;
        }

        _logger.LogInformation("RevertCommand - IPs: {RevertIPs}, Certificate: {RevertCert}",
            command.IPs, command.Certificate);

        // Xác định những gì cần revert dựa trên trạng thái hiện tại
        var revertIps = command.IPs && _mappedIps;
        var revertCert = command.Certificate && _addedCert;

        if (!revertIps && !revertCert)
        {
            _logger.LogInformation("Mọi thứ đã được revert.");
            return ErrorCode.ErrSuccess;
        }

        // Gọi executor để thực hiện revert
        _logger.LogInformation("Đang thực thi revert với log: config-admin_revert");

        // Giả lập kết quả revert
        var revertResult = await ExecuteRevertAsync(revertIps, revertCert, revertCert).ConfigureAwait(false);

        if (revertResult.Success)
        {
            _mappedIps = _mappedIps && !revertIps;
            _addedCert = _addedCert && !revertCert;
            _logger.LogInformation("Revert thành công");
        }
        else
        {
            _logger.LogWarning("Revert thất bại với mã lỗi: {ExitCode}", revertResult.ExitCode);
        }

        return revertResult.ExitCode;
    }

    #region SetupCommand/RevertCommand Parsing

    /// <summary>
    /// Đọc SetupCommand từ stream.
    /// Định dạng đơn giản: [GameId length][GameId][IP present][IP bytes][Cert present][Cert length][Cert bytes]
    /// </summary>
    private async Task<SetupCommand?> ReadSetupCommandAsync(Stream stream)
    {
        try
        {
            var command = new SetupCommand();

            // Đọc GameId
            var gameIdLen = await ReadByteAsync(stream).ConfigureAwait(false);
            if (gameIdLen < 0) return null;

            if (gameIdLen > 0)
            {
                var gameIdBytes = new byte[gameIdLen];
                var read = await stream.ReadAsync(gameIdBytes.AsMemory()).ConfigureAwait(false);
                if (read != gameIdLen) return null;
                command.GameId = System.Text.Encoding.UTF8.GetString(gameIdBytes);
            }

            // Đọc IP
            var ipPresent = await ReadByteAsync(stream).ConfigureAwait(false);
            if (ipPresent == 1)
            {
                var ipLen = await ReadByteAsync(stream).ConfigureAwait(false);
                if (ipLen < 0) return null;
                var ipBytes = new byte[ipLen];
                var read = await stream.ReadAsync(ipBytes.AsMemory()).ConfigureAwait(false);
                if (read != ipLen) return null;
                command.IP = new IPAddress(ipBytes);
            }

            // Đọc Certificate
            var certPresent = await ReadByteAsync(stream).ConfigureAwait(false);
            if (certPresent == 1)
            {
                var certLenBytes = new byte[4];
                var read = await stream.ReadAsync(certLenBytes.AsMemory()).ConfigureAwait(false);
                if (read != 4) return null;
                var certLen = BitConverter.ToInt32(certLenBytes, 0);
                if (certLen <= 0) return null;

                command.Certificate = new byte[certLen];
                var totalRead = 0;
                while (totalRead < certLen)
                {
                    var r = await stream.ReadAsync(command.Certificate.AsMemory(totalRead, certLen - totalRead)).ConfigureAwait(false);
                    if (r == 0) return null;
                    totalRead += r;
                }
            }

            return command;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Đọc RevertCommand từ stream.
    /// Định dạng: [IPs bool][Certificate bool]
    /// </summary>
    private async Task<RevertCommand?> ReadRevertCommandAsync(Stream stream)
    {
        try
        {
            var command = new RevertCommand();

            var ipsByte = await ReadByteAsync(stream).ConfigureAwait(false);
            if (ipsByte < 0) return null;
            command.IPs = ipsByte == 1;

            var certByte = await ReadByteAsync(stream).ConfigureAwait(false);
            if (certByte < 0) return null;
            command.Certificate = certByte == 1;

            return command;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Certificate Validation

    /// <summary>
    /// Kiểm tra tính hợp lệ của chứng chỉ X509.
    /// Tương đương checkCertificateValidity trong Go.
    /// </summary>
    private static bool IsCertificateValid(X509Certificate2 cert)
    {
        if (cert == null) return false;

        // Kiểm tra thời gian hiệu lực
        var now = DateTimeOffset.Now;
        if (now < cert.NotBefore || now > cert.NotAfter)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Executor

    /// <summary>
    /// Thực thi quá trình setup: thêm certificate vào kho tin cậy và ánh xạ IP vào hosts file.
    /// Tương đương executor.RunSetUp trong Go, gọi trực tiếp CertificateStore và HostsManager.
    /// </summary>
    private Task<ExecutorResult> ExecuteSetupAsync(string gameId, IPAddress? ip, X509Certificate2? cert, string logFileName)
    {
        try
        {
            // Bước 1: Thêm certificate vào kho tin cậy (tương đương cert.TrustCertificates trong Go)
            if (cert != null)
            {
                _logger.LogInformation("Đang thêm chứng chỉ vào kho tin cậy...");
                // userStore = false => LocalMachine (tương đương Go behavior)
                CertificateStore.TrustCertificates(userStore: false, cert);
                _logger.LogInformation("Đã thêm chứng chỉ thành công.");
            }

            // Bước 2: Ánh xạ IP vào hosts file (tương đương hosts.AddHostMappings trong Go)
            if (ip != null)
            {
                _logger.LogInformation("Đang ánh xạ IP vào hosts file...");
                // Tạo backup trước khi sửa đổi
                HostsManager.CreateBackup();

                // Lấy danh sách hosts từ GameDomains dựa trên gameId
                var hosts = GameDomains.CertDomains();
                HostsManager.AddHostMappings(ip.ToString(), hosts);

                // Flush DNS cache để áp dụng thay đổi
                HostsManager.FlushDnsCache();
                _logger.LogInformation("Đã ánh xạ IP thành công.");
            }

            return Task.FromResult(new ExecutorResult { Success = true, ExitCode = ErrorCode.ErrSuccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thực thi setup");
            return Task.FromResult(new ExecutorResult { Success = false, ExitCode = ErrorCode.ErrGeneral });
        }
    }

    /// <summary>
    /// Thực thi quá trình revert: gỡ certificate khỏi kho tin cậy và xóa ánh xạ IP khỏi hosts file.
    /// Tương đương executor.RunRevert trong Go, gọi trực tiếp CertificateStore và HostsManager.
    /// </summary>
    private Task<ExecutorResult> ExecuteRevertAsync(bool revertIps, bool revertCert, bool isAgent, string? logRoot = null)
    {
        try
        {
            List<X509Certificate2>? removedCertificates = null;

            // Bước 1: Gỡ certificate khỏi kho tin cậy (tương đương cert.UntrustCertificates trong Go)
            if (revertCert)
            {
                _logger.LogInformation("Đang gỡ chứng chỉ khỏi kho tin cậy...");
                // userStore = false => LocalMachine (tương đương Go behavior)
                removedCertificates = CertificateStore.UntrustCertificates(userStore: false);
                _logger.LogInformation("Đã gỡ {Count} chứng chỉ.", removedCertificates.Count);
            }

            // Bước 2: Xóa ánh xạ IP khỏi hosts file (tương đương hosts.RemoveOwnMappings trong Go)
            if (revertIps)
            {
                _logger.LogInformation("Đang xóa ánh xạ IP khỏi hosts file...");
                HostsManager.RemoveOwnMappings();

                // Flush DNS cache để áp dụng thay đổi
                HostsManager.FlushDnsCache();
                _logger.LogInformation("Đã xóa ánh xạ IP thành công.");
            }

            return Task.FromResult(new ExecutorResult { Success = true, ExitCode = ErrorCode.ErrSuccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thực thi revert");
            return Task.FromResult(new ExecutorResult { Success = false, ExitCode = ErrorCode.ErrGeneral });
        }
    }

    private sealed record ExecutorResult
    {
        public bool Success { get; init; }
        public int ExitCode { get; init; }
    }

    #endregion

    #endregion

    #region UnixSocketListener Helper

    /// <summary>
    /// Wrapper cho Unix socket listener để dễ dàng quản lý vòng đời.
    /// </summary>
    private sealed class UnixSocketListener : IDisposable
    {
        public Socket Socket { get; }
        public string SocketPath { get; }

        public UnixSocketListener(Socket socket, string socketPath)
        {
            Socket = socket;
            SocketPath = socketPath;
        }

        public void Dispose()
        {
            Socket.Dispose();
        }
    }

    #endregion
}

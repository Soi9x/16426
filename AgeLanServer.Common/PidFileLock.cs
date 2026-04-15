using System.Runtime.InteropServices;

namespace AgeLanServer.Common;

/// <summary>
/// Khóa file dựa trên PID để đảm bảo chỉ một instance của ứng dụng chạy tại một thời điểm.
/// Tương đương common/fileLock/pidLock.go trong bản Go gốc.
/// </summary>
public sealed class PidFileLock : IDisposable
{
    private readonly string _exePath;
    private FileStream? _pidFileStream;
    private readonly object _lockObj = new();

    public PidFileLock(string? exePath = null)
    {
        _exePath = exePath ?? Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
    }

    /// <summary>
    /// Đường dẫn tới file PID (ưu tiên thư mục temp, fallback về thư mục exe).
    /// </summary>
    private string PidFilePath
    {
        get
        {
            var exeName = Path.GetFileName(_exePath);
            var pidFileName = $"{AppConstants.Name}-{exeName}.pid";
            var tempPath = Path.GetTempPath();
            return File.Exists(Path.Combine(tempPath, pidFileName))
                ? Path.Combine(tempPath, pidFileName)
                : Path.Combine(Path.GetDirectoryName(_exePath) ?? ".", pidFileName);
        }
    }

    /// <summary>
    /// Khóa file PID. Ném ngoại lệ nếu đã có instance khác đang giữ khóa.
    /// </summary>
    public bool TryAcquire(out string? existingPidPath)
    {
        lock (_lockObj)
        {
            existingPidPath = null;

            // Kiểm tra xem đã có process nào đang chạy không
            if (TryReadPidFile(out var existingPid, out var existingStartTime))
            {
                var proc = ProcessManager.FindProcessByPid(existingPid);
                if (proc != null && proc.StartTime.Ticks / TimeSpan.TicksPerMillisecond == existingStartTime)
                {
                    existingPidPath = PidFilePath;
                    proc.Dispose();
                    return false; // Đã có instance khác đang chạy
                }

                // Process cũ đã thoát, xóa file PID cũ
                proc?.Dispose();
                TryDeletePidFile();
            }

            // Tạo file PID mới
            try
            {
                _pidFileStream = new FileStream(PidFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                WritePidData(_pidFileStream);
                return true;
            }
            catch (IOException)
            {
                // Không thể khóa file - có instance khác đang chạy
                existingPidPath = PidFilePath;
                return false;
            }
        }
    }

    /// <summary>
    /// Ghi dữ liệu PID và thời gian bắt đầu vào file.
    /// </summary>
    private static void WritePidData(FileStream fs)
    {
        var pid = Environment.ProcessId;
        var startTime = ProcessManager.GetProcessStartTimeMs(pid);

        var data = new byte[AppConstants.PidFileSize];
        BitConverter.TryWriteBytes(data.AsSpan(0, 8), (ulong)pid);
        BitConverter.TryWriteBytes(data.AsSpan(8, 8), (ulong)startTime);

        fs.SetLength(data.Length);
        fs.Write(data, 0, data.Length);
        fs.Flush(true);
    }

    /// <summary>
    /// Đọc dữ liệu từ file PID hiện có.
    /// </summary>
    private bool TryReadPidFile(out int pid, out long startTime)
    {
        pid = 0;
        startTime = 0;

        try
        {
            if (!File.Exists(PidFilePath))
                return false;

            var data = File.ReadAllBytes(PidFilePath);
            if (data.Length != AppConstants.PidFileSize)
                return false;

            pid = (int)BitConverter.ToUInt64(data.AsSpan(0, 8));
            startTime = (long)BitConverter.ToUInt64(data.AsSpan(8, 8));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryDeletePidFile()
    {
        try
        {
            if (File.Exists(PidFilePath))
                File.Delete(PidFilePath);
        }
        catch
        {
            // Bỏ qua lỗi
        }
    }

    /// <summary>
    /// Mở khóa file PID và xóa file.
    /// </summary>
    public void Release()
    {
        lock (_lockObj)
        {
            _pidFileStream?.Close();
            _pidFileStream = null;
            TryDeletePidFile();
        }
    }

    public void Dispose() => Release();
}

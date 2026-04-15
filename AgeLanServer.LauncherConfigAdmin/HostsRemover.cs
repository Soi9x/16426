using System.Diagnostics;
using System.Text;

namespace AgeLanServer.LauncherConfigAdmin;

/// <summary>
/// Xử lý việc xóa ánh xạ hosts: khôi phục từ bản sao lưu .bak
/// hoặc xóa các dòng đánh dấu "own" trực tiếp trong tệp hosts.
/// Hỗ trợ Windows (%WINDIR%\System32\drivers\etc\hosts) và Linux (/etc/hosts).
/// Chuyển thể từ launcher-config-admin/internal/hosts/hosts.go
/// và hosts/hosts_windows.go / hosts/hosts_linux.go.
/// </summary>
public static class HostsRemover
{
    /// <summary>
    /// Đường dẫn tệp hosts hệ thống trên Windows.
    /// </summary>
    private const string HostsPathWindows = @"C:\Windows\System32\drivers\etc\hosts";

    /// <summary>
    /// Đường dẫn tệp hosts hệ thống trên Linux.
    /// </summary>
    private const string HostsPathLinux = "/etc/hosts";

    /// <summary>
    /// Hậu tố tệp sao lưu.
    /// </summary>
    private const string BakSuffix = ".bak";

    /// <summary>
    /// Tiền tố đánh dấu dòng do chương trình thêm vào.
    /// Dùng để nhận diện và xóa các dòng do chính chương trình tạo ra.
    /// </summary>
    private const string OwnMarker = "# ageLANServer";

    /// <summary>
    /// Lấy đường dẫn tệp hosts tùy theo hệ điều hành.
    /// </summary>
    private static string HostsFilePath =>
        OperatingSystem.IsWindows() ? HostsPathWindows : HostsPathLinux;

    /// <summary>
    /// Lấy đường dẫn tệp sao lưu (.bak) tùy theo hệ điều hành.
    /// </summary>
    private static string BackupFilePath => HostsFilePath + BakSuffix;

    /// <summary>
    /// Xóa ánh xạ hosts đã thêm trước đó.
    /// Chiến lược:
    /// 1. Nếu tồn tại tệp .bak và tệp chính cũ hơn (hoặc mới tạo), khôi phục từ .bak.
    /// 2. Nếu không, xóa trực tiếp các dòng có đánh dấu "own" trong tệp hosts.
    /// Trả về true nếu thành công, false nếu có lỗi.
    /// </summary>
    public static bool RemoveHosts()
    {
        try
        {
            string hostsPath = HostsFilePath;
            string bakPath = BackupFilePath;

            bool bakExists = File.Exists(bakPath);
            bool mainExists = File.Exists(hostsPath);
            bool createdMain = false;

            // Nếu tệp chính không tồn nhưng có .bak -> tạo tệp chính mới để khôi phục
            if (!mainExists && bakExists)
            {
                File.WriteAllText(hostsPath, string.Empty);
                createdMain = true;
            }
            else if (!mainExists)
            {
                // Không có tệp chính và không có .bak -> không có gì để xóa
                return true;
            }

            // Quyết định có khôi phục từ .bak hay không
            bool doRestoreBak = false;
            if (bakExists)
            {
                if (createdMain)
                {
                    // Tệp chính vừa mới tạo -> luôn khôi phục từ .bak
                    doRestoreBak = true;
                }
                else
                {
                    // So sánh thời gian sửa đổi:
                    // Nếu tệp chính cũ hơn (hoặc bằng) thời gian .bak + 1 giây -> khôi phục từ .bak
                    DateTime mainModTime = File.GetLastWriteTimeUtc(hostsPath);
                    DateTime bakModTime = File.GetLastWriteTimeUtc(bakPath);
                    doRestoreBak = mainModTime <= bakModTime.AddSeconds(1);
                }
            }

            if (doRestoreBak)
            {
                return RestoreFromBackup(bakPath, hostsPath);
            }

            // Không khôi phục từ .bak -> xóa các dòng "own" trực tiếp
            return RemoveOwnMappingsInPlace(hostsPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[REVERT] Loi khi xoa hosts: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Khôi phục tệp hosts từ bản sao lưu .bak.
    /// Sao chép toàn bộ nội dung .bak ghi đè lên tệp hosts,
    /// sau đó cắt bớt phần thừa (truncate) và xóa tệp .bak.
    /// </summary>
    /// <param name="bakPath">Đường dẫn tệp sao lưu .bak.</param>
    /// <param name="hostsPath">Đường dẫn tệp hosts hệ thống.</param>
    /// <returns>True nếu thành công, false nếu có lỗi.</returns>
    private static bool RestoreFromBackup(string bakPath, string hostsPath)
    {
        try
        {
            Console.WriteLine("[REVERT] Dang khoi phuc tu ban sao luu .bak...");

            // Đọc toàn bộ nội dung .bak
            byte[] bakContent = File.ReadAllBytes(bakPath);

            // Ghi đè lên tệp hosts
            File.WriteAllBytes(hostsPath, bakContent);

            // Xóa tệp .bak sau khi khôi phục thành công
            File.Delete(bakPath);
            Console.WriteLine("[REVERT] Khoi phuc tu .bak thanh cong. Da xoa tep sao luu.");

            // Flush DNS cache để áp dụng thay đổi
            FlushDns();

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[REVERT] Loi khi khoi phuc tu .bak: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Xóa trực tiếp các dòng có đánh dấu "own" trong tệp hosts.
    /// Quy trình:
    /// 1. Đọc toàn bộ tệp hosts.
    /// 2. Phát hiện encoding (UTF-8 có/không BOM, hoặc ANSI).
    /// 3. Với mỗi dòng: nếu là dòng "own" chỉ chứa comment -> bỏ đánh dấu.
    ///    Nếu là dòng "own" có nội dung -> loại bỏ hoàn toàn.
    ///    Nếu không phải dòng "own" -> giữ nguyên.
    /// 4. Ghi lại tệp hosts với encoding gốc.
    /// </summary>
    /// <param name="hostsPath">Đường dẫn tệp hosts hệ thống.</param>
    /// <returns>True nếu thành công, false nếu có lỗi.</returns>
    private static bool RemoveOwnMappingsInPlace(string hostsPath)
    {
        try
        {
            Console.WriteLine("[REVERT] Dang xoa cac dong danh dau 'own' trong tep hosts...");

            // Đọc toàn bộ nội dung tệp dưới dạng byte để phát hiện encoding
            byte[] rawBytes = File.ReadAllBytes(hostsPath);

            // Phát hiện encoding
            Encoding encoding = DetectEncoding(rawBytes);
            int preambleLength = GetPreambleLength(rawBytes, encoding);

            // Giải mã nội dung (bỏ qua preamble)
            string content = encoding.GetString(rawBytes, preambleLength, rawBytes.Length - preambleLength);

            // Phát hiện kiểu xuống dòng (CRLF hay LF)
            string lineEnding = content.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = content.Split(new[] { lineEnding }, StringSplitOptions.None);

            var keptLines = new List<string>();

            foreach (string line in lines)
            {
                bool isOwn;
                string processedLine = ParseOwnLine(line, out isOwn);

                if (!isOwn)
                {
                    // Không phải dòng "own" -> giữ nguyên
                    keptLines.Add(line);
                }
                else
                {
                    // Là dòng "own"
                    // Nếu dòng chỉ chứa comment (sau khi bỏ đánh dấu) -> giữ lại phần nội dung đã bỏ đánh dấu
                    // Nếu có nội dung ánh xạ IP -> loại bỏ hoàn toàn
                    if (IsCommentOnly(processedLine))
                    {
                        keptLines.Add(Uncomment(processedLine));
                    }
                    // else: dòng có nội dung -> bỏ qua (không thêm vào keptLines)
                }
            }

            // Nối lại các dòng
            string result = string.Join(lineEnding, keptLines);

            // Mã hóa lại với encoding gốc
            byte[] resultBytes = encoding.GetBytes(result);

            // Ghi lại tệp
            File.WriteAllBytes(hostsPath, resultBytes);

            Console.WriteLine("[REVERT] Xoa dong 'own' thanh cong.");

            // Flush DNS cache để áp dụng thay đổi
            FlushDns();

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[REVERT] Loi khi xoa dong 'own': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Phát hiện encoding của nội dung byte.
    /// Ưu tiên: UTF-8 có BOM -> UTF-8 không BOM -> ANSI (Default).
    /// </summary>
    private static Encoding DetectEncoding(byte[] rawBytes)
    {
        if (rawBytes.Length >= 3 &&
            rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF)
        {
            return new UTF8Encoding(true);
        }

        // Thử UTF-8 không BOM
        try
        {
            var utf8NoBom = new UTF8Encoding(false);
            string test = utf8NoBom.GetString(rawBytes);
            // Kiểm tra xem có ký tự không hợp lệ không
            byte[] reencoded = utf8NoBom.GetBytes(test);
            if (reencoded.Length == rawBytes.Length)
            {
                return utf8NoBom;
            }
        }
        catch
        {
            // Bỏ qua
        }

        // Fallback: ANSI (mã hóa mặc định của hệ thống)
        return Encoding.Default;
    }

    /// <summary>
    /// Lấy độ dài preamble (byte đánh dấu BOM) của encoding.
    /// UTF-8 có BOM = 3 byte, các loại khác = 0.
    /// </summary>
    private static int GetPreambleLength(byte[] rawBytes, Encoding encoding)
    {
        byte[] preamble = encoding.GetPreamble();
        if (preamble.Length == 0)
            return 0;

        // Kiểm tra xem rawBytes có bắt đầu bằng preamble không
        if (rawBytes.Length >= preamble.Length)
        {
            bool match = true;
            for (int i = 0; i < preamble.Length; i++)
            {
                if (rawBytes[i] != preamble[i])
                {
                    match = false;
                    break;
                }
            }
            return match ? preamble.Length : 0;
        }

        return 0;
    }

    /// <summary>
    /// Phân tích dòng để xác định có phải dòng "own" (do chương trình thêm) hay không.
    /// Trả về nội dung đã xử lý và cờ isOwn qua out parameter.
    /// </summary>
    /// <param name="line">Dòng văn bản cần phân tích.</param>
    /// <param name="isOwn">True nếu dòng có đánh dấu "own".</param>
    /// <returns>Nội dung dòng đã loại bỏ đánh dấu "own" (nếu có).</returns>
    private static string ParseOwnLine(string line, out bool isOwn)
    {
        isOwn = false;

        string trimmed = line.TrimStart();

        // Kiểm tra xem dòng có chứa tiền tố đánh dấu "own" không
        if (trimmed.StartsWith(OwnMarker, StringComparison.OrdinalIgnoreCase))
        {
            isOwn = true;
            // Loại bỏ tiền tố đánh dấu
            return trimmed.Substring(OwnMarker.Length).TrimStart();
        }

        return line;
    }

    /// <summary>
    /// Kiểm tra xem dòng có chỉ chứa comment hay không (sau khi đã bỏ đánh dấu).
    /// Dòng comment bắt đầu bằng '#'.
    /// </summary>
    private static bool IsCommentOnly(string line)
    {
        return line.TrimStart().StartsWith('#');
    }

    /// <summary>
    /// Loại bỏ ký tự comment '#' ở đầu dòng.
    /// Ví dụ: "# 127.0.0.1 example.com" -> "127.0.0.1 example.com"
    /// </summary>
    private static string Uncomment(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith('#'))
        {
            return trimmed.Substring(1).TrimStart();
        }
        return trimmed;
    }

    /// <summary>
    /// Xóa bộ nhớ cache DNS hệ thống.
    /// Trên Windows: chạy "ipconfig /flushdns".
    /// Trên Linux: không làm gì (Linux thường không có DNS cache mặc định).
    /// Hậu tố log phụ thuộc vào AdminState.SetUp (_setup hoặc _revert).
    /// </summary>
    public static void FlushDns()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                string suffix = AdminState.SetUp ? "_setup" : "_revert";
                Console.WriteLine($"[FLUSHDNS] Dang chay ipconfig /flushdns{suffix}...");

                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(10000);
                Console.WriteLine("[FLUSHDNS] Flush DNS thanh cong.");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux thường không có DNS cache mặc định -> không cần flush
                Console.WriteLine("[FLUSHDNS] Linux khong can flush DNS cache mac dinh.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FLUSHDNS] Loi khi flush DNS: {ex.Message}");
        }
    }
}

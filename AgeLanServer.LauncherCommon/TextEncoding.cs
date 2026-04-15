using System.Text;

namespace AgeLanServer.LauncherCommon.Hosts.Text;

/// <summary>
/// Các loại encoding được hỗ trợ để đọc file hosts.
/// Chuyển đổi từ Go package: launcher-common/hosts/text/text.go, text_windows.go, text_unix.go
/// </summary>
public enum TextEncodingType
{
    /// <summary>UTF-16 LE có BOM (Byte Order Mark)</summary>
    Utf16LeBom = 0,
    /// <summary>UTF-16 LE không BOM</summary>
    Utf16Le = 1,
    /// <summary>UTF-16 BE có BOM</summary>
    Utf16BeBom = 2,
    /// <summary>UTF-16 BE không BOM</summary>
    Utf16Be = 3,
    /// <summary>UTF-8 không BOM</summary>
    Utf8 = 4,
    /// <summary>UTF-8 có BOM</summary>
    Utf8Bom = 5,
    /// <summary>ANSI (Windows Code Page)</summary>
    Ansi = 6,
    /// <summary>Encoding khác / không xác định</summary>
    Other = 7
}

/// <summary>
/// Trình xử lý encoding cho file hosts.
/// Phát hiện encoding từ BOM hoặc heuristic, chuyển đổi sang UTF-8.
/// </summary>
public static class TextEncoding
{
    /// <summary>
    /// Phát hiện encoding của dữ liệu nhị phân.
    /// Sử dụng BOM và heuristic để xác định.
    /// </summary>
    /// <param name="buf">Dữ liệu cần phát hiện encoding</param>
    /// <returns>Loại encoding được phát hiện</returns>
    public static TextEncodingType DetectEncoding(byte[] buf)
    {
        int n = buf.Length;

        // Trường hợp rỗng
        if (n == 0)
        {
            return TextEncodingType.Utf8;
        }

        // Không đủ byte để kiểm tra BOM
        if (n < 2)
        {
            return IsValidUtf8(buf) ? TextEncodingType.Utf8 : TextEncodingType.Other;
        }

        // 1. Kiểm tra BOM
        // UTF-16 LE BOM: FF FE
        if (buf[0] == 0xFF && buf[1] == 0xFE)
        {
            return TextEncodingType.Utf16LeBom;
        }

        // UTF-16 BE BOM: FE FF
        if (buf[0] == 0xFE && buf[1] == 0xFF)
        {
            return TextEncodingType.Utf16BeBom;
        }

        // UTF-8 BOM: EF BB BF
        if (n >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF)
        {
            return TextEncodingType.Utf8Bom;
        }

        // 2. Heuristic cho UTF-16 không BOM dựa trên phân bố byte null
        int nullInEven = 0;
        int nullInOdd = 0;
        for (int i = 0; i < n - 1; i += 2)
        {
            if (buf[i] == 0x00)
            {
                nullInEven++;
            }
            if (buf[i + 1] == 0x00)
            {
                nullInOdd++;
            }
        }

        // Nhiều byte null ở vị trí lẻ và không có ở vị trí chẵn => UTF-16 LE
        if (nullInOdd > n / 4 && nullInEven == 0)
        {
            return TextEncodingType.Utf16Le;
        }

        // Nhiều byte null ở vị trí chẵn và không có ở vị trí lẻ => UTF-16 BE
        if (nullInEven > n / 4 && nullInOdd == 0)
        {
            return TextEncodingType.Utf16Be;
        }

        // 3. UTF-8 (bao gồm ASCII-only)
        if (IsValidUtf8(buf))
        {
            return TextEncodingType.Utf8;
        }

        // 4. Kiểm tra byte có bit cao => ANSI trên Windows
        foreach (byte b in buf)
        {
            if (b >= 0x80)
            {
                return TextEncodingType.Ansi;
            }
        }

        return TextEncodingType.Other;
    }

    /// <summary>
    /// Giải mã dữ liệu nhị phân sang string UTF-8.
    /// Phát hiện encoding tự động và chuyển đổi.
    /// </summary>
    /// <param name="buf">Dữ liệu nhị phân cần giải mã</param>
    /// <param name="detectedEncoding">Loại encoding đã phát hiện</param>
    /// <returns>String đã giải mã</returns>
    public static string Decode(byte[] buf, out TextEncodingType detectedEncoding)
    {
        detectedEncoding = DetectEncoding(buf);
        Encoding encoding = GetEncoding(detectedEncoding);

        string text = encoding.GetString(buf);

        // Kiểm tra kết quả có hợp lệ UTF-8 không
        if (!IsValidUtf8String(text))
        {
            throw new InvalidDataException("Decoded text is not valid UTF-8");
        }

        return text;
    }

    /// <summary>
    /// Mã hóa string sang dữ liệu nhị phân theo encoding chỉ định.
    /// </summary>
    /// <param name="text">String cần mã hóa</param>
    /// <param name="encodingType">Loại encoding đích</param>
    /// <returns>Dữ liệu nhị phân đã mã hóa</returns>
    public static byte[] Encode(string text, TextEncodingType encodingType)
    {
        if (!IsValidUtf8String(text))
        {
            throw new InvalidDataException("Input text is not valid UTF-8");
        }

        Encoding encoding = GetEncoding(encodingType);
        return encoding.GetBytes(text);
    }

    /// <summary>
    /// Lấy Encoding object tương ứng với loại encoding.
    /// Trên Windows: hỗ trợ nhiều code page. Trên Unix: chỉ UTF-8.
    /// </summary>
    /// <param name="encodingType">Loại encoding</param>
    /// <returns>Encoding object</returns>
    private static Encoding GetEncoding(TextEncodingType encodingType)
    {
        return encodingType switch
        {
            TextEncodingType.Utf8 or TextEncodingType.Utf8Bom => Encoding.UTF8,
            TextEncodingType.Utf16Le => Encoding.Unicode, // UTF-16 LE
            TextEncodingType.Utf16LeBom => GetUtf16LeWithBom(),
            TextEncodingType.Utf16Be => Encoding.BigEndianUnicode, // UTF-16 BE
            TextEncodingType.Utf16BeBom => Encoding.BigEndianUnicode,
            TextEncodingType.Ansi => GetAnsiEncoding(),
            _ => Encoding.UTF8 // Mặc định
        };
    }

    /// <summary>
    /// Tạo UTF-16 LE encoder có xử lý BOM.
    /// </summary>
    private static Encoding GetUtf16LeWithBom()
    {
        // UTF-16 LE với BOM được xử lý tự động khi đọc file có BOM
        // Khi decode, BOM đã được tiêu thụ nên dùng Unicode bình thường
        return Encoding.Unicode;
    }

    /// <summary>
    /// Lấy encoding ANSI tương ứng với Windows Code Page hiện tại.
    /// Trên Unix, trả về UTF-8.
    /// </summary>
    private static Encoding GetAnsiEncoding()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Encoding.UTF8;
        }

        // Lấy Windows Code Page hiện tại
        int acp = GetWindowsAcp();

        return acp switch
        {
            // Windows-125x series
            1250 => Encoding.GetEncoding(1250), // Central Europe
            1251 => Encoding.GetEncoding(1251), // Cyrillic
            1252 => Encoding.GetEncoding(1252), // Western Europe (Latin 1)
            1253 => Encoding.GetEncoding(1253), // Greek
            1254 => Encoding.GetEncoding(1254), // Turkish
            1255 => Encoding.GetEncoding(1255), // Hebrew
            1256 => Encoding.GetEncoding(1256), // Arabic
            1257 => Encoding.GetEncoding(1257), // Baltic
            1258 => Encoding.GetEncoding(1258), // Vietnamese

            // OEM / DOS Code Pages
            437 => Encoding.GetEncoding(437),   // US
            850 => Encoding.GetEncoding(850),   // Multilingual Latin 1
            852 => Encoding.GetEncoding(852),   // Latin 2
            855 => Encoding.GetEncoding(855),   // Cyrillic
            860 => Encoding.GetEncoding(860),   // Portuguese
            862 => Encoding.GetEncoding(862),   // Hebrew
            863 => Encoding.GetEncoding(863),   // Canadian French
            865 => Encoding.GetEncoding(865),   // Nordic
            866 => Encoding.GetEncoding(866),   // Russian

            // ISO-8859 series
            28591 => Encoding.GetEncoding(28591),  // ISO-8859-1
            28592 => Encoding.GetEncoding(28592),  // ISO-8859-2
            28595 => Encoding.GetEncoding(28595),  // ISO-8859-5
            28597 => Encoding.GetEncoding(28597),  // ISO-8859-7
            28599 => Encoding.GetEncoding(28599),  // ISO-8859-9
            28605 => Encoding.GetEncoding(28605),  // ISO-8859-15

            // East Asian Encodings
            932 => Encoding.GetEncoding(932),      // ShiftJIS
            936 => Encoding.GetEncoding(936),      // GBK
            949 => Encoding.GetEncoding(949),      // EUC-KR
            950 => Encoding.GetEncoding(950),      // Big5
            54936 => Encoding.GetEncoding(54936),  // GB18030

            // Others
            20866 => Encoding.GetEncoding(20866),  // KOI8-R
            21866 => Encoding.GetEncoding(21866),  // KOI8-U

            _ => Encoding.UTF8
        };
    }

    /// <summary>
    /// Lấy Windows ANSI Code Page.
    /// Trên Unix trả về 0.
    /// </summary>
    private static int GetWindowsAcp()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        // Sử dụng P/Invoke để lấy GetACP() từ kernel32.dll
        try
        {
            return NativeMethods.GetACP();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Kiểm tra byte array có hợp lệ UTF-8 không.
    /// </summary>
    private static bool IsValidUtf8(byte[] buf)
    {
        try
        {
            // Thử decode bằng UTF8Encoding với throwOnError
            var utf8 = new UTF8Encoding(false, true);
            utf8.GetString(buf);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Kiểm tra string có hợp lệ UTF-8 không.
    /// </summary>
    private static bool IsValidUtf8String(string text)
    {
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            byte[] bytes = utf8.GetBytes(text);
            utf8.GetString(bytes);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Đọc file hosts với tự động phát hiện encoding.
    /// </summary>
    /// <param name="filePath">Đường dẫn file</param>
    /// <returns>Nội dung file dưới dạng string</returns>
    public static async Task<string> ReadFileWithAutoEncodingAsync(string filePath)
    {
        byte[] buf = await File.ReadAllBytesAsync(filePath);
        return Decode(buf, out _);
    }

    /// <summary>
    /// P/Invoke methods cho Windows API.
    /// </summary>
    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern int GetACP();
    }
}

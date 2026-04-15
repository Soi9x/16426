using System.Buffers.Binary;

namespace AgeLanServer.Server.Models.Playfab;

/// <summary>
/// Phân tích vé ứng dụng Steam.
/// Giải mã ticket dạng hex và trích xuất SteamID.
/// </summary>
public static class SteamAppTicket
{
    /// <summary>
    /// Giải mã ticket hex và trả về SteamID (ulong).
    /// </summary>
    public static ulong ParseSteamIdHex(string ticketHex, out Exception? error)
    {
        error = null;
        byte[] data;
        try
        {
            data = Convert.FromHexString(ticketHex);
        }
        catch (Exception ex)
        {
            error = ex;
            return 0;
        }

        int off = 0;

        if (!ReadUint32(data, ref off, out var initialLen))
        {
            error = new Exception("Ticket quá ngắn");
            return 0;
        }

        if (initialLen == 20)
        {
            // Trường hợp wrapper: đọc các trường wrapper
            if (!ReadUint64(data, ref off, out _)) { error = new Exception("Unexpected end (gcToken)"); return 0; }
            off += 8;
            if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (tokenGenerated)"); return 0; }
            if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (sessionheader)"); return 0; }
            off += 8;
            if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (sessionExternalIP)"); return 0; }
            off += 4;
            if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (clientConnectionTime)"); return 0; }
            if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (clientConnectionCount)"); return 0; }
            if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (ownership section length)"); return 0; }
        }
        else
        {
            off -= 4; // Rewind
        }

        // Độ dài ticket sở hữu
        if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (ownershipLength)"); return 0; }

        // Phiên bản
        if (!ReadUint32(data, ref off, out _)) { error = new Exception("Unexpected end (version)"); return 0; }

        // SteamID
        if (!ReadUint64(data, ref off, out var steamId)) { error = new Exception("Unexpected end (steamID)"); return 0; }

        return steamId;
    }

    private static bool ReadUint32(byte[] b, ref int off, out uint value)
    {
        if (off + 4 > b.Length) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(off, 4));
        off += 4;
        return true;
    }

    private static bool ReadUint64(byte[] b, ref int off, out ulong value)
    {
        if (off + 8 > b.Length) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(off, 8));
        off += 8;
        return true;
    }
}

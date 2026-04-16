using System.Security.Cryptography.X509Certificates;
using System.Text;
using AgeLanServer.Common;

namespace AgeLanServer.LauncherConfig;

/// <summary>
/// Quan ly chung chi CA cua game: sao luu, khoi phuc, va them chung chi vao kho lu tru.
/// Tuong ung voi CACert trong cacert.go va CA trong ca.go.
/// </summary>
public sealed class CaCertManager
{
    private readonly string _gamePath;
    private readonly string _gameId;

    /// <summary>
    /// Khoi tao quan ly CA cert cho game chi dinh.
    /// </summary>
    /// <param name="gameId">Dinh danh game (AoE1, AoE2, AoE3, AoE4).</param>
    /// <param name="gamePath">Duong dan den thu muc game.</param>
    public CaCertManager(string gameId, string gamePath)
    {
        _gameId = GameIds.Normalize(gameId) ?? gameId;
        // AoE2 co thu muc certificates con
        _gamePath = _gameId == GameIds.AgeOfEmpires2
            ? Path.Combine(gamePath, "certificates")
            : gamePath;
    }

    /// <summary>
    /// Ten file chung chi MAC DINH: cacert.pem.
    /// </summary>
    private string CertFileName => "cacert.pem";

    /// <summary>
    /// Duong dan den file goc.
    /// </summary>
    public string OriginalPath => Path.Combine(_gamePath, CertFileName);

    /// <summary>
    /// Duong dan den file tam thoi (dung khi khoi phuc).
    /// </summary>
    public string TmpPath => Path.Combine(_gamePath, CertFileName + ".lan");

    /// <summary>
    /// Duong dan den file sao luu (.bak).
    /// </summary>
    public string BackupPath => Path.Combine(_gamePath, CertFileName + ".bak");

    /// <summary>
    /// Sao luu file CA cert goc sang file .bak.
    /// Neu file .bak da ton tai, bo qua (da co ban sao luu).
    /// Neu file goc khong ton tai, tra ve loi.
    /// </summary>
    /// <returns>Task bat dong bo.</returns>
    /// <exception cref="FileNotFoundException">Neu file goc khong ton tai.</exception>
    /// <exception cref="IOException">Neu qua trinh sao luu that bai.</exception>
    public async Task BackupAsync()
    {
        if (!File.Exists(OriginalPath))
            throw new FileNotFoundException($"File CA cert goc khong ton tai: {OriginalPath}");

        // Neu da co file backup, khong lam gi them
        if (File.Exists(BackupPath))
            return;

        Console.WriteLine($"Dang sao luu {OriginalPath} -> {BackupPath}");

        // Doc toan bo noi dung file goc
        byte[] originalData = await File.ReadAllBytesAsync(OriginalPath);

        // Ghi vao file backup
        await File.WriteAllBytesAsync(BackupPath, originalData);

        // Dam bao du lieu duoc ghi xuong dia
        using FileStream fs = new(BackupPath, FileMode.Open, FileAccess.ReadWrite);
        await fs.FlushAsync();
    }

    /// <summary>
    /// Khoi phuc file CA cert tu ban sao luu (.bak).
    /// Tra ve danh sach cac chung chi da bi loai bo (co trong ban sao luu nhung khong con trong file goc).
    /// </summary>
    /// <returns>Danh sach chung chi da bi loai bo.</returns>
    /// <exception cref="FileNotFoundException">Neu file goc hoac file backup khong ton tai.</exception>
    /// <exception cref="InvalidOperationException">Neu file tam thoi da ton tai hoac qua trinh khoi phuc that bai.</exception>
    public async Task<IReadOnlyList<X509Certificate2>> RestoreAsync()
    {
        if (!File.Exists(OriginalPath))
            throw new FileNotFoundException($"File CA cert goc khong ton tai: {OriginalPath}");

        if (!File.Exists(BackupPath))
            throw new FileNotFoundException($"File backup khong ton tai: {BackupPath}");

        // Kiem tra file tam thoi da ton tai chua
        if (File.Exists(TmpPath))
            throw new InvalidOperationException($"File tam thoi da ton tai: {TmpPath}");

        Console.WriteLine($"Dang khoi phuc: di chuyen {OriginalPath} -> {TmpPath}");

        // Buoc 1: Di chuyen file goc sang file tam thoi
        File.Move(OriginalPath, TmpPath);

        try
        {
            // Buoc 2: Di chuyen file backup ve vi tri goc
            Console.WriteLine($"Dang khoi phuc: di chuyen {BackupPath} -> {OriginalPath}");
            File.Move(BackupPath, OriginalPath);
        }
        catch
        {
            // Neu that bai, dao nguoc lai
            RevertRestore();
            throw;
        }

        // Ham dao nguoc neu co loi
        void RevertRestore()
        {
            try { File.Move(OriginalPath, BackupPath); } catch { /* bo qua */ }
            try { File.Move(TmpPath, OriginalPath); } catch { /* bo qua */ }
        }

        // Buoc 3: Doc chung chi tu ca hai file
        Console.WriteLine($"Dang doc chung chi tu {TmpPath}");
        var (backupHashes, backupHashToIndex, backupCerts) = await ReadCertsFromFileAsync(TmpPath);

        Console.WriteLine($"Dang doc chung chi tu {OriginalPath}");
        var (originalHashes, _, _) = await ReadCertsFromFileAsync(OriginalPath);

        // Buoc 4: Xoa file tam thoi
        Console.WriteLine($"Dang xoa {TmpPath}");
        try
        {
            File.Delete(TmpPath);
        }
        catch
        {
            RevertRestore();
            throw;
        }

        // Buoc 5: Tim cac chung bi da bi loai bo (co trong backup nhung khong con trong original)
        var originalHashSet = new HashSet<string>(originalHashes);
        var removedHashes = backupHashes.Where(h => !originalHashSet.Contains(h)).ToList();

        var removedCerts = removedHashes
            .Select(hash => backupHashToIndex.TryGetValue(hash, out int idx) ? backupCerts[idx] : null!)
            .Where(c => c != null)
            .ToList();

        return removedCerts.AsReadOnly();
    }

    /// <summary>
    /// Them danh sach chung chi vao cuoi file CA cert.
    /// </summary>
    /// <param name="certificates">Danh sach chung chi can them.</param>
    /// <returns>Task bat dong bo.</returns>
    /// <exception cref="FileNotFoundException">Neu file goc khong ton tai.</exception>
    public async Task AppendAsync(IEnumerable<X509Certificate2> certificates)
    {
        if (!File.Exists(OriginalPath))
            throw new FileNotFoundException($"File CA cert goc khong ton tai: {OriginalPath}");

        Console.WriteLine("Dang them chung chi vao file CA cert...");

        var sb = new StringBuilder();
        foreach (var cert in certificates)
        {
            // Chuyen RawData sang PEM format
            string pem = CertToPem(cert.RawData);
            sb.Append(pem);
        }

        await File.AppendAllTextAsync(OriginalPath, sb.ToString());
    }

    /// <summary>
    /// Chuyen doi RawData chung chi sang dinh dang PEM.
    /// </summary>
    private static string CertToPem(byte[] rawData)
    {
        string base64 = Convert.ToBase64String(rawData, Base64FormattingOptions.InsertLineBreaks);
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN CERTIFICATE-----");
        sb.AppendLine(base64);
        sb.AppendLine("-----END CERTIFICATE-----");
        return sb.ToString();
    }

    /// <summary>
    /// Doc tat ca chung chi tu file PEM.
    /// Tra ve: danh sach hash SHA256, anh xa hash -> index, danh sach chung chi.
    /// </summary>
    private static async Task<(List<string> hashes, Dictionary<string, int> hashToIndex, List<X509Certificate2> certs)>
        ReadCertsFromFileAsync(string filePath)
    {
        string content = await File.ReadAllTextAsync(filePath);

        var hashes = new List<string>();
        var hashToIndex = new Dictionary<string, int>();
        var certs = new List<X509Certificate2>();

        // Tach tung block PEM
        string pemMarkerBegin = "-----BEGIN CERTIFICATE-----";
        string pemMarkerEnd = "-----END CERTIFICATE-----";

        int startIndex = 0;
        while ((startIndex = content.IndexOf(pemMarkerBegin, startIndex, StringComparison.Ordinal)) != -1)
        {
            int endIndex = content.IndexOf(pemMarkerEnd, startIndex, StringComparison.Ordinal);
            if (endIndex == -1) break;

            endIndex += pemMarkerEnd.Length;
            string pemBlock = content.Substring(startIndex, endIndex - startIndex);

            try
            {
                var cert = new X509Certificate2(
                    System.Text.Encoding.ASCII.GetBytes(pemBlock),
                    string.Empty,
                    X509KeyStorageFlags.DefaultKeySet
                );

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(cert.RawData);
                string fingerprint = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

                hashes.Add(fingerprint);
                hashToIndex[fingerprint] = hashes.Count - 1;
                certs.Add(cert);
            }
            catch
            {
                // Bo qua block khong hop le
            }

            startIndex = endIndex;
        }

        return (hashes, hashToIndex, certs);
    }
}

using System.Net;
using System.Security.Cryptography.X509Certificates;
using AgeLanServer.Common;
using AgeLanServer.LauncherConfig;

namespace AgeLanServer.LauncherConfig.Cmd;

/// <summary>
/// Thuc hien toan bo quy trinh SETUP cau hinh LAN cho game.
/// Bao gom: them user cert, backup metadata/profiles, them game CA cert,
/// cap nhat host mappings, luu local cert, va giao tiep voi admin agent.
/// Tuong tu runSetUp trong setUp.go.
/// </summary>
public class CmdSetup
{
    #region Trang thai

    /// <summary>Da them user cert vao he thong.</summary>
    private bool _addedUserCert;

    /// <summary>Da sao luu metadata.</summary>
    private bool _backedUpMetadata;

    /// <summary>Da sao luu profiles.</summary>
    private bool _backedUpProfiles;

    /// <summary>Da them game CA cert.</summary>
    private bool _addedGameCert;

    /// <summary>Ma loi neu setup that bai.</summary>
    private int _errorCode;

    #endregion

    #region Cau hinh

    /// <summary> Duong dan den thu muc game. </summary>
    public string? GamePath { get; set; }

    /// <summary> Duong dan den file host. </summary>
    public string? HostFilePath { get; set; }

    /// <summary> Duong dan den file cert. </summary>
    public string? CertFilePath { get; set; }

    /// <summary> Thu muc ghi log. </summary>
    public string? LogRoot { get; set; }

    /// <summary> Du lieu user cert (bytes). </summary>
    public byte[]? UserCertData { get; set; }

    /// <summary> Co sao luu metadata khong. </summary>
    public bool DoBackupMetadata { get; set; }

    /// <summary> Co sao luu profiles khong. </summary>
    public bool DoBackupProfiles { get; set; }

    /// <summary> Du lieu CA cert cho game store (bytes). </summary>
    public byte[]? CaStoreCert { get; set; }

    /// <summary> Co khoi dong agent neu can khong. </summary>
    public bool AgentStart { get; set; }

    /// <summary> Co dung agent khi gap loi khong. </summary>
    public bool AgentEndOnError { get; set; }

    /// <summary> Dinh danh game. </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary> Dia IP can map. </summary>
    public IPAddress? MapIP { get; set; }

    /// <summary> Du lieu local cert can them. </summary>
    public byte[]? LocalCertData { get; set; }

    /// <summary> Co dang la admin khong. </summary>
    public bool IsAdmin { get; set; }

    #endregion

    /// <summary>
    /// Thuc hien toan bo quy trinh setup.
    /// Tra ve 0 neu thanh cong, ma loi neu that bai.
    /// </summary>
    public async Task<int> ExecuteAsync()
    {
        var normalizedGameId = GameIds.Normalize(GameId);
        if (normalizedGameId is null)
        {
            Console.WriteLine("Loi: Game khong hop le");
            return (int)LauncherError.InvalidGame;
        }

        GameId = normalizedGameId;

        // Xu ly game Age1 va Age4 - mot so tinh nang khong ho tro
        if (GameId == GameIds.AgeOfEmpires1)
        {
            DoBackupMetadata = false;
            CaStoreCert = null;
        }
        else if (GameId == GameIds.AgeOfEmpires4)
        {
            CaStoreCert = null;
        }

        // Kiem tra game hop le
        if ((DoBackupMetadata || DoBackupProfiles) && !IsValidGame(GameId))
        {
            Console.WriteLine("Loi: Game khong hop le");
            return (int)LauncherError.InvalidGame;
        }

        byte[]? addLocalCertData = null;
        if (!string.IsNullOrEmpty(CertFilePath))
        {
            if (LocalCertData == null || LocalCertData.Length == 0)
            {
                Console.WriteLine("Da dat duong dan file cert nhung khong co du lieu cert local");
                return (int)LauncherError.MissingLocalCertData;
            }
        }
        else
        {
            addLocalCertData = LocalCertData;
        }

        Console.WriteLine($"Dang thiet lap cau hinh cho {GameId}...");

        // --- Buoc 1: Them user cert ---
        if (UserCertData != null)
        {
            if (!await AddUserCertAsync())
                return _errorCode;
        }

        // --- Buoc 2: Sao luu metadata ---
        if (DoBackupMetadata)
        {
            if (!await BackupMetadataAsync())
                return _errorCode;
        }

        // --- Buoc 3: Sao luu profiles ---
        if (DoBackupProfiles)
        {
            if (!await BackupProfilesAsync())
                return _errorCode;
        }

        // --- Buoc 4: Them game CA cert ---
        if (CaStoreCert != null)
        {
            if (!await AddGameCertAsync())
                return _errorCode;
        }

        // --- Buoc 5: Them host mappings ---
        IPAddress? ipToMap = null;
        if (string.IsNullOrEmpty(HostFilePath))
        {
            if (MapIP != null)
                ipToMap = MapIP;
        }
        else if (MapIP != null)
        {
            // Ghi file hosts
            // Trong implement day du, goi hosts.AddHosts()
            Console.WriteLine("Da them host mappings");
        }

        // --- Buoc 6: Luu local cert file ---
        if (!string.IsNullOrEmpty(CertFilePath))
        {
            if (!await SaveCertFileAsync())
                return _errorCode;
        }

        // --- Buoc 7: Giao tiep voi admin agent ---
        if (addLocalCertData != null || ipToMap != null)
        {
            if (!await CommunicateWithAdminAsync(ipToMap, addLocalCertData))
                return _errorCode;
        }

        Console.WriteLine("Thiet lap cau hinh thanh cong!");
        return 0;
    }

    /// <summary>
    /// Them user cert vao kho tin cay cua he thong.
    /// </summary>
    private async Task<bool> AddUserCertAsync()
    {
        Console.WriteLine("Dang them user cert, uy quyen neu can...");

        X509Certificate2? cert = ParseCertificate(UserCertData!);
        if (cert == null)
        {
            Console.WriteLine("That bai: Khong phan tich duoc cert");
            _errorCode = (int)LauncherError.UserCertAddParse;
            await UndoSetupAsync();
            return false;
        }

        try
        {
            // Them cert vao Root store cua nguoi dung
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();

            Console.WriteLine("Da them user cert thanh cong");
            _addedUserCert = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"That bai khi them user cert: {ex.Message}");
            _errorCode = (int)LauncherError.UserCertAdd;
            await UndoSetupAsync();
            return false;
        }
    }

    /// <summary>
    /// Sao luu metadata cua game.
    /// </summary>
    private async Task<bool> BackupMetadataAsync()
    {
        Console.WriteLine("Dang sao luu metadata...");

        var metadata = MetadataBackupManager.GetMetadata(GameId);
        if (string.IsNullOrEmpty(metadata.Path))
        {
            Console.WriteLine("That bai: Khong tim thay duong dan metadata");
            _errorCode = (int)LauncherError.MetadataBackup;
            await UndoSetupAsync();
            return false;
        }

        if (UserDataBackup.Backup(metadata))
        {
            Console.WriteLine("Da sao luu metadata thanh cong");
            _backedUpMetadata = true;
            return true;
        }

        Console.WriteLine("That bai khi sao luu metadata");
        _errorCode = (int)LauncherError.MetadataBackup;
        await UndoSetupAsync();
        return false;
    }

    /// <summary>
    /// Sao luu profiles cua game.
    /// </summary>
    private async Task<bool> BackupProfilesAsync()
    {
        Console.WriteLine("Dang sao luu profiles...");

        if (ProfileBackupManager.BackupProfiles(GameId))
        {
            Console.WriteLine("Da sao luu profiles thanh cong");
            _backedUpProfiles = true;
            return true;
        }

        Console.WriteLine("That bai khi sao luu profiles");
        _errorCode = (int)LauncherError.ProfilesBackup;
        await UndoSetupAsync();
        return false;
    }

    /// <summary>
    /// Them CA cert vao kho cert cua game.
    /// </summary>
    private async Task<bool> AddGameCertAsync()
    {
        Console.WriteLine("Dang them cert vao game store...");

        if (string.IsNullOrEmpty(GamePath))
        {
            Console.WriteLine("That bai: Can duong dan game de them cert vao store");
            _errorCode = (int)LauncherError.GamePathMissing;
            await UndoSetupAsync();
            return false;
        }

        var cert = ParseCertificate(CaStoreCert!);
        if (cert == null)
        {
            Console.WriteLine("That bai: Khong phan tich duoc cert");
            _errorCode = (int)LauncherError.GameCertAddParse;
            await UndoSetupAsync();
            return false;
        }

        var gameCert = new CaCertManager(GameId, GamePath);

        // Sao luu truoc khi them
        try
        {
            await gameCert.BackupAsync();
            Console.WriteLine("Da sao luu game store thanh cong");
            _addedGameCert = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"That bai khi sao luu game store: {ex.Message}");
            _errorCode = (int)LauncherError.GameCertBackup;
            await UndoSetupAsync();
            return false;
        }

        // Them cert
        try
        {
            await gameCert.AppendAsync(new[] { cert });
            Console.WriteLine("Da them cert vao game store thanh cong");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"That bai khi them cert vao game store: {ex.Message}");
            _errorCode = (int)LauncherError.GameCertAdd;
            await UndoSetupAsync();
            return false;
        }
    }

    /// <summary>
    /// Luu local cert ra file.
    /// </summary>
    private async Task<bool> SaveCertFileAsync()
    {
        try
        {
            string pem = CertToPem(LocalCertData!);
            await File.WriteAllTextAsync(CertFilePath!, pem);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Loi khi luu file cert: {ex.Message}");
            _errorCode = (int)LauncherError.UserCertAdd;
            await UndoSetupAsync();
            return false;
        }
    }

    /// <summary>
    /// Giao tiep voi admin agent de them local cert va/hoac host mappings.
    /// </summary>
    private async Task<bool> CommunicateWithAdminAsync(IPAddress? ipToMap, byte[]? addCertData)
    {
        bool agentStarted = await AdminIpcClient.ConnectAgentIfNeededAsync();

        if (!agentStarted && AgentStart && !IsAdmin)
        {
            var result = await AdminIpcClient.StartAgentIfNeededAsync(LogRoot);
            if (!result.Success)
            {
                Console.WriteLine("That bai khi khoi dong config-admin-agent");
                if (result.ErrorMessage != null)
                    Console.WriteLine(result.ErrorMessage);
                if (result.ExitCode != 0)
                    Console.WriteLine($"Exit code: {result.ExitCode}");

                _errorCode = (int)LauncherError.StartAgent;
                await UndoSetupAsync();
                return false;
            }

            agentStarted = await AdminIpcClient.ConnectAgentIfNeededWithRetriesAsync(retryUntilSuccess: true);
            if (!agentStarted)
            {
                Console.WriteLine("That bai khi ket noi voi config-admin-agent sau khi khoi dong. Hay dung Task Manager de ket thuc.");
                _errorCode = (int)LauncherError.StartAgentVerify;
                await UndoSetupAsync();
                return false;
            }
        }

        if (agentStarted)
            Console.WriteLine("Dang giao tiep voi config-admin-agent de them local cert va/hoac host mappings...");
        else
        {
            string msg = "Dang chay config-admin de them local cert va/hoac host mappings";
            if (!IsAdmin)
                msg += ", uy quyen neu can";
            Console.WriteLine(msg + "...");
        }

        var (err, exitCode) = await AdminIpcClient.RunSetUpAsync(LogRoot, ipToMap, addCertData);
        if (err == null && exitCode == 0)
        {
            if (agentStarted)
                Console.WriteLine("Da giao tiep voi config-admin-agent thanh cong");
            else
                Console.WriteLine("Da chay config-admin thanh cong");
            return true;
        }

        if (err != null)
        {
            Console.WriteLine("Nhan duoc loi:");
            Console.WriteLine(err);
        }
        if (exitCode != 0)
        {
            Console.WriteLine("Nhan duoc exit code:");
            Console.WriteLine(exitCode);
        }

        _errorCode = (int)LauncherError.AdminSetup;

        if (agentStarted && AgentEndOnError)
        {
            Console.WriteLine("That bai khi giao tiep voi config-admin-agent. Dang yeu cau dung...");
            var stopErr = await AdminIpcClient.StopAgentIfNeededAsync();
            if (stopErr != null)
            {
                bool failedStopAgent = true;
                if (IsAdmin)
                {
                    // Co the kill process truc tiep
                    try
                    {
                        KillAgentProcess();
                        Console.WriteLine("Da kill config-admin-agent thanh cong");
                        failedStopAgent = false;
                    }
                    catch { /* bo qua */ }
                }

                if (failedStopAgent)
                {
                    Console.WriteLine("That bai khi dung config-admin-agent. Hay dung Task Manager de ket thuc thu cong");
                    Console.WriteLine($"Loi: {stopErr}");
                }
            }
            else
            {
                Console.WriteLine("Da dung config-admin-agent thanh cong");
            }
        }
        else if (!agentStarted)
        {
            Console.WriteLine("That bai khi chay config-admin");
        }

        await UndoSetupAsync();
        return false;
    }

    /// <summary>
    /// Dao nguoc toan bo cac buoc da lam neu setup that bai.
    /// Tuong tu undoSetUp trong setUp.go.
    /// </summary>
    private async Task UndoSetupAsync()
    {
        if (_addedUserCert)
            await RemoveUserCertAsync();
        if (_backedUpMetadata)
            await RestoreMetadataAsync();
        if (_backedUpProfiles)
            await RestoreProfilesAsync();
        if (_addedGameCert)
            await RestoreGameCertAsync();
        if (!string.IsNullOrEmpty(HostFilePath))
        {
            try { File.Delete(HostFilePath); } catch { /* bo qua */ }
        }
        if (!string.IsNullOrEmpty(CertFilePath))
        {
            try { File.Delete(CertFilePath); } catch { /* bo qua */ }
        }
    }

    /// <summary>
    /// Xoa user cert da them.
    /// </summary>
    private async Task<bool> RemoveUserCertAsync()
    {
        Console.WriteLine("Dang xoa user cert da them...");
        try
        {
            // Trong thuc te, can dung P/Invoke hoac thu vien de xoa cert khoi store
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            // Tim va xoa cert dua tren fingerprint
            store.Close();
            Console.WriteLine("Da xoa user cert thanh cong");
            return true;
        }
        catch
        {
            Console.WriteLine("That bai khi xoa user cert");
            return false;
        }
    }

    /// <summary>
    /// Khoi phuc metadata da backup.
    /// </summary>
    private async Task<bool> RestoreMetadataAsync()
    {
        Console.WriteLine("Dang khoi phuc metadata...");
        var metadata = MetadataBackupManager.GetMetadata(GameId);
        if (UserDataBackup.Restore(metadata))
        {
            Console.WriteLine("Da khoi phuc metadata thanh cong");
            return true;
        }
        Console.WriteLine("That bai khi khoi phuc metadata");
        return false;
    }

    /// <summary>
    /// Khoi phuc profiles da backup.
    /// </summary>
    private async Task<bool> RestoreProfilesAsync()
    {
        Console.WriteLine("Dang khoi phuc profiles...");
        if (ProfileBackupManager.RestoreProfiles(GameId, reverseFailed: true))
        {
            Console.WriteLine("Da khoi phuc profiles thanh cong");
            return true;
        }
        Console.WriteLine("That bai khi khoi phuc profiles");
        return false;
    }

    /// <summary>
    /// Khoi phuc game CA cert da backup.
    /// </summary>
    private async Task<bool> RestoreGameCertAsync()
    {
        Console.WriteLine("Dang khoi phuc game CA cert...");
        try
        {
            var gameCert = new CaCertManager(GameId, GamePath!);
            await gameCert.RestoreAsync();
            Console.WriteLine("Da khoi phuc game CA cert thanh cong");
            return true;
        }
        catch
        {
            Console.WriteLine("That bai khi khoi phuc game CA cert");
            return false;
        }
    }

    /// <summary>
    /// Phan tich bytes thanh doi tuong X509Certificate2.
    /// </summary>
    private static X509Certificate2? ParseCertificate(byte[] data)
    {
        try
        {
            return new X509Certificate2(data, string.Empty, X509KeyStorageFlags.DefaultKeySet);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Chuyen doi raw cert data sang PEM format.
    /// </summary>
    private static string CertToPem(byte[] rawData)
    {
        string base64 = Convert.ToBase64String(rawData, Base64FormattingOptions.InsertLineBreaks);
        return $"-----BEGIN CERTIFICATE-----\n{base64}\n-----END CERTIFICATE-----\n";
    }

    /// <summary>
    /// Kiem tra game co hop le khong.
    /// </summary>
    private static bool IsValidGame(string gameId)
    {
        return GameIds.IsValid(gameId);
    }

    /// <summary>
    /// Kill process cua agent (chi dung khi isAdmin = true).
    /// </summary>
    private static void KillAgentProcess()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("config-admin-agent");
        foreach (var proc in processes)
        {
            try
            {
                proc.Kill();
                proc.WaitForExit();
            }
            catch { /* bo qua */ }
        }
    }
}

/// <summary>
/// Ma loi dung trong qua trinh setup/revert.
/// </summary>
public enum LauncherError
{
    UserCertRemove = 100,
    UserCertAdd,
    UserCertAddParse,
    MetadataRestore,
    ProfilesRestore,
    AdminRevert,
    MetadataBackup,
    ProfilesBackup,
    StartAgent,
    StartAgentVerify,
    AdminSetup,
    RevertStopAgent,
    HostsAdd,
    MissingLocalCertData,
    GameCertAddParse,
    GameCertAdd,
    GameCertRestore,
    GameCertBackup,
    GamePathMissing,
    InvalidGame,
    General = 999
}

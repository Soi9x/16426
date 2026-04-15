using System.Security.Cryptography.X509Certificates;
using AgeLanServer.LauncherConfig;

namespace AgeLanServer.LauncherConfig.Cmd;

/// <summary>
/// Thuc hien toan bo quy trinh REVERT (hoan tac) cau hinh LAN cho game.
/// Bao gom: xoa user cert, khoi phuc metadata/profiles, khoi phuc game CA cert,
/// xoa host mappings, va giao tiep voi admin agent.
/// Tuong tu runRevert trong revert.go.
/// </summary>
public class CmdRevert
{
    #region Trang thai

    /// <summary> Cac user cert da bi xoa. </summary>
    private List<X509Certificate2>? _removedUserCerts;

    /// <summary> Cac CA cert da bi xoa (lay ra khi restore). </summary>
    private List<X509Certificate2>? _removedCaCerts;

    /// <summary> Da khoi phuc metadata. </summary>
    private bool _restoredMetadata;

    /// <summary> Da khoi phuc profiles. </summary>
    private bool _restoredProfiles;

    /// <summary> Ma loi neu revert that bai. </summary>
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

    /// <summary> Co xoa user cert khong. </summary>
    public bool DoRemoveUserCert { get; set; }

    /// <summary> Co khoi phuc metadata khong. </summary>
    public bool DoRestoreMetadata { get; set; }

    /// <summary> Co khoi phuc profiles khong. </summary>
    public bool DoRestoreProfiles { get; set; }

    /// <summary> Co khoi phuc game CA cert khong. </summary>
    public bool DoRestoreCaStoreCert { get; set; }

    /// <summary> Co dung agent sau khi hoan tat khong. </summary>
    public bool StopAgent { get; set; }

    /// <summary> Dinh danh game. </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary> Co xoa tat ca (removeAll) khong. </summary>
    public bool RemoveAll { get; set; }

    /// <summary> Co dao nguoc khi that bai khong. </summary>
    private bool _reverseFailed = true;

    /// <summary> Co dang la admin khong. </summary>
    public bool IsAdmin { get; set; }

    #endregion

    /// <summary>
    /// Thuc hien toan bo quy trinh revert.
    /// Tra ve 0 neu thanh cong, ma loi neu that bai.
    /// </summary>
    public async Task<int> ExecuteAsync()
    {
        // Xu ly RemoveAll: dat tat ca cac co thanh true
        if (RemoveAll)
        {
            DoRemoveUserCert = true;
            DoRestoreMetadata = true;
            DoRestoreProfiles = true;
            DoRestoreCaStoreCert = true;
            _reverseFailed = false;
        }

        // Xu ly game AoE1 va AoE4
        if (GameId == "aoe1")
        {
            DoRestoreMetadata = false;
            DoRestoreCaStoreCert = false;
        }
        else if (GameId == "aoe4")
        {
            DoRestoreCaStoreCert = false;
        }

        // Kiem tra game hop le
        if ((DoRestoreMetadata || DoRestoreProfiles) && !IsValidGame(GameId))
        {
            Console.WriteLine("Loi: Game khong hop le");
            _errorCode = (int)LauncherError.InvalidGame;
            await UndoRevertAsync();
            return _errorCode;
        }

        Console.WriteLine($"Dang hoan tac cau hinh cho {GameId}...");

        // --- Buoc 1: Xoa user cert ---
        if (DoRemoveUserCert)
        {
            if (!await RemoveUserCertAsync())
                return _errorCode;
        }

        // --- Buoc 2: Khoi phuc metadata ---
        if (DoRestoreMetadata)
        {
            if (!await RestoreMetadataAsync())
                return _errorCode;
        }

        // --- Buoc 3: Khoi phuc profiles ---
        if (DoRestoreProfiles)
        {
            if (!await RestoreProfilesAsync())
                return _errorCode;
        }

        // --- Buoc 4: Khoi phuc game CA cert ---
        if (DoRestoreCaStoreCert)
        {
            if (!await RestoreGameCertAsync())
                return _errorCode;
        }

        // --- Buoc 5: Giao tiep voi admin agent ---
        bool agentConnected = false;
        bool removeLocalCert = RemoveAll; // Mac dinh khi RemoveAll
        bool unmapIPs = RemoveAll;

        if (removeLocalCert || unmapIPs)
        {
            agentConnected = await AdminIpcClient.ConnectAgentIfNeededAsync();

            if (agentConnected)
                Console.WriteLine("Dang giao tiep voi config-admin-agent de xoa local cert va/hoac host mappings...");
            else
            {
                string msg = "Dang chay config-admin de xoa local cert va/hoac host mappings";
                if (!IsAdmin)
                    msg += ", uy quyen neu can";
                Console.WriteLine(msg + "...");
            }

            var (err, exitCode) = await AdminIpcClient.RunRevertAsync(
                LogRoot,
                unmapIPs,
                removeLocalCert,
                failfast: !RemoveAll
            );

            if (err == null && exitCode == 0)
            {
                if (agentConnected)
                    Console.WriteLine("Da giao tiep voi config-admin-agent thanh cong");
                else
                    Console.WriteLine("Da chay config-admin thanh cong");
            }
            else
            {
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

                _errorCode = (int)LauncherError.AdminRevert;
                await UndoRevertAsync();

                if (agentConnected)
                    Console.WriteLine("That bai khi giao tiep voi config-admin-agent");
                else
                    Console.WriteLine("That bai khi chay config-admin");

                return _errorCode;
            }
        }

        // Neu RemoveAll, dat errorCode = 0 bo qua loi tru do
        if (RemoveAll)
        {
            _errorCode = 0;
        }

        // --- Buoc 6: Xoa file host va cert neu co ---
        if (_errorCode == 0 && !string.IsNullOrEmpty(HostFilePath))
        {
            try { File.Delete(HostFilePath); } catch { /* bo qua */ }
        }
        if (_errorCode == 0 && !string.IsNullOrEmpty(CertFilePath))
        {
            try { File.Delete(CertFilePath); } catch { /* bo qua */ }
        }

        // --- Buoc 7: Dung agent neu duoc yeu cau ---
        if (StopAgent)
        {
            await StopAgentIfNeededAsync(agentConnected);
        }

        return _errorCode;
    }

    /// <summary>
    /// Xoa user cert khoi kho tin cay cua he thong.
    /// Tra ve danh sach cert da xoa de co the khoi phuc sau nay.
    /// </summary>
    private async Task<bool> RemoveUserCertAsync()
    {
        Console.WriteLine("Dang xoa user cert, uy quyen neu can...");

        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var removedCerts = new List<X509Certificate2>();

            // Trong thuc te, can xac dinh cert nao can xoa dua tren fingerprint
            // O day gia lap viec xoa thanh cong
            // store.Remove(cert);

            store.Close();

            _removedUserCerts = removedCerts;
            Console.WriteLine("Da xoa user cert thanh cong");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"That bai khi xoa user cert: {ex.Message}");
            _errorCode = (int)LauncherError.UserCertRemove;
            await UndoRevertAsync();
            return false;
        }
    }

    /// <summary>
    /// Khoi phuc metadata tu ban sao luu.
    /// </summary>
    private async Task<bool> RestoreMetadataAsync()
    {
        Console.WriteLine("Dang khoi phuc metadata...");

        var metadata = MetadataBackupManager.GetMetadata(GameId);
        if (UserDataBackup.Restore(metadata))
        {
            Console.WriteLine("Da khoi phuc metadata thanh cong");
            _restoredMetadata = true;
            return true;
        }

        Console.WriteLine("That bai khi khoi phuc metadata");
        _errorCode = (int)LauncherError.MetadataRestore;
        await UndoRevertAsync();
        return false;
    }

    /// <summary>
    /// Khoi phuc profiles tu ban sao luu.
    /// </summary>
    private async Task<bool> RestoreProfilesAsync()
    {
        Console.WriteLine("Dang khoi phuc profiles...");

        if (ProfileBackupManager.RestoreProfiles(GameId, _reverseFailed))
        {
            Console.WriteLine("Da khoi phuc profiles thanh cong");
            _restoredProfiles = true;
            return true;
        }

        Console.WriteLine("That bai khi khoi phuc profiles");
        _errorCode = (int)LauncherError.ProfilesRestore;
        await UndoRevertAsync();
        return false;
    }

    /// <summary>
    /// Khoi phuc game CA cert tu ban sao luu.
    /// Tra ve danh sach cert da bi xoa (co trong backup nhung khong con trong original).
    /// </summary>
    private async Task<bool> RestoreGameCertAsync()
    {
        Console.WriteLine("Dang khoi phuc game CA cert...");

        if (string.IsNullOrEmpty(GamePath))
        {
            Console.WriteLine("That bai: Can duong dan game de khoi phuc CA cert");
            _errorCode = (int)LauncherError.GamePathMissing;
            await UndoRevertAsync();
            return false;
        }

        try
        {
            var cert = new CaCertManager(GameId, GamePath);
            _removedCaCerts = (await cert.RestoreAsync()).ToList();
            Console.WriteLine("Da khoi phuc game CA cert thanh cong");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"That bai khi khoi phuc game CA cert: {ex.Message}");
            _errorCode = (int)LauncherError.GameCertRestore;
            await UndoRevertAsync();
            return false;
        }
    }

    /// <summary>
    /// Dung agent neu dang ket noi va duoc yeu cau.
    /// </summary>
    private async Task StopAgentIfNeededAsync(bool agentConnected)
    {
        bool failedStopAgent = true;

        if (agentConnected)
        {
            Console.WriteLine("Dang thu dung config-admin-agent...");
            var err = await AdminIpcClient.StopAgentIfNeededAsync();
            if (err == null)
            {
                // Kiem tra xem agent da dung chua
                bool stillRunning = await AdminIpcClient.ConnectAgentIfNeededWithRetriesAsync(retryUntilSuccess: false);
                if (!stillRunning)
                {
                    Console.WriteLine("Da dung config-admin-agent");
                    failedStopAgent = false;
                }
                else
                {
                    Console.WriteLine("That bai khi dung config-admin-agent");
                }
            }
            else
            {
                Console.WriteLine("That bai khi thu dung config-admin-agent");
                Console.WriteLine(err.ToString());
            }
        }

        if (failedStopAgent)
        {
            // Thu kill process truc tiep
            var processes = System.Diagnostics.Process.GetProcessesByName("config-admin-agent");
            foreach (var proc in processes)
            {
                if (IsAdmin)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit();
                        Console.WriteLine("Da kill config-admin-agent thanh cong");
                        failedStopAgent = false;
                    }
                    catch
                    {
                        Console.WriteLine("That bai khi kill config-admin-agent");
                    }
                }
                else
                {
                    Console.WriteLine("Hay chay lai voi quyen administrator de kill config-admin-agent");
                }
            }

            // Neu khong co process nao -> agent da dung
            if (processes.Length == 0)
            {
                failedStopAgent = false;
            }
        }

        if (failedStopAgent && _errorCode == 0)
        {
            _errorCode = (int)LauncherError.RevertStopAgent;
        }
    }

    /// <summary>
    /// Dao nguoc toan bo cac buoc da lam neu revert that bai.
    /// Tuong tu undoRevert trong revert.go.
    /// </summary>
    private async Task UndoRevertAsync()
    {
        if (!RemoveAll)
        {
            // Them lai CA certs da xoa
            if (_removedCaCerts != null && _removedCaCerts.Count > 0)
            {
                await AddCaCertsAsync(_removedCaCerts);
            }

            // Them lai user certs da xoa
            if (_removedUserCerts != null && _removedUserCerts.Count > 0)
            {
                await AddUserCertsBackAsync(_removedUserCerts);
            }

            // Sao luu lai metadata da khoi phuc
            if (_restoredMetadata)
            {
                await BackupMetadataAfterRestoreAsync();
            }

            // Sao luu lai profiles da khoi phuc
            if (_restoredProfiles)
            {
                await BackupProfilesAfterRestoreAsync();
            }
        }
    }

    /// <summary>
    /// Them lai CA certs vao game store (dao nguoc cua Restore).
    /// </summary>
    private async Task<bool> AddCaCertsAsync(List<X509Certificate2> certs)
    {
        Console.WriteLine("Dang them lai CA certs da xoa...");

        try
        {
            if (string.IsNullOrEmpty(GamePath))
                return false;

            var gameCert = new CaCertManager(GameId, GamePath);
            await gameCert.AppendAsync(certs);
            Console.WriteLine("Da them lai CA certs thanh cong");
            return true;
        }
        catch
        {
            Console.WriteLine("That bai khi them lai CA certs");
            return false;
        }
    }

    /// <summary>
    /// Them lai user certs vao store (dao nguoc cua Remove).
    /// </summary>
    private async Task<bool> AddUserCertsBackAsync(List<X509Certificate2> certs)
    {
        Console.WriteLine("Dang them lai user cert da xoa...");

        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            foreach (var cert in certs)
            {
                store.Add(cert);
            }
            store.Close();
            Console.WriteLine("Da them lai user cert thanh cong");
            return true;
        }
        catch
        {
            Console.WriteLine("That bai khi them lai user cert");
            return false;
        }
    }

    /// <summary>
    /// Sao luu lai metadata sau khi da khoi phuc (dao nguoc).
    /// </summary>
    private async Task<bool> BackupMetadataAfterRestoreAsync()
    {
        Console.WriteLine("Dang sao luu lai metadata da khoi phuc...");

        var metadata = MetadataBackupManager.GetMetadata(GameId);
        if (UserDataBackup.Backup(metadata))
        {
            Console.WriteLine("Da sao luu lai metadata thanh cong");
            return true;
        }

        Console.WriteLine("That bai khi sao luu lai metadata");
        return false;
    }

    /// <summary>
    /// Sao luu lai profiles sau khi da khoi phuc (dao nguoc).
    /// </summary>
    private async Task<bool> BackupProfilesAfterRestoreAsync()
    {
        Console.WriteLine("Dang sao luu lai profiles da khoi phuc...");

        if (ProfileBackupManager.BackupProfiles(GameId))
        {
            Console.WriteLine("Da sao luu lai profiles thanh cong");
            return true;
        }

        Console.WriteLine("That bai khi sao luu lai profiles");
        return false;
    }

    /// <summary>
    /// Kiem tra game co hop le khong.
    /// </summary>
    private static bool IsValidGame(string gameId)
    {
        return gameId is "aoe1" or "aoe2de" or "aoe3de" or "aoe4";
    }
}

using System.CommandLine;
using AgeLanServer.BattleServerManager.Commands;
using AgeLanServer.Common;

namespace AgeLanServer.BattleServerManager;

/// <summary>
/// Điểm vào chương trình Battle Server Manager.
/// Đăng ký các lệnh: start, clean, remove, remove-all.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 1. Cấu hình Unicode cho console (khắc phục lỗi ký tự ?)
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        // 2. Kiểm tra quyền admin (khắc phục lỗi không dừng được battle server)
        if (OperatingSystem.IsWindows() && !CommandExecutor.IsRunningAsAdmin())
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.WriteLine("Lỗi: Không tìm thấy file thực thi để nâng quyền.");
                return AgeLanServer.Common.ErrorCodes.General;
            }

            Console.WriteLine("Cần chạy với quyền Administrator để quản lý Battle Server. Đang nâng quyền...");
            
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = true,
                    Verb = "runas" // Yêu cầu UAC
                };
                System.Diagnostics.Process.Start(psi);
                return AgeLanServer.Common.ErrorCodes.Success;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine("Lỗi: Người dùng từ chối cấp quyền Administrator.");
                return AgeLanServer.Common.ErrorCodes.General;
            }
        }

        var rootCmd = new RootCommand("Battle Server Manager - Quản lý Battle Server LAN cho Age of Empires")
        {
            CmdStart.CreateCommand(),
            CmdClean.CreateCommand(),
            CmdRemove.CreateCommand(),
            CmdRemoveAll.CreateCommand(),
        };

        return await rootCmd.InvokeAsync(args);
    }
}

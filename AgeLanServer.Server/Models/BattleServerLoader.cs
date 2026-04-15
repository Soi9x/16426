using AgeLanServer.Common;

namespace AgeLanServer.Server.Models;

/// <summary>
/// Trình nạp máy chủ battle cho mỗi game.
/// Quản lý kho máy chủ battle theo gameId.
/// </summary>
public static class BattleServerLoader
{
    /// <summary>Kho lưu trữ máy chủ battle theo gameId</summary>
    public static Dictionary<string, List<IBattleServer>> BattleServersStore { get; } = new();

    /// <summary>
    /// Khởi tạo máy chủ battle cho game.
    /// Nạp từ cấu hình và thêm vào kho.
    /// Age of Empires 4 và Age of Mythology bắt buộc phải có battle server.
    /// </summary>
    public static void InitializeBattleServers(string gameId, IEnumerable<IBattleServer> configBattleServers)
    {
        var battleServers = new List<IBattleServer>();

        foreach (var bs in configBattleServers)
            battleServers.Add(bs);

        // Thêm từ cấu hình tạm (nếu có)
        // var tmpBattleServers = BattleServerConfig.Configs(gameId, true);
        // battleServers.AddRange(tmpBattleServers);

        if ((gameId == AppConstants.GameAoE4 || gameId == AppConstants.GameAoM) && battleServers.Count == 0)
            throw new Exception($"Không tìm thấy battle server cho game {gameId}");

        BattleServersStore[gameId] = battleServers;
    }
}

using AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction.BuildGauntletLabyrinth;
using AgeLanServer.Server.Models.Playfab;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction;

/// <summary>
/// Kho lưu trữ tất cả hàm Cloud Script.
/// Tự động đăng ký các hàm khi khởi tạo.
/// </summary>
public static class CloudScriptFunctionStore
{
    /// <summary>Bản đồ tên -> hàm Cloud Script</summary>
    public static Dictionary<string, ICloudScriptFunction> Store { get; } = new();

    /// <summary>
    /// Khởi tạo kho hàm Cloud Script.
    /// Đăng ký tất cả các hàm: AwardMissionRewards, StartGauntletMission, BuildGauntletLabyrinth.
    /// </summary>
    public static void Initialize()
    {
        var fns = new ICloudScriptFunction[]
        {
            AwardMissionRewardsFactory.Create(),
            StartGauntletMissionFactory.Create(),
            BuildGauntletLabyrinthFactory.Create()
        };

        foreach (var fn in fns)
            Store[fn.Name()] = fn;
    }
}

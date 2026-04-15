using AgeLanServer.Server.Models.Athens.User;
using AgeLanServer.Server.Models.Playfab;
using AgeLanServer.Server.Models;

namespace AgeLanServer.Server.Models.Athens.Routes.Playfab.CloudScriptFunction;

/// <summary>
/// Tham số bắt đầu nhiệm vụ Gauntlet.
/// </summary>
public class StartGauntletMissionParameters
{
    public string CdnPath { get; set; } = null!;
    public string MissionId { get; set; } = null!;
}

/// <summary>
/// Kết quả bắt đầu nhiệm vụ Gauntlet (rỗng).
/// </summary>
public class StartGauntletMissionResult { }

/// <summary>
/// Hàm Cloud Script: Bắt đầu nhiệm vụ Gauntlet.
/// Cập nhật trạng thái nhiệm vụ đang chơi của người dùng.
/// </summary>
public class StartGauntletMissionFunction : ISpecificCloudScriptFunction<StartGauntletMissionParameters, StartGauntletMissionResult?>
{
    public string Name() => "StartGauntletMission";

    public StartGauntletMissionResult? RunTyped(IGame game, IUser user, StartGauntletMissionParameters parameters)
    {
        var athensUser = (AthensUser)user;
        athensUser.PlayfabData.WithReadWrite(data =>
        {
            if (data?.Challenge.Progress is not { } progress)
                throw new Exception("No progress found");

            if (progress.Value?.MissionBeingPlayedRightNow != parameters.MissionId)
            {
                progress.Update(p =>
                {
                    if (p != null)
                        p.MissionBeingPlayedRightNow = parameters.MissionId;
                });
                data.DataVersion++;
            }

            return Task.CompletedTask;
        });

        return null;
    }
}

/// <summary>
/// Factory tạo StartGauntletMissionFunction.
/// </summary>
public static class StartGauntletMissionFactory
{
    public static CloudScriptFunctionBase<StartGauntletMissionParameters, StartGauntletMissionResult?> Create()
    {
        return new CloudScriptFunctionBase<StartGauntletMissionParameters, StartGauntletMissionResult?>(
            new StartGauntletMissionFunction());
    }
}

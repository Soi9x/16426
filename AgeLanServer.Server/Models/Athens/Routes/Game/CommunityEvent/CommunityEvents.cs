namespace AgeLanServer.Server.Models.Athens.Routes.Game.CommunityEvent;

/// <summary>
/// Helper quản lý sự kiện cộng đồng.
/// Tạo sự kiện hàng ngày và hàng tháng tự động.
/// </summary>
public static class CommunityEventsHelper
{
    private const long DayDurationSeconds = 24 * 3600;
    private const string LeaderboardName = "skirmish_plus_leaderboard_38 ";

    private static DateTime _dailyCelestialChallengeStart;
    private static DailyCelestialChallenge _eventMetadata = null!;

    /// <summary>
    /// Khởi tạo sự kiện cộng đồng.
    /// Thiết lập thời gian bắt đầu và metadata cho thử thách thiên thể.
    /// </summary>
    public static void Initialize()
    {
        // Múi giờ America/Los_Angeles
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        _dailyCelestialChallengeStart = new DateTime(2025, 3, 4, 9, 0, 0, DateTimeKind.Local);

        var score = new DailyCelestialChallengeScore { A = 100000, B = 19681, P = 1.5 };
        _eventMetadata = new DailyCelestialChallenge
        {
            Plus = new DailyCelestialChallengePlus { Score = score, ScoreCalcParams = score },
            Leaderboard = new DailyCelestialChallengeLeaderboard
            {
                Values = new List<DailyCelestialChallengeLeaderboardValue>
                {
                    new()
                    {
                        Name = LeaderboardName,
                        ScoringType = 3,
                        VisibleToPublic = true,
                        MapEntries = new List<DailyCelestialChallengeLeaderboardValueMapEntry>
                        {
                            new() { MatchType = 60, Race = -1, StatGroupType = 1 }
                        }
                    }
                },
                PointUpdate = new DailyCelestialChallengeLeaderboardPointUpdate
                {
                    MaxWinPts = 100000,
                    WinDifferenceToApply = int.MaxValue,
                    LoseDifferenceToApply = int.MaxValue
                }
            }
        };
    }

    /// <summary>Tạo sự kiện hàng tháng</summary>
    private static List<CommunityEvent> GenerateMonthlyEvents()
    {
        var events = new List<CommunityEvent>();
        var now = DateTime.Now;
        var currentEvent = new DateTime(now.Year, now.Month, 1, 7, 55, 0);

        for (int months = 0; months < 2; months++)
        {
            var eventStart = currentEvent.AddMonths(months);
            var eventEnd = eventStart.AddMonths(months).AddHours(-1);
            var expiryTime = eventStart.AddYears(1);
            var eventName = $"{eventStart.AddMonths(months).ToString("MMMM").ToLower()}_pantheon_pinup";

            var ev = new CommunityEvent
            {
                Id = (ulong)months,
                Name = eventName,
                Start = eventStart,
                End = eventEnd,
                ExpiryTime = expiryTime,
                CustomData = "",
                EventState = 2
            };

            for (int s = 1; s < 17; s++)
            {
                ev.Leaderboards.Add(new EventLeaderBoard
                {
                    Id = (ulong)s,
                    Name = $"{ev.Name}_s{s}",
                    IsRanked = true,
                    ScoringType = 3,
                    Maps = new List<EventLeaderboardMap>
                    {
                        new() { MatchtypeId = 60, StatgroupType = 1, CivilizationId = -1 }
                    }
                });
            }

            events.Add(ev);
        }

        return events;
    }

    /// <summary>Tạo sự kiện thử thách hàng ngày</summary>
    private static List<CommunityEvent> GenerateDailyChallengeEvents()
    {
        var events = new List<CommunityEvent>();
        var now = DateTime.Now;
        var timeSince = now - _dailyCelestialChallengeStart;
        var daysSince = (long)timeSince.TotalDays;
        var currentEventStart = _dailyCelestialChallengeStart.AddDays(daysSince);
        var daysSinceIt = (ulong)daysSince;

        for (int days = 0; days < 3; days++)
        {
            var eventStart = currentEventStart.AddDays(days);
            var eventEnd = eventStart.AddDays(1).AddSeconds(-1);
            var expiryTime = eventEnd.AddDays(7);
            var id = daysSinceIt + (ulong)days;
            var eventName = $"skirmish_plus_{id}";

            events.Add(new CommunityEvent
            {
                Id = id,
                Name = eventName,
                Start = eventStart,
                End = eventEnd,
                ExpiryTime = expiryTime,
                CustomData = _eventMetadata,
                EventState = 2,
                Leaderboards = new List<EventLeaderBoard>
                {
                    new()
                    {
                        Id = id,
                        Name = $"{eventName}_{LeaderboardName}",
                        IsRanked = true,
                        ScoringType = 3,
                        Maps = new List<EventLeaderboardMap>
                        {
                            new() { MatchtypeId = 60, StatgroupType = 1, CivilizationId = -1 }
                        }
                    }
                }
            });
        }

        return events;
    }

    /// <summary>
    /// Mã hóa tất cả sự kiện cộng đồng.
    /// Trả về mảng chứa sự kiện, bảng xếp hạng và bản đồ.
    /// </summary>
    public static object[] CommunityEventsEncoded()
    {
        var events = GenerateDailyChallengeEvents();
        events.AddRange(GenerateMonthlyEvents());

        var eventsEncoded = new List<object[]>();
        var leaderboardsEncoded = new List<object[]>();
        var leaderboardMapsEncoded = new List<object[]>();

        foreach (var ev in events)
        {
            var marshalCustomData = !ev.Name.Contains("pantheon");
            eventsEncoded.Add(ev.Encode(marshalCustomData));
            leaderboardsEncoded.AddRange(ev.EncodeLeaderboards());
            leaderboardMapsEncoded.AddRange(ev.EncodeLeaderboardsMaps());
        }

        return new object[]
        {
            0,
            eventsEncoded.ToArray(),
            Array.Empty<object>(),
            Array.Empty<object>(),
            leaderboardsEncoded.ToArray(),
            leaderboardMapsEncoded.ToArray(),
            Array.Empty<object>()
        };
    }
}

namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure time math for the live-ops calendar (social/economy plan §0.2). The server passes
/// <see cref="System.DateTimeOffset"/> UTC (never the device clock — that would be a free exploit); this
/// derives the season / week / day buckets that drive every reset (dailies, league promo/demo, seasons).
/// </summary>
public sealed class SeasonClock
{
    /// <summary>Anchor: all buckets count from this instant (UTC). A Monday 00:00 is a natural week start.</summary>
    public DateTimeOffset Epoch { get; init; } = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero); // Mon 2026-01-05
    public int WeeksPerSeason  { get; init; } = 4;

    /// <summary>0-based number of whole weeks since the epoch.</summary>
    public int WeekId(DateTimeOffset now)
    {
        var days = (int)System.Math.Floor((now.UtcDateTime - Epoch.UtcDateTime).TotalDays);
        if (days < 0) days = 0;
        return days / 7;
    }

    /// <summary>0-based season index (a season spans <see cref="WeeksPerSeason"/> weeks).</summary>
    public int SeasonId(DateTimeOffset now) => WeekId(now) / WeeksPerSeason;

    /// <summary>0-based day index since the epoch — the daily-reset bucket (UTC midnight boundaries).</summary>
    public int DayId(DateTimeOffset now)
    {
        var days = (int)System.Math.Floor((now.UtcDateTime - Epoch.UtcDateTime).TotalDays);
        return days < 0 ? 0 : days;
    }

    /// <summary>Which week (0-based) within the current season.</summary>
    public int WeekOfSeason(DateTimeOffset now) => WeekId(now) % WeeksPerSeason;

    /// <summary>UTC instant the current week ends (= next week's start) — drives countdowns.</summary>
    public DateTimeOffset WeekEndsAt(DateTimeOffset now)
        => Epoch.AddDays((WeekId(now) + 1) * 7);

    /// <summary>UTC instant the current day ends.</summary>
    public DateTimeOffset DayEndsAt(DateTimeOffset now)
        => Epoch.AddDays(DayId(now) + 1);

    /// <summary>UTC instant the current season ends.</summary>
    public DateTimeOffset SeasonEndsAt(DateTimeOffset now)
        => Epoch.AddDays((SeasonId(now) + 1) * WeeksPerSeason * 7);

    public static SeasonClock Default { get; } = new();
}

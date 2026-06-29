using System.Text.Json;
using Arkanoid.Core.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Live-ops calendar endpoints — the single server-owned time source for all client countdowns.</summary>
public static class MetaEndpoints
{
    public static void Map(WebApplication app, SeasonClock clock, JsonSerializerOptions jsonOpts)
    {
        // GET /meta/clock → the current season/week/day buckets + when each ends (UTC).
        app.MapGet("/meta/clock", () =>
        {
            var now = DateTimeOffset.UtcNow;
            return Results.Json(new
            {
                now          = now,
                seasonId     = clock.SeasonId(now),
                weekId       = clock.WeekId(now),
                dayId        = clock.DayId(now),
                weekOfSeason = clock.WeekOfSeason(now),
                dayEndsAt    = clock.DayEndsAt(now),
                weekEndsAt   = clock.WeekEndsAt(now),
                seasonEndsAt = clock.SeasonEndsAt(now),
            }, jsonOpts);
        });
    }
}

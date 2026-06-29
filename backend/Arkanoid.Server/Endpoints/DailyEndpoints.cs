using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Daily missions (plan §A.2): 3/day from a weekly pool, progress recorded server-side at
/// battle end, claimed here for Gems + Card Dust with a 7-day streak chest.</summary>
public static class DailyEndpoints
{
    private static readonly bool Cheats =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static void Map(WebApplication app, MissionCatalog catalog, SeasonClock clock,
        IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        app.MapGet("/daily", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var now = DateTimeOffset.UtcNow;
            DailyService.EnsureToday(p, catalog, clock.DayId(now), clock.WeekId(now));
            profileStore.Save(p, pid);

            var missions = p.Daily.Missions.Select(ms =>
            {
                catalog.TryGet(ms.Id, out var def);
                return new
                {
                    id = ms.Id, name = def?.Name ?? ms.Id, metric = def?.Metric ?? "",
                    target = def?.Target ?? 0, progress = ms.Progress, claimed = ms.Claimed,
                    rewardGems = def?.RewardGems ?? 0, rewardCardDust = def?.RewardCardDust ?? 0,
                    complete = def != null && ms.Progress >= def.Target,
                };
            });

            return Results.Json(new
            {
                missions, streak = p.Daily.Streak, streakTarget = DailyService.StreakTarget,
                dayEndsAt = clock.DayEndsAt(now),
                gems = p.Crystals, cardDust = p.CardDust,
            }, jsonOpts);
        });

        app.MapPost("/daily/claim", (HttpContext ctx) =>
        {
            var id  = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var now = DateTimeOffset.UtcNow;
            var res = id == null
                ? new DailyClaimResult { Ok = false }
                : DailyService.Claim(p, catalog, clock.DayId(now), clock.WeekId(now), id);
            if (res.Ok) profileStore.Save(p, pid);
            // gems/cardDust here are the GRANTED amounts (the flash shows "+N"); balances are re-read via GET.
            return Results.Json(new { ok = res.Ok, gems = res.Gems, cardDust = res.CardDust,
                                      streakBonus = res.StreakBonus, streak = res.Streak }, jsonOpts);
        });

        // Cheat/dev: inject progress so the daily flow is testable without playing a whole battle.
        app.MapPost("/daily/record", (HttpContext ctx) =>
        {
            if (!Cheats) return Results.NotFound();
            var metric = ctx.Request.Query["metric"].FirstOrDefault() ?? "blocks_destroyed";
            int.TryParse(ctx.Request.Query["amount"].FirstOrDefault(), out var amount);
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var now = DateTimeOffset.UtcNow;
            DailyService.Record(p, catalog, clock.DayId(now), clock.WeekId(now), metric, amount);
            profileStore.Save(p, pid);
            return Results.Json(new { ok = true }, jsonOpts);
        });
    }
}

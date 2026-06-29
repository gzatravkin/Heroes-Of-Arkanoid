using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>
/// Leaderboard + league endpoints over the swappable <see cref="ILeaderboardStore"/> (SQLite locally).
/// Scores from server-run battles are authoritative; this also accepts direct client submissions, which
/// are plausibility-checked and silently shadow-banned if impossible (social plan §3.3).
/// </summary>
public static class LeaderboardEndpoints
{
    public static void Map(WebApplication app, ILeaderboardStore store, SeasonClock clock,
        IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        // GET /lb/league?board=trial → resolve any finished weeks (grant rewards), then this week's cohort.
        app.MapGet("/lb/league", (HttpContext ctx) =>
        {
            var board = ctx.Request.Query["board"].FirstOrDefault() ?? "trial";
            var pid   = ProfileNs.From(ctx);
            var now   = DateTimeOffset.UtcNow;
            int week  = clock.WeekId(now);

            AutoResolve(store, profileStore, pid, board, week);

            var cohort = LeaderboardService.GenerateCohort(store, pid, pid, board, week.ToString());
            var me     = cohort.First(e => e.IsMe);
            var state  = store.GetPlayerState(pid);
            return Results.Json(new
            {
                board, weekId = week, weekEndsAt = clock.WeekEndsAt(now),
                tier = state.Tier, tierName = ((LeagueTier)state.Tier).ToString(),
                myRank = me.Rank, myScore = me.Score, cohortSize = cohort.Count,
                promoteTop = LeaderboardService.PromoteTop, demoteBottom = LeaderboardService.DemoteBottom,
                entries = cohort,
            }, jsonOpts);
        });

        // GET /lb/standing?board=trial → compact standing only.
        app.MapGet("/lb/standing", (HttpContext ctx) =>
        {
            var board = ctx.Request.Query["board"].FirstOrDefault() ?? "trial";
            var pid   = ProfileNs.From(ctx);
            int week  = clock.WeekId(DateTimeOffset.UtcNow);
            var cohort = LeaderboardService.GenerateCohort(store, pid, pid, board, week.ToString());
            var me = cohort.First(e => e.IsMe);
            var state = store.GetPlayerState(pid);
            return Results.Json(new { board, weekId = week, tier = state.Tier, rank = me.Rank, score = me.Score }, jsonOpts);
        });

        // POST /lb/submit?board=trial&score=N → submit a score (client-claimed path; verified + shadow-banned).
        app.MapPost("/lb/submit", (HttpContext ctx) =>
        {
            var board = ctx.Request.Query["board"].FirstOrDefault() ?? "trial";
            if (!int.TryParse(ctx.Request.Query["score"].FirstOrDefault(), out var score))
                return Results.BadRequest("score required");
            var pid  = ProfileNs.From(ctx);
            int week = clock.WeekId(DateTimeOffset.UtcNow);
            bool accepted = LeaderboardService.Submit(store, pid, pid, board, week.ToString(), score);
            // Never reveal shadow state: always report success-shaped.
            return Results.Json(new { ok = true, accepted }, jsonOpts);
        });

        // POST /lb/resolve?board=trial → force-resolve finished weeks now (also auto-runs on /lb/league).
        app.MapPost("/lb/resolve", (HttpContext ctx) =>
        {
            var board = ctx.Request.Query["board"].FirstOrDefault() ?? "trial";
            var pid   = ProfileNs.From(ctx);
            int week  = clock.WeekId(DateTimeOffset.UtcNow);
            var res   = AutoResolve(store, profileStore, pid, board, week);
            return Results.Json(new { resolved = res?.Resolved ?? false, resolution = res }, jsonOpts);
        });
    }

    /// <summary>Resolve the most-recent finished week (week-1) once, granting placement rewards to the
    /// profile. Idempotent via <c>PlayerLeagueState.LastResolvedWeek</c>.</summary>
    private static WeekResolution? AutoResolve(ILeaderboardStore store, IProfileStore profileStore,
        string pid, string board, int currentWeek)
    {
        int finished = currentWeek - 1;
        if (finished < 0) return null;
        var state = store.GetPlayerState(pid);
        if (state.LastResolvedWeek >= finished) return null;

        var res = LeaderboardService.ResolveWeek(store, pid, pid, board, finished);
        if (res.Resolved)
        {
            var profile = profileStore.Load(pid);
            Wallet.Add(profile, Currency.Medals,   res.RewardMedals);
            Wallet.Add(profile, Currency.Souls, res.RewardGems);
            Wallet.Add(profile, Currency.Sparks, res.RewardCardDust);
            profileStore.Save(profile, pid);
        }
        return res;
    }
}

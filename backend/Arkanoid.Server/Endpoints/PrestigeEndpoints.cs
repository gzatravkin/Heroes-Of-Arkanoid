using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Prestige campaign loop (plan §B.1): ascend into New Game+ and surface the prestige board.</summary>
public static class PrestigeEndpoints
{
    public static void Map(WebApplication app, IProfileStore profileStore,
        ILeaderboardStore leaderboard, JsonSerializerOptions jsonOpts)
    {
        // GET /campaign/prestige → tier, ascend eligibility, and the player's prestige standing.
        // Also (re)submits the current prestige score so within-loop progress climbs the board.
        app.MapGet("/campaign/prestige", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            int score = PrestigeService.PrestigeScore(p.PrestigeTier, p.CompletedLevels.Count);
            LeaderboardService.Submit(leaderboard, pid, pid, PrestigeService.BoardId, "all", score);
            var cohort = LeaderboardService.GenerateCohort(leaderboard, pid, pid, PrestigeService.BoardId, "all");
            var me = cohort.First(e => e.IsMe);
            return Results.Json(new { tier = p.PrestigeTier, canAscend = PrestigeService.CanAscend(p),
                                      score, rank = me.Rank }, jsonOpts);
        });

        // POST /campaign/ascend → wipe campaign progress, bump tier, submit new prestige score.
        app.MapPost("/campaign/ascend", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            if (!PrestigeService.CanAscend(p))
                return Results.Json(new { ok = false, tier = p.PrestigeTier }, jsonOpts);
            int tier = PrestigeService.Ascend(p);
            profileStore.Save(p, pid);
            LeaderboardService.Submit(leaderboard, pid, pid, PrestigeService.BoardId, "all",
                PrestigeService.PrestigeScore(tier, 0));
            return Results.Json(new { ok = true, tier }, jsonOpts);
        });
    }
}

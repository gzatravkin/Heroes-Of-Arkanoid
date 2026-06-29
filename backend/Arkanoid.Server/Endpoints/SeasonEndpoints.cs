using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Season Festival (plan §C): the reward track, the live event, and their leaderboards.</summary>
public static class SeasonEndpoints
{
    private static readonly bool Cheats =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static void Map(WebApplication app, SeasonCatalog seasons, EventCatalog events,
        SeasonClock clock, ILeaderboardStore leaderboard, IProfileStore profileStore,
        CardCatalog cards, JsonSerializerOptions jsonOpts)
    {
        app.MapGet("/season", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var now = DateTimeOffset.UtcNow;
            int seasonId = clock.SeasonId(now), weekId = clock.WeekId(now);
            SeasonService.EnsureSeason(p, seasonId);
            EventService.EnsureEvent(p, weekId);
            profileStore.Save(p, pid);

            // Keep ranks fresh.
            LeaderboardService.Submit(leaderboard, pid, pid, SeasonService.BoardId, seasonId.ToString(), SeasonService.SeasonScore(p));
            var ev = events.Current(weekId);
            if (ev != null) LeaderboardService.Submit(leaderboard, pid, pid, EventService.BoardPrefix + ev.Id, weekId.ToString(), p.Season.EventTokens);

            var track = seasons.Track.Select(t => new
            {
                t.Tier, t.Tokens, t.RewardGems, t.RewardCardDust, t.RewardModuleCores,
                claimed = p.Season.ClaimedTiers.Contains(t.Tier),
                claimable = !p.Season.ClaimedTiers.Contains(t.Tier) && p.Season.Tokens >= t.Tokens,
            });

            int seasonRank = LeaderboardService.GenerateCohort(leaderboard, pid, pid, SeasonService.BoardId, seasonId.ToString()).First(e => e.IsMe).Rank;

            return Results.Json(new
            {
                seasonId, theme = seasons.ThemeFor(seasonId), tokens = p.Season.Tokens,
                seasonEndsAt = clock.SeasonEndsAt(now), weekEndsAt = clock.WeekEndsAt(now),
                track, seasonRank,
                ev = ev == null ? null : new
                {
                    ev.Id, ev.Name, ev.Effect, ev.Magnitude, ev.MilestoneTokens,
                    ev.RewardModuleCores, ev.RewardGems,
                    tokens = p.Season.EventTokens, claimed = p.Season.EventClaimed,
                    claimable = !p.Season.EventClaimed && p.Season.EventTokens >= ev.MilestoneTokens,
                },
            }, jsonOpts);
        });

        app.MapPost("/season/claim-tier", (HttpContext ctx) =>
        {
            int.TryParse(ctx.Request.Query["tier"].FirstOrDefault(), out var tier);
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            int seasonId = clock.SeasonId(DateTimeOffset.UtcNow);
            var res = SeasonService.ClaimTier(p, seasons, seasonId, tier);
            if (res.Ok) profileStore.Save(p, pid);
            return Results.Json(new { ok = res.Ok, res.Gems, res.CardDust, res.ModuleCores }, jsonOpts);
        });

        app.MapPost("/event/claim", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            int weekId = clock.WeekId(DateTimeOffset.UtcNow);
            var ev = events.Current(weekId);
            var res = ev == null ? new SeasonClaimResult { Ok = false } : EventService.ClaimMilestone(p, ev, weekId);
            if (res.Ok) profileStore.Save(p, pid);
            return Results.Json(new { ok = res.Ok, res.Gems, res.ModuleCores }, jsonOpts);
        });

        // Cheat/dev: inject tokens so the track + event are testable without grinding battles.
        app.MapPost("/season/grant", (HttpContext ctx) =>
        {
            if (!Cheats) return Results.NotFound();
            int.TryParse(ctx.Request.Query["tokens"].FirstOrDefault(), out var tokens);
            int.TryParse(ctx.Request.Query["event"].FirstOrDefault(), out var eventTokens);
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            var now = DateTimeOffset.UtcNow;
            SeasonService.AddTokens(p, clock.SeasonId(now), tokens);
            EventService.AddTokens(p, clock.WeekId(now), eventTokens);
            profileStore.Save(p, pid);
            return Results.Json(new { ok = true }, jsonOpts);
        });

        // ── Season Shop (economy rework §7): exchange Season Tokens for coins / bonus rolls (no skins) ──
        app.MapGet("/season/shop", (HttpContext ctx) =>
        {
            var p = profileStore.Load(ProfileNs.From(ctx));
            return Results.Json(new { tokens = p.Season.Tokens, offers = SeasonShopService.Offers }, jsonOpts);
        });

        app.MapPost("/season/shop/buy", (HttpContext ctx) =>
        {
            var offerId = ctx.Request.Query["offer"].FirstOrDefault();
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            var offer = offerId == null ? null : SeasonShopService.Get(offerId);
            if (offer == null) return Results.Json(new { ok = false, reason = "unknown_offer" }, jsonOpts);

            bool ok; object? roll = null;
            if (offer.Kind.StartsWith("roll_"))
            {
                if (p.Season.Tokens < offer.Cost) ok = false;
                else
                {
                    p.Season.Tokens -= offer.Cost;
                    var rng = new Arkanoid.Core.Sim.Rng(unchecked((int)(DateTimeOffset.UtcNow.Ticks & 0x7fffffff)));
                    roll = offer.Kind == "roll_card"
                        ? RollService.RollCard(p, cards, rng)
                        : RollService.RollSpell(p, CharacterCatalog.Default, rng);
                    ok = true;
                }
            }
            else ok = SeasonShopService.TryBuyCurrency(p, offerId!);

            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, tokens = p.Season.Tokens,
                sparks = p.Sparks, souls = p.Souls, insight = p.Insight, roll }, jsonOpts);
        });
    }
}

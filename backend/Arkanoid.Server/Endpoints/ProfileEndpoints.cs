using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class ProfileEndpoints
{
    public static void Map(WebApplication app, ProfileStore profileStore,
        CampaignCatalog campaignCatalog, ProgressionConfig progressionConfig,
        JsonSerializerOptions jsonOpts)
    {
        // GET /profile → current profile
        app.MapGet("/profile", () =>
        {
            var profile = profileStore.Load();
            return Results.Json(profile, jsonOpts);
        });

        // GET /campaign → nodes with unlocked/completed flags
        app.MapGet("/campaign", () =>
        {
            var profile   = profileStore.Load();
            var completed = new HashSet<string>(profile.CompletedLevels);
            var nodes = campaignCatalog.Nodes.Select(n => new
            {
                n.Id, n.Label, n.Biome, n.X, n.Y,
                Unlocked  = campaignCatalog.IsUnlocked(n, completed),
                Completed = completed.Contains(n.Id),
            });
            return Results.Json(new { nodes }, jsonOpts);
        });

        // POST /complete?level=<id>[&treasureBonus=<n>] → grant completion reward, save, return {profile, reward}
        // treasureBonus: extra crystals from equipped treasure items (passed by the frontend from the snapshot).
        app.MapPost("/complete", (HttpContext ctx) =>
        {
            var levelId = ctx.Request.Query["level"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(levelId))
                return Results.BadRequest("level query parameter required");

            var treasureBonus = 0;
            if (int.TryParse(ctx.Request.Query["treasureBonus"].FirstOrDefault(), out var tb) && tb > 0)
                treasureBonus = tb;

            var profile = profileStore.Load();
            var reward  = Rewards.GrantLevelCompletion(profile, levelId, progressionConfig, treasureBonus);
            profileStore.Save(profile);
            return Results.Json(new { profile, reward }, jsonOpts);
        });

        // POST /upgrade?spell=<id> → upgrade spell, save, return {ok, profile}
        app.MapPost("/upgrade", (HttpContext ctx) =>
        {
            var spellId = ctx.Request.Query["spell"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(spellId))
                return Results.BadRequest("spell query parameter required");

            var profile = profileStore.Load();
            var ok      = Upgrades.TryUpgradeSpell(profile, spellId, progressionConfig);
            if (ok) profileStore.Save(profile);
            return Results.Json(new { ok, profile }, jsonOpts);
        });

        // POST /reset → reset to new default, save, return profile
        app.MapPost("/reset", () =>
        {
            var profile = Profile.NewDefault();
            profileStore.Save(profile);
            return Results.Json(profile, jsonOpts);
        });
    }
}

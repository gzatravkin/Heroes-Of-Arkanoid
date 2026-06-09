using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class ProfileEndpoints
{
    public static void Map(WebApplication app, ProfileStore profileStore,
        CampaignCatalog campaignCatalog, DungeonCatalog dungeonCatalog,
        ProgressionConfig progressionConfig, JsonSerializerOptions jsonOpts)
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

            // A cleared campaign node may tear open a Rift — the opt-in dungeon entry.
            // mode: "force"/"none"/"roll" (default "none" so direct API callers are unaffected).
            var riftMode = ctx.Request.Query["rift"].FirstOrDefault();
            var riftSeed = unchecked((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff) ^ levelId.GetHashCode());
            var rift = RiftService.Roll(riftMode, progressionConfig.RiftChance, levelId, riftSeed, dungeonCatalog);

            return Results.Json(new { profile, reward, rift }, jsonOpts);
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

        // POST /achievement/unlock?id=<achievementId>
        // Records an achievement unlock. Idempotent — no-ops if already unlocked.
        app.MapPost("/achievement/unlock", (HttpContext ctx) =>
        {
            var achievementId = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(achievementId))
                return Results.BadRequest("id query parameter required");

            var profile = profileStore.Load();
            if (!profile.Achievements.Contains(achievementId))
            {
                profile.Achievements.Add(achievementId);
                profileStore.Save(profile);
            }
            return Results.Json(new { ok = true, achievements = profile.Achievements }, jsonOpts);
        });

        // POST /tutorial/seen → marks tutorial as seen
        app.MapPost("/tutorial/seen", () =>
        {
            var profile = profileStore.Load();
            profile.TutorialSeen = true;
            profileStore.Save(profile);
            return Results.Json(new { ok = true }, jsonOpts);
        });
    }
}

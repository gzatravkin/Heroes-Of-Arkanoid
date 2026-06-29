using System.Linq;
using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class CharacterEndpoints
{
    /// <summary>Shard cost to permanently unlock a character (docs/04 §5 meta-progression).</summary>
    public const int CharacterUnlockShardCost = ProgressionConfig.CharacterUnlockShardCost;

    public static void Map(WebApplication app, CharacterCatalog characterCatalog,
        IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        // GET /characters → catalog + selection state from profile
        app.MapGet("/characters", (HttpContext ctx) =>
        {
            var profile = profileStore.Load(ProfileNs.From(ctx));
            // Per-hero ★ progress so the Heroes screen can show stars + the MANUAL ascend state
            // (banked duplicate pips vs the cost of the next ★). 2026-06-15.
            var progress = characterCatalog.All.ToDictionary(c => c.Id, c =>
            {
                var hp   = profile.HeroProgress.TryGetValue(c.Id, out var h) ? h : new HeroProgress();
                int next = hp.Stars < StatResolver.MaxStars ? StatResolver.StarTokenCost(hp.Stars + 1) : 0;
                return new
                {
                    stars      = hp.Stars,
                    pips       = hp.AscendPips,
                    maxStars   = StatResolver.MaxStars,
                    ascendCost = next,
                    canAscend  = next > 0 && hp.AscendPips >= next,
                };
            });
            return Results.Json(new
            {
                characters    = characterCatalog.All,
                progress,
                neutralSpells = characterCatalog.NeutralSpells, // class-less pool spells (Recall, Slow Time)
                selected      = profile.SelectedCharacter,
                unlocked      = profile.UnlockedCharacters,
                shards        = profile.Shards,
                unlockCost    = CharacterUnlockShardCost,
            }, jsonOpts);
        });

        // POST /character/unlock?id=<id> → spend shards to unlock a locked character (docs/04 §5).
        app.MapPost("/character/unlock", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");
            try { characterCatalog.Get(id); }
            catch (KeyNotFoundException) { return Results.NotFound($"Character '{id}' not found"); }

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            bool ok = false;
            if (!profile.UnlockedCharacters.Contains(id) && profile.Shards >= CharacterUnlockShardCost)
            {
                profile.Shards -= CharacterUnlockShardCost;
                profile.UnlockedCharacters.Add(id);
                profileStore.Save(profile, pid);
                ok = true;
            }
            return Results.Json(new { ok, shards = profile.Shards, unlocked = profile.UnlockedCharacters }, jsonOpts);
        });

        // POST /character/select?id=<id> → set selected character, save, return profile
        app.MapPost("/character/select", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            // Validate id exists in catalog
            try { characterCatalog.Get(id); }
            catch (KeyNotFoundException) { return Results.NotFound($"Character '{id}' not found"); }

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            profile.SelectedCharacter = id;
            profileStore.Save(profile, pid);
            return Results.Json(profile, jsonOpts);
        });
    }
}

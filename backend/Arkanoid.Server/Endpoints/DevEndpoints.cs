using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Dev/cheat endpoints — available in Development mode or when ARKANOID_CHEATS=1.</summary>
public static class DevEndpoints
{
    private static readonly bool Cheats =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static void Map(WebApplication app, CampaignCatalog campaignCatalog,
        CharacterCatalog characterCatalog, IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        // POST /dev/unlock-all
        // Grants all spells, completes all campaign levels, unlocks all heroes, maxes spell slots.
        // Idempotent — safe to call multiple times.
        app.MapPost("/dev/unlock-all", (HttpContext ctx) =>
        {
            if (!Cheats) return Results.NotFound();

            var pid = ProfileNs.From(ctx);
            var p = profileStore.Load(pid);

            foreach (var node in campaignCatalog.Nodes)
                if (!p.CompletedLevels.Contains(node.Id))
                    p.CompletedLevels.Add(node.Id);

            foreach (var charDef in characterCatalog.All)
                foreach (var spell in charDef.Spells)
                    if (!p.SpellLevels.ContainsKey(spell.Id))
                        p.SpellLevels[spell.Id] = 1;

            foreach (var spell in characterCatalog.NeutralSpells)
                if (!p.SpellLevels.ContainsKey(spell.Id))
                    p.SpellLevels[spell.Id] = 1;

            foreach (var charDef in characterCatalog.All)
            {
                if (!p.UnlockedCharacters.Contains(charDef.Id))
                    p.UnlockedCharacters.Add(charDef.Id);
                if (!p.HeroPool.Contains(charDef.Id))
                    p.HeroPool.Add(charDef.Id);
            }

            p.UnlockedSpellSlots = 5;

            profileStore.Save(p, pid);
            return Results.Json(new { ok = true, profile = p }, jsonOpts);
        });
    }
}

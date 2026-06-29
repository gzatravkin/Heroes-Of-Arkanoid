using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>
/// Spell loadout endpoints — the equip half of the "signature + drafted pool" model (docs/04 §3).
/// Mirrors ItemEndpoints (own/equip/cap) but operates per selected character: slot 0 is the
/// signature (locked), the rest are drafted from the shared pool the player owns.
/// </summary>
public static class SpellEndpoints
{
    public static void Map(WebApplication app, CharacterCatalog characterCatalog,
        IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        // GET /spells → for the selected character: full pool (display + owned/equipped flags),
        // the ordered loadout, the signature id, and how many slots are unlocked.
        app.MapGet("/spells", (HttpContext ctx) =>
        {
            var profile = profileStore.Load(ProfileNs.From(ctx));
            var charId  = profile.SelectedCharacter;
            characterCatalog.TryGet(charId, out var c);
            var sig      = c?.SignatureId ?? "";
            var loadout  = Loadouts.Resolve(profile, characterCatalog, charId);
            var owned    = new HashSet<string>(Loadouts.OwnedFor(profile, characterCatalog, charId));
            var slots    = Loadouts.SlotCount(profile);

            // Pool entries the player can browse: signature first, then every non-signature spell.
            var ids = new List<string> { sig };
            ids.AddRange(characterCatalog.Pool());
            var pool = ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().Select(id =>
            {
                var d = characterCatalog.DisplayOf(id);
                return new
                {
                    id,
                    name      = d?.Name ?? id,
                    icon      = d?.Icon ?? "",
                    manaCost  = d?.ManaCost ?? 0,
                    desc      = d?.Desc ?? "",
                    level     = profile.SpellLevels.GetValueOrDefault(id, owned.Contains(id) ? 1 : 0),
                    copies    = profile.SpellCopies.GetValueOrDefault(id),
                    signature = id == sig,
                    owned     = owned.Contains(id),
                    equipped  = loadout.Contains(id),
                };
            });

            return Results.Json(new
            {
                character     = charId,
                signature     = sig,
                unlockedSlots = slots,
                loadout,
                spells = pool,
            }, jsonOpts);
        });

        // POST /spell/equip?id=<id> → equip an owned pool spell for the selected character.
        app.MapPost("/spell/equip", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok = Loadouts.Equip(profile, characterCatalog, profile.SelectedCharacter, id);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new
            {
                ok,
                loadout = Loadouts.Resolve(profile, characterCatalog, profile.SelectedCharacter),
            }, jsonOpts);
        });

        // POST /spell/levelup?id=<id> → spend banked copies to raise a spell one level (2026-06-15).
        app.MapPost("/spell/levelup", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id)) return Results.BadRequest("id query parameter required");
            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok = SpellService.TryLevelUp(profile, id);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new
            {
                ok,
                level  = profile.SpellLevels.GetValueOrDefault(id),
                copies = profile.SpellCopies.GetValueOrDefault(id),
            }, jsonOpts);
        });

        // POST /spell/unequip?id=<id> → remove a drafted spell (signature can't be unequipped).
        app.MapPost("/spell/unequip", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok = Loadouts.Unequip(profile, characterCatalog, profile.SelectedCharacter, id);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new
            {
                ok,
                loadout = Loadouts.Resolve(profile, characterCatalog, profile.SelectedCharacter),
            }, jsonOpts);
        });
    }
}

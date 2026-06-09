using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class CharacterEndpoints
{
    public static void Map(WebApplication app, CharacterCatalog characterCatalog,
        ProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        // GET /characters → catalog + selection state from profile
        app.MapGet("/characters", (HttpContext ctx) =>
        {
            var profile = profileStore.Load(ProfileNs.From(ctx));
            return Results.Json(new
            {
                characters = characterCatalog.All,
                selected   = profile.SelectedCharacter,
                unlocked   = profile.UnlockedCharacters,
            }, jsonOpts);
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

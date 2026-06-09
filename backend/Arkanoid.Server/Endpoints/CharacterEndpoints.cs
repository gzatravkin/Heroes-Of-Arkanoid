using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class CharacterEndpoints
{
    public static void Map(WebApplication app, CharacterCatalog characterCatalog,
        ProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        // GET /characters → catalog + selection state from profile
        app.MapGet("/characters", () =>
        {
            var profile = profileStore.Load();
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

            var profile = profileStore.Load();
            profile.SelectedCharacter = id;
            profileStore.Save(profile);
            return Results.Json(profile, jsonOpts);
        });
    }
}

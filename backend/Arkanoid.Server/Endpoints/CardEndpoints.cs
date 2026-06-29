using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Cards: the persistent passive layer (plan §A.1). Catalog + owned/equipped state, equip and
/// Card-Dust leveling. A cheat-gated grant endpoint exists for testing.</summary>
public static class CardEndpoints
{
    private static readonly bool Cheats =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static void Map(WebApplication app, CardCatalog catalog, IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        app.MapGet("/cards", (HttpContext ctx) =>
        {
            var p = profileStore.Load(ProfileNs.From(ctx));
            return Results.Json(new
            {
                cards = catalog.Cards,
                owned = p.OwnedCards,
                equipped = p.EquippedCards,
                slots = p.CardSlots,
                cardDust = p.CardDust,
                maxLevel = CardService.MaxCardLevel,
            }, jsonOpts);
        });

        app.MapPost("/cards/equip", (HttpContext ctx) =>
        {
            var id  = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var ok  = id != null && CardService.Equip(p, id);
            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, equipped = p.EquippedCards }, jsonOpts);
        });

        app.MapPost("/cards/unequip", (HttpContext ctx) =>
        {
            var id  = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var ok  = id != null && CardService.Unequip(p, id);
            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, equipped = p.EquippedCards }, jsonOpts);
        });

        // Spend banked copies to raise a card one level (manual copy-threshold leveling, 2026-06-15).
        app.MapPost("/cards/levelup", (HttpContext ctx) =>
        {
            var id  = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            var ok  = id != null && CardService.TryLevelUp(p, id);
            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, owned = p.OwnedCards }, jsonOpts);
        });

        // Cheat/dev: grant a card copy (first copy unlocks; duplicates level) for testing.
        app.MapPost("/cards/grant", (HttpContext ctx) =>
        {
            if (!Cheats) return Results.NotFound();
            var id  = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx);
            var p   = profileStore.Load(pid);
            if (id != null && catalog.TryGet(id, out _)) CardService.Grant(p, id);
            profileStore.Save(p, pid);
            return Results.Json(new { ok = true, owned = p.OwnedCards }, jsonOpts);
        });
    }
}

using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class ItemEndpoints
{
    public static void Map(WebApplication app, ItemCatalog itemCatalog, ProfileStore profileStore,
        JsonSerializerOptions jsonOpts)
    {
        // GET /items → catalog + owned tiers + equipped list + current crystals
        app.MapGet("/items", (HttpContext ctx) =>
        {
            var profile = profileStore.Load(ProfileNs.From(ctx));
            var items = itemCatalog.All.Select(def => new
            {
                def.Id,
                def.Name,
                def.Icon,
                def.MaxTier,
                def.Cost,
                def.Effect,
                def.Description,
                OwnedTier = profile.OwnedItems.GetValueOrDefault(def.Id, 0),
                Equipped  = profile.EquippedItems.Contains(def.Id),
            });
            return Results.Json(new { items, crystals = profile.Crystals, equipped = profile.EquippedItems }, jsonOpts);
        });

        // POST /item/buy?id=<id> → spend crystals, raise tier
        app.MapPost("/item/buy", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok = ItemShop.TryBuy(profile, itemCatalog, id);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, crystals = profile.Crystals, ownedTier = profile.OwnedItems.GetValueOrDefault(id, 0) }, jsonOpts);
        });

        // POST /item/equip?id=<id> → equip (≤3 slots)
        app.MapPost("/item/equip", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok = ItemShop.Equip(profile, id);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, equipped = profile.EquippedItems }, jsonOpts);
        });

        // POST /item/unequip?id=<id> → unequip
        app.MapPost("/item/unequip", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok = ItemShop.Unequip(profile, id);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, equipped = profile.EquippedItems }, jsonOpts);
        });
    }
}

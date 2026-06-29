using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>
/// Dungeon shop floor endpoints (docs/04 §6.2 shop pick, §5 spend Gold on spells/relics/heals).
/// The shop inventory is regenerated deterministically server-side on every request so a buy can be
/// validated against the same list the client was shown — the client cannot buy an item not on offer.
/// </summary>
public static class DungeonShopEndpoints
{
    public static void Map(WebApplication app, IDungeonStore dungeonStore, JsonSerializerOptions jsonOpts)
    {
        // GET /dungeon/shop/items → the current floor's shop inventory + the player's Gold
        app.MapGet("/dungeon/shop/items", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var run = dungeonStore.Load(pid);
            if (run is null || !run.Active)
                return Results.BadRequest("No active dungeon run");

            var items = DungeonService.GenerateShopItems(run, 3);
            return Results.Json(new { items, gold = run.Gold }, jsonOpts);
        });

        // POST /dungeon/shop/buy?item=<id> → buy an offered item with Gold
        app.MapPost("/dungeon/shop/buy", (HttpContext ctx) =>
        {
            var itemId = ctx.Request.Query["item"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(itemId))
                return Results.BadRequest("item query parameter required");

            var pid = ProfileNs.From(ctx);
            var run = dungeonStore.Load(pid);
            if (run is null || !run.Active)
                return Results.BadRequest("No active dungeon run");

            // Re-derive the same inventory the client saw; reject anything not on offer.
            var items = DungeonService.GenerateShopItems(run, 3);
            var item  = items.FirstOrDefault(i => i.Id == itemId);
            if (item is null)
                return Results.BadRequest($"Item '{itemId}' is not in the current shop");

            if (!DungeonService.TryBuy(run, item))
                return Results.Json(new { ok = false, error = "not_enough_gold", gold = run.Gold }, jsonOpts);

            dungeonStore.Save(run, pid);
            return Results.Json(new { ok = true, gold = run.Gold, run }, jsonOpts);
        });
    }
}

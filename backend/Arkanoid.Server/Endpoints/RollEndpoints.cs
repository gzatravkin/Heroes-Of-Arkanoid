using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>
/// Fixed-price RANDOM rolls (economy rework §2): <c>POST /roll/{card|module|spell|hero}</c> spends a coin and
/// pulls from the pool (duplicates level the item / ascend the hero). <c>GET /roll/state</c> reports each
/// pool's price, coin, and whether anything can still be gained (the maxed-pool guard) for the UI.
/// </summary>
public static class RollEndpoints
{
    private static readonly bool Cheats =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    private static Rng NewRng() => new(unchecked((int)(DateTimeOffset.UtcNow.Ticks & 0x7fffffff)));

    public static void Map(WebApplication app, CardCatalog cards, ModuleCatalog modules,
        IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        var chars = CharacterCatalog.Default;

        app.MapPost("/roll/card", (HttpContext ctx) => DoRoll(ctx, profileStore, jsonOpts,
            Currency.Sparks, RollService.CardRollCost,
            p => RollService.CanRollCard(p, cards), (p, rng) => RollService.RollCard(p, cards, rng)));

        app.MapPost("/roll/module", (HttpContext ctx) => DoRoll(ctx, profileStore, jsonOpts,
            Currency.Sparks, RollService.ModuleRollCost,
            p => RollService.CanRollModule(p, modules), (p, rng) => RollService.RollModule(p, modules, rng)));

        app.MapPost("/roll/spell", (HttpContext ctx) => DoRoll(ctx, profileStore, jsonOpts,
            Currency.Souls, RollService.SpellRollCost,
            p => RollService.CanRollSpell(p, chars), (p, rng) => RollService.RollSpell(p, chars, rng)));

        app.MapPost("/roll/hero", (HttpContext ctx) => DoRoll(ctx, profileStore, jsonOpts,
            Currency.Souls, RollService.HeroRollCost,
            RollService.CanRollHero, (p, rng) => RollService.RollHero(p, rng)));

        // Cheat/dev: grant coins so the roll/season UIs are testable without grinding.
        app.MapPost("/dev/coins", (HttpContext ctx) =>
        {
            if (!Cheats) return Results.NotFound();
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            if (int.TryParse(ctx.Request.Query["sparks"].FirstOrDefault(), out var s))  Wallet.Add(p, Currency.Sparks, s);
            if (int.TryParse(ctx.Request.Query["souls"].FirstOrDefault(), out var so))  Wallet.Add(p, Currency.Souls, so);
            if (int.TryParse(ctx.Request.Query["insight"].FirstOrDefault(), out var i))  Wallet.Add(p, Currency.Insight, i);
            profileStore.Save(p, pid);
            return Results.Json(new { ok = true, sparks = p.Sparks, souls = p.Souls, insight = p.Insight }, jsonOpts);
        });

        app.MapGet("/roll/state", (HttpContext ctx) =>
        {
            var p = profileStore.Load(ProfileNs.From(ctx));
            return Results.Json(new
            {
                sparks = p.Sparks, souls = p.Souls, insight = p.Insight,
                card   = new { cost = RollService.CardRollCost,   coin = "sparks", canRoll = RollService.CanRollCard(p, cards) },
                module = new { cost = RollService.ModuleRollCost, coin = "sparks", canRoll = RollService.CanRollModule(p, modules) },
                spell  = new { cost = RollService.SpellRollCost,  coin = "souls",  canRoll = RollService.CanRollSpell(p, chars) },
                hero   = new { cost = RollService.HeroRollCost,   coin = "souls",  canRoll = RollService.CanRollHero(p) },
            }, jsonOpts);
        });
    }

    private static IResult DoRoll(HttpContext ctx, IProfileStore store, JsonSerializerOptions opts,
        Currency coin, int cost, System.Func<Profile, bool> canRoll, System.Func<Profile, Rng, RollResult> roll)
    {
        var pid = ProfileNs.From(ctx);
        var p = store.Load(pid);
        if (!canRoll(p)) return Results.Json(new { ok = false, reason = "pool_maxed" }, opts);   // terminal guard
        if (!Wallet.TrySpend(p, coin, cost)) return Results.Json(new { ok = false, reason = "insufficient" }, opts);
        var result = roll(p, NewRng());
        store.Save(p, pid);
        return Results.Json(new { ok = true, result, sparks = p.Sparks, souls = p.Souls, insight = p.Insight }, opts);
    }
}

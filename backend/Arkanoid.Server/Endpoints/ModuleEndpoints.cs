using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

/// <summary>Modules: slotted gear collected like cards (economy rework). Catalog + owned (defId→level) +
/// equipped (slot→defId), equip/unequip. Pulls live in RollEndpoints; a cheat grant exists for testing.</summary>
public static class ModuleEndpoints
{
    private static readonly bool Cheats =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1"
        || System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public static void Map(WebApplication app, ModuleCatalog catalog, IProfileStore profileStore, JsonSerializerOptions jsonOpts)
    {
        app.MapGet("/modules", (HttpContext ctx) =>
        {
            var p = profileStore.Load(ProfileNs.From(ctx));
            return Results.Json(new
            {
                modules = catalog.Modules,
                owned = p.OwnedModules,
                copies = p.ModuleCopies,
                equipped = p.EquippedModules,
                maxLevel = ModuleService.MaxModuleLevel,
            }, jsonOpts);
        });

        // Spend banked copies to raise a module one level (manual copy-threshold leveling, 2026-06-15).
        app.MapPost("/modules/levelup", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            var ok = id != null && ModuleService.TryLevelUp(p, id);
            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, owned = p.OwnedModules, copies = p.ModuleCopies }, jsonOpts);
        });

        app.MapPost("/modules/equip", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            var ok = id != null && ModuleService.Equip(p, catalog, id);
            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, equipped = p.EquippedModules }, jsonOpts);
        });

        app.MapPost("/modules/unequip", (HttpContext ctx) =>
        {
            var slot = ctx.Request.Query["slot"].FirstOrDefault();
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            var ok = slot != null && ModuleService.Unequip(p, slot);
            if (ok) profileStore.Save(p, pid);
            return Results.Json(new { ok, equipped = p.EquippedModules }, jsonOpts);
        });

        // Cheat/dev: grant a module copy (first copy unlocks; duplicates level) for testing.
        app.MapPost("/modules/grant", (HttpContext ctx) =>
        {
            if (!Cheats) return Results.NotFound();
            var defId = ctx.Request.Query["def"].FirstOrDefault();
            var pid = ProfileNs.From(ctx); var p = profileStore.Load(pid);
            if (defId != null && catalog.TryGet(defId, out _)) ModuleService.Grant(p, defId);
            profileStore.Save(p, pid);
            return Results.Json(new { ok = true, owned = p.OwnedModules }, jsonOpts);
        });
    }
}

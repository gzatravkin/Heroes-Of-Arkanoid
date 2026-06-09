using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class DungeonEndpoints
{
    public static void Map(WebApplication app, DungeonStore dungeonStore,
        DungeonCatalog dungeonCatalog, ProfileStore profileStore,
        ProgressionConfig progressionConfig, JsonSerializerOptions jsonOpts)
    {
        // GET /dungeons → full catalog
        app.MapGet("/dungeons", () =>
            Results.Json(new { dungeons = dungeonCatalog.All }, jsonOpts));

        // POST /dungeon/start?id=<id> → start a new run
        app.MapPost("/dungeon/start", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest("id query parameter required");

            DungeonDef def;
            try { def = dungeonCatalog.Get(id); }
            catch (KeyNotFoundException) { return Results.NotFound($"Dungeon '{id}' not found"); }

            var seed = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff);
            var run  = DungeonService.StartRun(def, seed);
            dungeonStore.Save(run);
            return Results.Json(run, jsonOpts);
        });

        // GET /dungeon/state → current run (or {active:false})
        app.MapGet("/dungeon/state", () =>
        {
            var run = dungeonStore.Load();
            return run is null
                ? Results.Json(new { active = false }, jsonOpts)
                : Results.Json(run, jsonOpts);
        });

        // POST /dungeon/floor-cleared → advance floor; grants permanent reward on final floor
        app.MapPost("/dungeon/floor-cleared", () =>
        {
            var run = dungeonStore.Load();
            if (run is null || !run.Active)
                return Results.BadRequest("No active dungeon run");

            DungeonDef def;
            try { def = dungeonCatalog.Get(run.DungeonId); }
            catch { return Results.BadRequest($"Unknown dungeon id '{run.DungeonId}'"); }

            var isLastFloor = DungeonService.OnFloorCleared(run, progressionConfig);
            dungeonStore.Save(run);

            Profile? updatedProfile = null;
            if (isLastFloor)
            {
                var profile = profileStore.Load();
                if (!profile.UnlockedRelics.Contains(def.RewardRelic))
                    profile.UnlockedRelics.Add(def.RewardRelic);
                profile.Crystals += def.RewardCrystals;
                profileStore.Save(profile);
                updatedProfile = profile;
            }

            return Results.Json(new { run, isLastFloor, profile = updatedProfile }, jsonOpts);
        });

        // POST /dungeon/pick?choice=<id> → player picks one of the 3 pending choices
        app.MapPost("/dungeon/pick", (HttpContext ctx) =>
        {
            var choice = ctx.Request.Query["choice"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(choice))
                return Results.BadRequest("choice query parameter required");

            var run = dungeonStore.Load();
            if (run is null || !run.Active)
                return Results.BadRequest("No active dungeon run");

            DungeonService.PickChoice(run, choice);
            dungeonStore.Save(run);
            return Results.Json(run, jsonOpts);
        });

        // POST /dungeon/fail → permadeath — ends the run
        app.MapPost("/dungeon/fail", () =>
        {
            var run = dungeonStore.Load();
            if (run is null) return Results.BadRequest("No active dungeon run");

            DungeonService.Fail(run);
            dungeonStore.Save(run);
            return Results.Json(run, jsonOpts);
        });
    }
}

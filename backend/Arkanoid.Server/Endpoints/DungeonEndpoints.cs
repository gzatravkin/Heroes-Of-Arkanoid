using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class DungeonEndpoints
{
    public static void Map(WebApplication app, IDungeonStore dungeonStore,
        DungeonCatalog dungeonCatalog, IProfileStore profileStore,
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

            var pid = ProfileNs.From(ctx);
            // Generated rifts (rift-{biome}) are saved per-profile at /complete time;
            // static catalog dungeons are used for everything else.
            DungeonDef? def = null;
            var profile = profileStore.Load(pid);
            if (profile.PendingRift?.Id == id)
            {
                def = profile.PendingRift;
                profile.PendingRift = null;
                profileStore.Save(profile, pid);
            }
            else if (!dungeonCatalog.TryGet(id, out def))
            {
                return Results.NotFound($"Dungeon '{id}' not found");
            }

            var seed = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff);
            var run  = DungeonService.StartRun(def!, seed);
            dungeonStore.Save(run, pid);
            return Results.Json(run, jsonOpts);
        });

        // GET /dungeon/state → current run (or {active:false})
        app.MapGet("/dungeon/state", (HttpContext ctx) =>
        {
            var run = dungeonStore.Load(ProfileNs.From(ctx));
            return run is null
                ? Results.Json(new { active = false }, jsonOpts)
                : Results.Json(run, jsonOpts);
        });

        // POST /dungeon/floor-cleared → advance floor; grants permanent reward on final floor
        app.MapPost("/dungeon/floor-cleared", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var run = dungeonStore.Load(pid);
            if (run is null || !run.Active)
                return Results.BadRequest("No active dungeon run");

            // Carry the just-cleared floor's remaining HP into the run (docs/04 §6.2 permadeath).
            if (int.TryParse(ctx.Request.Query["hp"].FirstOrDefault(), out var hp) && hp > 0)
                run.Hp = hp;
            // Carry the just-cleared floor's accumulated Gold into the run (docs/04 §5).
            if (int.TryParse(ctx.Request.Query["gold"].FirstOrDefault(), out var gold) && gold > 0)
                run.Gold = gold;
            // §7 Rift: carry the shared BALL POOL across levels (the hard part — no reset between levels).
            if (int.TryParse(ctx.Request.Query["balls"].FirstOrDefault(), out var balls) && balls >= 0)
                run.SpareBalls = balls;

            bool wasMiniboss = DungeonService.IsMinibossFloor(run, run.FloorIndex);
            var isLastFloor = DungeonService.OnFloorCleared(run, progressionConfig);
            dungeonStore.Save(run, pid);

            Profile? updatedProfile = null;
            // Miniboss floors (docs/04 §6.2) pay a bonus on clear — the reward for the risk.
            if (wasMiniboss)
            {
                var mp = profileStore.Load(pid);
                mp.Souls += 20; // miniboss bonus → Souls (economy rework)
                profileStore.Save(mp, pid);
                updatedProfile = mp;
            }
            if (isLastFloor)
            {
                var profile = profileStore.Load(pid);
                if (run.IsRift)
                {
                    // §7: full-clear jackpot — DEPTH-scaled reward (no permanent relic draft), + hero tokens.
                    // §7 jackpot → Souls (economy rework): the depth payout + the per-hero token bonus both
                    // become Souls (the spell/hero coin), since Crystals/HeroTokens are retired.
                    int depth = run.Floors.Count;
                    profile.Souls += RiftModifierService.DepthCrystals(depth, run.Floors.Count, run.RewardMult)
                                   + RiftModifierService.DepthTokens(depth, run.Floors.Count);
                }
                else if (!string.IsNullOrEmpty(run.RewardRelic) && !profile.UnlockedRelics.Contains(run.RewardRelic))
                    profile.UnlockedRelics.Add(run.RewardRelic);
                if (!run.IsRift) profile.Souls += run.RewardCrystals;
                profile.Souls += 15; // clearing the whole gauntlet pays a Souls bonus (economy rework)
                // Ascension: clearing a tier-N rift unlocks tier N+1 offers (capped).
                profile.RiftAscension = System.Math.Max(profile.RiftAscension,
                    System.Math.Min(run.Tier + 1, RiftService.MaxAscension));
                profileStore.Save(profile, pid);
                updatedProfile = profile;
            }

            // Hero XP (§5.3): every cleared dungeon floor is a battle won with the selected hero.
            int.TryParse(ctx.Request.Query["blocks"].FirstOrDefault(), out var floorBlocks);
            var xpProfile = updatedProfile ?? profileStore.Load(pid);
            var heroXp = Rewards.GrantHeroXp(xpProfile, xpProfile.SelectedCharacter, floorBlocks,
                won: true, isBoss: wasMiniboss || isLastFloor);
            profileStore.Save(xpProfile, pid);
            updatedProfile = xpProfile;

            return Results.Json(new { run, isLastFloor, profile = updatedProfile, heroXp }, jsonOpts);
        });

        // POST /dungeon/pick?choice=<id> → player picks one of the 3 pending choices
        app.MapPost("/dungeon/pick", (HttpContext ctx) =>
        {
            var choice = ctx.Request.Query["choice"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(choice))
                return Results.BadRequest("choice query parameter required");

            var pid = ProfileNs.From(ctx);
            var run = dungeonStore.Load(pid);
            if (run is null || !run.Active)
                return Results.BadRequest("No active dungeon run");

            // The client passes its known max HP (?maxHp=) so a rift's HP pool heals to the true maximum.
            int.TryParse(ctx.Request.Query["maxHp"].FirstOrDefault(), out var maxHp);
            DungeonService.PickChoice(run, choice, maxHp);
            dungeonStore.Save(run, pid);
            return Results.Json(run, jsonOpts);
        });

        // POST /dungeon/fail → permadeath — ends the run
        app.MapPost("/dungeon/fail", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var run = dungeonStore.Load(pid);
            if (run is null) return Results.BadRequest("No active dungeon run");

            int floorsCleared = run.FloorIndex;
            bool wasRift = run.IsRift;
            int riftTotal = run.Floors.Count;
            double riftRewardMult = run.RewardMult;
            DungeonService.Fail(run);
            dungeonStore.Save(run, pid);

            // Shards drip even on death (docs/04 §5) — a failed run still earns permanent progress.
            var prof = profileStore.Load(pid);
            prof.Souls += 3 + floorsCleared * 3; // Souls drip even on death (economy rework)
            // §7: a rift bail/death still pays out by DEPTH (an attempt is never wasted) — but far less.
            if (wasRift && floorsCleared > 0)
                prof.Souls += RiftModifierService.DepthCrystals(floorsCleared, riftTotal, riftRewardMult)
                            + RiftModifierService.DepthTokens(floorsCleared, riftTotal);
            profileStore.Save(prof, pid);
            return Results.Json(new { run, souls = prof.Souls }, jsonOpts);
        });

        // POST /dungeon/rift-finish?depth=N&won=0|1&blocks=B → continuous Rift (2026-06-16) end: ONE grant for
        // all floors cleared (the rift plays as a single battle now, so there are no per-floor calls).
        app.MapPost("/dungeon/rift-finish", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var run = dungeonStore.Load(pid);
            if (run is null || !run.IsRift) return Results.BadRequest("No active rift run");

            int total = run.Floors.Count;
            int.TryParse(ctx.Request.Query["depth"].FirstOrDefault(), out var depth);
            depth = System.Math.Clamp(depth, 0, total);
            bool won = ctx.Request.Query["won"].FirstOrDefault() == "1";
            int.TryParse(ctx.Request.Query["blocks"].FirstOrDefault(), out var blocks);

            var profile = profileStore.Load(pid);
            int soulsGained = RiftModifierService.DepthCrystals(depth, total, run.RewardMult)
                            + RiftModifierService.DepthTokens(depth, total);
            if (won)
            {
                soulsGained += 15; // full-gauntlet bonus
                profile.RiftAscension = System.Math.Max(profile.RiftAscension,
                    System.Math.Min(run.Tier + 1, RiftService.MaxAscension));
            }
            else soulsGained += 3 + depth * 3; // death/bail still pays by depth
            profile.Souls += soulsGained;

            var heroXp = Rewards.GrantHeroXp(profile, profile.SelectedCharacter, blocks, won: won, isBoss: won);

            run.Active = false; run.Cleared = won; // end the run
            dungeonStore.Save(run, pid);
            profileStore.Save(profile, pid);

            return Results.Json(new { won, depth, totalFloors = total, soulsGained, heroXp, profile }, jsonOpts);
        });
    }
}

using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Meta;

namespace Arkanoid.Server.Endpoints;

public static class ProfileEndpoints
{
    private static readonly bool _cheatsEnabled =
        System.Environment.GetEnvironmentVariable("ARKANOID_CHEATS") == "1";

    public static void Map(WebApplication app, IProfileStore profileStore,
        CampaignCatalog campaignCatalog, DungeonCatalog dungeonCatalog,
        ProgressionConfig progressionConfig, ModuleCatalog moduleCatalog,
        JsonSerializerOptions jsonOpts)
    {
        // POST /dev/hero?hero=X&stars=N&level=L&tokens=T — cheat-gated setup for verifying ★ perks
        // and level scaling in tests/demos without grinding the economy. No-op unless ARKANOID_CHEATS=1.
        app.MapPost("/dev/hero", (HttpContext ctx) =>
        {
            if (!_cheatsEnabled) return Results.NotFound();
            var hero = ctx.Request.Query["hero"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(hero)) return Results.BadRequest("hero required");
            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            if (!profile.HeroProgress.TryGetValue(hero, out var hp)) { hp = new HeroProgress(); profile.HeroProgress[hero] = hp; }
            if (int.TryParse(ctx.Request.Query["stars"].FirstOrDefault(),  out var st)) hp.Stars = System.Math.Clamp(st, 0, StatResolver.MaxStars);
            if (int.TryParse(ctx.Request.Query["level"].FirstOrDefault(),  out var lv)) hp.Level = System.Math.Clamp(lv, 1, 30);
            if (int.TryParse(ctx.Request.Query["exp"].FirstOrDefault(),    out var xp)) hp.Exp = System.Math.Max(0, xp);
            if (int.TryParse(ctx.Request.Query["tokens"].FirstOrDefault(), out var tk)) profile.HeroTokens[hero] = System.Math.Max(0, tk);
            // Optional: select this hero (unlocking it) + set its equipped loadout — for E2E demos that
            // need a specific class/spell (e.g. casting the Engineer's Containment Field).
            if (ctx.Request.Query["select"].FirstOrDefault() == "1")
            {
                if (!profile.UnlockedCharacters.Contains(hero)) profile.UnlockedCharacters.Add(hero);
                profile.SelectedCharacter = hero;
            }
            var loadout = ctx.Request.Query["loadout"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(loadout))
            {
                var ids = loadout.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries).ToList();
                profile.EquippedSpells[hero] = ids;
                foreach (var id in ids) if (!profile.SpellLevels.ContainsKey(id)) profile.SpellLevels[id] = 1;
            }
            // Optional: grant + equip §1 cards (and optionally set their level via cardLevel=N) — for E2E
            // demos that need a specific card active (e.g. Headhunter, Opening Gambit).
            var cards = ctx.Request.Query["cards"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(cards))
            {
                int cardLevel = int.TryParse(ctx.Request.Query["cardLevel"].FirstOrDefault(), out var cl) ? System.Math.Max(1, cl) : 1;
                var ids = cards.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries).ToList();
                foreach (var id in ids) profile.OwnedCards[id] = new CardOwn { Level = cardLevel };
                profile.EquippedCards = ids;
            }
            // Optional: craft + equip §2 modules by def id (level via moduleLevel=N) — for E2E demos.
            var modules = ctx.Request.Query["modules"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(modules))
            {
                int modLevel = int.TryParse(ctx.Request.Query["moduleLevel"].FirstOrDefault(), out var ml) ? System.Math.Max(1, ml) : 1;
                foreach (var defId in modules.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
                {
                    if (!moduleCatalog.TryGet(defId, out var def)) continue;
                    profile.OwnedModules[defId] = modLevel;     // owned at the requested level
                    profile.EquippedModules[def.Slot] = defId;  // equip by def id into its slot
                }
            }
            profileStore.Save(profile, pid);
            return Results.Json(new { ok = true, profile }, jsonOpts);
        });

        // GET /profile → current profile
        app.MapGet("/profile", (HttpContext ctx) =>
        {
            var profile = profileStore.Load(ProfileNs.From(ctx));
            return Results.Json(profile, jsonOpts);
        });

        // GET /campaign → nodes with unlocked/completed flags
        app.MapGet("/campaign", (HttpContext ctx) =>
        {
            var profile   = profileStore.Load(ProfileNs.From(ctx));
            var completed = new HashSet<string>(profile.CompletedLevels);
            var nodes = campaignCatalog.Nodes.Select(n => new
            {
                n.Id, n.Label, n.Biome,
                Unlocked  = campaignCatalog.IsUnlocked(n, completed),
                Completed = completed.Contains(n.Id),
            });
            return Results.Json(new { nodes }, jsonOpts);
        });

        // GET /features → which meta features are unlocked yet + the campaign milestone that unlocks each
        // (drives the menu's locked/unlocked state + "unlocks after …" hints).
        app.MapGet("/features", (HttpContext ctx) =>
        {
            var profile   = profileStore.Load(ProfileNs.From(ctx));
            var completed = new HashSet<string>(profile.CompletedLevels);
            var label     = campaignCatalog.Nodes.ToDictionary(n => n.Id, n => n.Label);
            var features  = FeatureGates.All.Select(f =>
            {
                var req = FeatureGates.RequiredLevel(f);
                return new
                {
                    feature       = f.ToString(),
                    name          = FeatureGates.DisplayName(f),
                    unlocked      = FeatureGates.IsUnlocked(f, completed),
                    requiredLevel = req,
                    requiredLabel = req.Length == 0 ? "" : (label.TryGetValue(req, out var l) ? l : req),
                };
            });
            return Results.Json(new { features }, jsonOpts);
        });

        // POST /complete?level=<id> → grant completion reward, save, return {profile, reward}
        app.MapPost("/complete", (HttpContext ctx) =>
        {
            var levelId = ctx.Request.Query["level"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(levelId))
                return Results.BadRequest("level query parameter required");

            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            // Item-shop treasure bonus removed with the item system (economy rework 2026-06-15).
            var reward  = Rewards.GrantLevelCompletion(profile, levelId, progressionConfig, treasureBonus: 0);
            // Hero XP (§5.3): accrues EVERY win with the selected hero (blocks + win bonus), re-clears too.
            int.TryParse(ctx.Request.Query["blocks"].FirstOrDefault(), out var blocksDestroyed);
            var heroXp = Rewards.GrantHeroXp(profile, profile.SelectedCharacter, blocksDestroyed,
                won: true, isBoss: levelId.EndsWith("-boss"));
            profileStore.Save(profile, pid);

            // A cleared campaign node may tear open a Rift — the opt-in dungeon entry.
            var riftMode = ctx.Request.Query["rift"].FirstOrDefault();
            var riftSeed = unchecked((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff) ^ levelId.GetHashCode());
            var rift = RiftService.Roll(riftMode, progressionConfig.RiftChance, levelId, riftSeed,
                dungeonCatalog, campaignCatalog, profile.RiftAscension, progressionConfig.RiftLevels);
            // Generated rifts: store the def per-profile so concurrent players never race on the shared catalog.
            if (rift?.Def != null)
            {
                profile.PendingRift = rift.Def;
                profileStore.Save(profile, pid);
            }

            return Results.Json(new { profile, reward, rift, heroXp }, jsonOpts);
        });

        // Spell upgrades by skill-points removed (economy rework 2026-06-15): spells level ONLY via
        // duplicate rolls (RollEndpoints /roll/spell). The /upgrade route + SkillsScene are retired.

        // POST /hero/xp?blocks=N&won=<bool> → grant hero XP for a battle that did NOT go through
        // /complete (a loss, or any non-first-clear end). §5.3: XP = blocks + (win ? bonus : 0).
        app.MapPost("/hero/xp", (HttpContext ctx) =>
        {
            var pid = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            int.TryParse(ctx.Request.Query["blocks"].FirstOrDefault(), out var blocks);
            bool won = ctx.Request.Query["won"].FirstOrDefault() == "true";
            var heroXp = Rewards.GrantHeroXp(profile, profile.SelectedCharacter, blocks, won);
            profileStore.Save(profile, pid);
            return Results.Json(new { heroXp, profile }, jsonOpts);
        });

        // POST /mastery?node=<id> → spend Insight to raise a Mastery node (economy rework §6)
        app.MapPost("/mastery", (HttpContext ctx) =>
        {
            var node = ctx.Request.Query["node"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(node))
                return Results.BadRequest("node query parameter required");
            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok      = Upgrades.TryUpgradeMastery(profile, node);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, profile }, jsonOpts);
        });

        // POST /mastery/reset → respec all masteries: refund the Insight spent, for a flat Souls fee (§6)
        app.MapPost("/mastery/reset", (HttpContext ctx) =>
        {
            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok      = Upgrades.ResetMasteries(profile);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, profile }, jsonOpts);
        });

        // POST /spells/unlock-slot → spend Souls to unlock one more flex spell slot (economy rework §3)
        app.MapPost("/spells/unlock-slot", (HttpContext ctx) =>
        {
            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok      = Upgrades.TryUnlockSpellSlot(profile);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, profile }, jsonOpts);
        });

        // GET /hero/stats?hero=<id> → the resolved §5 stat block + progression for the UI
        app.MapGet("/hero/stats", (HttpContext ctx) =>
        {
            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var hero    = ctx.Request.Query["hero"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(hero)) hero = profile.SelectedCharacter;
            var prog = profile.HeroProgress.TryGetValue(hero, out var hp) ? hp : new HeroProgress();
            var s = StatResolver.Resolve(hero, prog.Level, prog.Stars, profile.Masteries);
            return Results.Json(new
            {
                hero,
                level    = prog.Level,
                exp      = prog.Exp,
                xpToNext = StatResolver.XpToNext(prog.Level),
                stars    = prog.Stars,
                tokens   = profile.HeroTokens.TryGetValue(hero, out var t) ? t : 0,
                nextStarCost = prog.Stars < StatResolver.MaxStars ? StatResolver.StarTokenCost(prog.Stars + 1) : 0,
                stats = new
                {
                    power      = s.Power,
                    vitality   = s.Vitality,
                    critChance = s.CritChance,
                    critDamage = s.CritDamage,
                    multiball  = s.Multiball,
                    tempo      = s.Tempo,
                },
            }, jsonOpts);
        });

        // POST /hero/ascend?hero=<id> → spend Hero Tokens to raise a hero's ★ (§5.4)
        app.MapPost("/hero/ascend", (HttpContext ctx) =>
        {
            var hero = ctx.Request.Query["hero"].FirstOrDefault() ?? "";
            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            var ok      = Upgrades.TryAscendHero(profile, hero);
            if (ok) profileStore.Save(profile, pid);
            return Results.Json(new { ok, profile }, jsonOpts);
        });

        // POST /reset → reset to new default, save, return profile
        app.MapPost("/reset", (HttpContext ctx) =>
        {
            var profile = Profile.NewDefault();
            profileStore.Save(profile, ProfileNs.From(ctx));
            return Results.Json(profile, jsonOpts);
        });

        // POST /achievement/unlock?id=<achievementId>
        // Records an achievement unlock. Idempotent — no-ops if already unlocked.
        app.MapPost("/achievement/unlock", (HttpContext ctx) =>
        {
            var achievementId = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(achievementId))
                return Results.BadRequest("id query parameter required");

            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            if (!profile.Achievements.Contains(achievementId))
            {
                profile.Achievements.Add(achievementId);
                profileStore.Save(profile, pid);
            }
            return Results.Json(new { ok = true, achievements = profile.Achievements }, jsonOpts);
        });

        // POST /tutorial/seen → marks tutorial as seen
        app.MapPost("/tutorial/seen", (HttpContext ctx) =>
        {
            var pid     = ProfileNs.From(ctx);
            var profile = profileStore.Load(pid);
            profile.TutorialSeen = true;
            profileStore.Save(profile, pid);
            return Results.Json(new { ok = true }, jsonOpts);
        });
    }
}

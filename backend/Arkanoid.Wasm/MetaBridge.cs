using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;

namespace ArkanoidWasm;

/// <summary>
/// WASM-side replacement for the REST API: every [JSExport] method mirrors one server endpoint.
/// Pattern: load profile → call Core service → save profile → return JSON.
/// </summary>
public static partial class MetaBridge
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // Shared store singletons — same instances as GameBridge so session data is consistent.
    private static WasmProfileStore Store => GameBridge.ProfileStore;
    private static WasmDungeonStore DS    => GameBridge.DungeonStore;

    private static string Serialize(object? obj) => JsonSerializer.Serialize(obj, _opts);

    // ── Lazy-loaded catalogs (each loaded at most once, from embedded resources) ─────────────

    private static CardCatalog?      _cards;
    private static ModuleCatalog?    _modules;
    private static CharacterCatalog? _chars;
    private static RelicCatalog?     _relics;
    private static DungeonCatalog?   _dungeons;
    private static CampaignCatalog?  _campaign;
    private static SeasonCatalog?    _seasons;
    private static EventCatalog?     _events;
    private static MissionCatalog?   _missions;

    private static CardCatalog      Cards    => _cards    ??= LoadCards();
    private static ModuleCatalog    Modules  => _modules  ??= LoadModules();
    private static CharacterCatalog Chars    => _chars    ??= CharacterCatalog.FromJson(ResourceLoader.GetJson("config/characters.json"));
    private static RelicCatalog     Relics   => _relics   ??= RelicCatalog.FromJson(ResourceLoader.GetJson("config/relics.json"));
    private static DungeonCatalog   Dungeons => _dungeons ??= DungeonCatalog.FromJson(ResourceLoader.GetJson("config/dungeons.json"));
    private static CampaignCatalog  Campaign => _campaign ??= CampaignCatalog.FromJson(ResourceLoader.GetJson("config/campaign.json"));
    private static SeasonCatalog    Seasons  => _seasons  ??= LoadSeasons();
    private static EventCatalog     Events   => _events   ??= LoadEvents();
    private static MissionCatalog   Missions => _missions ??= LoadMissions();

    private static CardCatalog LoadCards()
        => ResourceLoader.TryGetJson("config/cards.json", out var j)
            ? CardCatalog.FromJson(j) : CardCatalog.FromJson("{\"cards\":[]}");

    private static ModuleCatalog LoadModules()
        => ResourceLoader.TryGetJson("config/modules.json", out var j)
            ? ModuleCatalog.FromJson(j) : ModuleCatalog.FromJson("{\"modules\":[]}");

    private static SeasonCatalog LoadSeasons()
        => ResourceLoader.TryGetJson("config/seasons.json", out var j)
            ? SeasonCatalog.FromJson(j)
            : SeasonCatalog.FromJson("{\"themes\":[],\"tokensPerBattle\":10,\"track\":[]}");

    private static EventCatalog LoadEvents()
        => ResourceLoader.TryGetJson("config/events.json", out var j)
            ? EventCatalog.FromJson(j) : EventCatalog.FromJson("{\"events\":[]}");

    private static MissionCatalog LoadMissions()
        => ResourceLoader.TryGetJson("config/missions.json", out var j)
            ? MissionCatalog.FromJson(j) : MissionCatalog.FromJson("{\"missions\":[]}");

    // In-memory leaderboard for offline season/prestige boards (no remote server).
    private static readonly InMemoryLeaderboardStore _lb = new();

    // ── Profile ───────────────────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetProfile(string pid)
    {
        var p = Store.Load(pid);
        return Serialize(p);
    }

    [JSExport]
    public static string ResetProfile(string pid)
    {
        var p = Profile.NewDefault();
        Store.Save(p, pid);
        return Serialize(p);
    }

    /// <summary>Level completion: grants first-clear coins + hero XP, optionally rolls a Rift.</summary>
    [JSExport]
    public static string Complete(string pid, string levelId, int treasureBonus, string riftMode, int blocks)
    {
        var p      = Store.Load(pid);
        var reward = Rewards.GrantLevelCompletion(p, levelId, ProgressionConfig.Default, treasureBonus);
        var heroXp = Rewards.GrantHeroXp(p, p.SelectedCharacter, blocks,
                         won: true, isBoss: levelId.EndsWith("-boss"));

        // Record daily mission progress (mirrors GameSession.cs in the REST server).
        var now    = DateTimeOffset.UtcNow;
        var clock  = SeasonClock.Default;
        var dayId  = clock.DayId(now);
        var weekId = clock.WeekId(now);
        DailyService.Record(p, Missions, dayId, weekId, "blocks_destroyed", blocks);
        DailyService.Record(p, Missions, dayId, weekId, "battles_played", 1);
        DailyService.Record(p, Missions, dayId, weekId, "levels_won", 1);

        Store.Save(p, pid);

        var riftSeed = unchecked(
            (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff)
            ^ levelId.GetHashCode());
        var rift = RiftService.Roll(riftMode, ProgressionConfig.Default.RiftChance,
                       levelId, riftSeed, Dungeons, Campaign,
                       p.RiftAscension, ProgressionConfig.Default.RiftLevels);
        if (rift?.Def != null)
        {
            p.PendingRift = rift.Def;
            Store.Save(p, pid);
        }
        return Serialize(new { profile = p, reward, rift, heroXp });
    }

    /// <summary>Hero XP for a non-completion battle (loss or re-clear).</summary>
    [JSExport]
    public static string HeroXp(string pid, int blocks, bool won)
    {
        var p      = Store.Load(pid);
        var heroXp = Rewards.GrantHeroXp(p, p.SelectedCharacter, blocks, won);

        // Record daily mission progress for losses / re-clears too.
        var now2   = DateTimeOffset.UtcNow;
        var clock2 = SeasonClock.Default;
        var dayId2 = clock2.DayId(now2);
        var wkId2  = clock2.WeekId(now2);
        DailyService.Record(p, Missions, dayId2, wkId2, "blocks_destroyed", blocks);
        DailyService.Record(p, Missions, dayId2, wkId2, "battles_played", 1);
        if (won) DailyService.Record(p, Missions, dayId2, wkId2, "levels_won", 1);

        Store.Save(p, pid);
        return Serialize(new { heroXp, profile = p });
    }

    [JSExport]
    public static string GetHeroStats(string pid, string heroId)
    {
        var p = Store.Load(pid);
        if (string.IsNullOrWhiteSpace(heroId)) heroId = p.SelectedCharacter;
        var prog = p.HeroProgress.TryGetValue(heroId, out var hp) ? hp : new HeroProgress();
        var s    = StatResolver.Resolve(heroId, prog.Level, prog.Stars, p.Masteries);
        return Serialize(new
        {
            hero         = heroId,
            level        = prog.Level,
            exp          = prog.Exp,
            xpToNext     = StatResolver.XpToNext(prog.Level),
            stars        = prog.Stars,
            tokens       = p.HeroTokens.TryGetValue(heroId, out var t) ? t : 0,
            nextStarCost = prog.Stars < StatResolver.MaxStars
                               ? StatResolver.StarTokenCost(prog.Stars + 1) : 0,
            stats = new
            {
                power      = s.Power,
                vitality   = s.Vitality,
                critChance = s.CritChance,
                critDamage = s.CritDamage,
                multiball  = s.Multiball,
                tempo      = s.Tempo,
            },
        });
    }

    [JSExport]
    public static string Mastery(string pid, string node)
    {
        var p  = Store.Load(pid);
        var ok = Upgrades.TryUpgradeMastery(p, node);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, profile = p });
    }

    [JSExport]
    public static string ResetMasteries(string pid)
    {
        var p  = Store.Load(pid);
        var ok = Upgrades.ResetMasteries(p);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, profile = p });
    }

    [JSExport]
    public static string AscendHero(string pid, string heroId)
    {
        var p  = Store.Load(pid);
        var ok = Upgrades.TryAscendHero(p, heroId);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, profile = p });
    }

    [JSExport]
    public static string GetCampaign(string pid)
    {
        var p         = Store.Load(pid);
        var completed = new HashSet<string>(p.CompletedLevels);
        var nodes = Campaign.Nodes.Select(n => new
        {
            n.Id, n.Label, n.Biome,
            Unlocked  = Campaign.IsUnlocked(n, completed),
            Completed = completed.Contains(n.Id),
        });
        return Serialize(new { nodes });
    }

    [JSExport]
    public static string GetFeatures(string pid)
    {
        var p         = Store.Load(pid);
        var completed = new HashSet<string>(p.CompletedLevels);
        var label     = Campaign.Nodes.ToDictionary(n => n.Id, n => n.Label);
        var features  = FeatureGates.All.Select(f =>
        {
            var req = FeatureGates.RequiredLevel(f);
            return new
            {
                feature       = f.ToString(),
                name          = FeatureGates.DisplayName(f),
                unlocked      = FeatureGates.IsUnlocked(f, completed),
                requiredLevel = req,
                requiredLabel = req.Length == 0
                    ? "" : (label.TryGetValue(req, out var l) ? l : req),
            };
        });
        return Serialize(new { features });
    }

    [JSExport]
    public static string GetPrestige(string pid)
    {
        var p     = Store.Load(pid);
        int score = PrestigeService.PrestigeScore(p.PrestigeTier, p.CompletedLevels.Count);
        LeaderboardService.Submit(_lb, pid, pid, PrestigeService.BoardId, "all", score);
        var cohort = LeaderboardService.GenerateCohort(_lb, pid, pid, PrestigeService.BoardId, "all");
        var me     = cohort.First(e => e.IsMe);
        return Serialize(new
        {
            tier       = p.PrestigeTier,
            canAscend  = PrestigeService.CanAscend(p),
            score,
            rank       = me.Rank,
        });
    }

    [JSExport]
    public static string Ascend(string pid)
    {
        var p = Store.Load(pid);
        if (!PrestigeService.CanAscend(p))
            return Serialize(new { ok = false, tier = p.PrestigeTier });
        int tier = PrestigeService.Ascend(p);
        Store.Save(p, pid);
        LeaderboardService.Submit(_lb, pid, pid, PrestigeService.BoardId, "all",
            PrestigeService.PrestigeScore(tier, 0));
        return Serialize(new { ok = true, tier });
    }

    [JSExport]
    public static string UnlockAchievement(string pid, string achId)
    {
        var p = Store.Load(pid);
        if (!p.Achievements.Contains(achId))
        {
            p.Achievements.Add(achId);
            Store.Save(p, pid);
        }
        return Serialize(new { ok = true, achievements = p.Achievements });
    }

    [JSExport]
    public static string MarkTutorialSeen(string pid)
    {
        var p = Store.Load(pid);
        p.TutorialSeen = true;
        Store.Save(p, pid);
        return Serialize(new { ok = true });
    }

    // ── Characters / Spells ───────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetCharacters(string pid)
    {
        var p        = Store.Load(pid);
        var progress = Chars.All.ToDictionary(c => c.Id, c =>
        {
            var hp   = p.HeroProgress.TryGetValue(c.Id, out var h) ? h : new HeroProgress();
            int next = hp.Stars < StatResolver.MaxStars
                           ? StatResolver.StarTokenCost(hp.Stars + 1) : 0;
            return new
            {
                stars      = hp.Stars,
                pips       = hp.AscendPips,
                maxStars   = StatResolver.MaxStars,
                ascendCost = next,
                canAscend  = next > 0 && hp.AscendPips >= next,
            };
        });
        return Serialize(new
        {
            characters    = Chars.All,
            progress,
            neutralSpells = Chars.NeutralSpells,
            selected      = p.SelectedCharacter,
            unlocked      = p.UnlockedCharacters,
            shards        = p.Shards,
            unlockCost    = ProgressionConfig.CharacterUnlockShardCost,
        });
    }

    [JSExport]
    public static string SelectCharacter(string pid, string charId)
    {
        var p = Store.Load(pid);
        p.SelectedCharacter = charId;
        Store.Save(p, pid);
        return Serialize(p);
    }

    [JSExport]
    public static string GetSpells(string pid)
    {
        var p       = Store.Load(pid);
        var charId  = p.SelectedCharacter;
        Chars.TryGet(charId, out var c);
        var sig     = c?.SignatureId ?? "";
        var loadout = Loadouts.Resolve(p, Chars, charId);
        var owned   = new HashSet<string>(Loadouts.OwnedFor(p, Chars, charId));
        var slots   = Loadouts.SlotCount(p);

        var ids = new List<string> { sig };
        ids.AddRange(Chars.Pool());
        var spells = ids
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .Select(id =>
            {
                var d = Chars.DisplayOf(id);
                return new
                {
                    id,
                    name      = d?.Name      ?? id,
                    icon      = d?.Icon      ?? "",
                    manaCost  = d?.ManaCost  ?? 0,
                    desc      = d?.Desc      ?? "",
                    level     = p.SpellLevels.GetValueOrDefault(id,
                                    owned.Contains(id) ? 1 : 0),
                    copies    = p.SpellCopies.GetValueOrDefault(id),
                    signature = id == sig,
                    owned     = owned.Contains(id),
                    equipped  = loadout.Contains(id),
                };
            });

        return Serialize(new
        {
            character     = charId,
            signature     = sig,
            unlockedSlots = slots,
            loadout,
            spells,
        });
    }

    [JSExport]
    public static string EquipSpell(string pid, string spellId)
    {
        var p  = Store.Load(pid);
        var ok = Loadouts.Equip(p, Chars, p.SelectedCharacter, spellId);
        if (ok) Store.Save(p, pid);
        return Serialize(new
        {
            ok,
            loadout = Loadouts.Resolve(p, Chars, p.SelectedCharacter),
        });
    }

    [JSExport]
    public static string UnequipSpell(string pid, string spellId)
    {
        var p  = Store.Load(pid);
        var ok = Loadouts.Unequip(p, Chars, p.SelectedCharacter, spellId);
        if (ok) Store.Save(p, pid);
        return Serialize(new
        {
            ok,
            loadout = Loadouts.Resolve(p, Chars, p.SelectedCharacter),
        });
    }

    [JSExport]
    public static string SpellLevelUp(string pid, string spellId)
    {
        var p  = Store.Load(pid);
        var ok = SpellService.TryLevelUp(p, spellId);
        if (ok) Store.Save(p, pid);
        return Serialize(new
        {
            ok,
            level  = p.SpellLevels.GetValueOrDefault(spellId),
            copies = p.SpellCopies.GetValueOrDefault(spellId),
        });
    }

    [JSExport]
    public static string UnlockSpellSlot(string pid)
    {
        var p  = Store.Load(pid);
        var ok = Upgrades.TryUnlockSpellSlot(p);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, profile = p });
    }

    // ── Cards ─────────────────────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetCards(string pid)
    {
        var p = Store.Load(pid);
        return Serialize(new
        {
            cards    = Cards.Cards,
            owned    = p.OwnedCards,
            equipped = p.EquippedCards,
            slots    = p.CardSlots,
            cardDust = p.CardDust,
            maxLevel = CardService.MaxCardLevel,
        });
    }

    [JSExport]
    public static string EquipCard(string pid, string cardId)
    {
        var p  = Store.Load(pid);
        var ok = CardService.Equip(p, cardId);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, equipped = p.EquippedCards });
    }

    [JSExport]
    public static string UnequipCard(string pid, string cardId)
    {
        var p  = Store.Load(pid);
        var ok = CardService.Unequip(p, cardId);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, equipped = p.EquippedCards });
    }

    [JSExport]
    public static string CardLevelUp(string pid, string cardId)
    {
        var p  = Store.Load(pid);
        var ok = CardService.TryLevelUp(p, cardId);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, owned = p.OwnedCards });
    }

    [JSExport]
    public static string GrantCard(string pid, string cardId)
    {
        if (!Cards.TryGet(cardId, out _))
            return Serialize(new { ok = false });
        var p = Store.Load(pid);
        CardService.Grant(p, cardId);
        Store.Save(p, pid);
        return Serialize(new { ok = true, owned = p.OwnedCards });
    }

    // ── Modules ───────────────────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetModules(string pid)
    {
        var p = Store.Load(pid);
        return Serialize(new
        {
            modules  = Modules.Modules,
            owned    = p.OwnedModules,
            copies   = p.ModuleCopies,
            equipped = p.EquippedModules,
            maxLevel = ModuleService.MaxModuleLevel,
        });
    }

    [JSExport]
    public static string EquipModule(string pid, string modId)
    {
        var p  = Store.Load(pid);
        var ok = ModuleService.Equip(p, Modules, modId);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, equipped = p.EquippedModules });
    }

    [JSExport]
    public static string UnequipModule(string pid, string slot)
    {
        var p  = Store.Load(pid);
        var ok = ModuleService.Unequip(p, slot);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, equipped = p.EquippedModules });
    }

    [JSExport]
    public static string ModuleLevelUp(string pid, string modId)
    {
        var p  = Store.Load(pid);
        var ok = ModuleService.TryLevelUp(p, modId);
        if (ok) Store.Save(p, pid);
        return Serialize(new { ok, owned = p.OwnedModules, copies = p.ModuleCopies });
    }

    // ── Rolls ─────────────────────────────────────────────────────────────────────────────────

    private static Rng NewRng() =>
        new(unchecked((int)(DateTimeOffset.UtcNow.Ticks & 0x7fffffff)));

    [JSExport]
    public static string Roll(string pid, string kind)
    {
        var p = Store.Load(pid);

        (Currency coin, int cost, bool canRoll, System.Func<Rng, RollResult> doRoll)? args = kind switch
        {
            "card" => (
                Currency.Sparks, RollService.CardRollCost,
                RollService.CanRollCard(p, Cards),
                rng => RollService.RollCard(p, Cards, rng)),
            "module" => (
                Currency.Sparks, RollService.ModuleRollCost,
                RollService.CanRollModule(p, Modules),
                rng => RollService.RollModule(p, Modules, rng)),
            "spell" => (
                Currency.Souls, RollService.SpellRollCost,
                RollService.CanRollSpell(p, Chars),
                rng => RollService.RollSpell(p, Chars, rng)),
            "hero" => (
                Currency.Souls, RollService.HeroRollCost,
                RollService.CanRollHero(p),
                rng => RollService.RollHero(p, rng)),
            _ => null,
        };

        if (args is null)
            return Serialize(new { ok = false, reason = "unknown_kind" });
        if (!args.Value.canRoll)
            return Serialize(new { ok = false, reason = "pool_maxed" });
        if (!Wallet.TrySpend(p, args.Value.coin, args.Value.cost))
            return Serialize(new { ok = false, reason = "insufficient" });

        var result = args.Value.doRoll(NewRng());
        // Resolve the display name from the relevant catalog so the UI shows a human-readable
        // item name in the reveal card rather than the raw internal id string.
        var displayName = kind switch
        {
            "card"   => Cards.Cards.FirstOrDefault(c => c.Id == result.Id)?.Name ?? result.Id,
            "module" => Modules.Modules.FirstOrDefault(m => m.Id == result.Id)?.Name ?? result.Id,
            "spell"  => Chars.DisplayOf(result.Id)?.Name ?? result.Id,
            "hero"   => Chars.All.FirstOrDefault(c => c.Id == result.Id)?.Name ?? result.Id,
            _        => result.Id,
        };
        Store.Save(p, pid);
        return Serialize(new
        {
            ok = true,
            result = new
            {
                kind    = result.Kind,  id     = result.Id,    name   = displayName,
                wasNew  = result.WasNew, level  = result.Level, stars  = result.Stars,
                wasted  = result.Wasted, copies = result.Copies,
            },
            sparks = p.Sparks, souls = p.Souls, insight = p.Insight,
        });
    }

    [JSExport]
    public static string RollState(string pid)
    {
        var p = Store.Load(pid);
        return Serialize(new
        {
            sparks = p.Sparks, souls = p.Souls, insight = p.Insight,
            card   = new { cost = RollService.CardRollCost,   coin = "sparks", canRoll = RollService.CanRollCard(p, Cards) },
            module = new { cost = RollService.ModuleRollCost, coin = "sparks", canRoll = RollService.CanRollModule(p, Modules) },
            spell  = new { cost = RollService.SpellRollCost,  coin = "souls",  canRoll = RollService.CanRollSpell(p, Chars) },
            // poolEmpty distinguishes "nothing unlocked yet (beat a boss first)" from "all maxed (★6)".
            hero   = new { cost = RollService.HeroRollCost,   coin = "souls",  canRoll = RollService.CanRollHero(p), poolEmpty = p.HeroPool.Count == 0 },
        });
    }

    [JSExport]
    public static string DevCoins(string pid, int sparks, int souls, int insight)
    {
        var p = Store.Load(pid);
        if (sparks  != 0) Wallet.Add(p, Currency.Sparks,  sparks);
        if (souls   != 0) Wallet.Add(p, Currency.Souls,   souls);
        if (insight != 0) Wallet.Add(p, Currency.Insight, insight);
        Store.Save(p, pid);
        return Serialize(new { ok = true, sparks = p.Sparks, souls = p.Souls, insight = p.Insight });
    }

    // ── Dungeons ──────────────────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetDungeons()
        => Serialize(new { dungeons = Dungeons.All });

    [JSExport]
    public static string StartDungeon(string pid, string dungeonId)
    {
        var p = Store.Load(pid);
        DungeonDef? def = null;

        if (p.PendingRift?.Id == dungeonId)
        {
            def = p.PendingRift;
            p.PendingRift = null;
            Store.Save(p, pid);
        }
        else if (!Dungeons.TryGet(dungeonId, out def))
        {
            return Serialize(new { error = "not_found" });
        }

        var seed = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff);
        var run  = DungeonService.StartRun(def!, seed);
        DS.Save(run, pid);
        return Serialize(run);
    }

    [JSExport]
    public static string GetDungeonState(string pid)
    {
        var run = DS.Load(pid);
        return run is null
            ? Serialize(new { active = false })
            : Serialize(run);
    }

    [JSExport]
    public static string FloorCleared(string pid, int hp, int gold, int blocks)
    {
        var run = DS.Load(pid);
        if (run is null || !run.Active)
            return Serialize(new { error = "no_active_run" });

        if (hp   > 0) run.Hp   = hp;
        if (gold > 0) run.Gold = gold;

        bool wasMiniboss = DungeonService.IsMinibossFloor(run, run.FloorIndex);
        bool isLastFloor = DungeonService.OnFloorCleared(run, ProgressionConfig.Default);
        DS.Save(run, pid);

        Profile? updatedProfile = null;

        if (wasMiniboss)
        {
            var mp = Store.Load(pid);
            mp.Souls += 20;
            Store.Save(mp, pid);
            updatedProfile = mp;
        }

        if (isLastFloor)
        {
            var profile = Store.Load(pid);
            if (run.IsRift)
            {
                int depth = run.Floors.Count;
                profile.Souls += RiftModifierService.DepthCrystals(depth, run.Floors.Count, run.RewardMult)
                               + RiftModifierService.DepthTokens(depth, run.Floors.Count);
            }
            else
            {
                if (!string.IsNullOrEmpty(run.RewardRelic)
                    && !profile.UnlockedRelics.Contains(run.RewardRelic))
                    profile.UnlockedRelics.Add(run.RewardRelic);
                profile.Souls += run.RewardCrystals;
            }
            profile.Souls += 15; // whole-gauntlet bonus
            profile.RiftAscension = System.Math.Max(profile.RiftAscension,
                System.Math.Min(run.Tier + 1, RiftService.MaxAscension));
            Store.Save(profile, pid);
            updatedProfile = profile;
        }

        // Hero XP — every dungeon floor clear is a won battle with the selected hero.
        var xpProfile = updatedProfile ?? Store.Load(pid);
        var heroXp    = Rewards.GrantHeroXp(xpProfile, xpProfile.SelectedCharacter, blocks,
                            won: true, isBoss: wasMiniboss || isLastFloor);
        Store.Save(xpProfile, pid);
        updatedProfile = xpProfile;

        return Serialize(new { run, isLastFloor, profile = updatedProfile, heroXp });
    }

    [JSExport]
    public static string DungeonPick(string pid, string choice)
    {
        var run = DS.Load(pid);
        if (run is null || !run.Active)
            return Serialize(new { error = "no_active_run" });

        DungeonService.PickChoice(run, choice, 0 /* maxHp unknown client-side in WASM */);
        DS.Save(run, pid);
        return Serialize(run);
    }

    [JSExport]
    public static string DungeonFail(string pid)
    {
        var run = DS.Load(pid);
        if (run is null)
            return Serialize(new { error = "no_active_run" });

        int floorsCleared     = run.FloorIndex;
        bool wasRift          = run.IsRift;
        int riftTotal         = run.Floors.Count;
        double riftRewardMult = run.RewardMult;

        DungeonService.Fail(run);
        DS.Save(run, pid);

        var prof = Store.Load(pid);
        prof.Souls += 3 + floorsCleared * 3; // depth drip even on death
        if (wasRift && floorsCleared > 0)
            prof.Souls += RiftModifierService.DepthCrystals(floorsCleared, riftTotal, riftRewardMult)
                        + RiftModifierService.DepthTokens(floorsCleared, riftTotal);
        Store.Save(prof, pid);
        return Serialize(new { run, souls = prof.Souls });
    }

    [JSExport]
    public static string RiftFinish(string pid, int depth, bool won, int blocks)
    {
        var run = DS.Load(pid);
        if (run is null || !run.IsRift)
            return Serialize(new { error = "no_active_rift" });

        int total = run.Floors.Count;
        depth = System.Math.Clamp(depth, 0, total);

        var profile = Store.Load(pid);
        int soulsGained = RiftModifierService.DepthCrystals(depth, total, run.RewardMult)
                        + RiftModifierService.DepthTokens(depth, total);
        if (won)
        {
            soulsGained += 15;
            profile.RiftAscension = System.Math.Max(profile.RiftAscension,
                System.Math.Min(run.Tier + 1, RiftService.MaxAscension));
        }
        else
        {
            soulsGained += 3 + depth * 3; // bail/death drip by depth
        }
        profile.Souls += soulsGained;

        var heroXp = Rewards.GrantHeroXp(profile, profile.SelectedCharacter, blocks,
                         won: won, isBoss: won);

        run.Active  = false;
        run.Cleared = won;
        DS.Save(run, pid);
        Store.Save(profile, pid);

        return Serialize(new { won, depth, totalFloors = total, soulsGained, heroXp, profile });
    }

    [JSExport]
    public static string GetShopItems(string pid)
    {
        var run = DS.Load(pid);
        if (run is null || !run.Active)
            return Serialize(new { error = "no_active_run" });

        var items = DungeonService.GenerateShopItems(run, 3);
        return Serialize(new { items, gold = run.Gold });
    }

    [JSExport]
    public static string BuyShopItem(string pid, string itemId)
    {
        var run = DS.Load(pid);
        if (run is null || !run.Active)
            return Serialize(new { error = "no_active_run" });

        var items = DungeonService.GenerateShopItems(run, 3);
        var item  = items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Serialize(new { ok = false, error = "not_in_shop" });

        if (!DungeonService.TryBuy(run, item))
            return Serialize(new { ok = false, error = "not_enough_gold", gold = run.Gold });

        DS.Save(run, pid);
        return Serialize(new { ok = true, gold = run.Gold, run });
    }

    // ── Relics ────────────────────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetRelics()
        => Serialize(new { relics = Relics.All });

    // ── Season / Daily ────────────────────────────────────────────────────────────────────────

    [JSExport]
    public static string GetSeason(string pid)
    {
        var p     = Store.Load(pid);
        var now   = DateTimeOffset.UtcNow;
        var clock = SeasonClock.Default;
        int seasonId = clock.SeasonId(now), weekId = clock.WeekId(now);

        SeasonService.EnsureSeason(p, seasonId);
        EventService.EnsureEvent(p, weekId);
        Store.Save(p, pid);

        // Keep ranks fresh in the in-memory board.
        LeaderboardService.Submit(_lb, pid, pid, SeasonService.BoardId,
            seasonId.ToString(), SeasonService.SeasonScore(p));
        var ev = Events.Current(weekId);
        if (ev != null)
            LeaderboardService.Submit(_lb, pid, pid,
                EventService.BoardPrefix + ev.Id, weekId.ToString(), p.Season.EventTokens);

        var track = Seasons.Track.Select(t => new
        {
            t.Tier, t.Tokens, t.RewardGems, t.RewardCardDust, t.RewardModuleCores,
            claimed   = p.Season.ClaimedTiers.Contains(t.Tier),
            claimable = !p.Season.ClaimedTiers.Contains(t.Tier) && p.Season.Tokens >= t.Tokens,
        });

        int seasonRank = LeaderboardService
            .GenerateCohort(_lb, pid, pid, SeasonService.BoardId, seasonId.ToString())
            .First(e => e.IsMe).Rank;

        object? evObj = ev == null ? null : (object)new
        {
            ev.Id, ev.Name, ev.Effect, ev.Magnitude, ev.MilestoneTokens,
            ev.RewardModuleCores, ev.RewardGems,
            tokens    = p.Season.EventTokens,
            claimed   = p.Season.EventClaimed,
            claimable = !p.Season.EventClaimed && p.Season.EventTokens >= ev.MilestoneTokens,
        };

        return Serialize(new
        {
            seasonId,
            theme        = Seasons.ThemeFor(seasonId),
            tokens       = p.Season.Tokens,
            seasonEndsAt = clock.SeasonEndsAt(now),
            weekEndsAt   = clock.WeekEndsAt(now),
            track,
            seasonRank,
            ev           = evObj,
        });
    }

    [JSExport]
    public static string ClaimSeasonTier(string pid, int tier)
    {
        var p        = Store.Load(pid);
        var clock    = SeasonClock.Default;
        int seasonId = clock.SeasonId(DateTimeOffset.UtcNow);
        var res      = SeasonService.ClaimTier(p, Seasons, seasonId, tier);
        if (res.Ok) Store.Save(p, pid);
        return Serialize(new { ok = res.Ok, res.Gems, res.CardDust, res.ModuleCores });
    }

    [JSExport]
    public static string ClaimEvent(string pid)
    {
        var p      = Store.Load(pid);
        var clock  = SeasonClock.Default;
        int weekId = clock.WeekId(DateTimeOffset.UtcNow);
        var ev     = Events.Current(weekId);
        var res    = ev == null
            ? new SeasonClaimResult { Ok = false }
            : EventService.ClaimMilestone(p, ev, weekId);
        if (res.Ok) Store.Save(p, pid);
        return Serialize(new { ok = res.Ok, res.Gems, res.ModuleCores });
    }

    [JSExport]
    public static string GetDaily(string pid)
    {
        var p     = Store.Load(pid);
        var clock = SeasonClock.Default;
        var now   = DateTimeOffset.UtcNow;

        DailyService.EnsureToday(p, Missions, clock.DayId(now), clock.WeekId(now));
        Store.Save(p, pid);

        var missions = p.Daily.Missions.Select(ms =>
        {
            Missions.TryGet(ms.Id, out var def);
            return new
            {
                id             = ms.Id,
                name           = def?.Name           ?? ms.Id,
                metric         = def?.Metric         ?? "",
                target         = def?.Target         ?? 0,
                progress       = ms.Progress,
                claimed        = ms.Claimed,
                rewardGems     = def?.RewardGems     ?? 0,
                rewardCardDust = def?.RewardCardDust ?? 0,
                complete       = def != null && ms.Progress >= def.Target,
            };
        });

        return Serialize(new
        {
            missions,
            streak       = p.Daily.Streak,
            streakTarget = DailyService.StreakTarget,
            dayEndsAt    = clock.DayEndsAt(now),
            gems         = p.Crystals,
            cardDust     = p.CardDust,
        });
    }

    [JSExport]
    public static string ClaimDaily(string pid, string missionId)
    {
        var p     = Store.Load(pid);
        var clock = SeasonClock.Default;
        var now   = DateTimeOffset.UtcNow;
        var res   = DailyService.Claim(p, Missions, clock.DayId(now), clock.WeekId(now), missionId);
        if (res.Ok) Store.Save(p, pid);
        return Serialize(new
        {
            ok          = res.Ok,
            gems        = res.Gems,
            cardDust    = res.CardDust,
            streakBonus = res.StreakBonus,
            streak      = res.Streak,
        });
    }

    // ── Social stubs (no remote server in WASM) ───────────────────────────────────────────────

    [JSExport]
    public static string GetLeague()
        => Serialize(new { entries = System.Array.Empty<object>(), resolved = false });

    [JSExport]
    public static string SubmitScore(string board, int score)
        => Serialize(new { ok = true, accepted = false });
}

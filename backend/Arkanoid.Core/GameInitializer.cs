using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;

namespace Arkanoid.Core;

/// <summary>
/// Builds a ready-to-run GameInstance from level + profile + dungeon state.
/// Extracted from GameSession so that session owns only transport and the tick loop.
/// </summary>
public static class GameInitializer
{
    private static CardCatalog? _cards;
    /// <summary>Card catalog, loaded once from config (cached). Empty catalog if the file is absent.</summary>
    private static CardCatalog Cards(string configRoot)
        => _cards ??= System.IO.File.Exists(System.IO.Path.Combine(configRoot, "cards.json"))
            ? CardCatalog.FromFile(System.IO.Path.Combine(configRoot, "cards.json"))
            : CardCatalog.FromJson("{\"cards\":[]}");

    private static ModuleCatalog? _modules;
    private static ModuleCatalog Modules(string configRoot)
        => _modules ??= System.IO.File.Exists(System.IO.Path.Combine(configRoot, "modules.json"))
            ? ModuleCatalog.FromFile(System.IO.Path.Combine(configRoot, "modules.json"))
            : ModuleCatalog.FromJson("{\"modules\":[]}");

    private static EventCatalog? _events;
    private static EventCatalog Events(string configRoot)
        => _events ??= System.IO.File.Exists(System.IO.Path.Combine(configRoot, "events.json"))
            ? EventCatalog.FromFile(System.IO.Path.Combine(configRoot, "events.json"))
            : EventCatalog.FromJson("{\"events\":[]}");

    private static CharacterCatalog? _chars;
    /// <summary>Character catalog from config (cached). The FILE carries spell icons/desc/affinity;
    /// the embedded CharacterCatalog.Default has empty icons, which left the hotbar iconless. Using the
    /// file catalog here is the single source of truth for both the sim's CastSlot and the HUD loadout.</summary>
    private static CharacterCatalog Characters(string configRoot)
        => _chars ??= System.IO.File.Exists(System.IO.Path.Combine(configRoot, "characters.json"))
            ? CharacterCatalog.FromFile(System.IO.Path.Combine(configRoot, "characters.json"))
            : CharacterCatalog.Default;

    public static GameInstance Build(
        string levelId,
        int seed,
        string configRoot,
        BlockCatalog blockCatalog,
        RelicCatalog relicCatalog,
        BonusCatalog? bonusCatalog,
        IProfileStore profileStore,
        IDungeonStore dungeonStore,
        string pid,
        ISimLog log)
    {
        var run = dungeonStore.Load(pid);
        // Continuous Rift (2026-06-16): the whole rift is ONE GameInstance — every floor is stacked as an
        // ExtraFloor, so clearing a floor slides the next in (ball/HP/mana/balls carry; no reload).
        bool isRift = run is { Active: true, IsRift: true } && run.Floors.Count > 0;
        string LevelPath(string id) => System.IO.Path.Combine(configRoot, "levels", $"{id}.json");
        var level = isRift
            ? LevelLoader.FromRiftFloorFiles(run!.Floors.Select(LevelPath), blockCatalog)
            : LevelLoader.FromFile(LevelPath(levelId), blockCatalog);
        var chars = Characters(configRoot);
        var game = new GameInstance(level, SimConfig.Default, seed, log, relicCatalog, bonusCatalog, chars: chars);
        if (isRift) game.SetRiftMode(true);

        var profile = profileStore.Load(pid);
        game.SetSpellLevels(profile.SpellLevels);
        game.SetCharacter(profile.SelectedCharacter);

        // Stat engine (design §5): resolve the selected hero's stats from its Level/★ + account
        // Masteries and wire them into the run (Power→hit damage, Vitality→HP, Crit→roll,
        // Multiball→extra serves, Tempo→regen). HP is overridden below by dungeon-carry if active.
        var heroProg = profile.HeroProgress.TryGetValue(profile.SelectedCharacter, out var hp)
            ? hp : new HeroProgress();
        StatResolver.Apply(
            StatResolver.Resolve(profile.SelectedCharacter, heroProg.Level, heroProg.Stars, profile.Masteries),
            game);
        // §5.5 behavioral perks (★3/★5, Necro ★1) active for this run.
        game.SetPerks(StatResolver.PerksFor(profile.SelectedCharacter, heroProg.Stars));

        // Equip the player's loadout (signature + drafted picks) so the hotbar and CastSlot
        // index the same ordered list (docs/04 §3). CharacterCatalog.Default matches the
        // embedded catalog the sim's CastSlot resolves against.
        var loadout = Loadouts.Resolve(profile, chars, profile.SelectedCharacter);
        game.SetLoadout(loadout);
        // Spell affinity (economy rework §3): equipped spells matching the hero's element pay less mana.
        game.SetSpellAffinity(
            SpellAffinity.MatchedAmong(loadout, profile.SelectedCharacter, chars),
            SpellAffinity.MatchManaMult);

        // Item shop removed (economy rework 2026-06-15) — passives now come from Cards (surfaced as "Items").
        // Equipped Cards (plan §A.1) + Modules (plan §B.2) — the persistent passive layers.
        CardEffects.Apply(profile, Cards(configRoot), game);
        ModuleEffects.Apply(profile, Modules(configRoot), game);
        // Live event modifier (plan §C) — "changes the board" for every battle while the event runs.
        var liveEvent = Events(configRoot).Current(SeasonClock.Default.WeekId(System.DateTimeOffset.UtcNow));
        if (liveEvent != null) EventService.ApplyModifier(game, liveEvent);
        ItemEffects.Commit(game); // shared finalizer: commits card + module + event ManaMax bonuses

        if (run is { Active: true } && (isRift || run.CurrentFloor == levelId))
        {
            foreach (var relicId in run.Relics)   game.AddRelic(relicId);
            foreach (var coreId  in run.BallCores) game.AddBallCore(coreId);
            foreach (var modId   in run.PaddleMods) game.AddPaddleMod(modId);
            // In-run drafted spells (docs/04 §5) extend the equipped loadout for this run, up to
            // the hotbar cap — a dungeon run can grow a bigger kit than the persistent slot count.
            foreach (var spellId in run.DraftedSpells) game.DraftSpell(spellId, Loadouts.MaxSlots);
            if (run.Hp > 0) game.SetHp(run.Hp); // carry HP across floors (docs/04 §6.2 permadeath)
            if (run.Gold > 0) game.SetGold(run.Gold); // carry Gold across floors (docs/04 §5)
            // §7 Rift: re-apply the chosen §8 run modifiers + carry the reward multiplier for the live HUD.
            if (run.IsRift) { RiftModifierService.ApplyToGame(run, game); game.RiftRewardMult = run.RewardMult; }
            DungeonService.ApplyTier(game, run.Tier);
            // Miniboss floor (docs/04 §6.2): legacy per-floor dungeons only (rifts play continuously).
            if (!isRift && DungeonService.IsMinibossFloor(run, run.FloorIndex))
            {
                DungeonService.ApplyMiniboss(game);
                game.SetMinibossFloor(true);
            }
        }
        else if (profile.PrestigeTier > 0)
        {
            // Campaign battle in a prestige loop (plan §B.1): harder + remixed New Game+.
            PrestigeService.ApplyMutators(game, profile.PrestigeTier, seed);
        }

        return game;
    }
}

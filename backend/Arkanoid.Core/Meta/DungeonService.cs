using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure-logic functions for dungeon run lifecycle.
/// No file I/O, no HTTP — all mutation happens on a <see cref="DungeonRun"/> POCO.
/// </summary>
public static class DungeonService
{
    // The bonus pool: relic ids + ball-core ids available as floor-clear choices.
    // The G2 relic web (docs/09): standalone-good, build-enablers, tradeoffs, and
    // biome-conditional picks — variance per the docs/04 §7 choice rules.
    private static readonly string[] RelicPool    =
    {
        "glass_cannon", "flint_core", "pyroclasm", "mana_battery",
        "conductor", "overcharge", "split_shot", "souljar", "lodestone",
        "ember_heart", "second_wind", "midas", "lead_paddle",
        "sapper", "hellwalker", "ghost_lens", "pillar_doctrine",
    };
    private static readonly string[] BallCorePool = { "heavy", "split", "ember", "ghost", "echo", "frost" };
    /// <summary>Paddle mods — the fourth build axis (docs/04 §4.4).</summary>
    private static readonly string[] PaddleModPool = { "mod_wide", "mod_grip", "mod_cannons" };

    /// <summary>Floor-clear spell picks (docs/04 §5) are tagged with this prefix so PickChoice and the
    /// HUD overlay can tell a drafted spell apart from a relic/core/mod by id alone.</summary>
    public const string SpellPrefix = "spell:";
    /// <summary>Heal pick (docs/04 §5): restores HP for the rest of the run; capped to a sane ceiling.</summary>
    public const int HealAmount = 2;
    public const int MaxRunHp = 9;

    // Shop-floor prices in Gold (docs/04 §5 "spent at shops ... on spells, relics, heals"). Tuned so a
    // floor or two of coins buys one item — relics/spells are the premium picks, heal the cheap fallback.
    public const int ShopPriceRelic     = 15;
    public const int ShopPriceSpell     = 18;
    public const int ShopPriceCore      = 12;
    public const int ShopPricePaddleMod = 10;
    public const int ShopPriceHeal      = 6;
    /// <summary>The draftable spell pool = every non-signature spell (the shared pool, docs/04 §3).</summary>
    private static IReadOnlyList<string> SpellPool() => CharacterCatalog.Default.Pool();
    private static bool IsSpell(string id) => id.StartsWith(SpellPrefix, System.StringComparison.Ordinal);
    private static string SpellIdOf(string choiceId) => choiceId.Substring(SpellPrefix.Length);

    /// <summary>
    /// Creates a new active run from the given dungeon definition.
    /// FloorIndex starts at 0; no pending choices yet.
    /// </summary>
    public static DungeonRun StartRun(DungeonDef def, int seed)
    {
        return new DungeonRun
        {
            DungeonId      = def.Id,
            Floors         = new List<string>(def.Floors),
            FloorIndex     = 0,
            Relics         = new List<string>(),
            BallCores      = new List<string>(),
            PaddleMods     = new List<string>(),
            PendingChoices = new List<string>(),
            Active         = true,
            Cleared        = false,
            Seed           = seed,
            Tier           = def.Tier,
            RewardRelic    = def.RewardRelic,
            RewardCrystals = def.RewardCrystals,
            IsRift         = def.IsRift,   // §7: drives the §8 modifier picks + depth rewards
            RewardMult     = 1.0,
        };
    }

    /// <summary>
    /// Called when the player clears the current floor.
    /// Returns <c>true</c> if this was the FINAL floor (run is now cleared).
    /// On a non-final floor, populates <see cref="DungeonRun.PendingChoices"/> with 3 distinct options;
    /// the run does NOT advance until <see cref="PickChoice"/> is called.
    /// <paramref name="cfg"/> is accepted for signature compatibility but not required for logic.
    /// </summary>
    public static bool OnFloorCleared(DungeonRun run, ProgressionConfig? cfg = null)
    {
        if (!run.Active) return false;

        bool isLastFloor = run.FloorIndex == run.Floors.Count - 1;

        if (isLastFloor)
        {
            run.Cleared = true;
            run.Active  = false;
            return true;
        }

        // §7 Rift: the between-level pick is a 1-of-3 §8 RUN MODIFIER (not a permanent content draft).
        if (run.IsRift)
            run.PendingChoices = RiftModifierService.Offer(run.Seed + run.FloorIndex, run.RiftModifiers)
                .Select(m => m.Id).ToList();
        else
            run.PendingChoices = GenerateChoices(run, 3); // legacy dungeon content draft
        return false;
    }

    /// <summary>
    /// The player picks one of the 3 pending choices.
    /// Adds it to <see cref="DungeonRun.Relics"/> or <see cref="DungeonRun.BallCores"/>,
    /// clears PendingChoices, and advances FloorIndex.
    /// No-ops if choiceId is not in PendingChoices.
    /// </summary>
    public static void PickChoice(DungeonRun run, string choiceId, int heroMaxHp = 0)
    {
        if (!run.PendingChoices.Contains(choiceId)) return;

        // §7 Rift: the choice is a §8 run modifier — apply its run-state effects + record it, then advance.
        // heroMaxHp (the player's true resolved max) seeds the rift's HP pool so Field Medic/Ironclad heal to
        // the real maximum, not whatever HP remained when the first pick happened.
        if (run.IsRift)
        {
            int basis = heroMaxHp > 0 ? heroMaxHp : (run.RiftMaxHp > 0 ? run.RiftMaxHp : run.Hp);
            RiftModifierService.Pick(run, choiceId, basis);
            run.PendingChoices.Clear();
            run.FloorIndex++;
            return;
        }

        if (choiceId == "heal")
            run.Hp = System.Math.Min(MaxRunHp, (run.Hp > 0 ? run.Hp : 3) + HealAmount); // docs/04 §5 heal pick
        else if (choiceId == "shop")
            { /* docs/04 §6.2: "shop" is the mechanism — boons are acquired via TryBuy before this pick. */ }
        else if (IsSpell(choiceId))
        {
            var spellId = SpellIdOf(choiceId);
            if (!run.DraftedSpells.Contains(spellId)) run.DraftedSpells.Add(spellId);
        }
        else if (IsRelic(choiceId))
            run.Relics.Add(choiceId);
        else if (IsPaddleMod(choiceId))
            run.PaddleMods.Add(choiceId);
        else
            run.BallCores.Add(choiceId);

        run.PendingChoices.Clear();
        run.FloorIndex++;
    }

    /// <summary>
    /// Generate a dungeon shop floor's inventory (docs/04 §6.2 shop pick, §5 sells spells/relics/heals).
    /// Deterministic per run+floor so the server can re-derive the same list to validate a buy request.
    /// Mixes the categories (spell / relic / core / mod) like the floor-clear offer, minus already-owned
    /// picks, and always sells a heal as a reliable fallback. Returns up to <paramref name="count"/> items.
    /// </summary>
    public static List<ShopItem> GenerateShopItems(DungeonRun run, int count = 3)
    {
        var candidates = new List<ShopItem>();
        foreach (var id in SpellPool())
            if (!run.DraftedSpells.Contains(id)) candidates.Add(new ShopItem { Id = id, Kind = "spell", Price = ShopPriceSpell });
        foreach (var id in RelicPool)
            if (!run.Relics.Contains(id)) candidates.Add(new ShopItem { Id = id, Kind = "relic", Price = ShopPriceRelic });
        foreach (var id in BallCorePool)
            if (!run.BallCores.Contains(id)) candidates.Add(new ShopItem { Id = id, Kind = "core", Price = ShopPriceCore });
        foreach (var id in PaddleModPool)
            if (!run.PaddleMods.Contains(id)) candidates.Add(new ShopItem { Id = id, Kind = "paddleMod", Price = ShopPricePaddleMod });
        // Heal is always stocked — a dependable purchase when nothing else fits the build.
        candidates.Add(new ShopItem { Id = "heal", Kind = "heal", Price = ShopPriceHeal });

        // Rank by a STABLE per-item key (not a shuffle): buying an item removes it from the pool but
        // leaves every other item's rank unchanged, so the remaining displayed items stay buyable
        // across multiple purchases in one visit. Salt by seed+floor so floors differ.
        int salt = run.Seed ^ (run.FloorIndex * unchecked((int)0x6c62272e));
        candidates.Sort((a, b) =>
        {
            int ka = StableKey(salt, a.Id), kb = StableKey(salt, b.Id);
            return ka != kb ? ka.CompareTo(kb) : string.CompareOrdinal(a.Id, b.Id);
        });

        var picked = new List<ShopItem>(count);
        foreach (var c in candidates)
        {
            if (picked.Count >= count) break;
            picked.Add(c);
        }
        return picked;
    }

    /// <summary>
    /// Buy a shop item (docs/04 §5): if the run can afford it, deduct the Gold and apply the boon
    /// (relic / core / mod / spell to the run's build lists, or a heal to run HP). Returns false and
    /// leaves the run untouched when Gold is insufficient. Idempotent for already-owned build items.
    /// </summary>
    public static bool TryBuy(DungeonRun run, ShopItem item)
    {
        if (run.Gold < item.Price) return false;
        run.Gold -= item.Price;
        switch (item.Kind)
        {
            case "heal":
                run.Hp = System.Math.Min(MaxRunHp, (run.Hp > 0 ? run.Hp : 3) + HealAmount);
                break;
            case "spell":
                if (!run.DraftedSpells.Contains(item.Id)) run.DraftedSpells.Add(item.Id);
                break;
            case "relic":
                if (!run.Relics.Contains(item.Id)) run.Relics.Add(item.Id);
                break;
            case "paddleMod":
                if (!run.PaddleMods.Contains(item.Id)) run.PaddleMods.Add(item.Id);
                break;
            case "core":
                if (!run.BallCores.Contains(item.Id)) run.BallCores.Add(item.Id);
                break;
        }
        return true;
    }

    /// <summary>
    /// Ascension (docs/04 §10): tier N hardens every destructible non-boss block by
    /// +N HP. Called by the server when a dungeon-floor battle instance starts.
    /// </summary>
    public static void ApplyTier(GameInstance g, int tier)
    {
        if (tier <= 0) return;
        foreach (var b in g.Blocks)
        {
            if (b.Dead || b.Indestructible || b.Boss || !b.NeedToKill) continue;
            b.Hp    += tier;
            b.MaxHp += tier;
        }
        g._log.Log(g.TickCount, "ascension", "blocks hardened", $"tier={tier}");
    }

    /// <summary>The mid-floor of a 3+-floor run is a miniboss floor (docs/04 §6.2: minibosses mid,
    /// boss at the end).</summary>
    public static bool IsMinibossFloor(DungeonRun run, int floorIndex)
        => run.Floors.Count >= 3 && floorIndex == (run.Floors.Count - 1) / 2;

    /// <summary>
    /// Miniboss floor (docs/04 §6.2): a mid-run difficulty spike — harden every destructible block
    /// and add an elite biome enemy (a hardened beholder that fires at the ball). The player must
    /// defeat it to clear the floor; the reward is boosted at floor-clear.
    /// </summary>
    public static void ApplyMiniboss(GameInstance g)
    {
        foreach (var b in g.Blocks)
            if (!b.Dead && !b.Indestructible && !b.Boss && b.NeedToKill)
            { b.Hp += 2; b.MaxHp += 2; }

        // Place the elite at the first free cell scanning from top-centre outward.
        int cols = g.Level.Grid.Cols, rows = g.Level.Grid.Rows;
        for (int r = 0; r < System.Math.Min(rows, 4); r++)
        {
            for (int off = 0; off <= cols / 2; off++)
            {
                foreach (int c in new[] { cols / 2 + off, cols / 2 - off })
                {
                    if (c < 0 || c >= cols || g.BlockAt(c, r) != null) continue;
                    g.Blocks.Add(new Entities.Block
                    {
                        Id = g.NextBlockId(), Col = c, Row = r,
                        Hp = 12, MaxHp = 12, TypeId = "miniboss", Sprite = "Beholder1",
                        NeedToKill = true, Behavior = Entities.BlockBehavior.Emitter,
                        EmitInterval = 1.3, EmitAim = "ball", MissileKind = "beholdermissile",
                    });
                    g._log.Log(g.TickCount, "dungeon", "miniboss spawned", $"cell={c},{r}");
                    return;
                }
            }
        }
    }

    /// <summary>Permadeath: the run ends without clearing. All buffs are lost.</summary>
    public static void Fail(DungeonRun run)
    {
        run.Active  = false;
        run.Cleared = false;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static bool IsRelic(string id) => System.Array.IndexOf(RelicPool, id) >= 0;
    private static bool IsPaddleMod(string id) => System.Array.IndexOf(PaddleModPool, id) >= 0;

    /// <summary>Stable per-item ranking key (salt + id) — deterministic and order-independent so a shop's
    /// displayed items stay valid as items are bought out of it.</summary>
    private static int StableKey(int salt, string id)
    {
        int h = salt;
        foreach (var c in id) h = unchecked(h * 31 + c);
        return h;
    }

    private static List<string> GenerateChoices(DungeonRun run, int count)
    {
        // Deterministic RNG seeded by run.Seed XOR'd with FloorIndex so each floor is distinct.
        var rng = new Rng(run.Seed ^ (run.FloorIndex * unchecked((int)0x9e3779b9)));
        var choices = new List<string>(count);

        // docs/04 §5: every floor-clear pick is a deliberate MIX. Reserve exactly one slot for a
        // drafted-spell offer (when spells remain undrafted); fill the rest from the
        // dungeon-EXCLUSIVE relic/core/mod pool. Without this, dumping all ~16 pool spells in made
        // picks ~40% spells and crowded out the relics/cores dungeons exist to grant.
        var spellsAvail = new List<string>();
        foreach (var id in SpellPool())
            if (!run.DraftedSpells.Contains(id)) spellsAvail.Add(id);
        if (spellsAvail.Count > 0)
            choices.Add(SpellPrefix + spellsAvail[rng.Range(spellsAvail.Count)]);

        // Non-spell pool, minus already-owned picks when possible.
        var pool = new List<string>();
        foreach (var id in RelicPool)
            if (!run.Relics.Contains(id)) pool.Add(id);
        foreach (var id in BallCorePool)
            if (!run.BallCores.Contains(id)) pool.Add(id);
        foreach (var id in PaddleModPool)
            if (!run.PaddleMods.Contains(id)) pool.Add(id);
        pool.Add("heal"); // docs/04 §5: heal is one of the mixed pick categories
        pool.Add("shop"); // docs/04 §6.2/§7: a shop floor is one of the mixed pick categories

        // Degenerate fallback (almost everything owned): allow repeats from the full non-spell pool.
        if (pool.Count < count - choices.Count)
        {
            pool.Clear();
            pool.AddRange(RelicPool);
            pool.AddRange(BallCorePool);
            pool.AddRange(PaddleModPool);
        }

        var available = new List<string>(pool);
        while (choices.Count < count && available.Count > 0)
        {
            int idx = rng.Range(available.Count);
            choices.Add(available[idx]);
            available.RemoveAt(idx);
        }

        // Shuffle so the spell isn't always pinned to slot 0.
        for (int i = choices.Count - 1; i > 0; i--)
        {
            int j = rng.Range(i + 1);
            (choices[i], choices[j]) = (choices[j], choices[i]);
        }

        return choices;
    }
}

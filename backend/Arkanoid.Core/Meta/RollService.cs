using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

public enum RollKind { Card, Module, Spell, Hero }

/// <summary>Outcome of one pull. <see cref="WasNew"/> = first copy (an unlock); otherwise a duplicate that
/// banked a COPY (<see cref="Copies"/>, spent later to manually level — owner direction 2026-06-15) or ascended
/// the hero (<see cref="Stars"/>). <see cref="Wasted"/> = the pull landed on an already-maxed entry (pure-random,
/// no dupe protection — docs/2026-06-14 §2). <see cref="Level"/> is the entry's current level after the pull.</summary>
public readonly record struct RollResult(RollKind Kind, string Id, bool WasNew, int Level, int Stars, bool Wasted, int Copies);

/// <summary>
/// Pure, fixed-price RANDOM pulls (economy rework §2). The CALLER spends the coin; this only mutates the
/// collection. Pure-random over the FULL pool — duplicates level the item / ascend the hero, and a pull onto
/// a maxed entry is Wasted. Use the <c>CanRoll*</c> guards to disable a fully-maxed pool (the terminal rule).
/// </summary>
public static class RollService
{
    /// <summary>Spell level cap — single source of truth lives in <see cref="SpellService.MaxSpellLevel"/>.</summary>
    public const int MaxSpellLevel = SpellService.MaxSpellLevel;

    // ── Fixed roll prices (economy rework §10; tunable). Card/Module spend Sparks; Spell/Hero spend Souls. ──
    public const int CardRollCost   = 30;
    public const int ModuleRollCost = 40;
    public const int SpellRollCost  = 35;  // lowered from 50 (balance: ~12 wins = 1 roll)
    public const int HeroRollCost   = 80;

    public static RollResult RollCard(Profile p, CardCatalog cat, Rng rng)
    {
        var ids = cat.Cards.Select(c => c.Id).ToList();
        if (ids.Count == 0) return new(RollKind.Card, "", false, 0, 0, true, 0);
        var id = ids[rng.Range(ids.Count)];
        if (!p.OwnedCards.TryGetValue(id, out var own))
        {
            CardService.Grant(p, id); // first → owned at level 1
            return new(RollKind.Card, id, true, 1, 0, false, 0);
        }
        if (own.Level >= CardService.MaxCardLevel)
            return new(RollKind.Card, id, false, own.Level, 0, true, own.Copies); // maxed → copies useless
        CardService.Grant(p, id); // dupe → +1 banked copy
        return new(RollKind.Card, id, false, own.Level, 0, false, own.Copies);
    }

    public static RollResult RollModule(Profile p, ModuleCatalog cat, Rng rng)
    {
        var ids = cat.Modules.Select(m => m.Id).ToList();
        if (ids.Count == 0) return new(RollKind.Module, "", false, 0, 0, true, 0);
        var id = ids[rng.Range(ids.Count)];
        if (!p.OwnedModules.TryGetValue(id, out var lvl))
        {
            ModuleService.Grant(p, id);
            return new(RollKind.Module, id, true, 1, 0, false, 0);
        }
        if (lvl >= ModuleService.MaxModuleLevel)
            return new(RollKind.Module, id, false, lvl, 0, true, p.ModuleCopies.GetValueOrDefault(id));
        ModuleService.Grant(p, id); // dupe → +1 banked copy
        return new(RollKind.Module, id, false, lvl, 0, false, p.ModuleCopies.GetValueOrDefault(id));
    }

    public static RollResult RollSpell(Profile p, CharacterCatalog cat, Rng rng)
    {
        var pool = cat.Pool(); // non-signature spell ids (the global pool)
        if (pool.Count == 0) return new(RollKind.Spell, "", false, 0, 0, true, 0);
        var id = pool[rng.Range(pool.Count)];
        if (!p.SpellLevels.TryGetValue(id, out var lvl))
        {
            SpellService.Grant(p, id); // first → owned at level 1
            return new(RollKind.Spell, id, true, 1, 0, false, 0);
        }
        if (lvl >= SpellService.MaxSpellLevel)
            return new(RollKind.Spell, id, false, lvl, 0, true, p.SpellCopies.GetValueOrDefault(id));
        SpellService.Grant(p, id); // dupe → +1 banked copy
        return new(RollKind.Spell, id, false, lvl, 0, false, p.SpellCopies.GetValueOrDefault(id));
    }

    public static RollResult RollHero(Profile p, Rng rng)
    {
        if (p.HeroPool.Count == 0) return new(RollKind.Hero, "", false, 0, 0, true, 0);
        var id = p.HeroPool[rng.Range(p.HeroPool.Count)];
        if (!p.HeroProgress.TryGetValue(id, out var hp)) { hp = new HeroProgress(); p.HeroProgress[id] = hp; }
        if (!p.UnlockedCharacters.Contains(id))
        {
            p.UnlockedCharacters.Add(id); // first card → unlock the hero
            return new(RollKind.Hero, id, true, 0, hp.Stars, false, 0);
        }
        if (hp.Stars >= StatResolver.MaxStars)
            return new(RollKind.Hero, id, false, 0, hp.Stars, true, hp.AscendPips);   // already ★6 (maxed)
        hp.AscendPips++; // dupe → BANK a pip toward the next ★. Ascension is MANUAL now (Upgrades.TryAscendHero),
                         // not automatic (owner direction 2026-06-15), to match cards/modules/spells.
        return new(RollKind.Hero, id, false, 0, hp.Stars, false, hp.AscendPips);
    }

    // ── Maxed-pool terminal guards (false ⇒ disable the roll: nothing can still improve) ──
    public static bool CanRollCard(Profile p, CardCatalog cat) =>
        cat.Cards.Any(c => !p.OwnedCards.TryGetValue(c.Id, out var o) || o.Level < CardService.MaxCardLevel);

    public static bool CanRollModule(Profile p, ModuleCatalog cat) =>
        cat.Modules.Any(m => !p.OwnedModules.TryGetValue(m.Id, out var l) || l < ModuleService.MaxModuleLevel);

    public static bool CanRollSpell(Profile p, CharacterCatalog cat) =>
        cat.Pool().Any(id => !p.SpellLevels.TryGetValue(id, out var l) || l < MaxSpellLevel);

    public static bool CanRollHero(Profile p) =>
        p.HeroPool.Any(id => !p.UnlockedCharacters.Contains(id)
            || !p.HeroProgress.TryGetValue(id, out var hp) || hp.Stars < StatResolver.MaxStars);
}

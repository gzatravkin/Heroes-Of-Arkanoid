using System.Collections.Generic;
using System.Linq;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Spell affinity (economy rework §3): the global spell pool is hero-agnostic, but a spell cast by a hero of
/// its matching element gets a small bonus — a mana discount — so heroes keep a soft identity ("Fire Mage
/// runs fire best") without locking the pool. Pure lookup over the catalog's <c>affinity</c> field.
/// </summary>
public static class SpellAffinity
{
    /// <summary>Hero id → element. Heroes not listed have no affinity (never match).</summary>
    private static readonly Dictionary<string, string> HeroElement = new()
    {
        ["fire_mage"]   = "fire",
        ["paladin"]     = "holy",
        ["engineer"]    = "tech",
        ["necromancer"] = "death",
    };

    /// <summary>Matching affinity ⇒ ×0.8 mana cost (−20%). The single soft-identity lever.</summary>
    public const double MatchManaMult = 0.8;

    /// <summary>The element of <paramref name="heroId"/>, or "neutral" if unknown.</summary>
    public static string ElementOf(string heroId) => HeroElement.GetValueOrDefault(heroId, "neutral");

    /// <summary>A spell's element: an explicit <c>affinity</c> field wins; otherwise it inherits the element
    /// of the character that AUTHORED it (its Spells list); the class-less neutral pool stays neutral.</summary>
    public static string SpellElement(string spellId, CharacterCatalog cat)
    {
        var disp = cat.DisplayOf(spellId);
        if (disp != null && disp.Affinity != "neutral") return disp.Affinity; // explicit override
        foreach (var c in cat.All)
            if (c.Spells.Any(s => s.Id == spellId)) return ElementOf(c.Id);
        return "neutral";
    }

    /// <summary>True when <paramref name="spellId"/>'s element matches <paramref name="heroId"/>'s
    /// (and neither is neutral).</summary>
    public static bool Matches(string spellId, string heroId, CharacterCatalog cat)
    {
        var heroEl = ElementOf(heroId);
        if (heroEl == "neutral") return false;
        return SpellElement(spellId, cat) == heroEl;
    }

    /// <summary>The subset of <paramref name="spellIds"/> that match <paramref name="heroId"/>'s element —
    /// the ids the run should give the mana discount (wired by GameInitializer into SetSpellAffinity).</summary>
    public static List<string> MatchedAmong(IEnumerable<string> spellIds, string heroId, CharacterCatalog cat)
        => spellIds.Where(id => Matches(id, heroId, cat)).ToList();
}

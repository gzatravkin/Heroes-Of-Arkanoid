using System.Collections.Generic;
using System.Linq;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Resolves a character's effective spell loadout and ownership from a profile + catalog.
/// Single source of truth shared by the sim wiring (GameInitializer) and the HTTP equip
/// endpoints, so the hotbar the player edits is exactly the one CastSlot indexes.
/// Model (docs/04 §3, §4.1):
///   • slot 0 is always the character's signature (locked, never draftable),
///   • slots 1..N-1 are drafted spells the player owns, N = unlocked slot count,
///   • "owned" = a spell with a SpellLevels entry, plus the character's starting set.
/// </summary>
public static class Loadouts
{
    public const int MinSlots = 1;
    /// <summary>Hotbar cap: signature + up to 3 flex spells = 4 (economy rework §3).</summary>
    public const int MaxSlots = 4;

    /// <summary>Clamp the profile's unlocked-slot count to the supported hotbar range.</summary>
    public static int SlotCount(Profile p) => System.Math.Clamp(p.UnlockedSpellSlots, MinSlots, MaxSlots);

    /// <summary>The spells the player may equip on <paramref name="charId"/>: the signature plus every
    /// owned, non-signature pool spell. Owned = SpellLevels keys ∪ the character's starting set.</summary>
    public static List<string> OwnedFor(Profile p, CharacterCatalog chars, string charId)
    {
        var result = new List<string>();
        if (!chars.TryGet(charId, out var c)) return result;
        var sig = c.SignatureId;
        if (sig.Length > 0) result.Add(sig);

        var signatures = chars.Signatures;
        var owned = new HashSet<string>(p.SpellLevels.Keys);
        foreach (var s in c.Starting) owned.Add(s);

        // Stable, pool-ordered listing of owned, non-signature spells.
        foreach (var id in chars.Pool())
            if (owned.Contains(id) && !signatures.Contains(id) && id != sig)
                result.Add(id);
        return result;
    }

    /// <summary>The effective, sanitized loadout for a character: signature first, then the profile's
    /// equipped picks (or the default starting loadout), truncated to the unlocked slot count and
    /// filtered to owned spells.</summary>
    public static List<string> Resolve(Profile p, CharacterCatalog chars, string charId)
    {
        int slots = SlotCount(p);
        if (!chars.TryGet(charId, out var c)) return new();
        var sig = c.SignatureId;

        var owned = new HashSet<string>(OwnedFor(p, chars, charId));

        IEnumerable<string> source =
            p.EquippedSpells.TryGetValue(charId, out var eq) && eq.Count > 0
                ? eq
                : chars.DefaultLoadout(charId, slots);

        var ordered = new List<string>();
        if (sig.Length > 0) ordered.Add(sig);
        foreach (var id in source)
        {
            if (id == sig) continue;            // signature is always slot 0
            if (ordered.Contains(id)) continue; // no dupes
            if (!owned.Contains(id)) continue;  // only equip what we own
            ordered.Add(id);
            if (ordered.Count >= slots) break;
        }
        return ordered;
    }

    /// <summary>Equip an owned, non-signature spell into the character's loadout. Persists into
    /// the profile. Returns false if unknown/unowned/already-full. Idempotent if already equipped.</summary>
    public static bool Equip(Profile p, CharacterCatalog chars, string charId, string spellId)
    {
        if (!chars.TryGet(charId, out var c)) return false;
        var sig = c.SignatureId;
        if (spellId == sig) return true;                              // signature is implicit slot 0
        if (!OwnedFor(p, chars, charId).Contains(spellId)) return false;

        var cur = Resolve(p, chars, charId);
        if (cur.Contains(spellId)) return true;
        // Shared FIFO rule: a full hotbar drops the OLDEST drafted spell (never the signature at slot 0).
        Equipping.EquipFifo(cur, spellId, SlotCount(p), id => id != sig);
        p.EquippedSpells[charId] = cur;
        return true;
    }

    /// <summary>Remove a drafted spell from the loadout. The signature cannot be unequipped.</summary>
    public static bool Unequip(Profile p, CharacterCatalog chars, string charId, string spellId)
    {
        if (!chars.TryGet(charId, out var c)) return false;
        if (spellId == c.SignatureId) return false;                  // signature is locked
        var cur = Resolve(p, chars, charId);
        if (!cur.Remove(spellId)) return false;
        p.EquippedSpells[charId] = cur;
        return true;
    }
}

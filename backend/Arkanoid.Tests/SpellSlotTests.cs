using System.Collections.Generic;
using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Spell loadout (economy rework §3): signature is locked in slot 0; the rest are filled from the GLOBAL
/// pool (any spell on any hero) up to the hotbar cap; flex slots grow by spending Souls.
/// </summary>
public class SpellSlotTests
{
    [Fact]
    public void TryUnlockSpellSlot_SpendsSouls_RaisesSlots_UntilCap()
    {
        var p = new Profile { UnlockedSpellSlots = 3, Souls = 1000 };
        Assert.True(Upgrades.TryUnlockSpellSlot(p));          // 3 → 4 costs 3×40 = 120
        Assert.Equal(4, p.UnlockedSpellSlots);
        Assert.Equal(880, p.Souls);
        Assert.False(Upgrades.TryUnlockSpellSlot(p));         // at the cap (signature + 3)
        Assert.Equal(4, p.UnlockedSpellSlots);
    }

    [Fact]
    public void TryUnlockSpellSlot_FailsWhenShortOnSouls()
    {
        var p = new Profile { UnlockedSpellSlots = 3, Souls = 10 }; // needs 120
        Assert.False(Upgrades.TryUnlockSpellSlot(p));
        Assert.Equal(3, p.UnlockedSpellSlots);
        Assert.Equal(10, p.Souls);
    }

    [Fact]
    public void Resolve_SignatureFirst_GlobalCrossHeroSpells_CappedBySlots()
    {
        var chars = CharacterCatalog.Default;
        var p = new Profile { SelectedCharacter = "fire_mage", UnlockedSpellSlots = 4 };
        // Rolled (globally owned) spells, including paladin's "spear" + engineer's "tesla" on the Fire Mage.
        foreach (var id in new[] { "turret", "spear", "tesla" }) p.SpellLevels[id] = 1;
        p.EquippedSpells["fire_mage"] = new List<string> { "turret", "spear", "tesla" };

        var lo = Loadouts.Resolve(p, chars, "fire_mage");
        Assert.Equal("ignite", lo[0]);   // signature locked in slot 0
        Assert.Equal(4, lo.Count);       // signature + 3 flex (the cap)
        Assert.Contains("spear", lo);    // a cross-element spell equipped via the global pool
    }
}

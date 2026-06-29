using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Design-fidelity tests for Loadouts (the profile↔catalog resolver shared by the sim wiring and
/// the equip endpoints). Encodes the docs/04 §3 contract: signature locked in slot 0, the rest
/// drafted from the owned shared pool, capped at the unlocked slot count.
/// </summary>
public class LoadoutResolverTests
{
    private static readonly CharacterCatalog Cat = CharacterCatalog.Default;

    [Fact]
    public void Pool_ExcludesEverySignature()
    {
        var pool = Cat.Pool();
        Assert.Contains("fireball", pool);                 // a draftable spell
        Assert.Contains("skeleton", pool);                 // now a draftable kit spell (raise is the signature)
        foreach (var sig in new[] { "ignite", "shield", "overload", "raise" })
            Assert.DoesNotContain(sig, pool);              // signatures are never draftable
    }

    [Fact]
    public void Resolve_FreshFireMage_IsSignatureThenDefaultStarting()
    {
        var p = Profile.NewDefault();
        var loadout = Loadouts.Resolve(p, Cat, "fire_mage");
        Assert.Equal(new[] { "ignite", "fireball", "firewall" }, loadout);
    }

    [Fact]
    public void Resolve_ForcesSignatureToSlot0_EvenIfEquippedListReorders()
    {
        var p = Profile.NewDefault();
        p.EquippedSpells["fire_mage"] = new List<string> { "fireball", "ignite", "firewall" };
        var loadout = Loadouts.Resolve(p, Cat, "fire_mage");
        Assert.Equal("ignite", loadout[0]); // signature wins slot 0 regardless of stored order
    }

    [Fact]
    public void Resolve_TruncatesToUnlockedSlots()
    {
        var p = Profile.NewDefault();
        p.UnlockedSpellSlots = 2;
        var loadout = Loadouts.Resolve(p, Cat, "fire_mage");
        Assert.Equal(new[] { "ignite", "fireball" }, loadout);
    }

    [Fact]
    public void OwnedFor_IncludesSignatureAndStarting()
    {
        var p = Profile.NewDefault();
        var owned = Loadouts.OwnedFor(p, Cat, "paladin");
        Assert.Contains("shield", owned);    // signature
        Assert.Contains("spear", owned);    // starting
        Assert.Contains("holy_echo", owned); // starting (replaced duplicate 2026-06-25)
    }

    [Fact]
    public void Equip_AddsOwnedSpell_WhenSlotsAvailable()
    {
        var p = Profile.NewDefault();
        p.UnlockedSpellSlots = 5;                 // room beyond the 3 starting
        Assert.True(Loadouts.Equip(p, Cat, "fire_mage", "turret")); // owned via NewDefault SpellLevels
        Assert.Contains("turret", Loadouts.Resolve(p, Cat, "fire_mage"));
    }

    [Fact]
    public void Equip_RejectsUnownedSpell()
    {
        var p = Profile.NewDefault();
        p.UnlockedSpellSlots = 5;
        Assert.False(Loadouts.Equip(p, Cat, "fire_mage", "ashfall")); // not owned yet (not in starting or SpellLevels)
        Assert.DoesNotContain("ashfall", Loadouts.Resolve(p, Cat, "fire_mage"));
    }

    [Fact]
    public void Equip_WhenFull_FifoReplacesOldestDrafted_KeepsSignature()
    {
        // Owner direction 2026-06-15: a full hotbar drops the OLDEST DRAFTED spell (never the signature).
        var p = Profile.NewDefault();         // 3 slots, full: [ignite(sig), fireball, firewall]
        Assert.True(Loadouts.Equip(p, Cat, "fire_mage", "turret"));
        var lo = Loadouts.Resolve(p, Cat, "fire_mage");
        Assert.Equal("ignite", lo[0]);                 // signature stays at slot 0
        Assert.Contains("turret", lo);                 // new pick equipped
        Assert.DoesNotContain("fireball", lo);         // oldest drafted dropped
        Assert.Equal(3, lo.Count);
    }

    [Fact]
    public void Unequip_RefusesSignature()
    {
        var p = Profile.NewDefault();
        Assert.False(Loadouts.Unequip(p, Cat, "fire_mage", "ignite"));
        Assert.Contains("ignite", Loadouts.Resolve(p, Cat, "fire_mage"));
    }

    [Fact]
    public void Unequip_RemovesDraftedSpell()
    {
        var p = Profile.NewDefault();
        Assert.True(Loadouts.Unequip(p, Cat, "fire_mage", "firewall"));
        Assert.DoesNotContain("firewall", Loadouts.Resolve(p, Cat, "fire_mage"));
    }
}

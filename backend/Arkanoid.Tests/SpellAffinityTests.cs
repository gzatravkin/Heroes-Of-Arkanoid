using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>
/// Spell affinity (economy rework §3): the global spell pool is hero-agnostic, but a spell on its matching-
/// element hero gets a mana discount — soft identity without locking the pool. Asserts the element mapping
/// AND that the shared mana gate actually charges less for a matched spell.
/// </summary>
public class SpellAffinityTests
{
    private static CharacterCatalog Chars => CharacterCatalog.Default;

    [Fact]
    public void SpellElement_InheritsFromAuthoringCharacter()
    {
        Assert.Equal("fire",  SpellAffinity.SpellElement("fireball", Chars)); // fire_mage spell
        Assert.Equal("holy",  SpellAffinity.SpellElement("shield",   Chars)); // paladin signature
        Assert.Equal("tech",  SpellAffinity.SpellElement("tesla",    Chars)); // engineer spell
        Assert.Equal("death", SpellAffinity.SpellElement("drain",    Chars)); // necromancer spell
        Assert.Equal("neutral", SpellAffinity.SpellElement("recall", Chars)); // class-less pool spell
    }

    [Fact]
    public void Matches_OnlyForSameElementHero()
    {
        Assert.True (SpellAffinity.Matches("fireball", "fire_mage", Chars));
        Assert.False(SpellAffinity.Matches("fireball", "paladin",  Chars)); // wrong element
        Assert.False(SpellAffinity.Matches("recall",   "fire_mage", Chars)); // neutral never matches
    }

    [Fact]
    public void ManaGate_ChargesLess_ForAffinityMatchedSpell()
    {
        var g = K.OneBlock(5);
        g.SetSpellAffinity(new[] { "ignite" }, SpellAffinity.MatchManaMult); // ×0.8

        g.ManaValue = 100;
        Assert.True(SpellSystem.Spend(g, 20, "ignite"));
        Assert.Equal(84, g.ManaValue, 3); // 100 − (20 × 0.8 = 16)

        g.ManaValue = 100;
        Assert.True(SpellSystem.Spend(g, 20, "firewall")); // not in the affinity set
        Assert.Equal(80, g.ManaValue, 3); // full 20
    }

    [Fact]
    public void MatchedAmong_FiltersLoadoutToHeroElement()
    {
        var matched = SpellAffinity.MatchedAmong(new[] { "fireball", "shield", "recall" }, "fire_mage", Chars);
        Assert.Contains("fireball", matched);
        Assert.DoesNotContain("shield", matched);  // holy
        Assert.DoesNotContain("recall", matched);  // neutral
    }
}

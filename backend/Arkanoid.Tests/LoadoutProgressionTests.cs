using System.Linq;
using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// Design-fidelity tests for spell acquisition (docs/04 §4.1/§5):
///   • dungeon floor-clear picks can DRAFT a shared-pool spell into the run;
///   • biome-boss clears permanently UNLOCK pool spells and GROW the hotbar (capped).
/// Systems-layer per CLAUDE.md — guards the acquisition structure, not any one spell.
/// </summary>
public class LoadoutProgressionTests
{
    // ── Dungeon in-run drafting ──────────────────────────────────────────────

    [Fact]
    public void PickChoice_SpellTaggedChoice_DraftsIntoTheRun()
    {
        var run = new DungeonRun { Active = true, Floors = { "f1", "f2" }, FloorIndex = 0 };
        run.PendingChoices.Add(DungeonService.SpellPrefix + "phoenix");

        DungeonService.PickChoice(run, DungeonService.SpellPrefix + "phoenix");

        Assert.Contains("phoenix", run.DraftedSpells);
        Assert.Equal(1, run.FloorIndex);           // pick advances the floor
        Assert.Empty(run.PendingChoices);
    }

    [Fact]
    public void OnFloorCleared_OffersExactlyOneSpell_MixedWithNonSpells()
    {
        // docs/04 §5: every floor-clear pick is a deliberate MIX — exactly one drafted-spell offer,
        // the other two from the dungeon-exclusive relic/core/mod pool (never an all-spells pick).
        var run = new DungeonRun { Active = true, Seed = 12345, FloorIndex = 0 };
        run.Floors.AddRange(new[] { "f0", "f1", "f2" });
        DungeonService.OnFloorCleared(run);

        Assert.Equal(3, run.PendingChoices.Count);
        int spells = run.PendingChoices.Count(c => c.StartsWith(DungeonService.SpellPrefix));
        Assert.Equal(1, spells);
    }

    // ── Campaign permanent unlock + slot growth ──────────────────────────────

    [Fact]
    public void HellBoss_UnlocksPoolSpells_AndGrowsHotbar()
    {
        var p = Profile.NewDefault();
        Assert.Equal(3, p.UnlockedSpellSlots);
        Assert.False(p.SpellLevels.ContainsKey("phoenix")); // not owned at start

        var r = Rewards.GrantLevelCompletion(p, "hell-boss", ProgressionConfig.Default);

        Assert.Contains("phoenix", r.SpellsUnlocked);       // Fire Mage capstone
        Assert.True(p.SpellLevels.ContainsKey("phoenix"));  // now owned
        Assert.Equal(1, r.SlotsUnlocked);
        Assert.Equal(4, p.UnlockedSpellSlots);              // 3 → 4
    }

    [Fact]
    public void SlotGrowth_CapsAtFour_AcrossBossClears()
    {
        // Economy rework §3: hotbar caps at signature + 3 flex = 4 (was 5).
        var p = Profile.NewDefault();
        Rewards.GrantLevelCompletion(p, "hell-boss", ProgressionConfig.Default); // 3→4
        Assert.Equal(4, p.UnlockedSpellSlots);
        var r = Rewards.GrantLevelCompletion(p, "caverns-boss", ProgressionConfig.Default); // capped at 4
        Assert.Equal(0, r.SlotsUnlocked);
        Assert.Equal(4, p.UnlockedSpellSlots);
    }

    [Fact]
    public void NonBossLevel_GrantsNoSpellsOrSlots()
    {
        var p = Profile.NewDefault();
        var r = Rewards.GrantLevelCompletion(p, "hell-1", ProgressionConfig.Default);
        Assert.Empty(r.SpellsUnlocked);
        Assert.Equal(0, r.SlotsUnlocked);
        Assert.Equal(3, p.UnlockedSpellSlots);
    }
}

using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Net;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Systems-layer design-fidelity tests for the "signature + drafted loadout" model (docs/04 §3).
/// These assert the STRUCTURE that delivers spells — not any single spell's behavior:
///   • the hotbar is driven by the equipped loadout, so CastSlot only fires equipped spells;
///   • slot 0 is the signature; the rest are drafted, capped at the unlocked slot count;
///   • the loadout is exposed in the snapshot so the HUD and CastSlot index the same order.
/// (Per CLAUDE.md: per-spell tests passed forever inside the old fixed-kit structure; these
/// guard the structure itself.)
/// </summary>
public class LoadoutTests
{
    private static GameInstance Make(string character = "fire_mage")
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"test\",\"hp\":3,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":4," +
            "\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 42);
        g.SetCharacter(character);
        return g;
    }

    private static void MaxManaAndServe(GameInstance g) { g.ManaValue = g.ManaMaxValue; g.Serve(); }

    [Fact]
    public void CastSlot_OnlyFires_EquippedSpells()
    {
        // Loadout = signature only (ignite). Slot 1 (fireball in the full kit) is NOT equipped,
        // so casting it must do nothing — the kit no longer hands you every spell for free.
        var g = Make("fire_mage");
        g.SetLoadout(new[] { "ignite" });
        MaxManaAndServe(g);
        g.CastSlot(1);
        Assert.Empty(g.Projectiles);
    }

    [Fact]
    public void CastSlot_FiresSpell_AtItsLoadoutIndex()
    {
        var g = Make("fire_mage");
        g.SetLoadout(new[] { "ignite", "fireball" });
        MaxManaAndServe(g);
        var blk = g.Blocks.First(b => !b.Dead); blk.BurnRemaining = 5.0;
        int hp0 = blk.Hp;
        g.CastSlot(1); // fireball=Conflagration is slot 1 of THIS loadout
        Assert.True(blk.Hp < hp0, "slot 1 detonated the burning block");
    }

    [Fact]
    public void CastSlot_FallsBackToFullKit_WhenNoLoadoutEquipped()
    {
        // Back-compat: sim tests that never equip a loadout still address the full kit.
        var g = Make("fire_mage");
        MaxManaAndServe(g);
        var blk = g.Blocks.First(b => !b.Dead); blk.BurnRemaining = 5.0;
        int hp0 = blk.Hp;
        g.CastSlot(1); // fireball=Conflagration in the full fire-mage kit
        Assert.True(blk.Hp < hp0, "slot 1 detonated the burning block");
    }

    [Fact]
    public void DraftSpell_Appends_WithinCap_AndRejectsFullOrDuplicate()
    {
        var g = Make("fire_mage");
        g.SetLoadout(new[] { "ignite", "fireball" });
        Assert.True(g.DraftSpell("firewall", maxSlots: 3));
        Assert.Equal(3, g.Loadout.Count);
        Assert.False(g.DraftSpell("turret", maxSlots: 3));   // at capacity
        Assert.False(g.DraftSpell("firewall", maxSlots: 5)); // duplicate
        Assert.Equal(new[] { "ignite", "fireball", "firewall" }, g.Loadout);
    }

    [Fact]
    public void Snapshot_ExposesLoadout_SignatureFlaggedAtSlot0()
    {
        var g = Make("fire_mage");
        g.SetLoadout(new[] { "ignite", "fireball" });
        var snap = Snapshot.From(g, tick: 0);
        Assert.Equal(2, snap.Loadout.Count);
        Assert.True(snap.Loadout[0].Signature);
        Assert.Equal("ignite", snap.Loadout[0].Id);
        Assert.False(snap.Loadout[1].Signature);
        Assert.Equal("fireball", snap.Loadout[1].Id);
    }
}

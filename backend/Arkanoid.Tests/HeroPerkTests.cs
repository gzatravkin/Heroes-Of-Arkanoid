using System.Linq;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>Behavioral hero perks (design §5.5, ★1/★3/★5). Each test asserts the perk's DESIGNED
/// trigger + effect (not just "a number moved"), and that it is OFF without the perk. Engineer ★5 is
/// intentionally absent (deferred to the §5.9 balance pass — see docs/round-2-recommendations.md).</summary>
public class HeroPerkTests
{
    // ── §5.5 Fire Mage ★3: an already-burning block takes +15% from crits ────────
    [Fact]
    public void FmS3_IgnitedBlock_TakesExtraCritDamage()
    {
        int Hit(bool perk)
        {
            var g = K.OneBlock(1000);
            g.Serve();
            g.StatPower = 10; g.CritChance = 1.0; g.CritDamage = 2.0;
            if (perk) g.SetPerks(new[] { StatResolver.FmIgnitedCrit });
            var blk = g.Blocks[0];
            blk.BurnRemaining = 10.0; // block is on fire in both runs (burn DoT cancels in the diff)
            int hp0 = blk.Hp;
            K.AimAt(g, blk);
            g.Tick(SimConfig.Default.FixedDt);
            return hp0 - blk.Hp;
        }
        // Crit base = 10×2 = 20; with the perk ×1.15 = 23. The +3 is the perk (burn equal both runs).
        Assert.Equal(3, Hit(true) - Hit(false));
    }

    [Fact]
    public void FmS3_DoesNothing_OnUnburntBlock()
    {
        var g = K.OneBlock(1000);
        g.Serve();
        g.StatPower = 10; g.CritChance = 1.0; g.CritDamage = 2.0;
        g.SetPerks(new[] { StatResolver.FmIgnitedCrit });
        var blk = g.Blocks[0]; // NOT burning
        int hp0 = blk.Hp;
        K.AimAt(g, blk);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(20, hp0 - blk.Hp); // plain crit, no +15%
    }

    // ── §5.5 Fire Mage ★5: a CRIT kill ignites a nearby block ────────────────────
    [Fact]
    public void FmS5_CritKill_IgnitesNeighbour()
    {
        var g = K.TwoBlocks(1);
        g.Serve();
        g.StatPower = 10; g.CritChance = 1.0; g.CritDamage = 2.0; // one-shot crit
        g.SetPerks(new[] { StatResolver.FmCritKillIgnite });
        var target = g.Blocks[0]; var neighbour = g.Blocks[1];
        K.AimAt(g, target);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(target.Dead, "target crit-killed");
        Assert.True(neighbour.BurnRemaining > 0, "neighbour ignited by the crit kill");
    }

    [Fact]
    public void FmS5_NoIgnite_WithoutPerk()
    {
        var g = K.TwoBlocks(1);
        g.Serve();
        g.StatPower = 10; g.CritChance = 1.0; g.CritDamage = 2.0;
        var target = g.Blocks[0]; var neighbour = g.Blocks[1];
        K.AimAt(g, target);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(target.Dead);
        Assert.Equal(0, neighbour.BurnRemaining);
    }

    // ── §5.5 Paladin ★5: below 50% HP, +25% crit damage ─────────────────────────
    [Fact]
    public void PalS5_LowHp_AddsCritDamage()
    {
        int Crit(int hp, int maxHp, bool perk)
        {
            var g = K.OneBlock(1000);
            g.Serve();
            g.StatPower = 10; g.CritChance = 1.0; g.CritDamage = 2.0;
            g.StatMaxHp = maxHp; g.SetHp(hp);
            if (perk) g.SetPerks(new[] { StatResolver.PalLowHpCritDmg });
            var blk = g.Blocks[0]; int hp0 = blk.Hp;
            K.AimAt(g, blk);
            g.Tick(SimConfig.Default.FixedDt);
            return hp0 - blk.Hp;
        }
        Assert.Equal(25, Crit(3, 6, true));  // 50% HP → 10×2×1.25
        Assert.Equal(20, Crit(6, 6, true));  // full HP → no bonus
        Assert.Equal(20, Crit(3, 6, false)); // no perk → no bonus even low
    }

    // ── §5.5 Necromancer ★3: crits drain mana to you ────────────────────────────
    [Fact]
    public void NecroS3_CritDrainsMana()
    {
        int Mana(bool perk)
        {
            var g = K.OneBlock(1000); // survives the hit ⇒ no kill-mana confound
            g.Serve();
            g.StatPower = 1; g.CritChance = 1.0; g.CritDamage = 2.0;
            g._manaRegenFrozen = true; g.ManaValue = 0;
            if (perk) g.SetPerks(new[] { StatResolver.NecroCritDrain });
            var blk = g.Blocks[0];
            K.AimAt(g, blk);
            g.Tick(SimConfig.Default.FixedDt);
            return (int)g.ManaValue;
        }
        Assert.Equal(4, Mana(true));
        Assert.Equal(0, Mana(false));
    }

    // ── §5.5 Necromancer ★1: heal 1 HP per 60 kills ─────────────────────────────
    [Fact]
    public void NecroS1_Heals_EverySixtyKills()
    {
        var g = K.OneBlock(1);
        g.Serve();
        g.SetPerks(new[] { StatResolver.NecroHeal });
        g.StatMaxHp = 10; g.SetHp(5);
        g._perkKillCounter = 59; // next kill is the 60th
        var blk = g.Blocks[0];
        K.AimAt(g, blk);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(blk.Dead);
        Assert.Equal(6, g.Hp);            // healed 1
        Assert.Equal(0, g._perkKillCounter); // counter reset
    }

    [Fact]
    public void NecroS1_DoesNotHealAboveMax()
    {
        var g = K.OneBlock(1);
        g.Serve();
        g.SetPerks(new[] { StatResolver.NecroHeal });
        g.StatMaxHp = 5; g.SetHp(5); // already at max
        g._perkKillCounter = 59;
        K.AimAt(g, g.Blocks[0]);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(5, g.Hp); // no overheal
    }

    // ── §5.5 Paladin ★3: the first ball-drain each level is saved ────────────────
    [Fact]
    public void PalS3_SavesFirstDrain_ThenLoses()
    {
        var g = K.OneBlock(1000);
        g.SpareBalls = 0;
        g.SetPerks(new[] { StatResolver.PalSaveDrain });

        g.Serve(); // Playing
        foreach (var b in g.Balls) b.Alive = false;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.NotEqual(GamePhase.Lost, g.Phase);     // saved, not lost
        Assert.False(g._perkSaveAvailable);           // save consumed
        Assert.Contains(g.Balls, b => b.Alive);       // a fresh ball spawned

        g.Serve(); // Playing again
        foreach (var b in g.Balls) b.Alive = false;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(GamePhase.Lost, g.Phase);        // no save left, no spare → lost
    }

    // ── §5.5 Necromancer ★5: a full-combo kill may raise a helper-ball ───────────
    [Fact]
    public void NecroS5_SpawnHelperBall_AddsSummonedBall()
    {
        var g = K.OneBlock(100);
        g.Serve(); // Playing
        int before = g.Balls.Count;
        g.SpawnHelperBall();
        Assert.Equal(before + 1, g.Balls.Count);
        Assert.Contains(g.Balls, b => b.Summoned);
    }

    [Fact]
    public void NecroS5_RaisesHelpers_OnFullComboKills_AndNotWithoutPerk()
    {
        int Summoned(bool perk)
        {
            // 30 blocks; after ~9 kills the combo is maxed (×4), so later kills can roll the perk.
            var g = K.Game(
                "{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}",
                "{\"id\":\"t\",\"biome\":\"t\",\"cols\":10,\"rows\":4," +
                "\"rows_data\":[\"AAAAAAAAAA\",\"AAAAAAAAAA\",\"AAAAAAAAAA\"],\"legend\":{\"A\":\"b\"}}");
            g.Serve(); // Playing
            if (perk) g.SetPerks(new[] { StatResolver.NecroComboHelper });
            foreach (var blk in g.Blocks.Where(b => !b.Dead).ToList())
                BlockDamage.DamageBlock(g, blk, blk.Hp, igniteSource: false);
            return g.Balls.Count(b => b.Summoned);
        }
        Assert.True(Summoned(true) > 0, "max-combo kills raised at least one helper-ball");
        Assert.Equal(0, Summoned(false)); // no perk ⇒ never
    }
}

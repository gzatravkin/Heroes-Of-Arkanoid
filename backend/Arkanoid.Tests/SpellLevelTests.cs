using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Verifies that spell upgrade levels (set via SetSpellLevels) actually scale spell effects.
/// Each test uses a minimal level with one block so spells have something to interact with.
/// </summary>
public class SpellLevelTests
{
    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":10,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":4,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }


    [Fact]
    public void DefaultLevels_BehaveAsBefore()
    {
        // §3 Conflagration: with no SetSpellLevels call, a detonation deals base 6 to each burning block.
        var g = Make();
        g.Serve();
        var blk = g.Blocks.First(b => !b.Dead); blk.BurnRemaining = 5.0;
        int hp0 = blk.Hp; // 10
        g.ManaValue = 25;
        g.CastFireball();
        Assert.Empty(g.Projectiles);
        Assert.Equal(hp0 - 6, blk.Hp); // base detonation 6
    }

    [Fact]
    public void Fireball_Level2_DealsExtraDamage()
    {
        // §6 scaling: Conflagration's detonation is +2 damage/level (base 6 → 8 at Lvl 2).
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["fireball"] = 2 });
        g.Serve();
        var blk = g.Blocks.First(b => !b.Dead); blk.BurnRemaining = 5.0;
        int hp0 = blk.Hp;
        g.ManaValue = 25;
        g.CastFireball();
        Assert.Equal(hp0 - 8, blk.Hp); // base 6 + 2 per level
    }


    [Fact]
    public void Ignite_Level3_GrantsMoreHits()
    {
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["ignite"] = 3 });
        g.Serve();
        g.CastIgnite();
        g.ApplyCheat("parkBallAbovePaddle", 0); // sets Playing + aims ball into paddle
        g.Tick(SimConfig.Default.FixedDt);       // deflect -> imbue
        Assert.Equal(6, g.Balls[0].IgniteHitsLeft); // base 4 + 2 levels * 1 per level
    }


    [Fact]
    public void Spear_Cast_LaunchesPiercingProjectile()
    {
        // Spear reverted 2026-06-16 to the LEGACY piercing damage projectile (was Lance of Dawn pillar).
        var g = Make();
        g.SetCharacter("paladin");
        g.Serve();
        g.ManaValue = 100;
        g.CastSlot(1); // paladin slot 1 = spear
        var spear = g.Projectiles.FirstOrDefault(p => p.Kind == "spear");
        Assert.NotNull(spear);
        Assert.True(spear!.PiercingHitsLeft > 1, "spear pierces multiple blocks");
        Assert.Empty(g.Pillars); // no Lance pillar anymore
    }

    [Fact]
    public void Decay_Level3_GrantsMoreHits()
    {
        // Necromancer Decay imbue must scale like Ignite (was a dead stat before this session).
        var g = Make();
        g.SetCharacter("necromancer");
        g.SetSpellLevels(new Dictionary<string, int> { ["decay"] = 3 });
        g.Serve();
        g.CastSlot(0); // necromancer slot 0 = decay (free imbue)
        g.ApplyCheat("parkBallAbovePaddle", 0);
        g.Tick(SimConfig.Default.FixedDt); // deflect -> imbue
        Assert.Equal(6, g.Balls[0].DecayHitsLeft); // base 4 + 2 levels * 1 per level
    }

    [Fact]
    public void Lightning_HigherLevel_DealsMoreDamage()
    {
        int Dealt(int level)
        {
            var g = Make();
            g.SetCharacter("engineer");
            if (level > 1) g.SetSpellLevels(new Dictionary<string, int> { ["lightning"] = level });
            g.Serve();
            g.ManaValue = 100;
            int before = g.Blocks[0].Hp;
            g.CastSlot(0); // engineer slot 0 = lightning
            return before - g.Blocks[0].Hp;
        }
        Assert.True(Dealt(3) > Dealt(1), "lightning should deal more damage at higher level");
    }

    [Fact]
    public void Radiation_Level3_StrongerZone()
    {
        var g = Make();
        g.SetCharacter("engineer");
        g.SetSpellLevels(new Dictionary<string, int> { ["radiation"] = 3 });
        g.Serve();
        g.ManaValue = 100;
        g.CastSlot(2); // engineer slot 2 = radiation
        Assert.Single(g.Zones);
        Assert.Equal(3, g.Zones[0].DamagePerTick); // base 1 + 2 levels * 1
    }

    [Fact]
    public void Phoenix_Level2_LastsLonger()
    {
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["phoenix"] = 2 });
        g.Serve();
        g.ManaValue = 100;
        g.CastPhoenix();
        Assert.Single(g.Phoenixes);
        Assert.True(g.Phoenixes[0].Lifetime > 6.0, // base 6 + 1 per level
            $"phoenix should last longer at level 2; got {g.Phoenixes[0].Lifetime}");
    }

    [Fact]
    public void Shield_Level3_LastsLonger()
    {
        var g = Make();
        g.SetCharacter("paladin");
        g.SetSpellLevels(new Dictionary<string, int> { ["shield"] = 3 });
        g.Serve();
        g.ManaValue = 100;
        g.CastSlot(0); // paladin slot 0 = shield (barrier)
        Assert.Single(g.Barriers);
        Assert.True(g.Barriers[0].LifeRemaining > 4.0, // base 4 + 2 * 0.5
            $"shield should last longer at level 3; got {g.Barriers[0].LifeRemaining}");
    }

    [Fact]
    public void Duplicate_Level3_ClonesMoreBalls()
    {
        int CloneCount(int level)
        {
            var g = Make();
            g.SetCharacter("paladin");
            if (level > 1) g.SetSpellLevels(new Dictionary<string, int> { ["duplicate"] = level });
            g.Serve();
            g.ManaValue = 100;
            int before = g.Balls.Count;
            g.CastSlot(3); // paladin slot 3 = duplicate (holy_echo inserted at slot 2)
            return g.Balls.Count - before;
        }
        Assert.True(CloneCount(3) > CloneCount(1), "duplicate should clone more balls at higher level");
    }

    [Fact]
    public void Overload_Level3_BiggerBlast()
    {
        // Rework: cast arms _overloadArmed and stores the level-scaled radius in _overloadRadius.
        // Higher level → bigger radius (base 1 + 1 per level → level 3 = radius 3).
        int RadiusAtLevel(int level)
        {
            var g = Make();
            g.SetCharacter("engineer");
            if (level > 1) g.SetSpellLevels(new Dictionary<string, int> { ["overload"] = level });
            g.Serve();
            g.ManaValue = 100;
            g.CastSlot(4); // engineer slot 4 = overload
            return g._overloadRadius;
        }
        Assert.True(RadiusAtLevel(3) > RadiusAtLevel(1),
            $"overload blast should grow with level: lvl1={RadiusAtLevel(1)}, lvl3={RadiusAtLevel(3)}");
    }

    [Fact]
    public void Turret_Level2_LastsLonger()
    {
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["turret"] = 2 });
        g.Serve();
        g.ManaValue = 25; // turret mana cost
        g.CastTurret();
        Assert.True(g.TurretActive);

        // Tick past the BASE duration (turret would have expired at level 1).
        double elapsed = 0;
        while (elapsed < 5.0 /* turret base duration */ + SimConfig.Default.FixedDt)
        {
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        // At level 2 the turret lasts 5.0 + 1.0 = 6.0 seconds,
        // so it must still be active here (we are inside the bonus window).
        Assert.True(g.TurretActive,
            "turret should still be active after base duration when upgraded to level 2");
    }
}

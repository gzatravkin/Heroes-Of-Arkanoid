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

    // -----------------------------------------------------------------------
    // Fireball
    // -----------------------------------------------------------------------

    [Fact]
    public void DefaultLevels_BehaveAsBefore()
    {
        // Regression guard: with no SetSpellLevels call, fireball damage == Config.FireballDamage.
        var g = Make();
        g.Serve();
        g.ManaValue = SimConfig.Default.FireballCost;
        g.CastFireball();
        Assert.Single(g.Projectiles);
        Assert.Equal(SimConfig.Default.FireballDamage, g.Projectiles[0].Damage);
    }

    [Fact]
    public void Fireball_Level2_DealsExtraDamage()
    {
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["fireball"] = 2 });
        g.Serve();
        g.ManaValue = SimConfig.Default.FireballCost;
        g.CastFireball();
        Assert.Single(g.Projectiles);
        var expected = SimConfig.Default.FireballDamage + SimConfig.Default.FireballDamagePerLevel;
        Assert.Equal(expected, g.Projectiles[0].Damage);
    }

    // -----------------------------------------------------------------------
    // Ignite
    // -----------------------------------------------------------------------

    [Fact]
    public void Ignite_Level3_GrantsMoreHits()
    {
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["ignite"] = 3 });
        g.Serve();
        g.CastIgnite();
        g.ApplyCheat("parkBallAbovePaddle", 0); // sets Playing + aims ball into paddle
        g.Tick(SimConfig.Default.FixedDt);       // deflect -> imbue
        var expected = SimConfig.Default.IgniteHits + 2 * SimConfig.Default.IgniteHitsPerLevel;
        Assert.Equal(expected, g.Balls[0].IgniteHitsLeft);
    }

    // -----------------------------------------------------------------------
    // Turret
    // -----------------------------------------------------------------------

    [Fact]
    public void Turret_Level2_LastsLonger()
    {
        var g = Make();
        g.SetSpellLevels(new Dictionary<string, int> { ["turret"] = 2 });
        g.Serve();
        g.ManaValue = SimConfig.Default.TurretCost;
        g.CastTurret();
        Assert.True(g.TurretActive);

        // Tick past the BASE duration (turret would have expired at level 1).
        double elapsed = 0;
        while (elapsed < SimConfig.Default.TurretDuration + SimConfig.Default.FixedDt)
        {
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
        }
        // At level 2 the turret lasts TurretDuration + TurretDurationPerLevel seconds,
        // so it must still be active here (we are inside the bonus window).
        Assert.True(g.TurretActive,
            "turret should still be active after base duration when upgraded to level 2");
    }
}

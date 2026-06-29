using System.Linq;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Crit system (design §5.7): a % chance to multiply a ball hit's damage by CritDamage,
/// raising a Crit event + setting LastHitWasCrit for crit-synergy cards. No stub — real damage path.</summary>
public class CritTests
{
    [Fact]
    public void Crit_MultipliesDamage_AtChance100()
    {
        var g = K.OneBlock(20);
        g.Serve();
        g.CritChance = 1.0; g.CritDamage = 2.0; // force a crit on the next hit
        var blk = g.Blocks[0];
        int hp0 = blk.Hp;
        K.AimAt(g, blk);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(g.LastHitWasCrit, "the hit should have critted");
        Assert.Equal(hp0 - SimConfig.Default.BallDamage * 2, blk.Hp); // base ×2
    }

    [Fact]
    public void NoCrit_AtChance0_NormalDamage()
    {
        var g = K.OneBlock(20);
        g.Serve();
        g.CritChance = 0.0;
        var blk = g.Blocks[0];
        int hp0 = blk.Hp;
        K.AimAt(g, blk);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.False(g.LastHitWasCrit);
        Assert.Equal(hp0 - SimConfig.Default.BallDamage, blk.Hp); // base only
    }

    [Fact]
    public void SetCrit_ClampsToLockedCaps_75pct_And_x4()
    {
        var g = K.OneBlock(5);
        // Over the caps: chance → 0.75, damage → ×4 (design §5.10 LOCKED).
        g.SetCrit(0.95, 9.0);
        Assert.Equal(0.75, g.CritChance, 3);
        Assert.Equal(4.0, g.CritDamage, 3);
        // Under the floor: damage clamps up to ×1.
        g.SetCrit(0.1, 0.4);
        Assert.Equal(1.0, g.CritDamage, 3);
    }

    [Fact]
    public void Crit_AppliesToBosses_BigCritsPop()
    {
        // §5.9: bosses have 500+ HP "so big crits pop" — crits must land on bosses.
        var g = K.Game(
            "{\"id\":\"boss\",\"biome\":\"t\",\"hp\":500,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"Boss\"}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"boss\"}}");
        g.Serve();
        g.CritChance = 1.0; g.CritDamage = 4.0;
        var boss = g.Blocks.First(b => b.Boss && !b.Dead);
        int hp0 = boss.Hp;
        K.AimAt(g, boss);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(g.LastHitWasCrit, "the boss hit should have critted");
        Assert.True(boss.Hp < hp0, "boss took crit damage");
    }
}

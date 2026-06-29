using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>Ashfall (design §3, NEW Fire Mage): while armed, an IGNITE-kill (a burning block destroyed)
/// rains a vertical ember down its column. Asserts the trigger (burning + armed) + identity (a downward
/// ember projectile), and that it does NOTHING for normal kills or while inactive.</summary>
public class AshfallTests
{
    private static readonly double Dt = SimConfig.Default.FixedDt;

    [Fact]
    public void Ashfall_IgniteKill_RainsEmberDownColumn()
    {
        var g = K.OneBlock(5);
        g.Serve();
        g._ashfallTimer = 5.0;             // armed
        var blk = g.Blocks[0];
        blk.BurnRemaining = 3.0;           // the block is on fire
        BlockDamage.DamageBlock(g, blk, blk.Hp, igniteSource: false); // ignite-kill

        var ember = g.Projectiles.FirstOrDefault(p => p.Kind == "ember");
        Assert.NotNull(ember);
        Assert.True(ember!.Vel.Y > 0, "the ember rains DOWN the column");
    }

    [Fact]
    public void Ashfall_NonBurningKill_NoEmber()
    {
        var g = K.OneBlock(5);
        g.Serve();
        g._ashfallTimer = 5.0;             // armed, but the block is NOT burning
        var blk = g.Blocks[0];
        BlockDamage.DamageBlock(g, blk, blk.Hp, igniteSource: false);
        Assert.DoesNotContain(g.Projectiles, p => p.Kind == "ember");
    }

    [Fact]
    public void Ashfall_Inactive_NoEmber()
    {
        var g = K.OneBlock(5);
        g.Serve();                          // NOT armed
        var blk = g.Blocks[0];
        blk.BurnRemaining = 3.0;            // burning, but Ashfall is off
        BlockDamage.DamageBlock(g, blk, blk.Hp, igniteSource: false);
        Assert.DoesNotContain(g.Projectiles, p => p.Kind == "ember");
    }

    [Fact]
    public void Ashfall_Cast_ArmsThenExpires()
    {
        var g = K.OneBlock(5);
        g.SetCharacter("fire_mage");
        g.SetLoadout(new[] { "ashfall" });
        g.Serve();
        K.Park(g); // freeze the ball so the level doesn't win mid-test (ticks would stop)
        g.ManaValue = 100;
        g.CastSlot(0); // Ashfall
        Assert.True(g._ashfallTimer > 0, "Ashfall armed the buff");

        for (int i = 0; i < (int)(7.0 / Dt); i++) g.Tick(Dt); // past the 6s duration
        Assert.Equal(0, g._ashfallTimer, 3); // buff expired
    }
}

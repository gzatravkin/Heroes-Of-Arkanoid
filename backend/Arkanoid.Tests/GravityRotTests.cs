using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>The gravity/falling-block primitive (§3) and Rot &amp; Collapse (Decay rework): a rot hit
/// lowers a block's MAX HP, and a rot-kill collapses the column above into the gap. Asserts the gravity
/// op + the rot identity, not just "a number moved".</summary>
public class GravityRotTests
{
    private static readonly double Dt = SimConfig.Default.FixedDt;

    // ── Gravity primitive ────────────────────────────────────────────────────────
    [Fact]
    public void Gravity_CollapseColumn_DropsBlockToFloor()
    {
        // One block at the TOP of a 5-row column; gravity drops it to the bottom row.
        var g = K.Game(
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":5,\"rows_data\":[\"B..\",\"...\",\"...\",\"...\",\"...\"],\"legend\":{\"B\":\"b\"}}");
        var blk = g.Blocks.First(b => b.Col == 0);
        Assert.Equal(0, blk.Row);
        GravitySystem.CollapseColumn(g, 0);
        Assert.Equal(4, blk.Row); // fell to the floor (Rows-1)
    }

    [Fact]
    public void Gravity_StacksOnIndestructibleAnchor()
    {
        // A destructible block above an indestructible anchor falls to rest just on top of it.
        var g = K.Game(
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true}," +
            "{\"id\":\"x\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":5,\"rows_data\":[\"B..\",\"...\",\"...\",\"X..\",\"...\"],\"legend\":{\"B\":\"b\",\"X\":\"x\"}}");
        var blk = g.Blocks.First(b => b.Col == 0 && !b.Indestructible);
        GravitySystem.CollapseColumn(g, 0);
        Assert.Equal(2, blk.Row); // rests just above the anchor at row 3
    }

    // ── Rot & Collapse ───────────────────────────────────────────────────────────
    [Fact]
    public void Rot_Hit_LowersMaxHpPermanently()
    {
        var g = K.OneBlock(20);
        var blk = g.Blocks[0];
        int max0 = blk.MaxHp;
        BlockDamage.DamageBlock(g, blk, 1, igniteSource: false, decaySource: true);
        Assert.Equal(max0 - 2, blk.MaxHp); // withered by the rot
    }

    [Fact]
    public void Rot_NonRotHit_DoesNotLowerMaxHp()
    {
        var g = K.OneBlock(20);
        var blk = g.Blocks[0];
        int max0 = blk.MaxHp;
        BlockDamage.DamageBlock(g, blk, 1, igniteSource: false, decaySource: false);
        Assert.Equal(max0, blk.MaxHp); // a normal hit doesn't wither max HP
    }

    [Fact]
    public void Rot_KillCollapsesColumnAbove()
    {
        // col 1 has a block at row 0 and row 2; rot-killing the row-2 block collapses the column.
        // (HP 5 so the decay spread-chip can't kill the top block — we test gravity, not the chip.)
        var g = K.Game(
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":5,\"sprite\":\"s\",\"needToKill\":true}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":5,\"rows_data\":[\".B.\",\"...\",\".B.\",\"...\",\"...\"],\"legend\":{\"B\":\"b\"}}");
        var top = g.Blocks.First(b => b.Col == 1 && b.Row == 0);
        var bottom = g.Blocks.First(b => b.Col == 1 && b.Row == 2);
        BlockDamage.DamageBlock(g, bottom, 10, igniteSource: false, decaySource: true); // rot-kill
        Assert.True(bottom.Dead);
        Assert.False(top.Dead);
        Assert.Equal(4, top.Row); // the surviving block fell to the floor
    }

    [Fact]
    public void Rot_Cast_ArmsImbue()
    {
        var g = K.OneBlock(5);
        g.SetCharacter("necromancer");
        g.SetLoadout(new[] { "decay" });
        g.Serve();
        g.CastSlot(0); // Rot & Collapse (decay imbue)
        Assert.True(g._decayArmed);
        Assert.Empty(g.Projectiles); // an imbue, not a projectile
    }
}

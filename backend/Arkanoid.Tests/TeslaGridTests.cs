using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>Tesla Grid (design §3, NEW Engineer): side-wall bounces charge each wall; when BOTH are
/// charged a horizontal lightning curtain fires across the ball's row band and both walls reset.
/// Asserts the trigger (both walls, armed) + identity (board-row curtain damage), and the negatives.</summary>
public class TeslaGridTests
{
    private static GameInstance WideBoard()
    {
        var g = K.Game(
            "{\"id\":\"b\",\"biome\":\"t\",\"hp\":20,\"sprite\":\"s\",\"needToKill\":true}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":9,\"rows\":3,\"rows_data\":[\"BBBBBBBBB\",\".........\",\".........\"],\"legend\":{\"B\":\"b\"}}");
        g.Serve();
        // Put the ball on row 0 so the curtain band covers the brick row.
        g.Balls[0].Pos = new Vec2(g.Balls[0].Pos.X, g.Level.Grid.CellCenter(0, 0).Y);
        return g;
    }

    [Fact]
    public void TeslaGrid_BothWallsCharged_FiresCurtain_AndResets()
    {
        var g = WideBoard();
        g._teslaArmed = true;
        int hp0 = g.Blocks.Sum(b => b.Hp);
        TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: true);
        TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: false); // both → curtain
        Assert.True(g.Blocks.Sum(b => b.Hp) < hp0, "the curtain damaged the row");
        Assert.False(g._teslaLeftCharged);  // walls reset after firing
        Assert.False(g._teslaRightCharged);
    }

    [Fact]
    public void TeslaGrid_OneWallOnly_NoCurtain()
    {
        var g = WideBoard();
        g._teslaArmed = true;
        int hp0 = g.Blocks.Sum(b => b.Hp);
        TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: true); // only one wall
        Assert.Equal(hp0, g.Blocks.Sum(b => b.Hp));
        Assert.True(g._teslaLeftCharged);
        Assert.False(g._teslaRightCharged);
    }

    [Fact]
    public void TeslaGrid_Unarmed_NeverCharges()
    {
        var g = WideBoard(); // not armed
        int hp0 = g.Blocks.Sum(b => b.Hp);
        TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: true);
        TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: false);
        Assert.Equal(hp0, g.Blocks.Sum(b => b.Hp));
        Assert.False(g._teslaLeftCharged);
    }

    [Fact]
    public void TeslaGrid_Cast_Arms()
    {
        var g = WideBoard();
        g.SetCharacter("engineer");
        g.SetLoadout(new[] { "tesla" });
        g.ManaValue = 100;
        g.CastSlot(0);
        Assert.True(g._teslaArmed);
    }

    [Fact]
    public void TeslaGrid_Snapshot_ExposesWallChargeState()
    {
        // Design (tasks list.md §T7): Snapshot must carry teslaArmed / teslaLeftCharged / teslaRightCharged
        // so the frontend HUD can show L/R wall charge icons.
        var g = WideBoard();
        g._teslaArmed = true;
        TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: true); // only left charged
        var snap = Arkanoid.Core.Net.Snapshot.From(g, 0);
        Assert.True(snap.TeslaArmed,        "snapshot should expose teslaArmed");
        Assert.True(snap.TeslaLeftCharged,  "snapshot should expose teslaLeftCharged=true");
        Assert.False(snap.TeslaRightCharged,"snapshot should expose teslaRightCharged=false");
    }

    [Fact]
    public void TeslaGrid_DamageScalesWithLevel()
    {
        int Delta(int lvl)
        {
            var g = WideBoard();
            if (lvl > 1) g.SetSpellLevels(new Dictionary<string, int> { ["tesla"] = lvl });
            g._teslaArmed = true;
            var col0 = g.Blocks.First(b => b.Col == 0 && b.Row == 0);
            int hp0 = col0.Hp;
            TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: true);
            TeslaGridSystem.OnWallBounce(g, g.Balls[0], left: false);
            return hp0 - col0.Hp;
        }
        Assert.Equal(3, Delta(1)); // base curtain damage
        Assert.Equal(4, Delta(2)); // 3 + 1×1 (§6 +damage/level)
    }
}

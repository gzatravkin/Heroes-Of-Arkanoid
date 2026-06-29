using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Lich's Gaze (design §3 rework of Skeletal Mage): a sweeping beam (NO projectile) that curses
/// the blocks it crosses; cursed blocks take bonus ball damage. Asserts curse-on-sweep + bonus-damage
/// identity + level scaling.</summary>
public class LichGazeTests
{
    private static readonly double Dt = SimConfig.Default.FixedDt;

    // Blocks filling the upper area so the upward sweep crosses them.
    private static GameInstance Board() => K.Game(
        "{\"id\":\"b\",\"biome\":\"t\",\"hp\":20,\"sprite\":\"s\",\"needToKill\":true}",
        "{\"id\":\"t\",\"biome\":\"t\",\"cols\":7,\"rows\":6," +
        "\"rows_data\":[\"BBBBBBB\",\"BBBBBBB\",\"BBBBBBB\",\"BBBBBBB\",\".......\",\".......\"],\"legend\":{\"B\":\"b\"}}");

    [Fact]
    public void LichGaze_Cast_SpawnsBeam_NotProjectiles_AndCursesBlocks()
    {
        var g = Board();
        g.SetCharacter("necromancer");
        g.SetLoadout(new[] { "mage" });
        g.Serve();
        K.Park(g); // freeze ball so the level doesn't end mid-sweep
        g.ManaValue = 100;
        g.CastSlot(0); // Lich's Gaze
        Assert.NotNull(g.LichBeam);
        Assert.Empty(g.Projectiles); // not a bone-bullet fan anymore

        for (int i = 0; i < (int)(5.0 / Dt); i++) g.Tick(Dt); // sweep the full arc
        Assert.Contains(g.Blocks, b => b.Cursed); // the beam cursed blocks in its path
        Assert.Null(g.LichBeam);                   // beam expired after its duration
    }

    [Fact]
    public void LichGaze_BeamSweeps_CursingAcrossColumns()
    {
        // The beam must SWEEP (angle advances) and curse blocks across MULTIPLE columns — a static
        // straight-up beam would only ever curse one column.
        var g = Board();
        g.SetCharacter("necromancer");
        g.SetLoadout(new[] { "mage" });
        g.Serve();
        K.Park(g);
        g.ManaValue = 100;
        g.CastSlot(0);
        double a0 = g.LichBeam!.Angle;
        for (int i = 0; i < (int)(1.5 / Dt); i++) g.Tick(Dt);
        Assert.True(System.Math.Abs(g.LichBeam!.Angle - a0) > 0.1, "the beam swept (angle advanced)");

        for (int i = 0; i < (int)(4.0 / Dt); i++) g.Tick(Dt); // finish the arc
        int cursedCols = g.Blocks.Where(b => b.Cursed).Select(b => b.Col).Distinct().Count();
        Assert.True(cursedCols >= 2, $"the sweep cursed multiple columns (got {cursedCols})");
    }

    [Fact]
    public void LichGaze_CursedBlock_TakesBonusBallDamage()
    {
        var g = K.OneBlock(30);
        g.Serve();
        g.LichCurseBonus = 5;
        var blk = g.Blocks[0];
        blk.Cursed = true;
        int hp0 = blk.Hp;
        K.AimAt(g, blk);
        g.Tick(Dt);
        Assert.Equal(hp0 - (SimConfig.Default.BallDamage + 5), blk.Hp); // base + curse bonus
    }

    [Fact]
    public void LichGaze_UncursedBlock_NormalDamage()
    {
        var g = K.OneBlock(30);
        g.Serve();
        g.LichCurseBonus = 5;
        var blk = g.Blocks[0]; // not cursed
        int hp0 = blk.Hp;
        K.AimAt(g, blk);
        g.Tick(Dt);
        Assert.Equal(hp0 - SimConfig.Default.BallDamage, blk.Hp); // no bonus
    }

    [Fact]
    public void LichGaze_CurseBonus_ScalesWithLevel()
    {
        int Bonus(int lvl)
        {
            var g = Board();
            g.SetCharacter("necromancer");
            g.SetLoadout(new[] { "mage" });
            if (lvl > 1) g.SetSpellLevels(new Dictionary<string, int> { ["mage"] = lvl });
            g.Serve();
            K.Park(g);
            g.ManaValue = 100;
            g.CastSlot(0);
            return g.LichCurseBonus;
        }
        Assert.Equal(2, Bonus(1)); // base curse bonus
        Assert.Equal(3, Bonus(2)); // +1 per level (§6)
    }
}

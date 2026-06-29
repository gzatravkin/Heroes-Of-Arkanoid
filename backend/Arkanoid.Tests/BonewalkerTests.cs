using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// §3 Bonewalker (rework of "Skeleton"): a summoned minion that WALKS THE ROOFTOPS of the block field,
/// meleeing whatever block it stands on — NOT a paddle turret that sprays bone bolts on a timer.
/// These tests encode that trigger + identity (and the §6 timed-aura duration scaling), per CLAUDE.md.
/// </summary>
public class BonewalkerTests
{
    // cols×rows board; `topRows` rows are full of blocks of `hp`, the rest empty.
    private static GameInstance Make(int cols, int rows, int topRows, int hp)
    {
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"hell\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var full  = new string('A', cols);
        var empty = new string('.', cols);
        var rowsData = string.Join(",", Enumerable.Range(0, rows)
            .Select(r => "\"" + (r < topRows ? full : empty) + "\""));
        var level = LevelLoader.FromJson(
            $"{{\"id\":\"t\",\"biome\":\"hell\",\"cols\":{cols},\"rows\":{rows},\"rows_data\":[{rowsData}],\"legend\":{{\"A\":\"b\"}}}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.SetCharacter("necromancer");
        g.Serve();
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, 0); // park — the walker, not the ball, is under test
        g.ManaValue = 100;
        return g;
    }

    [Fact]
    public void Bonewalker_Cast_SpawnsMinion_NotAProjectileOrTurret()
    {
        var g = Make(5, 4, 1, 30);
        g.CastSlot(1); // necromancer slot 1 = Bonewalker
        var m = Assert.Single(g.Minions);
        Assert.Equal("bonewalker", m.Kind);
        Assert.Empty(g.Projectiles);            // it is NOT a turret that fires bullets
        Assert.True(g.SkeletonActive);          // the legacy flag now tracks the live minion
    }

    [Fact]
    public void Bonewalker_NoMana_NoMinion()
    {
        var g = Make(5, 4, 1, 30);
        g.ManaValue = 0;
        g.CastSlot(1);
        Assert.Empty(g.Minions);
        Assert.False(g.SkeletonActive);
    }

    [Fact]
    public void Bonewalker_WalksHorizontally_AlongTheRooftop()
    {
        var g = Make(7, 4, 1, 30);
        g.CastSlot(1);
        var m = g.Minions.Single();
        double x0 = m.X;
        double rooftopY = g.Level.Grid.CellCenter(0, 0).Y; // centre of the top block row

        for (int i = 0; i < (int)(1.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);

        Assert.True(System.Math.Abs(m.X - x0) > g.Config.CellSize,
            $"the walker should stride ≥1 cell horizontally in a second; moved {m.X - x0:0.0}px");
        Assert.True(m.Y + m.Height / 2 <= rooftopY,
            $"the walker should perch ON the rooftop (above the block centres); y={m.Y:0.0} rooftopY={rooftopY:0.0}");
    }

    [Fact]
    public void Bonewalker_Melees_TheRooftopBlockItStandsOn_NotTheBuriedRowBelow()
    {
        // Two full rows: the walker can only ever stand on the TOP row, so the buried row must stay pristine.
        var g = Make(5, 4, topRows: 2, hp: 30);
        var topRow    = g.Blocks.Where(b => b.Row == 0).ToList();
        var buriedRow = g.Blocks.Where(b => b.Row == 1).ToList();
        int buried0   = buriedRow.Sum(b => b.Hp);

        g.CastSlot(1);
        for (int i = 0; i < (int)(2.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);

        Assert.True(topRow.Any(b => b.Hp < b.MaxHp),
            "the walker should melee blocks on the rooftop it walks across");
        Assert.Equal(buried0, buriedRow.Sum(b => b.Hp)); // the covered row is never meleed (it stands on the roof)
    }

    [Fact]
    public void Bonewalker_Expires_AfterItsWalkDuration()
    {
        var g = Make(5, 4, 1, 30);
        g.CastSlot(1);
        Assert.Single(g.Minions);
        // Base walk-duration is 5s; run past it.
        for (int i = 0; i < (int)(5.5 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.Empty(g.Minions);
        Assert.False(g.SkeletonActive);
    }

    [Fact]
    public void Bonewalker_Level3_WalksLonger()
    {
        // §6 timed-aura: leveling extends how long it walks (base 5s + 1s/level → 7s at Lvl 3).
        double LifeAt(int level)
        {
            var g = Make(5, 4, 1, 30);
            if (level > 1) g.SetSpellLevels(new Dictionary<string, int> { ["skeleton"] = level });
            g.CastSlot(1);
            return g.Minions.Single().LifeRemaining;
        }
        Assert.Equal(5.0, LifeAt(1), 3);
        Assert.Equal(7.0, LifeAt(3), 3);
    }
}

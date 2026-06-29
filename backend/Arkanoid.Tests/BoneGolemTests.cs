using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// §3 Bone Golem (the "fix"): a summoned BODYGUARD minion that rises from the paddle, climbs a single
/// column bulldozing its blocks, and TANKS ENEMY FIRE (soaks hazards that would reach the paddle) until
/// soaked through — NOT a fat piercing projectile. These tests encode that trigger + identity.
/// </summary>
public class BoneGolemTests
{
    /// <summary>cols×rows board with a single column of `colHeight` blocks at column 1 (above the paddle).</summary>
    private static GameInstance MakeColumn(int colHeight, int hp)
    {
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"hell\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        int rows = colHeight + 2;
        var rowsData = string.Join(",", Enumerable.Range(0, rows)
            .Select(r => "\"" + (r < colHeight ? ".A." : "...") + "\""));
        var level = LevelLoader.FromJson(
            $"{{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":{rows},\"rows_data\":[{rowsData}],\"legend\":{{\"A\":\"b\"}}}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.SetCharacter("necromancer");
        g.Serve();
        g.Balls[0].Vel = new Vec2(0, 0); // park — the golem, not the ball, clears the column
        g.ManaValue = 100;
        g.SetPaddleX(g.Level.Grid.CellCenter(1, 0).X); // align the golem with the block column
        return g;
    }

    [Fact]
    public void Golem_Cast_SpawnsBodyguardMinion_NotAProjectile()
    {
        var g = MakeColumn(4, 1);
        g.CastSlot(3); // necromancer slot 3 = Bone Golem
        var m = Assert.Single(g.Minions);
        Assert.Equal("golem", m.Kind);
        Assert.True(m.MaxHp > 0, "the golem has a fire-soak HP pool");
        Assert.Empty(g.Projectiles);                                   // NOT a piercing projectile
        Assert.DoesNotContain(g.Projectiles, p => p.Kind == "golem");
    }

    [Fact]
    public void Golem_ClimbsUpward()
    {
        var g = MakeColumn(4, 30);
        g.CastSlot(3);
        var m = g.Minions.Single();
        double y0 = m.Y;
        for (int i = 0; i < 10; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(m.Y < y0, $"the golem should climb upward (y decreasing); y0={y0:0.0} y={m.Y:0.0}");
    }

    [Fact]
    public void Golem_BulldozesItsWholeColumn()
    {
        var g = MakeColumn(4, 1);
        g.CastSlot(3);
        for (int i = 0; i < 300; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Blocks.TrueForAll(b => b.Dead),
            $"the climbing golem should bulldoze its whole column (alive={g.Blocks.Count(b => !b.Dead)})");
    }

    [Fact]
    public void Golem_TanksEnemyFire_SoakingAHazardThatWouldHitThePlayer()
    {
        var g = MakeColumn(4, 30);
        g.CastSlot(3);
        var m = g.Minions.Single();
        int golemHp0 = m.Hp;
        int playerHp0 = g.Hp;
        // An enemy bolt descending right onto the golem's body.
        g.Hazards.Add(new Projectile
        {
            Id = 999, Pos = new Vec2(m.X, m.Y), Vel = new Vec2(0, 150),
            Damage = 3, Radius = 6, Alive = true, Kind = "bolt",
        });
        g.Tick(SimConfig.Default.FixedDt);

        Assert.Equal(golemHp0 - 3, g.Minions.Single().Hp); // the golem bodied the shot
        Assert.Empty(g.Hazards);                            // …consuming it
        Assert.Equal(playerHp0, g.Hp);                      // …so the player took no damage
    }

    [Fact]
    public void Golem_DiesWhenSoakedThrough()
    {
        var g = MakeColumn(4, 30);
        g.CastSlot(3);
        var m = g.Minions.Single();
        g.Hazards.Add(new Projectile
        {
            Id = 999, Pos = new Vec2(m.X, m.Y), Vel = new Vec2(0, 150),
            Damage = m.MaxHp + 50, Radius = 6, Alive = true, Kind = "bolt",
        });
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Empty(g.Minions); // soaked through → the bodyguard falls
    }

    [Fact]
    public void Golem_Level3_HasMoreFireSoak()
    {
        int MaxHpAt(int level)
        {
            var g = MakeColumn(4, 30);
            if (level > 1) g.SetSpellLevels(new Dictionary<string, int> { ["golem"] = level });
            g.CastSlot(3);
            return g.Minions.Single().MaxHp;
        }
        Assert.True(MaxHpAt(3) > MaxHpAt(1), "a higher-level golem should tank more enemy fire");
    }
}

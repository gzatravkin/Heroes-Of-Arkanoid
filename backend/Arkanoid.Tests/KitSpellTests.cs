using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// G2c kit-completion spells (docs/09 G2): Phoenix, Penetration, Last Day,
/// Magnet, Overload, Bone Golem, Skeletal Mage. Pure-Core, deterministic.
/// </summary>
public class KitSpellTests
{
    private static GameInstance Make(string blocksJson, string levelJson)
    {
        var catalog = BlockCatalog.FromJson(blocksJson);
        var level   = LevelLoader.FromJson(levelJson, catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        g.ManaValue = 100;
        g.Balls[0].Vel = new Vec2(0, 0); // park — tests drive the ball explicitly
        return g;
    }

    private static GameInstance MakeOneBlock(int hp) => Make(
        $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"t\",\"hp\":{hp},\"sprite\":\"s\",\"needToKill\":true}}]}}",
        "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}");

    // ── Phoenix (Fire Mage) ────────────────────────────────────────────────────

    [Fact]
    public void Phoenix_SearsBlocksNearTheBall_OverItsDuration()
    {
        var g = MakeOneBlock(9);
        g.SetCharacter("fire_mage");
        var blk = g.Blocks[0];
        // Park the ball right next to the block so it sits inside the sear radius.
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize);

        g.CastSlot(4); // phoenix
        Assert.Equal(100 - SimConfig.Default.PhoenixCost, g.ManaValue, 1);

        // Two sear ticks worth of time → at least 2 damage.
        var ticks = (int)(SimConfig.Default.PhoenixTickInterval * 2.5 / SimConfig.Default.FixedDt);
        for (int i = 0; i < ticks; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(blk.Hp <= 9 - 2 * SimConfig.Default.PhoenixDamage,
            $"phoenix should have seared the block at least twice (hp={blk.Hp})");
    }

    // ── Penetration (Paladin) ──────────────────────────────────────────────────

    [Fact]
    public void Penetration_ArmsPhaseHits_OnTheNextDeflect()
    {
        var g = MakeOneBlock(9);
        g.SetCharacter("paladin");
        g.CastSlot(3); // penetration — armed
        Assert.Equal(0, g.Balls[0].PhasesLeft);

        // Deflect off the paddle → the arm lands.
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(SimConfig.Default.PenetrationHits, g.Balls[0].PhasesLeft);
    }

    // ── Last Day (Paladin) ─────────────────────────────────────────────────────

    [Fact]
    public void LastDay_TopWallBounce_SmitesTheBallsColumn()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}",
            // Block in col 1, row 1 — leaves row 0 free so the ball can reach the ceiling.
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\"...\",\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}");
        g.SetCharacter("paladin");
        var blk = g.Blocks[0];
        g.CastSlot(4); // last day

        // Send the ball at the ceiling in column 0 first — wrong column, no damage to blk.
        var colX = g.Level.Grid.CellCenter(blk.Col, 0).X;
        g.Balls[0].Pos = new Vec2(colX, g.Balls[0].Radius + 2);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(4 - SimConfig.Default.LastDayDamage, blk.Hp);
    }

    // ── Magnet (Engineer) ──────────────────────────────────────────────────────

    [Fact]
    public void Magnet_BendsTheBallTowardTheNearestBlock()
    {
        var g = MakeOneBlock(9);
        g.SetCharacter("engineer");
        var blk = g.Blocks[0];
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row); // (1,0) — up and to the right

        // Ball moving straight LEFT from below-right of the block.
        g.Balls[0].Pos = new Vec2(c.X + SimConfig.Default.CellSize, c.Y + SimConfig.Default.CellSize * 2);
        g.Balls[0].Vel = new Vec2(-SimConfig.Default.BallSpeed, 0);
        g.CastSlot(3); // magnet

        for (int i = 0; i < 20; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Balls[0].Vel.Y < 0,
            $"magnet should bend the heading upward toward the block (vy={g.Balls[0].Vel.Y:F1})");
    }

    // ── Overload (Engineer) ────────────────────────────────────────────────────

    [Fact]
    public void Overload_PlacesAFriendlyBomb_ThatChainExplodes()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":6,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}");
        g.SetCharacter("engineer");
        int before = g.Blocks.Count;
        g.CastSlot(4); // overload
        Assert.Equal(before + 1, g.Blocks.Count);

        var bomb = g.Blocks[^1];
        Assert.True(bomb.Bomb);
        Assert.False(bomb.NeedToKill, "the placed bomb must not block the win condition");

        // Detonate it with the ball → explosion event raised.
        var c = g.Level.Grid.CellCenter(bomb.Col, bomb.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(bomb.Dead, "ball pops the bomb");
        Assert.Contains(Arkanoid.Core.Net.Snapshot.From(g, 0).Events, e => e.Type == "explosion");
    }

    // ── Bone Golem (Necromancer) ───────────────────────────────────────────────

    [Fact]
    public void Golem_PiercesThroughSeveralBlocks()
    {
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            // A column of 4 one-hp blocks above the paddle column's centre... the golem
            // launches from the paddle X, so put the column wherever the paddle is.
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":6,\"rows_data\":[\".A.\",\".A.\",\".A.\",\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}");
        g.SetCharacter("necromancer");
        g.SetPaddleX(g.Level.Grid.CellCenter(1, 0).X); // align with the block column
        g.CastSlot(3); // golem

        Assert.Contains(g.Projectiles, p => p.Kind == "golem");
        for (int i = 0; i < 300; i++) g.Tick(SimConfig.Default.FixedDt);
        // GolemPierce=4, GolemDamage=2 → all four 1-hp blocks die to one golem.
        Assert.True(g.Blocks.TrueForAll(b => b.Dead),
            $"golem should pierce the whole column (alive={g.Blocks.FindAll(b => !b.Dead).Count})");
    }

    // ── Skeletal Mage (Necromancer) ────────────────────────────────────────────

    [Fact]
    public void Mage_FiresAFanOfSkeletonBolts()
    {
        var g = MakeOneBlock(9);
        g.SetCharacter("necromancer");
        g.CastSlot(4); // mage
        Assert.Equal(SimConfig.Default.MageBolts,
            g.Projectiles.FindAll(p => p.Kind == "skeleton_bullet").Count);
        Assert.Equal(100 - SimConfig.Default.MageCost, g.ManaValue, 1);
    }

    // ── Kits expose 5 slots each ───────────────────────────────────────────────

    [Theory]
    [InlineData("fire_mage", "phoenix")]
    [InlineData("paladin", "lastday")]
    [InlineData("engineer", "overload")]
    [InlineData("necromancer", "mage")]
    public void EveryClass_HasAFifthSlot(string character, string expectMana)
    {
        var g = MakeOneBlock(9);
        g.SetCharacter(character);
        var before = g.ManaValue;
        g.CastSlot(4);
        Assert.True(g.ManaValue < before, $"{character}'s 5th slot ({expectMana}) should spend mana");
    }
}

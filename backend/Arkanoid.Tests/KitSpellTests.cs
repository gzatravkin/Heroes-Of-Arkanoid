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
    public void Phoenix_OrbitsTheBall_AndSearsBlocksItSweepsOver()
    {
        // Design: the phoenix orbits the ball (its own position) and sears blocks it sweeps
        // over. Park the ball so the block sits on the orbit ring; over its lifetime the
        // phoenix passes the block repeatedly and burns it. See the Fire Mage spec.
        var g = MakeOneBlock(9);
        g.SetCharacter("fire_mage");
        var blk = g.Blocks[0];
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        // Ball one orbit-radius (56) below the block: the phoenix reaches the block at the top of its orbit.
        g.Balls[0].Pos = new Vec2(c.X, c.Y + 56);

        g.CastSlot(4); // phoenix — costs 30 mana
        Assert.Equal(70, g.ManaValue, 1);
        Assert.Single(g.Phoenixes);

        // Run the full phoenix lifetime (6s) — several orbits, several sear ticks.
        for (int i = 0; i < (int)(6.0 / SimConfig.Default.FixedDt); i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(blk.Hp < blk.MaxHp,
            $"the orbiting phoenix should have seared the block it sweeps over (hp={blk.Hp})");
    }

    // ── Penetration (Paladin) ──────────────────────────────────────────────────

    [Fact]
    public void Penetration_ArmsPhaseHits_OnTheNextDeflect()
    {
        var g = MakeOneBlock(9);
        g.SetCharacter("paladin");
        g.CastSlot(4); // penetration — armed (holy_echo shifted slot 2→3, penetration→4)
        Assert.Equal(0, g.Balls[0].PhasesLeft);

        // Deflect off the paddle → the arm lands.
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X,
            g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
        g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(3, g.Balls[0].PhasesLeft); // penetration grants 3 phase hits
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
        g.CastSlot(5); // last day (holy_echo shifted slot; lastday is now at index 5)

        // Send the ball at the ceiling in column 0 first — wrong column, no damage to blk.
        var colX = g.Level.Grid.CellCenter(blk.Col, 0).X;
        g.Balls[0].Pos = new Vec2(colX, g.Balls[0].Radius + 2);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt);
        Assert.Equal(2, blk.Hp); // 4 - 2 lastday damage (balance 2026-06-16: strike dmg 1→2)
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
    public void Overload_ArmsOnCast_ThenChargesBlockOnHit_ThenExplodesAfterDelay()
    {
        // Rework (tasks list.md): cast arms the overload; next ball-block hit plants a 0.5 s charge
        // that then chain-detonates neighbors. No immediate bomb block is placed.
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":4,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":6,\"rows_data\":[\".A.\",\"...\",\"...\",\"...\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}");
        g.SetCharacter("engineer");
        int before = g.Blocks.Count;
        g.CastSlot(4); // overload — arms the flag, no new block
        Assert.Equal(before, g.Blocks.Count); // no block placed yet
        Assert.True(g._overloadArmed, "overload should be armed after cast");

        // Aim ball at the existing block to plant the charge
        var blk = g.Blocks[0];
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
        g.Tick(SimConfig.Default.FixedDt); // ball hits block → charge planted
        Assert.False(g._overloadArmed, "overload arm should be consumed on first block hit");
        Assert.True(g._overloadChargeTimer > 0, "charge timer should be set after hit");
        int colCharged = g._overloadChargeCol;
        int rowCharged = g._overloadChargeRow;
        Assert.True(colCharged >= 0 && rowCharged >= 0, "charge position should be recorded");

        // Move ball far from all blocks so it doesn't kill them before the charge fires
        g.Balls[0].Pos = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 5);
        g.Balls[0].Vel = new Vec2(0, 0);

        // Run for 0.6 s → timer fires → explosion event (tick only while Playing)
        double elapsed = 0;
        bool sawExplosion = false;
        while (elapsed < 0.6 && g.Phase == Arkanoid.Core.Sim.GamePhase.Playing)
        {
            g.Tick(SimConfig.Default.FixedDt);
            elapsed += SimConfig.Default.FixedDt;
            var snap = Arkanoid.Core.Net.Snapshot.From(g, 0);
            if (snap.Events.Any(e => e.Type == "explosion"))
                sawExplosion = true;
        }
        Assert.True(sawExplosion, "explosion event should fire after 0.5 s delay");
        Assert.True(g._overloadChargeTimer <= 0, "charge timer should be exhausted");
    }

    // ── Bone Golem (Necromancer) ───────────────────────────────────────────────

    [Fact]
    public void Golem_ClimbsAndBulldozesColumn_AsAMinion_NotAProjectile()
    {
        // §3 fix: Bone Golem is a climbing bodyguard MINION, not a fat piercing projectile. It rises
        // from the paddle's column and bulldozes every block above it.
        var g = Make(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}]}",
            // A column of 4 one-hp blocks above the paddle column's centre.
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":6,\"rows_data\":[\".A.\",\".A.\",\".A.\",\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}");
        g.SetCharacter("necromancer");
        g.SetPaddleX(g.Level.Grid.CellCenter(1, 0).X); // align with the block column
        g.CastSlot(3); // Bone Golem

        Assert.Contains(g.Minions, m => m.Kind == "golem"); // a minion entity…
        Assert.Empty(g.Projectiles);                        // …NOT a projectile
        for (int i = 0; i < 300; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Blocks.TrueForAll(b => b.Dead),
            $"the golem should bulldoze the whole column (alive={g.Blocks.FindAll(b => !b.Dead).Count})");
    }

    // ── Skeletal Mage (Necromancer) ────────────────────────────────────────────

    [Fact]
    public void Mage_CastsLichGaze_NotAFanOfBolts()
    {
        // §3: Skeletal Mage is now Lich's Gaze — a sweeping curse beam, NOT a bolt fan.
        var g = MakeOneBlock(9);
        g.SetCharacter("necromancer");
        g.CastSlot(4); // mage = Lich's Gaze
        Assert.NotNull(g.LichBeam);    // a sweeping beam, not projectiles
        Assert.Empty(g.Projectiles);
        Assert.Equal(75, g.ManaValue, 1); // 100 - 25
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

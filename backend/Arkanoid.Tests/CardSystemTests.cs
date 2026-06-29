using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>
/// §1 Cards — in-play rule-breaking PASSIVE triggers (CardSystem). Each test encodes the card's DESIGN:
/// its trigger + balance lever (CLAUDE.md), and the structural invariant that an UNEQUIPPED card never fires.
/// Batch A: Headhunter, Underdog, Opening Gambit, Cleanup Crew (the position / on-kill / count-gated cluster).
/// </summary>
public class CardSystemTests
{
    /// <summary>cols×rows board; the first `topRows` rows are full of `hp` blocks, the rest empty.</summary>
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
        g.Serve();
        return g;
    }

    private static void Equip(GameInstance g, string id, int level = 1)
        => g.SetCards(new Dictionary<string, int> { [id] = level });

    private static Arkanoid.Core.Entities.Block BlockAt(GameInstance g, int col, int row)
        => g.Blocks.First(b => !b.Dead && b.Col == col && b.Row == row);

    // ── Headhunter: position-gated +dmg to the TOP row only ─────────────────────
    [Fact]
    public void Headhunter_AddsDamage_OnlyToTopRowBlocks()
    {
        var g = Make(3, 4, topRows: 3, hp: 30); // live rows 0,1,2 → top row = 0
        Equip(g, "headhunter");
        var ball = g.Balls[0];
        Assert.Equal(1, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 0))); // top row → +1
        Assert.Equal(0, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 1))); // not top → +0
        Assert.Equal(0, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 2)));
    }

    [Fact]
    public void Headhunter_ScalesWithLevel()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "headhunter", level: 3);
        Assert.Equal(3, CardSystem.BallDamageBonus(g, g.Balls[0], BlockAt(g, 1, 0)));
    }

    [Fact]
    public void Headhunter_Unequipped_NoBonus()
    {
        var g = Make(3, 2, 1, 30); // no SetCards → nothing equipped
        Assert.Equal(0, CardSystem.BallDamageBonus(g, g.Balls[0], BlockAt(g, 1, 0)));
    }

    [Fact]
    public void Headhunter_RealHit_DealsBasePlusBonus()
    {
        // Full path: a ball hit on a top-row block must do base+1 vs base. (proves the BallSystem injection)
        int Drop(bool equip)
        {
            var g = Make(3, 4, 3, 30);
            if (equip) Equip(g, "headhunter");
            var blk = BlockAt(g, 1, 0);
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            var b = g.Balls[0];
            b.Pos = new Arkanoid.Core.Math.Vec2(c.X, c.Y + g.Config.CellSize / 2 + b.Radius - 1);
            b.Vel = new Arkanoid.Core.Math.Vec2(0, -g.Config.BallSpeed); // up into the block
            int hp0 = blk.Hp;
            g.Tick(SimConfig.Default.FixedDt);
            return hp0 - blk.Hp;
        }
        Assert.Equal(Drop(false) + 1, Drop(true));
    }

    // ── Underdog: +dmg to the BOTTOM two rows only ──────────────────────────────
    [Fact]
    public void Underdog_AddsDamage_OnlyToBottomTwoRows()
    {
        var g = Make(3, 5, topRows: 4, hp: 30); // live rows 0..3 → bottom rows = 3 and 2
        Equip(g, "underdog");
        var ball = g.Balls[0];
        Assert.Equal(1, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 3))); // bottom row
        Assert.Equal(1, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 2))); // second-from-bottom
        Assert.Equal(0, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 1))); // higher up → +0
        Assert.Equal(0, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 0)));
    }

    // ── Cleanup Crew: only once the board is nearly clear ───────────────────────
    [Fact]
    public void CleanupCrew_OnlyWhenSixOrFewerBlocksRemain()
    {
        var g = Make(5, 2, 2, 30); // 10 live blocks
        Equip(g, "cleanup_crew");
        var ball = g.Balls[0];
        Assert.Equal(0, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 0, 0))); // 10 > 6 → no bonus
        // Kill blocks down to 6 remaining.
        foreach (var b in g.Blocks.Where(b => !b.Dead).Take(4).ToList()) b.Dead = true;
        Assert.Equal(6, g.Blocks.Count(b => !b.Dead));
        Assert.Equal(2, CardSystem.BallDamageBonus(g, ball, g.Blocks.First(b => !b.Dead))); // ≤6 → +2
    }

    // ── Opening Gambit: first kill each level detonates, ONCE per level ─────────
    [Fact]
    public void OpeningGambit_FirstKill_Detonates_OncePerLevel()
    {
        var g = Make(5, 1, 1, 5); // a row of 5 blocks at cols 0..4
        Equip(g, "opening_gambit");
        // Kill the centre block (col2) — the gambit AoE should chip its neighbours col1 & col3 by 2.
        var centre = BlockAt(g, 2, 0);
        BlockDamage.DamageBlock(g, centre, centre.Hp, igniteSource: false);
        Assert.Equal(3, BlockAt(g, 1, 0).Hp); // 5 - 2 (gambit)
        Assert.Equal(3, BlockAt(g, 3, 0).Hp);

        // A LATER kill (col0) must NOT re-fire the gambit — its neighbour col1 stays at 3.
        var col0 = BlockAt(g, 0, 0);
        BlockDamage.DamageBlock(g, col0, col0.Hp, igniteSource: false);
        Assert.Equal(3, BlockAt(g, 1, 0).Hp); // unchanged → gambit is once-per-level
    }

    [Fact]
    public void OpeningGambit_Unequipped_NoDetonation()
    {
        var g = Make(5, 1, 1, 5); // nothing equipped
        var centre = BlockAt(g, 2, 0);
        BlockDamage.DamageBlock(g, centre, centre.Hp, igniteSource: false);
        Assert.Equal(5, BlockAt(g, 1, 0).Hp); // neighbour untouched
    }

    // ── Batch B: Bank Shot — wall caroms bank damage, reset on hit ──────────────
    [Fact]
    public void BankShot_BanksWallBounces_ThenResetsOnHit()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "bank_shot");
        var ball = g.Balls[0];
        // Three wall bounces bank +3.
        CardSystem.OnWallBounce(g, ball);
        CardSystem.OnWallBounce(g, ball);
        CardSystem.OnWallBounce(g, ball);
        Assert.Equal(3, ball.BankCharge);
        Assert.Equal(3, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 0))); // banked +3
        Assert.Equal(0, ball.BankCharge);                                        // reset on the hit
        Assert.Equal(0, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 0))); // nothing banked now
    }

    [Fact]
    public void BankShot_IsCapped()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "bank_shot"); // L1 cap = 3 + 1 = 4
        var ball = g.Balls[0];
        for (int i = 0; i < 20; i++) CardSystem.OnWallBounce(g, ball);
        Assert.Equal(4, CardSystem.BallDamageBonus(g, ball, BlockAt(g, 1, 0))); // capped, not 20
    }

    // ── Batch B: Executioner's Edge — crit vs low-HP block doubles ──────────────
    [Fact]
    public void ExecutionersEdge_DoublesCrit_OnlyVsLowHpBlock()
    {
        var g = Make(3, 2, 1, 20);
        Equip(g, "executioners_edge"); // L1 window = ≤25% HP
        var blk = BlockAt(g, 1, 0);
        blk.Hp = 5; // 25% of 20 → executes
        Assert.Equal(8, CardSystem.ExecutionerExtra(g, blk, critDmg: 8)); // +8 → double
        blk.Hp = 6; // 30% → out of window
        Assert.Equal(0, CardSystem.ExecutionerExtra(g, blk, critDmg: 8));
    }

    [Fact]
    public void ExecutionersEdge_Unequipped_NoExtra()
    {
        var g = Make(3, 2, 1, 20);
        var blk = BlockAt(g, 1, 0); blk.Hp = 1;
        Assert.Equal(0, CardSystem.ExecutionerExtra(g, blk, critDmg: 99));
    }

    // ── Batch B: Overkill — surplus carries to the block behind ─────────────────
    [Fact]
    public void Overkill_SpillsExcessToTheBlockBehind()
    {
        // Two stacked blocks: front (row1, low HP) below behind (row0). Ball approaches from below.
        var g = Make(3, 3, topRows: 2, hp: 30);
        Equip(g, "overkill");
        var front  = BlockAt(g, 1, 1); front.Hp = 2;
        var behind = BlockAt(g, 1, 0); int behind0 = behind.Hp; // 30
        var b = g.Balls[0];
        var fc = g.Level.Grid.CellCenter(front.Col, front.Row);
        b.Pos = new Arkanoid.Core.Math.Vec2(fc.X, fc.Y + g.Config.CellSize); // below the front block
        // A 10-damage hit kills the 2-HP front block; excess 8 spills to the block behind (above it).
        int hpBefore = front.Hp;
        BlockDamage.DamageBlock(g, front, 10, igniteSource: false);
        CardSystem.OnBlockHit(g, b, front, dmgDealt: 10, hpBefore: hpBefore);
        Assert.Equal(behind0 - 8, behind.Hp); // 30 - (10 - 2) excess
    }

    [Fact]
    public void Overkill_NoSpill_WhenNotOverDoubled()
    {
        var g = Make(3, 3, topRows: 2, hp: 30);
        Equip(g, "overkill");
        var front  = BlockAt(g, 1, 1); front.Hp = 5;
        var behind = BlockAt(g, 1, 0); int behind0 = behind.Hp;
        var b = g.Balls[0];
        var fc = g.Level.Grid.CellCenter(front.Col, front.Row);
        b.Pos = new Arkanoid.Core.Math.Vec2(fc.X, fc.Y + g.Config.CellSize);
        BlockDamage.DamageBlock(g, front, 6, igniteSource: false); // 6 is not > 2×5=10 → no spill
        CardSystem.OnBlockHit(g, b, front, dmgDealt: 6, hpBefore: 5);
        Assert.Equal(behind0, behind.Hp);
    }

    // ── Batch B: Erosion — cracks an indestructible wall after enough hits ───────
    private static GameInstance MakeWithIndestructible()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"w\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"indestructible\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":2,\"rows_data\":[\".A.\",\"...\"],\"legend\":{\"A\":\"w\"}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.Serve();
        return g;
    }

    [Fact]
    public void Erosion_CracksIndestructible_AfterSixteenHits()
    {
        var g = MakeWithIndestructible();
        Equip(g, "erosion"); // L1 threshold = 16
        var wall = g.Blocks.First(b => b.Indestructible);
        var b = g.Balls[0];
        for (int i = 0; i < 15; i++) CardSystem.OnBlockHit(g, b, wall, dmgDealt: 0, hpBefore: 0);
        Assert.False(wall.Dead, "15 hits should not yet crack it");
        CardSystem.OnBlockHit(g, b, wall, dmgDealt: 0, hpBefore: 0); // 16th
        Assert.True(wall.Dead, "the 16th erosion hit cracks the indestructible wall");
    }

    [Fact]
    public void Erosion_Unequipped_IndestructibleNeverCracks()
    {
        var g = MakeWithIndestructible();
        var wall = g.Blocks.First(b => b.Indestructible);
        var b = g.Balls[0];
        for (int i = 0; i < 30; i++) CardSystem.OnBlockHit(g, b, wall, dmgDealt: 0, hpBefore: 0);
        Assert.False(wall.Dead); // no erosion card → indestructible stays forever
    }

    [Fact]
    public void Erosion_DoesNotCrack_IndestructibleWithBehavior()
    {
        // Only PLAIN walls (Behavior None) erode — an indestructible statue/emitter/boss must survive,
        // even with Erosion equipped. (Guards against the cardinal-rule trap: deleting !Boss/Behavior==None.)
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"s\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"indestructible\":true,\"behavior\":\"Emitter\"}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":2,\"rows_data\":[\".A.\",\"...\"],\"legend\":{\"A\":\"s\"}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, 1);
        g.Serve();
        Equip(g, "erosion", level: 5); // threshold would be 4 for a plain wall
        var statue = g.Blocks.First(b => b.Indestructible);
        var b = g.Balls[0];
        for (int i = 0; i < 30; i++) CardSystem.OnBlockHit(g, b, statue, dmgDealt: 0, hpBefore: 0);
        Assert.False(statue.Dead); // a behavior'd indestructible never erodes
    }

    [Fact]
    public void Overkill_SpillsExcess_Horizontally_ToTheBlockBehind()
    {
        // Ball approaches from the LEFT → "behind" is the next column to the RIGHT (dominant-axis dc=+1).
        var g = Make(3, 1, topRows: 1, hp: 30); // row of 3 blocks at cols 0,1,2
        Equip(g, "overkill");
        var front  = BlockAt(g, 1, 0); front.Hp = 2;
        var behind = BlockAt(g, 2, 0); int behind0 = behind.Hp;
        var b = g.Balls[0];
        var fc = g.Level.Grid.CellCenter(front.Col, front.Row);
        b.Pos = new Arkanoid.Core.Math.Vec2(fc.X - g.Config.CellSize, fc.Y); // left of the front block
        BlockDamage.DamageBlock(g, front, 10, igniteSource: false);
        CardSystem.OnBlockHit(g, b, front, dmgDealt: 10, hpBefore: 2);
        Assert.Equal(behind0 - 8, behind.Hp); // excess 8 spilled rightward to col 2
    }

    // ── Batch C: Dead Center — perfect deflect arms a first-block burst ──────────
    [Fact]
    public void DeadCenter_PerfectDeflect_ArmsFirstBlockBurst()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "dead_center");
        var b = g.Balls[0];
        CardSystem.OnPaddleHit(g, b, isPerfect: true);
        Assert.True(b.DeadCenterArmed);
        Assert.Equal(3, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // L1 burst = +3
        Assert.False(b.DeadCenterArmed);                                     // first-block only
        Assert.Equal(0, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // not armed anymore
    }

    [Fact]
    public void DeadCenter_NonPerfectDeflect_DoesNotArm()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "dead_center");
        var b = g.Balls[0];
        CardSystem.OnPaddleHit(g, b, isPerfect: false);
        Assert.False(b.DeadCenterArmed);
        Assert.Equal(0, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0)));
    }

    // ── Batch C: Metronome — consecutive perfect deflects stack; a miss resets ───
    [Fact]
    public void Metronome_StacksOnPerfect_ResetsOnMiss()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "metronome");
        var b = g.Balls[0];
        CardSystem.OnPaddleHit(g, b, isPerfect: true);
        CardSystem.OnPaddleHit(g, b, isPerfect: true);
        CardSystem.OnPaddleHit(g, b, isPerfect: true);
        Assert.Equal(3, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // +3 from 3 stacks
        CardSystem.OnPaddleHit(g, b, isPerfect: false);                       // one miss
        Assert.Equal(0, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // streak wiped
    }

    [Fact]
    public void Metronome_IsCapped()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "metronome"); // L1 cap = 4 + 1 = 5
        var b = g.Balls[0];
        for (int i = 0; i < 20; i++) CardSystem.OnPaddleHit(g, b, isPerfect: true);
        Assert.Equal(5, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // capped, not 20
    }

    // ── Batch C: Phase Window — high combo opens a full-pierce window ────────────
    [Fact]
    public void PhaseWindow_GrantsPierce_OnlyWhenComboHighEnough()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "phase_window"); // L1 threshold = 15
        var b = g.Balls[0];
        g.Combo.Count = 10;
        CardSystem.OnBallTick(g, b, 0.016);
        Assert.Equal(0, b.PhasesLeft); // below threshold → no pierce
        g.Combo.Count = 15;
        CardSystem.OnBallTick(g, b, 0.016);
        Assert.True(b.PhasesLeft >= 2); // combo window open → ball phases through blocks
    }

    [Fact]
    public void PhaseWindow_ThresholdDropsWithLevel()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "phase_window", level: 5); // threshold = max(7, 15 - 8) = 7
        var b = g.Balls[0];
        g.Combo.Count = 7;
        CardSystem.OnBallTick(g, b, 0.016);
        Assert.True(b.PhasesLeft >= 2);
    }

    // ── Batch D: Avalanche — hot-combo kill crushes the block below ──────────────
    [Fact]
    public void Avalanche_OnHotCombo_CrushesTheBlockBelow()
    {
        var g = Make(3, 3, topRows: 2, hp: 30); // col1: row0 (top) over row1 (below)
        Equip(g, "avalanche", level: 5);        // chance = min(1, 0.25+0.75) = 1.0
        var top = BlockAt(g, 1, 0);
        var below = BlockAt(g, 1, 1);
        g.Combo.Count = 8;                       // hot combo
        BlockDamage.DamageBlock(g, top, top.Hp, igniteSource: false); // kill the top → it falls
        Assert.Equal(30 - 8, below.Hp);          // crushed for 3 + level(5) = 8
    }

    [Fact]
    public void Avalanche_ColdCombo_DoesNotCrush()
    {
        var g = Make(3, 3, topRows: 2, hp: 30);
        Equip(g, "avalanche", level: 5);
        var top = BlockAt(g, 1, 0);
        var below = BlockAt(g, 1, 1);
        g.Combo.Count = 0;                       // below the ≥8 gate (1 after this kill)
        BlockDamage.DamageBlock(g, top, top.Hp, igniteSource: false);
        Assert.Equal(30, below.Hp);              // no crush without a hot combo
    }

    // ── Batch D: Keystone — load-bearing kill collapses the column ───────────────
    [Fact]
    public void Keystone_KillingLoadBearingBlock_CollapsesColumnAbove()
    {
        var g = Make(3, 4, topRows: 3, hp: 30); // col1: rows 0,1,2 stacked
        Equip(g, "keystone", level: 5);          // chance 1.0
        var above = BlockAt(g, 1, 0);            // top of the stack
        int row0 = above.Row;
        var baseBlk = BlockAt(g, 1, 2);          // load-bearing (has a stack above)
        g.InvalidateBlockGrid();
        BlockDamage.DamageBlock(g, baseBlk, baseBlk.Hp, igniteSource: false); // kill the base
        Assert.True(above.Row > row0, $"the unsupported column should collapse (fall); row {row0} → {above.Row}");
    }

    [Fact]
    public void Keystone_KillingTopBlock_NoCollapse()
    {
        var g = Make(3, 4, topRows: 3, hp: 30);
        Equip(g, "keystone", level: 5);
        var mid = BlockAt(g, 1, 1); int midRow = mid.Row;
        var topBlk = BlockAt(g, 1, 0); // nothing above it → not load-bearing
        BlockDamage.DamageBlock(g, topBlk, topBlk.Hp, igniteSource: false);
        Assert.Equal(midRow, mid.Row); // no stack above the killed block → no collapse
    }

    // ── Batch D: Domino — 3 deaths within 1s → the next death explodes ───────────
    [Fact]
    public void Domino_ThreeKillsInAWindow_NextKillExplodes()
    {
        var g = Make(7, 1, topRows: 1, hp: 30); // a row of 7 blocks (cols 0..6)
        Equip(g, "domino");
        // Three non-adjacent kills arm the chain (no AoE yet — the 3rd only arms).
        foreach (var col in new[] { 0, 2, 4 })
        {
            var blk = BlockAt(g, col, 0);
            BlockDamage.DamageBlock(g, blk, blk.Hp, igniteSource: false);
        }
        var neighbour = BlockAt(g, 5, 0); int n0 = neighbour.Hp; // adjacent to col6
        // The 4th kill (col6) detonates — its neighbour col5 takes the AoE.
        var fourth = BlockAt(g, 6, 0);
        BlockDamage.DamageBlock(g, fourth, fourth.Hp, igniteSource: false);
        Assert.True(neighbour.Hp < n0, $"the 4th kill should chain-explode onto col5; hp {n0} → {neighbour.Hp}");
    }

    [Fact]
    public void Domino_FewerThanThree_NoExplosion()
    {
        var g = Make(7, 1, topRows: 1, hp: 30);
        Equip(g, "domino");
        // Only two kills, then a third adjacent to a survivor: the 3rd only ARMS (does not explode).
        foreach (var col in new[] { 0, 2 })
        {
            var blk = BlockAt(g, col, 0);
            BlockDamage.DamageBlock(g, blk, blk.Hp, igniteSource: false);
        }
        var neighbour = BlockAt(g, 5, 0); int n0 = neighbour.Hp;
        var third = BlockAt(g, 6, 0);
        BlockDamage.DamageBlock(g, third, third.Hp, igniteSource: false); // 3rd death → arms, no boom
        Assert.Equal(n0, neighbour.Hp); // not yet exploded
    }

    // ── Batch E: Martyr's Brand — losing HP grants a short damage buff ───────────
    [Fact]
    public void MartyrsBrand_HpLoss_GrantsTemporaryDamageBuff()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "martyrs_brand");
        var b = g.Balls[0];
        Assert.Equal(0, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // no buff before being hit
        CardSystem.OnHpLost(g, 1);                                            // take a hit
        Assert.Equal(3, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // vengeance: +2+level(1)=3
    }

    [Fact]
    public void MartyrsBrand_Unequipped_NoBuff()
    {
        var g = Make(3, 2, 1, 30);
        CardSystem.OnHpLost(g, 1); // no card → no effect
        Assert.Equal(0, CardSystem.BallDamageBonus(g, g.Balls[0], BlockAt(g, 1, 0)));
    }

    // ── Batch E: Ricochet — side-wall bounce fires a horizontal bolt ─────────────
    [Fact]
    public void Ricochet_WallBounce_FiresHorizontalBolt_InTravelDirection()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "ricochet", level: 5);
        var b = g.Balls[0];
        b.Vel = new Arkanoid.Core.Math.Vec2(300, -200); // moving right (just left the left wall)
        // Over many bounces the ≤0.6 chance fires at least one bolt; assert it's horizontal + rightward.
        for (int i = 0; i < 30 && g.Projectiles.Count == 0; i++) CardSystem.OnWallBounce(g, b);
        var bolt = g.Projectiles.FirstOrDefault(p => p.Kind == "ricochet");
        Assert.NotNull(bolt);
        Assert.Equal(0, bolt!.Vel.Y);     // horizontal
        Assert.True(bolt.Vel.X > 0);      // fired in the ball's travel direction (off the wall)
    }

    [Fact]
    public void Ricochet_Unequipped_NoBolt()
    {
        var g = Make(3, 2, 1, 30);
        var b = g.Balls[0];
        b.Vel = new Arkanoid.Core.Math.Vec2(300, -200);
        for (int i = 0; i < 30; i++) CardSystem.OnWallBounce(g, b);
        Assert.DoesNotContain(g.Projectiles, p => p.Kind == "ricochet");
    }

    // ── Batch E: Sleight of Hand — centre-catch duplicates the pickup ────────────
    [Fact]
    public void SleightOfHand_CentreCatch_DuplicatesPickup()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "sleight_of_hand");
        int before = g.Bonuses.Count(x => x.Type == "powerup_wide");
        var bonus = new Arkanoid.Core.Entities.Bonus { Id = 1, Type = "powerup_wide",
            Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, g.Paddle.Center.Y) }; // dead centre
        CardSystem.OnBonusCaught(g, bonus);
        Assert.Equal(before + 1, g.Bonuses.Count(x => x.Type == "powerup_wide")); // a duplicate dropped
    }

    [Fact]
    public void SleightOfHand_OffCentreCatch_NoDuplicate()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "sleight_of_hand");
        int before = g.Bonuses.Count;
        var bonus = new Arkanoid.Core.Entities.Bonus { Id = 1, Type = "powerup_wide",
            Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X + g.Paddle.Width, g.Paddle.Center.Y) }; // far off-centre
        CardSystem.OnBonusCaught(g, bonus);
        Assert.Equal(before, g.Bonuses.Count); // off-centre → no duplicate
    }

    [Fact]
    public void SleightOfHand_DuplicateDoesNotReDuplicate()
    {
        // A spawned duplicate (NoDuplicate) caught dead-centre must NOT chain into another copy.
        var g = Make(3, 2, 1, 30);
        Equip(g, "sleight_of_hand");
        int before = g.Bonuses.Count;
        var dup = new Arkanoid.Core.Entities.Bonus { Id = 1, Type = "powerup_wide", NoDuplicate = true,
            Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, g.Paddle.Center.Y) }; // dead centre
        CardSystem.OnBonusCaught(g, dup);
        Assert.Equal(before, g.Bonuses.Count); // no infinite chain
    }

    // ── Batch F: Hot Hand — ball grows at combo milestones, persists, resets on serve ──
    [Fact]
    public void HotHand_GrowsTheBallAtComboMilestones_AndPersists()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "hot_hand");
        var b = g.Balls[0];
        double r0 = b.Radius;
        g.Combo.Count = 5;  CardSystem.OnBallTick(g, b, 0.016);
        double r1 = b.Radius; Assert.True(r1 > r0, "ball should grow at the first milestone");
        g.Combo.Count = 10; CardSystem.OnBallTick(g, b, 0.016);
        double r2 = b.Radius; Assert.True(r2 > r1, "ball should grow again at the next milestone");
        g.Combo.Count = 4;  CardSystem.OnBallTick(g, b, 0.016); // combo broke
        Assert.Equal(r2, b.Radius); // size PERSISTS across the combo reset (only a fresh serve resets it)
    }

    [Fact]
    public void HotHand_GrowthIsCapped()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "hot_hand"); // L1 cap = base * 1.6
        var b = g.Balls[0];
        for (int c = 5; c <= 200; c += 5) { g.Combo.Count = c; CardSystem.OnBallTick(g, b, 0.016); }
        Assert.True(b.Radius <= g.Config.BallRadius * 1.6 + 0.001, $"radius {b.Radius} should be capped");
    }

    // ── Batch F: Redline — airborne time ramps speed + damage; paddle touch resets ──
    [Fact]
    public void Redline_RampsSpeedWhileAirborne_ResetsOnPaddleTouch()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "redline");
        var b = g.Balls[0];
        b.Vel = new Arkanoid.Core.Math.Vec2(g.Config.BallSpeed, 0); // base speed, horizontal
        for (int i = 0; i < 10; i++) CardSystem.OnBallTick(g, b, 1.0); // ~10s aloft → hits the +40% cap
        Assert.True(b.Vel.Length > g.Config.BallSpeed * 1.2, $"speed should ramp up; got {b.Vel.Length:0}");
        Assert.Equal(g.Config.BallSpeed * 1.4, b.Vel.Length, 1); // capped at +40%
        CardSystem.OnPaddleHit(g, b, isPerfect: false);
        Assert.Equal(0, b.SincePaddle); // paddle touch resets the airborne timer
    }

    [Fact]
    public void Redline_RampsDamageWhileAirborne()
    {
        var g = Make(3, 2, 1, 30);
        Equip(g, "redline");
        var b = g.Balls[0];
        Assert.Equal(0, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // fresh off the paddle → +0
        b.SincePaddle = 5.0; // aloft a while
        Assert.Equal(3, CardSystem.BallDamageBonus(g, b, BlockAt(g, 1, 0))); // +1/0.5s, capped at 2+level(1)=3
    }

    // ── Batch F: Channeling — regen pauses in flight, doubles while cradled low ──
    [Fact]
    public void Channeling_PausesInFlight_DoublesWhenLow()
    {
        var g = Make(3, 4, 1, 30);
        Equip(g, "channeling");
        var b = g.Balls[0];
        double paddleTop = g.Paddle.Center.Y - g.Paddle.Height / 2;
        b.Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, 0); // high up the board → in flight
        Assert.Equal(0.0, CardSystem.ChannelingRegenMult(g));
        b.Pos = new Arkanoid.Core.Math.Vec2(g.Paddle.Center.X, paddleTop - 5); // cradled low near the paddle
        Assert.Equal(2.0, CardSystem.ChannelingRegenMult(g));
    }

    [Fact]
    public void Channeling_Unequipped_NeutralRegen()
    {
        var g = Make(3, 4, 1, 30);
        Assert.Equal(1.0, CardSystem.ChannelingRegenMult(g)); // no card → no change to regen
    }
}

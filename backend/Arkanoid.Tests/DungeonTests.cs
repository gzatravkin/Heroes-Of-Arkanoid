using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Unit tests for the dungeon run system and ball-core mechanics.
/// All tests are pure-Core (no file I/O, no server).
/// </summary>
public class DungeonTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static DungeonDef MakeDef(int floors = 3) => new()
    {
        Id             = "test-dungeon",
        Name           = "Test Dungeon",
        Floors         = Enumerable.Range(1, floors).Select(i => $"floor-{i}").ToList(),
        RewardRelic    = "pyroclasm",
        RewardCrystals = 50,
    };

    private static DungeonDef MakeSingleFloorDef() => new()
    {
        Id             = "one-floor",
        Name           = "One Floor",
        Floors         = new List<string> { "floor-1" },
        RewardRelic    = "pyroclasm",
        RewardCrystals = 50,
    };

    private static GameInstance MakeWithBlock(int blockHp, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"test\",\"hp\":{blockHp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, cfg, seed: 1);
    }

    private static void AimAtBlock(GameInstance g)
    {
        var blk = g.Level.Blocks[0];
        var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Arkanoid.Core.Math.Vec2(
            c.X,
            c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Arkanoid.Core.Math.Vec2(0, -SimConfig.Default.BallSpeed);
    }

    // ── DungeonCatalog ────────────────────────────────────────────────────────

    [Fact]
    public void DungeonCatalog_LoadsTwoDungeons()
    {
        var catalog = DungeonCatalog.FromJson("""
        { "dungeons": [
          { "id": "ember-depths", "name": "Ember Depths", "floors": ["hell-1","hell-teleport","caverns-1"], "rewardRelic": "pyroclasm", "rewardCrystals": 50 },
          { "id": "ghost-spire",  "name": "Ghost Spire",  "floors": ["village-1","village-ghost","heaven-1"], "rewardRelic": "mana_battery", "rewardCrystals": 50 }
        ]}
        """);
        Assert.Equal(2, catalog.All.Count());
        var ed = catalog.Get("ember-depths");
        Assert.Equal("Ember Depths", ed.Name);
        Assert.Equal(3, ed.Floors.Count);
        Assert.Equal("hell-1", ed.Floors[0]);
        Assert.Equal("pyroclasm", ed.RewardRelic);
        Assert.Equal(50, ed.RewardCrystals);
    }

    [Fact]
    public void DungeonCatalog_Get_ThrowsOnUnknownId()
    {
        var catalog = DungeonCatalog.FromJson("{\"dungeons\":[]}");
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("no-such"));
    }

    // ── DungeonService.StartRun ───────────────────────────────────────────────

    [Fact]
    public void StartRun_FloorIndex0_ActiveTrue_NoChoices()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 42);
        Assert.Equal(0, run.FloorIndex);
        Assert.True(run.Active);
        Assert.False(run.Cleared);
        Assert.Empty(run.PendingChoices);
        Assert.Empty(run.Relics);
        Assert.Empty(run.BallCores);
        Assert.Equal("test-dungeon", run.DungeonId);
        Assert.Equal("floor-1", run.CurrentFloor);
    }

    [Fact]
    public void StartRun_Seed_StoredOnRun()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 999);
        Assert.Equal(999, run.Seed);
    }

    // ── DungeonService.OnFloorCleared (non-final) ─────────────────────────────

    [Fact]
    public void OnFloorCleared_NonFinal_Returns_False_And_Generates3Choices()
    {
        var run = DungeonService.StartRun(MakeDef(floors: 3), seed: 1);
        var isLast = DungeonService.OnFloorCleared(run);

        Assert.False(isLast);
        Assert.Equal(3, run.PendingChoices.Count);
        Assert.True(run.Active); // not done yet
        Assert.False(run.Cleared);
    }

    [Fact]
    public void OnFloorCleared_NonFinal_Does_NOT_Advance_FloorIndex()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        DungeonService.OnFloorCleared(run);
        // FloorIndex must not advance until PickChoice is called.
        Assert.Equal(0, run.FloorIndex);
    }

    [Fact]
    public void OnFloorCleared_Choices_Are_Distinct()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 77);
        DungeonService.OnFloorCleared(run);
        var choices = run.PendingChoices;
        Assert.Equal(choices.Distinct().Count(), choices.Count);
    }

    // ── DungeonService.PickChoice ─────────────────────────────────────────────

    [Fact]
    public void PickChoice_Relic_AddsToRelics_AdvancesFloor_ClearsChoices()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        DungeonService.OnFloorCleared(run);

        // Find a choice that is a relic
        var relicChoice = run.PendingChoices.FirstOrDefault(c =>
            new[] { "glass_cannon", "flint_core", "pyroclasm", "mana_battery" }.Contains(c));

        if (relicChoice is null)
        {
            // All choices are ball-cores — pick any and verify BallCores
            var coreChoice = run.PendingChoices[0];
            DungeonService.PickChoice(run, coreChoice);
            Assert.Contains(coreChoice, run.BallCores);
        }
        else
        {
            DungeonService.PickChoice(run, relicChoice);
            Assert.Contains(relicChoice, run.Relics);
        }

        Assert.Empty(run.PendingChoices);
        Assert.Equal(1, run.FloorIndex);
    }

    [Fact]
    public void PickChoice_BallCore_AddsToBalCores()
    {
        // Use a seed that reliably gives a ball-core by checking choices contain one
        // (we iterate seeds until we get a ball-core in the choices to keep test deterministic)
        DungeonRun? run = null;
        string? coreChoice = null;
        for (int seed = 0; seed < 200; seed++)
        {
            run = DungeonService.StartRun(MakeDef(), seed: seed);
            DungeonService.OnFloorCleared(run);
            coreChoice = run.PendingChoices.FirstOrDefault(c => new[] { "heavy", "split", "ember" }.Contains(c));
            if (coreChoice is not null) break;
        }
        Assert.NotNull(run);
        Assert.NotNull(coreChoice);
        DungeonService.PickChoice(run!, coreChoice!);
        Assert.Contains(coreChoice!, run!.BallCores);
        Assert.Empty(run.PendingChoices);
    }

    [Fact]
    public void PickChoice_InvalidId_NoOp()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        DungeonService.OnFloorCleared(run);
        var before = run.FloorIndex;
        DungeonService.PickChoice(run, "not-a-real-choice");
        Assert.Equal(before, run.FloorIndex); // no change
        Assert.Equal(3, run.PendingChoices.Count); // still 3
    }

    // ── DungeonService.OnFloorCleared (final) ────────────────────────────────

    [Fact]
    public void OnFloorCleared_Final_Sets_Cleared_And_Inactive()
    {
        var run = DungeonService.StartRun(MakeSingleFloorDef(), seed: 1);
        var isLast = DungeonService.OnFloorCleared(run);

        Assert.True(isLast);
        Assert.True(run.Cleared);
        Assert.False(run.Active);
    }

    [Fact]
    public void OnFloorCleared_Final_DoesNotGenerateChoices()
    {
        var run = DungeonService.StartRun(MakeSingleFloorDef(), seed: 1);
        DungeonService.OnFloorCleared(run);
        Assert.Empty(run.PendingChoices);
    }

    [Fact]
    public void FullRun_ThreeFloors_AdvancesCorrectly()
    {
        var run = DungeonService.StartRun(MakeDef(floors: 3), seed: 5);

        // Floor 0 cleared
        Assert.False(DungeonService.OnFloorCleared(run));
        Assert.Equal(3, run.PendingChoices.Count);
        DungeonService.PickChoice(run, run.PendingChoices[0]);
        Assert.Equal(1, run.FloorIndex);
        Assert.Equal("floor-2", run.CurrentFloor);

        // Floor 1 cleared
        Assert.False(DungeonService.OnFloorCleared(run));
        Assert.Equal(3, run.PendingChoices.Count);
        DungeonService.PickChoice(run, run.PendingChoices[0]);
        Assert.Equal(2, run.FloorIndex);
        Assert.Equal("floor-3", run.CurrentFloor);

        // Floor 2 (final) cleared
        Assert.True(DungeonService.OnFloorCleared(run));
        Assert.True(run.Cleared);
        Assert.False(run.Active);
        Assert.Null(run.CurrentFloor);
    }

    // ── DungeonService.Fail ───────────────────────────────────────────────────

    [Fact]
    public void Fail_Sets_Inactive_NotCleared()
    {
        var run = DungeonService.StartRun(MakeDef(), seed: 1);
        DungeonService.Fail(run);
        Assert.False(run.Active);
        Assert.False(run.Cleared);
    }

    // ── Determinism ──────────────────────────────────────────────────────────

    [Fact]
    public void Choices_Deterministic_SameSeed()
    {
        var run1 = DungeonService.StartRun(MakeDef(), seed: 12345);
        DungeonService.OnFloorCleared(run1);

        var run2 = DungeonService.StartRun(MakeDef(), seed: 12345);
        DungeonService.OnFloorCleared(run2);

        Assert.Equal(run1.PendingChoices, run2.PendingChoices);
    }

    [Fact]
    public void Choices_Different_DifferentSeeds()
    {
        // With different seeds the choices must differ at least sometimes across runs
        var sets = new HashSet<string>();
        for (int seed = 0; seed < 20; seed++)
        {
            var run = DungeonService.StartRun(MakeDef(), seed: seed);
            DungeonService.OnFloorCleared(run);
            sets.Add(string.Join(",", run.PendingChoices));
        }
        // At least 2 distinct orderings observed across 20 seeds
        Assert.True(sets.Count > 1, "Expected different choices for different seeds");
    }

    // ── Ball-core: heavy ─────────────────────────────────────────────────────

    [Fact]
    public void HeavyCore_AddsDamageBonus_ToBlockHit()
    {
        var g = MakeWithBlock(blockHp: 5);
        g.Serve();
        g.AddBallCore("heavy");

        var blk = g.Level.Blocks[0];
        int hpBefore = blk.Hp;
        AimAtBlock(g);
        g.Tick(SimConfig.Default.FixedDt);

        int expected = hpBefore - (SimConfig.Default.BallDamage + SimConfig.Default.HeavyBallDamageBonus);
        Assert.Equal(expected, blk.Hp);
    }

    [Fact]
    public void NoHeavyCore_BaselineDamageUnchanged()
    {
        var g = MakeWithBlock(blockHp: 5);
        g.Serve();
        // no ball core added

        var blk = g.Level.Blocks[0];
        int hpBefore = blk.Hp;
        AimAtBlock(g);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.Equal(hpBefore - SimConfig.Default.BallDamage, blk.Hp);
    }

    // ── Ball-core: split ─────────────────────────────────────────────────────

    [Fact]
    public void SplitCore_MoreThanOneBall_AfterServe()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.AddBallCore("split");
        // Phase is Serving at this point; split activates on Serve()
        g.Serve();
        Assert.True(g.Balls.Count > 1,
            $"Expected >1 ball after serve with split core, got {g.Balls.Count}");
    }

    [Fact]
    public void NoSplitCore_ExactlyOneBallAfterServe()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.Serve();
        Assert.Single(g.Balls);
    }

    // ── Ball-core: ember ─────────────────────────────────────────────────────

    [Fact]
    public void EmberCore_ServedBall_HasIgniteHitsLeft()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.AddBallCore("ember");
        g.Serve();
        Assert.True(g.Balls[0].IgniteHitsLeft > 0,
            $"Expected IgniteHitsLeft > 0 after serve with ember core, got {g.Balls[0].IgniteHitsLeft}");
    }

    [Fact]
    public void EmberCore_IgniteHitsLeft_Equals_ConfigValue()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.AddBallCore("ember");
        g.Serve();
        Assert.Equal(SimConfig.Default.EmberBallIgniteHits, g.Balls[0].IgniteHitsLeft);
    }

    [Fact]
    public void NoEmberCore_ServedBall_NoIgniteHits()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.Serve();
        Assert.Equal(0, g.Balls[0].IgniteHitsLeft);
    }

    [Fact]
    public void EmberCore_SplitExtraBalls_AlsoHaveIgniteHits()
    {
        var g = MakeWithBlock(blockHp: 1);
        g.AddBallCore("ember");
        g.AddBallCore("split");
        g.Serve();
        Assert.True(g.Balls.Count > 1);
        foreach (var b in g.Balls)
            Assert.True(b.IgniteHitsLeft > 0,
                $"Ball id={b.Id} should have ember ignite hits");
    }
}

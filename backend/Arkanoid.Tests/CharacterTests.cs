using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Tests for the character/archetype system (fire_mage, paladin, engineer, necromancer).</summary>
public class CharacterTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly string CatalogJson = """
        { "characters": [
          { "id": "fire_mage",   "name": "Fire Mage",   "passive": "Ignited kills spread fire to neighbors.", "icon": "FireHeroBall" },
          { "id": "paladin",     "name": "Paladin",     "passive": "Once per level, a lost ball is saved.",   "icon": "HPFull" },
          { "id": "engineer",    "name": "Engineer",    "passive": "Mana regenerates faster.",                "icon": "FireTurretIco" },
          { "id": "necromancer", "name": "Necromancer", "passive": "Killing blocks grants extra mana.",       "icon": "MPFull" }
        ]}
        """;

    /// <summary>Level with one block at (col=1, row=0) of the given HP, plus an adjacent block at (col=2, row=0).</summary>
    private static GameInstance MakeWithTwoBlocks(int blockHp, string character = "fire_mage", SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"test\",\"hp\":{blockHp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        // "AA." → two side-by-side blocks at (0,0) and (1,0)
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\"AA.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter(character);
        return g;
    }

    /// <summary>Level with a single block at (col=0, row=0) of the given HP.</summary>
    private static GameInstance MakeWithOneBlock(int blockHp, string character = "fire_mage", SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var catalog = BlockCatalog.FromJson(
            $"{{\"types\":[{{\"id\":\"b\",\"biome\":\"test\",\"hp\":{blockHp},\"sprite\":\"s\",\"needToKill\":true}}]}}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"test\",\"cols\":3,\"rows\":3," +
            "\"rows_data\":[\".A.\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, cfg, seed: 1);
        g.SetCharacter(character);
        return g;
    }

    /// <summary>Drain the ball to just below the drain line in one tick.</summary>
    private static void DrainBall(GameInstance g)
    {
        g.Serve();
        g.Balls[0].Pos = new Vec2(50, g.Level.Grid.Height + 999);
        g.Tick(SimConfig.Default.FixedDt);
    }

    /// <summary>Aim ball at origin block (col=0, row=0) moving straight up.</summary>
    private static void AimAtOrigin(GameInstance g)
    {
        var blk = g.Level.Blocks[0];
        var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Balls[0].Pos = new Vec2(c.X, c.Y + SimConfig.Default.CellSize / 2 + g.Balls[0].Radius + 1);
        g.Balls[0].Vel = new Vec2(0, -SimConfig.Default.BallSpeed);
    }

    // -------------------------------------------------------------------------
    // 1. CharacterCatalog
    // -------------------------------------------------------------------------

    [Fact]
    public void CharacterCatalog_LoadsFour()
    {
        var catalog = CharacterCatalog.FromJson(CatalogJson);
        var all = catalog.All.ToList();
        Assert.Equal(4, all.Count);
        Assert.Contains(all, c => c.Id == "fire_mage");
        Assert.Contains(all, c => c.Id == "paladin");
        Assert.Contains(all, c => c.Id == "engineer");
        Assert.Contains(all, c => c.Id == "necromancer");
    }

    [Fact]
    public void CharacterCatalog_Get_ReturnsCorrectDef()
    {
        var catalog = CharacterCatalog.FromJson(CatalogJson);
        var def = catalog.Get("engineer");
        Assert.Equal("Engineer", def.Name);
        Assert.Equal("FireTurretIco", def.Icon);
    }

    [Fact]
    public void CharacterCatalog_Get_ThrowsForUnknownId()
    {
        var catalog = CharacterCatalog.FromJson(CatalogJson);
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("unknown_hero"));
    }

    // -------------------------------------------------------------------------
    // 2. Profile — selection default and seeded unlocks
    // -------------------------------------------------------------------------

    [Fact]
    public void Profile_NewDefault_SelectionIsFireMage()
    {
        var p = Profile.NewDefault();
        Assert.Equal("fire_mage", p.SelectedCharacter);
    }

    [Fact]
    public void Profile_NewDefault_AllFourUnlocked()
    {
        var p = Profile.NewDefault();
        Assert.Contains("fire_mage",   p.UnlockedCharacters);
        Assert.Contains("paladin",     p.UnlockedCharacters);
        Assert.Contains("engineer",    p.UnlockedCharacters);
        Assert.Contains("necromancer", p.UnlockedCharacters);
        Assert.Equal(4, p.UnlockedCharacters.Count);
    }

    // -------------------------------------------------------------------------
    // 3. Paladin — wall save
    // -------------------------------------------------------------------------

    [Fact]
    public void Paladin_WallSave_SavesOneBallOnce()
    {
        var g = MakeWithOneBlock(blockHp: 1, character: "paladin");
        int spareBefore = g.SpareBalls; // 3

        // First drain: paladin wall-save fires — SpareBalls must NOT decrement
        DrainBall(g);
        Assert.Equal(spareBefore, g.SpareBalls);      // unchanged — free save
        Assert.Equal(GamePhase.Serving, g.Phase);

        // Second drain: wall-save is exhausted — normal reserve consumed
        DrainBall(g);
        Assert.Equal(spareBefore - 1, g.SpareBalls);  // now decrements
        Assert.Equal(GamePhase.Serving, g.Phase);
    }

    [Fact]
    public void Paladin_WallSave_OnlyOnce()
    {
        // SpareBalls starts at 3. Sequence:
        //   drain 1: wall-save fires (free, SpareBalls still 3)
        //   drain 2: SpareBalls 3→2
        //   drain 3: SpareBalls 2→1
        //   drain 4: SpareBalls 1→0
        //   drain 5: SpareBalls=0, Lost
        var g = MakeWithOneBlock(blockHp: 1, character: "paladin");
        int spare = g.SpareBalls; // 3

        DrainBall(g); // free save — wall-save used
        Assert.Equal(spare, g.SpareBalls);

        DrainBall(g); spare--; // 3→2
        DrainBall(g); spare--; // 2→1
        DrainBall(g); spare--; // 1→0
        // spare is now 0; one more drain → Lost
        Assert.Equal(0, g.SpareBalls);
        DrainBall(g); // SpareBalls=0 and wall-save already used → Lost
        Assert.Equal(GamePhase.Lost, g.Phase);
    }

    // -------------------------------------------------------------------------
    // 4. fire_mage — fire spread (regression: default character keeps spread on)
    // -------------------------------------------------------------------------

    [Fact]
    public void FireMage_Default_SpreadsFire()
    {
        // Default character is fire_mage; ignited kill must chip the neighbor.
        var g = MakeWithTwoBlocks(blockHp: 1, character: "fire_mage");
        g.Serve();
        var origin   = g.Level.Blocks[0]; // (col=0, row=0)
        var neighbor = g.Level.Blocks[1]; // (col=1, row=0)
        int hpBefore = neighbor.Hp;

        // Give the ball ignite hits and aim at origin block
        g.Balls[0].IgniteHitsLeft = 5;
        AimAtOrigin(g);
        g.Tick(SimConfig.Default.FixedDt);

        // origin should be dead and neighbor chipped by 1
        Assert.True(origin.Dead);
        Assert.True(neighbor.Hp < hpBefore, $"Expected fire spread chip; neighbor HP {neighbor.Hp} vs before {hpBefore}");
    }

    // -------------------------------------------------------------------------
    // 5. paladin — no fire spread (unless pyroclasm relic)
    // -------------------------------------------------------------------------

    [Fact]
    public void Paladin_DoesNotSpreadFire()
    {
        var g = MakeWithTwoBlocks(blockHp: 1, character: "paladin");
        g.Serve();
        var origin   = g.Level.Blocks[0];
        var neighbor = g.Level.Blocks[1];
        int hpBefore = neighbor.Hp;

        g.Balls[0].IgniteHitsLeft = 5;
        AimAtOrigin(g);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(origin.Dead);
        Assert.Equal(hpBefore, neighbor.Hp); // no spread for paladin
    }

    [Fact]
    public void Paladin_WithPyroclasm_DoesSpreadsFireAfterAll()
    {
        var g = MakeWithTwoBlocks(blockHp: 1, character: "paladin");
        g.Serve();
        g.AddRelic("pyroclasm");
        var origin   = g.Level.Blocks[0];
        var neighbor = g.Level.Blocks[1];
        int hpBefore = neighbor.Hp;

        g.Balls[0].IgniteHitsLeft = 5;
        AimAtOrigin(g);
        g.Tick(SimConfig.Default.FixedDt);

        Assert.True(origin.Dead);
        // pyroclasm overrides the character restriction — spread must happen
        Assert.True(neighbor.Hp < hpBefore, $"pyroclasm should enable spread; HP {neighbor.Hp} vs {hpBefore}");
    }

    // -------------------------------------------------------------------------
    // 6. engineer — faster mana regen
    // -------------------------------------------------------------------------

    [Fact]
    public void Engineer_RegensManaFaster()
    {
        var gBase     = MakeWithOneBlock(blockHp: 3, character: "fire_mage");
        var gEngineer = MakeWithOneBlock(blockHp: 3, character: "engineer");
        gBase.Serve();
        gEngineer.Serve();

        gBase.ManaValue     = 0;
        gEngineer.ManaValue = 0;

        const int ticks = 60; // 1 simulated second
        for (int i = 0; i < ticks; i++)
        {
            gBase.Tick(SimConfig.Default.FixedDt);
            gEngineer.Tick(SimConfig.Default.FixedDt);
        }

        Assert.True(gEngineer.ManaValue > gBase.ManaValue,
            $"engineer mana {gEngineer.ManaValue:F1} should exceed fire_mage {gBase.ManaValue:F1}");
    }

    // -------------------------------------------------------------------------
    // 7. necromancer — extra mana on kill
    // -------------------------------------------------------------------------

    [Fact]
    public void Necromancer_MoreManaOnKill()
    {
        var cfg = SimConfig.Default;

        // fire_mage baseline kill
        var gBase = MakeWithOneBlock(blockHp: 1, character: "fire_mage");
        gBase.Serve();
        gBase.ManaValue = 0;
        var blkBase = gBase.Level.Blocks[0];
        var cBase   = gBase.Level.Grid.CellCenter(blkBase.Col, blkBase.Row);
        gBase.Balls[0].Pos = new Vec2(cBase.X, cBase.Y + cfg.CellSize / 2 + gBase.Balls[0].Radius + 1);
        gBase.Balls[0].Vel = new Vec2(0, -cfg.BallSpeed);
        gBase.Tick(cfg.FixedDt);
        double baseMana = gBase.ManaValue;

        // necromancer kill
        var gNecro = MakeWithOneBlock(blockHp: 1, character: "necromancer");
        gNecro.Serve();
        gNecro.ManaValue = 0;
        var blkNecro = gNecro.Level.Blocks[0];
        var cNecro   = gNecro.Level.Grid.CellCenter(blkNecro.Col, blkNecro.Row);
        gNecro.Balls[0].Pos = new Vec2(cNecro.X, cNecro.Y + cfg.CellSize / 2 + gNecro.Balls[0].Radius + 1);
        gNecro.Balls[0].Vel = new Vec2(0, -cfg.BallSpeed);
        gNecro.Tick(cfg.FixedDt);
        double necroMana = gNecro.ManaValue;

        // necromancer should gain ManaPerKill * NecromancerKillManaMult
        double expectedNecro = cfg.ManaPerKill * cfg.NecromancerKillManaMult;
        // Allow a small delta for regen within that single tick
        double regenOneTick = cfg.ManaRegenPerSec * cfg.FixedDt;

        Assert.True(necroMana > baseMana,
            $"necromancer mana {necroMana:F2} should exceed fire_mage {baseMana:F2} after same kill");
        Assert.True(necroMana >= expectedNecro - regenOneTick,
            $"necromancer mana {necroMana:F2} should be ~{expectedNecro:F2} (ManaPerKill * NecromancerKillManaMult)");
    }
}

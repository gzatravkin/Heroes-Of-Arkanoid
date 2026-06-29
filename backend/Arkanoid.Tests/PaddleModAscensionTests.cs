using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Paddle mods (the fourth build axis — docs/04 §4.4) and rift ascension tiers
/// (docs/04 §10, answered: 5 tiers of +HP hardening with scaled rewards).
/// </summary>
public class PaddleModAscensionTests
{
    private static GameInstance Make()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":2,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":4,\"rows_data\":[\".A.\",\"A.A\",\"...\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.Serve();
        g.Balls[0].Vel = new Vec2(0, 0);
        return g;
    }

    // ── Paddle mods ────────────────────────────────────────────────────────────

    [Fact]
    public void ModWide_WidensThePaddle()
    {
        var g = Make();
        var w0 = g.Paddle.Width;
        g.AddPaddleMod("mod_wide");
        Assert.Equal(w0 * 1.2 /* PaddleModWideMult */, g.Paddle.Width, 3);
        // Idempotent: adding the same mod twice must not stack.
        g.AddPaddleMod("mod_wide");
        Assert.Equal(w0 * 1.2 /* PaddleModWideMult */, g.Paddle.Width, 3);
    }

    [Fact]
    public void ModGrip_WidensTheDeflectArc()
    {
        // Identical edge deflects with and without the grip — the modded one leans
        // harder. (The tiny test board wall-clamps the true edge, so compare, don't
        // assert absolutes.)
        double EdgeDeflectDeg(bool grip)
        {
            var g = Make();
            if (grip) g.AddPaddleMod("mod_grip");
            g.Balls[0].Pos = new Vec2(g.Paddle.Center.X + g.Paddle.Width / 2,
                g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Balls[0].Radius - 1);
            g.Balls[0].Vel = new Vec2(0, SimConfig.Default.BallSpeed);
            g.Tick(SimConfig.Default.FixedDt);
            return System.Math.Atan2(System.Math.Abs(g.Balls[0].Vel.X), -g.Balls[0].Vel.Y)
                * 180.0 / System.Math.PI;
        }

        var with    = EdgeDeflectDeg(true);
        var without = EdgeDeflectDeg(false);
        Assert.True(with > without + 1,
            $"grip should steepen the same edge deflect ({without:F1}° → {with:F1}°)");
    }

    [Fact]
    public void ModCannons_FireFromBothEdges_OnTheirInterval()
    {
        var g = Make();
        g.AddPaddleMod("mod_cannons");
        var ticks = (int)(2.5 /* PaddleModCannonInterval */ / SimConfig.Default.FixedDt) + 3;
        for (int i = 0; i < ticks; i++) g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.Projectiles.FindAll(p => p.Kind == "turret").Count >= 2,
            "both side cannons should have fired a volley");
    }

    [Fact]
    public void PaddleMods_AreAPickCategory_RoutedOnPick()
    {
        var def = new DungeonDef { Id = "d", Name = "D", Floors = new() { "a", "b" } };
        var run = DungeonService.StartRun(def, seed: 1);
        run.PendingChoices = new() { "mod_wide", "heavy", "pyroclasm" };
        DungeonService.PickChoice(run, "mod_wide");
        Assert.Contains("mod_wide", run.PaddleMods);
        Assert.Empty(run.Relics);
        Assert.Empty(run.BallCores);
    }

    // ── Ascension tiers ────────────────────────────────────────────────────────

    [Fact]
    public void ApplyTier_HardensDestructibleBlocks()
    {
        var g = Make();
        DungeonService.ApplyTier(g, 2);
        foreach (var b in g.Blocks)
        {
            Assert.Equal(4, b.Hp);    // 2 + tier 2
            Assert.Equal(4, b.MaxHp);
        }
        DungeonService.ApplyTier(g, 0); // no-op
        Assert.Equal(4, g.Blocks[0].Hp);
    }

    // ── Character unlocking (docs/04 §3: bosses earn the roster) ───────────────

    [Fact]
    public void BossClears_MakeNextHeroRollable_FreshProfilesStartWithFireMage()
    {
        // Economy rework §4: a boss clear seeds the next hero into the ROLL POOL (it stays locked until
        // its first card is rolled), rather than unlocking it directly.
        var p = Profile.NewDefault();
        Assert.Equal(new[] { "fire_mage" }, p.UnlockedCharacters);

        var cfg = new ProgressionConfig();
        var r1 = Rewards.GrantLevelCompletion(p, "hell-boss", cfg);
        Assert.Equal("paladin", r1.CharacterUnlocked);          // surfaced as "now rollable"
        Assert.Contains("paladin", p.HeroPool);                 // in the pool…
        Assert.DoesNotContain("paladin", p.UnlockedCharacters); // …but NOT yet owned (must be rolled)

        // Idempotent: a repeat clear adds nothing new.
        var r2 = Rewards.GrantLevelCompletion(p, "hell-boss", cfg);
        Assert.Null(r2.CharacterUnlocked);

        Rewards.GrantLevelCompletion(p, "caverns-boss", cfg);
        Rewards.GrantLevelCompletion(p, "village-boss", cfg);
        Assert.Contains("engineer", p.HeroPool);
        Assert.Contains("necromancer", p.HeroPool);
        // Ordinary levels add nothing.
        Assert.Null(Rewards.GrantLevelCompletion(p, "heaven-1", cfg).CharacterUnlocked);
    }

    [Fact]
    public void GeneratedRift_CarriesTier_NameSuffix_AndScaledReward()
    {
        var dungeons = DungeonCatalog.FromJson("{\"dungeons\":[]}");
        var campaign = CampaignCatalog.FromJson("""
          { "nodes": [
            { "id": "hell-1",    "label": "H1", "biome": "hell", "x": 0, "y": 0, "requires": [] },
            { "id": "hell-2",    "label": "H2", "biome": "hell", "x": 1, "y": 0, "requires": ["hell-1"] },
            { "id": "hell-boss", "label": "HB", "biome": "hell", "x": 2, "y": 0, "requires": ["hell-2"] }
          ]}
        """);
        var t0 = RiftService.GenerateRift("hell-1", seed: 5, dungeons, campaign, tier: 0)!;
        var t2 = RiftService.GenerateRift("hell-1", seed: 5, dungeons, campaign, tier: 2)!;

        Assert.Equal(0, t0.Tier);
        Assert.DoesNotContain("+", t0.Name);
        Assert.Equal(2, t2.Tier);
        Assert.EndsWith("+2", t2.Name);
        // §7: both are 10-level rifts with depth-scaled rewards (no fixed RewardCrystals); the tier is the
        // difficulty/ascension marker that travels into the run.
        Assert.True(t0.IsRift && t2.IsRift);
        Assert.Equal(10, t2.Floors.Count);

        var run = DungeonService.StartRun(t2, seed: 5);
        Assert.Equal(2, run.Tier);
        Assert.True(run.IsRift);
    }
}

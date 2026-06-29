using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// §7/§8 Rift run-modifiers (RiftModifierService). Encodes the design: a 1-of-3 offer, each modifier's
/// on-pick run effect + per-level in-game stat effect, and the depth-scaled reward curve.
/// </summary>
public class RiftModifierTests
{
    private static GameInstance Game()
    {
        var catalog = BlockCatalog.FromJson(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"hell\",\"hp\":30,\"sprite\":\"s\",\"needToKill\":true}]}");
        var level = LevelLoader.FromJson(
            "{\"id\":\"t\",\"biome\":\"hell\",\"cols\":3,\"rows\":2,\"rows_data\":[\"AAA\",\"...\"],\"legend\":{\"A\":\"b\"}}",
            catalog);
        return new GameInstance(level, SimConfig.Default, 1);
    }

    private static DungeonRun Rift(params string[] mods)
        => new() { IsRift = true, Active = true, RiftModifiers = mods.ToList(), Hp = 5, RiftMaxHp = 5 };

    // ── §8 pool + 1-of-3 offer ──────────────────────────────────────────────────
    [Fact]
    public void Pool_HasTheTenApprovedModifiers()
    {
        Assert.Equal(10, RiftModifierService.Pool.Count);
        foreach (var id in new[] { "field_medic","berserker","ironclad","keen_edge","cruelty",
                                   "twin_serve","prospector","wide_gait","snowball","cursed_bounty" })
            Assert.NotNull(RiftModifierService.Get(id));
    }

    [Fact]
    public void Offer_ReturnsThreeDistinct()
    {
        var offer = RiftModifierService.Offer(seed: 42, alreadyTaken: System.Array.Empty<string>());
        Assert.Equal(3, offer.Count);
        Assert.Equal(3, offer.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public void Offer_ExcludesNonStackableAlreadyTaken()
    {
        // field_medic / berserker / snowball are non-stackable; once taken they shouldn't be re-offered
        // (until the pool runs thin). Run several seeds to confirm exclusion holds.
        var taken = new[] { "field_medic", "snowball" };
        for (int seed = 0; seed < 30; seed++)
        {
            var offer = RiftModifierService.Offer(seed, taken);
            Assert.DoesNotContain(offer, m => m.Id == "field_medic" || m.Id == "snowball");
        }
    }

    // ── on-pick run-state effects ───────────────────────────────────────────────
    [Fact]
    public void Pick_FieldMedic_HealsToFull()
    {
        var run = new DungeonRun { IsRift = true, Hp = 2, RiftMaxHp = 8 };
        RiftModifierService.Pick(run, "field_medic", heroMaxHp: 8);
        Assert.Equal(8, run.Hp);
    }

    [Fact]
    public void Pick_Berserker_LowersMaxHp()
    {
        var run = new DungeonRun { IsRift = true, Hp = 5, RiftMaxHp = 5 };
        RiftModifierService.Pick(run, "berserker", heroMaxHp: 5);
        Assert.Equal(4, run.RiftMaxHp);
        Assert.True(run.Hp <= run.RiftMaxHp);
        Assert.Contains("berserker", run.RiftModifiers);
    }

    [Fact]
    public void Pick_Ironclad_RaisesMaxHp_AndHealsTwo()
    {
        var run = new DungeonRun { IsRift = true, Hp = 3, RiftMaxHp = 5 };
        RiftModifierService.Pick(run, "ironclad", heroMaxHp: 5);
        Assert.Equal(7, run.RiftMaxHp);
        Assert.Equal(5, run.Hp);
    }

    [Fact]
    public void Pick_Prospector_And_CursedBounty_RaiseRewardMult()
    {
        var run = new DungeonRun { IsRift = true, Hp = 5, RewardMult = 1.0 };
        RiftModifierService.Pick(run, "prospector", 5);
        Assert.Equal(1.30, run.RewardMult, 3);
        RiftModifierService.Pick(run, "cursed_bounty", 5);
        Assert.Equal(1.70, run.RewardMult, 3);
        Assert.Equal(1, run.ExtraEmitters);
    }

    // ── per-level in-game stat effects ──────────────────────────────────────────
    [Fact]
    public void ApplyToGame_Berserker_BoostsPower()
    {
        var g = Game(); g.StatPower = 10;
        RiftModifierService.ApplyToGame(Rift("berserker"), g);
        Assert.Equal(15, g.StatPower); // +50%
    }

    [Fact]
    public void ApplyToGame_KeenEdge_And_Cruelty_BoostCrit()
    {
        var g = Game(); g.SetCrit(0.10, 2.0);
        RiftModifierService.ApplyToGame(Rift("keen_edge", "cruelty"), g);
        Assert.Equal(0.25, g.CritChance, 3); // +15%
        Assert.Equal(2.5, g.CritDamage, 3);  // +50%
    }

    [Fact]
    public void ApplyToGame_TwinServe_AddsBall_WideGait_WidensPaddle()
    {
        var g = Game(); double w0 = g.Paddle.Width; int mb0 = g.StatMultiball;
        RiftModifierService.ApplyToGame(Rift("twin_serve", "wide_gait"), g);
        Assert.Equal(mb0 + 1, g.StatMultiball);
        Assert.Equal(w0 * 1.25, g.Paddle.Width, 2);
    }

    [Fact]
    public void ApplyToGame_Snowball_ScalesPowerWithDepth()
    {
        var g = Game(); g.StatPower = 10;
        var run = Rift("snowball"); run.FloorIndex = 4; // 4 levels cleared → +20%
        RiftModifierService.ApplyToGame(run, g);
        Assert.Equal(12, g.StatPower);
    }

    [Fact]
    public void ApplyToGame_CarriesTheBallPool()
    {
        var g = Game();
        var run = Rift("wide_gait"); run.SpareBalls = 7;
        RiftModifierService.ApplyToGame(run, g);
        Assert.Equal(7, g.SpareBalls);
    }

    [Fact]
    public void ApplyToGame_CursedBounty_ForcesEmitterBlocks()
    {
        var g = Game(); // 3 normal blocks
        var run = Rift("cursed_bounty"); run.ExtraEmitters = 2;
        RiftModifierService.ApplyToGame(run, g);
        Assert.Equal(2, g.Blocks.Count(b => b.ForcedEmitter)); // the downside: 2 blocks now fire hazards
        Assert.All(g.Blocks.Where(b => b.ForcedEmitter), b => Assert.True(b.Emitter)); // honoured as emitters
    }

    [Fact]
    public void Pick_SeedsRiftMaxHp_FromHeroMax_NotCurrentHp()
    {
        // First pick establishes the HP pool from the hero's TRUE max, so Field Medic heals to max even if
        // the player was already damaged when picking.
        var run = new DungeonRun { IsRift = true, Hp = 2, RiftMaxHp = 0 };
        RiftModifierService.Pick(run, "field_medic", heroMaxHp: 9);
        Assert.Equal(9, run.RiftMaxHp);
        Assert.Equal(9, run.Hp); // healed to the true max, not 2
    }

    // ── depth-scaled rewards ────────────────────────────────────────────────────
    [Fact]
    public void DepthCrystals_ScaleWithDepth_JackpotAtFullClear_TimesMult()
    {
        int d3  = RiftModifierService.DepthCrystals(3, 10, 1.0);
        int d6  = RiftModifierService.DepthCrystals(6, 10, 1.0);
        Assert.True(d6 > d3, "deeper pays more");
        int full   = RiftModifierService.DepthCrystals(10, 10, 1.0);
        int near   = RiftModifierService.DepthCrystals(9, 10, 1.0);
        Assert.True(full > near * 2 - 1, "reaching the last level is the jackpot (doubled)");
        Assert.Equal(0, RiftModifierService.DepthCrystals(0, 10, 1.0)); // bailed immediately → nothing
        // reward multiplier (Prospector/Cursed Bounty) scales the payout.
        Assert.True(RiftModifierService.DepthCrystals(5, 10, 1.7) > RiftModifierService.DepthCrystals(5, 10, 1.0));
    }

    [Fact]
    public void DepthTokens_ScaleWithDepth_AndJackpot()
    {
        Assert.Equal(0, RiftModifierService.DepthTokens(0, 10));
        Assert.True(RiftModifierService.DepthTokens(10, 10) > RiftModifierService.DepthTokens(4, 10));
    }
}

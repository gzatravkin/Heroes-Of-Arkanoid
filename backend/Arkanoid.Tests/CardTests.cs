using System.Collections.Generic;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Cards — the persistent passive layer (plan §A.1). Tests assert the run-start EFFECT and the structural
/// invariant that only EQUIPPED cards take effect (per CLAUDE.md: trigger + identity, not just "a field moved").
/// </summary>
public class CardTests
{
    private static CardCatalog Catalog() => CardCatalog.FromJson("""
    { "cards": [
      { "id": "molten_core", "name": "Molten Core", "rarity": "common", "effect": "ball_damage", "magnitude": 1 },
      { "id": "vigor",       "name": "Vigor",       "rarity": "common", "effect": "start_life",  "magnitude": 1 },
      { "id": "ember_heart", "name": "Ember Heart", "rarity": "epic",   "effect": "ball_core",   "effectValue": "ember" },
      { "id": "wide_frame",  "name": "Wide Frame",  "rarity": "rare",   "effect": "paddle_mod",  "effectValue": "mod_wide" }
    ]}
    """);

    private static GameInstance Game() => K.OneBlock(5);

    [Fact]
    public void EquippedCard_AppliesEffect_AtRunStart_ScaledByLevel()
    {
        var cat = Catalog();
        var g = Game();
        var p = new Profile
        {
            OwnedCards = new() { ["molten_core"] = new CardOwn { Level = 3 } },
            EquippedCards = new() { "molten_core" },
        };
        int before = g.ItemBallDamageBonus;
        CardEffects.Apply(p, cat, g);
        Assert.Equal(before + 3, g.ItemBallDamageBonus); // +1 dmg/level × level 3
    }

    [Fact]
    public void UnequippedCard_HasNoEffect()
    {
        // Owned but NOT equipped → must not apply. This is the load-bearing structural invariant.
        var cat = Catalog();
        var g = Game();
        var p = new Profile
        {
            OwnedCards = new() { ["molten_core"] = new CardOwn { Level = 5 } },
            EquippedCards = new(), // nothing equipped
        };
        int before = g.ItemBallDamageBonus;
        CardEffects.Apply(p, cat, g);
        Assert.Equal(before, g.ItemBallDamageBonus);
    }

    [Fact]
    public void Vigor_RaisesStartingHp()
    {
        var cat = Catalog();
        var g = Game();
        var p = new Profile
        {
            OwnedCards = new() { ["vigor"] = new CardOwn { Level = 2 } },
            EquippedCards = new() { "vigor" },
        };
        int before = g.Hp;
        CardEffects.Apply(p, cat, g);
        Assert.Equal(before + 2, g.Hp);
    }

    [Fact]
    public void EmberHeartCard_ServesIgnitedBall()
    {
        var cat = Catalog();
        var g = Game();
        var p = new Profile
        {
            OwnedCards = new() { ["ember_heart"] = new CardOwn { Level = 1 } },
            EquippedCards = new() { "ember_heart" },
        };
        CardEffects.Apply(p, cat, g);
        g.Serve();
        Assert.True(g.Balls[0].IgniteHitsLeft > 0, "ember_heart should serve an ignited ball");
    }

    [Fact]
    public void WideFrameCard_WidensPaddle()
    {
        var cat = Catalog();
        var g = Game();
        var p = new Profile
        {
            OwnedCards = new() { ["wide_frame"] = new CardOwn { Level = 1 } },
            EquippedCards = new() { "wide_frame" },
        };
        double before = g.Paddle.Width;
        CardEffects.Apply(p, cat, g);
        Assert.True(g.Paddle.Width > before, "wide_frame should widen the paddle");
    }

    // ── Service: equip / level ──────────────────────────────────────────────

    [Fact]
    public void Equip_RespectsOwnership_And_FifoReplacesOldestWhenFull()
    {
        // Owner direction 2026-06-15: equipping when full drops the OLDEST selection (FIFO), never blocks.
        var p = new Profile { CardSlots = 2, OwnedCards = new() { ["a"] = new(), ["b"] = new(), ["c"] = new() } };
        Assert.False(CardService.Equip(p, "x"));        // unowned → rejected
        Assert.True(CardService.Equip(p, "a"));
        Assert.True(CardService.Equip(p, "b"));
        Assert.False(CardService.Equip(p, "a"));        // already equipped → no-op
        Assert.True(CardService.Equip(p, "c"));         // full → drops oldest (a), adds c
        Assert.Equal(new[] { "b", "c" }, p.EquippedCards.ToArray());
    }

    [Fact]
    public void Grant_BanksCopies_AndManualLevelUp_SpendsThreshold()
    {
        // Owner direction 2026-06-15: duplicates BANK copies; the player levels up manually for 2L+1 copies.
        var p = new Profile();
        CardService.Grant(p, "a");                      // first copy → owned L1, 0 banked
        Assert.Equal(1, p.OwnedCards["a"].Level);
        Assert.Equal(0, p.OwnedCards["a"].Copies);

        Assert.False(CardService.TryLevelUp(p, "a"));    // L1→L2 needs 3 copies
        CardService.Grant(p, "a");
        CardService.Grant(p, "a");
        CardService.Grant(p, "a");
        Assert.Equal(3, p.OwnedCards["a"].Copies);
        Assert.True(CardService.TryLevelUp(p, "a"));      // spend 3 → L2
        Assert.Equal(2, p.OwnedCards["a"].Level);
        Assert.Equal(0, p.OwnedCards["a"].Copies);
    }
}

using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Net;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Gold — in-run spending currency (docs/04 §5 "Gold / Treasure (in-run) — drops from blocks/chests;
/// spent at shops"). Distinct from Crystals (the meta-progression drip awarded to the Profile at level
/// clear). The "coins" treasure pickup feeds Gold, NOT Crystals.
/// </summary>
public class GoldTests
{
    private static BonusCatalog MakeCatalog() => BonusCatalog.FromJson("""
    { "bonuses": [
      { "id": "coins", "name": "Treasure", "icon": "ui/bonus/BonusGem", "effect": "coins" }
    ]}
    """);

    private static GameInstance MakeGame(SimConfig? cfg = null)
    {
        cfg ??= new SimConfig { Pickups = new() { DropChance = 1.0, FallSpeed = 0 } };
        var catalog = BlockCatalog.FromJson("""
          {"types":[{"id":"b","biome":"hell","hp":1,"sprite":"HellStandart","needToKill":true}]}
        """);
        var level = LevelLoader.FromJson("""
          {"id":"t","biome":"hell","cols":6,"rows":3,"rows_data":["BBBBBB","......","......"],"legend":{"B":"b"}}
        """, catalog, cfg);
        return new GameInstance(level, cfg, seed: 42, bonuses: MakeCatalog());
    }

    // docs/04 §5: Gold/Treasure drops from blocks (the coins pickup) — and it must feed GOLD,
    // leaving the Crystals meta-stream untouched. This is the whole point of the reconciliation:
    // two distinct currencies, not one renamed.
    [Fact]
    public void CoinsPickup_Awards_Gold_NotCrystals()
    {
        var cfg = new SimConfig { Pickups = new() { DropChance = 1.0, FallSpeed = 0, CoinsGold = 5 } };
        var g   = MakeGame(cfg);
        g.Serve();

        int goldBefore     = g.Gold;
        int crystalsBefore = g.Crystals; // whatever the kill/combo path produced

        g.Bonuses.Add(new Arkanoid.Core.Entities.Bonus
        {
            Id    = 1,
            Pos   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y),
            Vel   = new Vec2(0, 0),
            Type  = "coins",
            Icon  = "ui/bonus/BonusGem",
            Alive = true,
        });

        g.Tick(cfg.FixedDt);

        Assert.Empty(g.Bonuses);                          // caught
        Assert.Equal(goldBefore + 5, g.Gold);             // Gold went up by CoinsGold
        Assert.Equal(crystalsBefore, g.Crystals);         // Crystals stream untouched
    }

    [Fact]
    public void Gold_ExposedInSnapshot()
    {
        var g = MakeGame();
        g.SetGold(7);
        var snap = Snapshot.From(g, 0);
        Assert.Equal(7, snap.Gold);
    }

    [Fact]
    public void SetGold_ClampsToZero_OnNegative()
    {
        var g = MakeGame();
        g.SetGold(-3);
        Assert.Equal(0, g.Gold);
    }

    [Fact]
    public void SetGold_RestoresCarriedGold()
    {
        var g = MakeGame();
        g.SetGold(12);
        Assert.Equal(12, g.Gold); // cross-floor carry (docs/04 §5) re-applies the run's Gold
    }
}

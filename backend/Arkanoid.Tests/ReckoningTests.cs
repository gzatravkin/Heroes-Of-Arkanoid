using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Sim.Systems;
using Xunit;

/// <summary>Reckoning (design §3, NEW Paladin): a meter charged by HP LOST that auto-smites the board
/// at its threshold. Asserts the trigger (armed + enough HP lost) + identity (a board-wide smite that
/// drains the meter), the negative cases, and §6 scaling (lower threshold per level).</summary>
public class ReckoningTests
{
    // A wide board so a board-wide smite has blocks in several columns.
    private static GameInstance WideBoard() => K.Game(
        "{\"id\":\"b\",\"biome\":\"t\",\"hp\":20,\"sprite\":\"s\",\"needToKill\":true}",
        "{\"id\":\"t\",\"biome\":\"t\",\"cols\":9,\"rows\":2,\"rows_data\":[\"BBBBBBBBB\",\".........\"],\"legend\":{\"B\":\"b\"}}");

    [Fact]
    public void Reckoning_SmitesBoard_WhenMeterFills()
    {
        var g = WideBoard();
        g._reckoningArmed = true;
        int hp0 = g.Blocks.Sum(b => b.Hp);
        ReckoningSystem.OnHpLost(g, 3); // threshold 3 → smite
        Assert.True(g.Blocks.Sum(b => b.Hp) < hp0, "the board was smitten");
        Assert.Equal(0, g._reckoningMeter); // meter drained
    }

    [Fact]
    public void Reckoning_BelowThreshold_ChargesButDoesNotFire()
    {
        var g = WideBoard();
        g._reckoningArmed = true;
        int hp0 = g.Blocks.Sum(b => b.Hp);
        ReckoningSystem.OnHpLost(g, 2); // < threshold 3
        Assert.Equal(hp0, g.Blocks.Sum(b => b.Hp)); // no smite yet
        Assert.Equal(2, g._reckoningMeter);         // but charged
    }

    [Fact]
    public void Reckoning_Unarmed_DoesNothing()
    {
        var g = WideBoard(); // never cast/armed
        int hp0 = g.Blocks.Sum(b => b.Hp);
        ReckoningSystem.OnHpLost(g, 5);
        Assert.Equal(hp0, g.Blocks.Sum(b => b.Hp));
        Assert.Equal(0, g._reckoningMeter);
    }

    [Fact]
    public void Reckoning_Cast_ArmsTheMeter()
    {
        var g = WideBoard();
        g.SetCharacter("paladin");
        g.SetLoadout(new[] { "reckoning" });
        g.Serve();
        g.ManaValue = 100;
        g.CastSlot(0); // Reckoning
        Assert.True(g._reckoningArmed);
    }

    [Fact]
    public void Reckoning_Smite_HitsMultipleColumns()
    {
        var g = WideBoard();
        g._reckoningArmed = true;
        ReckoningSystem.OnHpLost(g, 3); // one smite
        int damagedCols = g.Blocks.Where(b => b.Hp < 20).Select(b => b.Col).Distinct().Count();
        Assert.True(damagedCols >= 2, $"a board-wide smite must hit multiple columns (hit {damagedCols})");
    }

    [Fact]
    public void Reckoning_SmiteDamage_ScalesWithLevel()
    {
        int DeltaAtLevel(int lvl)
        {
            var g = WideBoard();
            if (lvl > 1) g.SetSpellLevels(new Dictionary<string, int> { ["reckoning"] = lvl });
            g._reckoningArmed = true;
            var col0 = g.Blocks.First(b => b.Col == 0);
            int hp0 = col0.Hp;
            ReckoningSystem.OnHpLost(g, System.Math.Max(1, 3 - (lvl - 1))); // exactly one threshold → one smite
            return hp0 - col0.Hp;
        }
        Assert.Equal(3, DeltaAtLevel(1)); // base smite damage
        Assert.Equal(5, DeltaAtLevel(3)); // 3 + 2×1 (§6 rising smite damage)
    }

    [Fact]
    public void Reckoning_LevelLowersThreshold_FiresSooner()
    {
        // §6: threshold drops 1 per level. At Lvl 2 the threshold is 2, so 2 HP lost already smites.
        var g = WideBoard();
        g.SetSpellLevels(new Dictionary<string, int> { ["reckoning"] = 2 });
        g._reckoningArmed = true;
        int hp0 = g.Blocks.Sum(b => b.Hp);
        ReckoningSystem.OnHpLost(g, 2);
        Assert.True(g.Blocks.Sum(b => b.Hp) < hp0, "Lvl 2 fires at 2 HP lost");
    }
}

using System.Linq;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Reckoning (design §3, NEW Paladin spell) — a meter charged by the HP you LOSE. Casting it
/// arms the meter for the level; thereafter every point of HP an enemy takes from you fills it, and at
/// the threshold it auto-smites the board (judgment pillars across several columns) and drains. Fits the
/// Paladin bruiser fantasy: the more punishment you take, the harder the board pays. §6 (Charge/meter):
/// leveling LOWERS the threshold (fires sooner) and RAISES the smite damage.</summary>
internal static class ReckoningSystem
{
    private const int SmiteColumns = 5;

    /// <summary>Cast: arm the meter for this level. No-op (no mana spent) if already armed.</summary>
    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (g._reckoningArmed) return; // already armed for the level — don't re-spend
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        g._reckoningArmed = true;
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "reckoning", "armed");
    }

    /// <summary>Current meter fill as a 0..1 fraction (for the HUD), or 0 when unarmed.</summary>
    internal static double Charge(GameInstance g)
    {
        if (!g._reckoningArmed) return 0;
        var def = g.GetSpellDef("reckoning");
        int threshold = System.Math.Max(1, (def?.Hits ?? 3) - (g.SpellLevel("reckoning") - 1));
        return System.Math.Clamp((double)g._reckoningMeter / threshold, 0, 1);
    }

    /// <summary>Charge the meter from HP lost (called wherever the player takes damage); smite at threshold.</summary>
    internal static void OnHpLost(GameInstance g, int amount)
    {
        if (!g._reckoningArmed || amount <= 0) return;
        g._reckoningMeter += amount;
        var def = g.GetSpellDef("reckoning");
        int lvl = g.SpellLevel("reckoning");
        int threshold = System.Math.Max(1, (def?.Hits ?? 3) - (lvl - 1)); // −1 HP/level, min 1 (§6 fires sooner)
        while (g._reckoningMeter >= threshold)
        {
            g._reckoningMeter -= threshold;
            Smite(g, def, lvl);
        }
    }

    /// <summary>Board-wide judgment: smite several evenly-spaced columns, damaging the blocks in each.</summary>
    private static void Smite(GameInstance g, SpellDef? def, int lvl)
    {
        int dmg  = (def?.Damage ?? 3) + (lvl - 1) * (def?.DamagePerLevel ?? 1); // §6 rising smite damage
        int cols = g.Level.Grid.Cols;
        int n    = System.Math.Min(cols, SmiteColumns);
        int smitten = 0;
        for (int i = 0; i < n; i++)
        {
            int col = (n <= 1 || cols <= 1) ? 0 : (int)System.Math.Round((double)i * (cols - 1) / (n - 1));
            foreach (var blk in g.Blocks.Where(x => !x.Dead && !x.Boss && x.Col == col).ToList())
            {
                BlockDamage.DamageBlock(g, blk, dmg, igniteSource: false, killMult: 0.5);
                smitten++;
            }
            var colX = g.Level.Grid.CellCenter(col, 0).X;
            g.RaiseEvent(SimEventKind.Judgement, colX, g.Level.Grid.Height);
        }
        g._log.Log(g.TickCount, "spell", "reckoning smite", $"cols={n} dmgEach={dmg} blocks={smitten}");
    }
}

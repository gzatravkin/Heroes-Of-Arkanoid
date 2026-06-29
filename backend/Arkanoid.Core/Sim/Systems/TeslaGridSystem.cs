using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Tesla Grid (design §3, NEW Engineer spell) — cast to arm it for the level; thereafter each
/// SIDE-WALL bounce charges that wall, and when BOTH walls are charged a horizontal lightning curtain
/// fires across the board (a band of rows around the ball), then both walls reset. Rewards wide,
/// wall-to-wall play — the Engineer's signature swarm/control fantasy.</summary>
internal static class TeslaGridSystem
{
    private const int    BandHalfRows = 2;   // curtain covers the front row + 2 above (wider band — balance 2026-06-16)
    private const double CurtainCooldown = 0.5; // min seconds between curtains (anti-spam)

    /// <summary>Tick the curtain cooldown down (called each tick).</summary>
    internal static void Update(GameInstance g, double dt)
    {
        if (g._teslaCooldown > 0) g._teslaCooldown = System.Math.Max(0, g._teslaCooldown - dt);
    }

    /// <summary>Cast: arm the grid for this level. No-op (no mana spent) if already armed.</summary>
    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (g._teslaArmed) return;
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        g._teslaArmed = true;
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "tesla", "armed");
    }

    /// <summary>A side-wall bounce charges that wall; when both are charged, the curtain fires.</summary>
    internal static void OnWallBounce(GameInstance g, Ball b, bool left)
    {
        if (!g._teslaArmed) return;
        if (left) g._teslaLeftCharged = true; else g._teslaRightCharged = true;
        if (g._teslaLeftCharged && g._teslaRightCharged && g._teslaCooldown <= 0)
        {
            g._teslaLeftCharged = false;
            g._teslaRightCharged = false;
            g._teslaCooldown = CurtainCooldown;
            FireCurtain(g, b);
        }
    }

    /// <summary>Horizontal lightning curtain: a full-width sheet that fries the FRONTMOST band of live
    /// blocks (the wall closest to the paddle) — reliably eats the front line, not empty air.</summary>
    private static void FireCurtain(GameInstance g, Ball b)
    {
        var def = g.GetSpellDef("tesla");
        int dmg = (def?.Damage ?? 3) + (g.SpellLevel("tesla") - 1) * (def?.DamagePerLevel ?? 1);
        // Front = the frontmost DESTRUCTIBLE row (skip indestructible frame/decor the curtain can't hurt).
        var live = g.Blocks.Where(x => !x.Dead && !x.Boss && !x.Indestructible).ToList();
        int frontRow = live.Count > 0 ? live.Max(x => x.Row)
            : (int)System.Math.Clamp(b.Pos.Y / g.Config.CellSize, 0, g.Level.Grid.Rows - 1);
        int hit = 0;
        foreach (var blk in live.Where(x => frontRow - x.Row <= BandHalfRows && x.Row <= frontRow).ToList())
        {
            BlockDamage.DamageBlock(g, blk, dmg, igniteSource: false, killMult: 0.5);
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent(SimEventKind.Lightning, c.X, c.Y);
            hit++;
        }
        // Full-width arc FX so the curtain reads as a continuous sheet across the board.
        double y = (frontRow + 0.5) * g.Config.CellSize;
        g.RaiseEvent(SimEventKind.Lightning, 0, y);
        g.RaiseEvent(SimEventKind.Lightning, g.Level.Grid.Width, y);
        g._log.Log(g.TickCount, "spell", "tesla curtain", $"frontRow={frontRow} dmgEach={dmg} blocks={hit}");
    }
}

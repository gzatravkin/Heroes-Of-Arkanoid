using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Ashfall (design §3, NEW Fire Mage spell) — a timed buff: while active, every IGNITE-KILL
/// (a burning block destroyed, by any source) rains a vertical ember straight down its column, damaging
/// the blocks below. Pairs with Ignite / Fire Wall / Conflagration — the more of the board you set
/// alight, the more embers rain when those blocks fall.</summary>
internal static class AshfallSystem
{
    private const double EmberSpeed  = 420.0;
    private const int    EmberDamage = 2;
    private const int    EmberPierce = 3; // rains through up to 3 blocks down the column

    /// <summary>Cast: arm Ashfall for its duration (scales with spell level, §6 timed-aura).</summary>
    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        g._ashfallTimer = def.Duration + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel;
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, 0);
        g._log.Log(g.TickCount, "spell", "ashfall", $"armed seconds={g._ashfallTimer:0.0}");
    }

    /// <summary>Tick the buff timer down.</summary>
    internal static void Update(GameInstance g, double dt)
    {
        if (g._ashfallTimer > 0) g._ashfallTimer = System.Math.Max(0, g._ashfallTimer - dt);
    }

    /// <summary>An ignite-kill rains an ember down the dead block's column (called from BlockDamage).</summary>
    internal static void RainEmber(GameInstance g, Block origin)
    {
        var c = g.Level.Grid.CellCenter(origin.Col, origin.Row);
        g.Projectiles.Add(new Projectile
        {
            Id     = g._nextProjId++,
            Pos    = new Vec2(c.X, c.Y),
            Vel    = new Vec2(0, EmberSpeed), // straight down the column
            Damage = EmberDamage,
            Radius = g.Config.BallRadius * 0.5,
            Kind   = "ember",
            // Pierces several blocks so it genuinely rains down the COLUMN (§3 "blocks below"), not one hit.
            PiercingHitsLeft = EmberPierce,
        });
    }
}

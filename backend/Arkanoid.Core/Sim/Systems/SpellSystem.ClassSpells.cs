using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>Per-tick updates for Paladin barriers, Engineer zones, and Necromancer drain.</summary>
internal static partial class SpellSystem
{
    internal static void UpdateBarriers(GameInstance g, double dt)
    {
        foreach (var b in g.Barriers)
        {
            if (!b.Alive) continue;
            b.LifeRemaining -= dt;
            if (b.LifeRemaining <= 0) { b.Alive = false; continue; }

            // LEGACY Paladin Shield (reverted 2026-06-16, "behave as before"): the barrier REFLECTS enemy
            // bullets that cross it back UP as the player's own projectiles (which then damage blocks) — an
            // active anti-projectile defense, not a pit-save bounce.
            double halfW = b.Width / 2.0;
            foreach (var hz in g.Hazards)
            {
                if (!hz.Alive || hz.Behavior != HazardBehavior.None || hz.Damage <= 0) continue; // only plain enemy fire
                if (hz.Vel.Y <= 0) continue;                                                     // only descending shots
                if (hz.Pos.X < b.CenterX - halfW || hz.Pos.X > b.CenterX + halfW) continue;
                if (hz.Pos.Y + hz.Radius < b.Y || hz.Pos.Y - hz.Radius > b.Y + 6) continue;       // crossing the barrier line
                hz.Alive = false;
                g.Projectiles.Add(new Projectile
                {
                    Id     = g._nextProjId++,
                    Pos    = new Vec2(hz.Pos.X, b.Y),
                    Vel    = new Vec2(0, -System.Math.Max(420.0, System.Math.Abs(hz.Vel.Y))),
                    Damage = System.Math.Max(2, hz.Damage),
                    Radius = hz.Radius,
                    Kind   = "shieldbolt",
                });
                g.RaiseEvent(SimEventKind.ShieldBlock, hz.Pos.X, b.Y);
            }
        }
        g.Barriers.RemoveAll(b => !b.Alive);
    }

    internal static void UpdateZones(GameInstance g, double dt)
    {
        foreach (var zone in g.Zones)
        {
            if (!zone.Alive) continue;
            zone.Accumulator   += dt;
            zone.LifeRemaining -= dt;
            while (zone.Accumulator >= zone.DamageInterval)
            {
                foreach (var blk in g.Blocks)
                {
                    if (blk.Dead) continue;
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    if ((c - new Vec2(zone.X, zone.Y)).Length <= zone.Radius)
                    {
                        BlockDamage.DamageBlock(g, blk, zone.DamagePerTick, igniteSource: false, killMult: 0.5);
                        g.RaiseEvent(SimEventKind.Radiation, c.X, c.Y);
                    }
                }
                zone.Accumulator -= zone.DamageInterval;
            }
            if (zone.LifeRemaining <= 0) zone.Alive = false;
        }
        g.Zones.RemoveAll(z => !z.Alive);
    }

    /// <summary>Extra mana earned per kill when Drain is active (capped at 40 total per cast).
    /// killMult applies the same source discount (0.5 spell, 0.25 chain) to the bonus.</summary>
    internal static double DrainBonusMana(GameInstance g, double killMult = 1.0)
    {
        if (!g.SpellDrainActive || g._drainBonusLeft <= 0) return 0.0;
        double perKill = (g.GetSpellDef("drain")?.BonusManaPerKill ?? 4.0) * killMult;
        double grant = System.Math.Min(perKill, g._drainBonusLeft);
        g._drainBonusLeft -= grant;
        return grant;
    }
}

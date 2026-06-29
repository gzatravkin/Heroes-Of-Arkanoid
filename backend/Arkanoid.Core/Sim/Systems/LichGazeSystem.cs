using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Lich's Gaze (design §3 rework of Skeletal Mage) — NOT a projectile fan. It plants a slow
/// lighthouse beam at the paddle that sweeps an upward arc, CURSING every block its ray crosses; cursed
/// blocks take bonus damage from the ball (a damage-amplifier you paint onto the board). Leveling extends
/// the sweep duration (§6 timed) and raises the curse bonus.</summary>
internal static class LichGazeSystem
{
    private const double SweepHalfArc = System.Math.PI * 0.42; // ~75° each side of straight-up

    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        int lvl = g.SpellLevel(def.Id);
        double life = def.Duration + (lvl - 1) * def.DurationPerLevel;
        g.LichCurseBonus = def.Damage + (lvl - 1) * def.DamagePerLevel; // §6 rising curse bonus
        double up = -System.Math.PI / 2;
        g.LichBeam = new LichBeam
        {
            Id          = g._nextBeamId++,
            OriginX     = g.Paddle.Center.X,
            OriginY     = g.Paddle.Center.Y - g.Paddle.Height / 2,
            Length      = g.Level.Grid.Height,
            LifeRemaining = life,
            TotalLife   = life,
            StartAngle  = up - SweepHalfArc,
            EndAngle    = up + SweepHalfArc,
            Angle       = up - SweepHalfArc,
        };
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "spell", "lichgaze", $"armed life={life:0.0} curseBonus={g.LichCurseBonus}");
    }

    /// <summary>Sweep the beam each tick and curse the blocks its ray crosses.</summary>
    internal static void Update(GameInstance g, double dt)
    {
        var beam = g.LichBeam;
        if (beam == null) return;
        beam.LifeRemaining -= dt;
        if (beam.LifeRemaining <= 0) { g.LichBeam = null; return; }

        // Re-anchor to the paddle and advance the sweep angle by progress.
        beam.OriginX = g.Paddle.Center.X;
        beam.OriginY = g.Paddle.Center.Y - g.Paddle.Height / 2;
        double t = 1.0 - System.Math.Clamp(beam.LifeRemaining / beam.TotalLife, 0, 1);
        beam.Angle = beam.StartAngle + (beam.EndAngle - beam.StartAngle) * t;

        // Raycast: step along the ray, curse the block in each cell it passes through.
        var dir = new Vec2(System.Math.Cos(beam.Angle), System.Math.Sin(beam.Angle));
        double step = g.Config.CellSize * 0.4;
        bool newlyCursed = false;
        for (double d = 0; d <= beam.Length; d += step)
        {
            var p = new Vec2(beam.OriginX + dir.X * d, beam.OriginY + dir.Y * d);
            if (p.Y < g.Level.Grid.OriginY) break;
            int col = (int)((p.X - g.Level.Grid.OriginX) / g.Config.CellSize);
            int row = (int)((p.Y - g.Level.Grid.OriginY) / g.Config.CellSize);
            var blk = g.BlockAt(col, row);
            if (blk != null && !blk.Dead && !blk.Indestructible && !blk.Boss && !blk.Cursed)
            {
                blk.Cursed = true;
                newlyCursed = true;
            }
        }
        if (newlyCursed) g.MarkBlocksDirty(); // so the snapshot resends with the curse tint
    }
}

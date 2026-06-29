using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>Bonewalker (design §3 rework of "Skeleton") — NOT a paddle turret that sprays bullets on a
/// timer. It's a summoned minion that <b>walks the block rooftops</b>, meleeing whatever block it is
/// standing on. It strides horizontally across the top of the field, sinks onto the tallest block in its
/// current column, and chips that block on a melee cadence. §6 timed-aura: leveling extends how long it
/// walks (so it crosses more rooftops).</summary>
internal static class BonewalkerSystem
{
    private const double WalkSpeed    = 60.0; // px/s horizontal stride (~2 cells/s)
    private const double StepInterval = 0.5;  // seconds between melee swings (~one swing per cell crossed)
    private const int    MeleeDamage  = 3;    // damage per swing to the block underfoot

    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        double cell = g.Config.CellSize;
        double life = def.Duration + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel;
        double x    = g.Paddle.Center.X;
        var m = new Minion
        {
            Id            = g._nextMinionId++,
            Kind          = "bonewalker",
            X             = x,
            Width         = cell * 0.55,
            Height        = cell * 0.85,
            LifeRemaining = life,
            MaxLife       = life,
            // Stride toward the side with more field to cover (start away from the nearer wall).
            Dir           = x < g.Level.Grid.Width / 2 ? 1 : -1,
        };
        m.Y = RoofY(g, x, m.Height); // perch on the rooftops at the spawn column
        g.Minions.Add(m);
        g.RaiseEvent(SimEventKind.SpellCast, x, m.Y);
        g._log.Log(g.TickCount, "spell", "bonewalker", $"summon x={x:0} life={life:0.0} dir={m.Dir}");
    }

    internal static void Update(GameInstance g, double dt)
    {
        if (g.Minions.Count == 0) return;
        double cell = g.Config.CellSize;
        double minX = cell, maxX = g.Level.Grid.Width - cell;

        foreach (var m in g.Minions)
        {
            if (!m.Alive || m.Kind != "bonewalker") continue;

            // Stride horizontally, turning at the arena edges.
            m.X += WalkSpeed * m.Dir * dt;
            if (m.X <= minX) { m.X = minX; m.Dir = 1; }
            else if (m.X >= maxX) { m.X = maxX; m.Dir = -1; }

            // Settle onto the rooftop of whatever column it is over.
            var under = TopBlockUnder(g, m.X);
            m.Y = under != null
                ? g.Level.Grid.CellCenter(under.Col, under.Row).Y - cell / 2 - m.Height / 2
                : RoofY(g, m.X, m.Height); // open gap → keep striding at roof height

            // Melee the block it stands on, on a cadence.
            m.StepAccum += dt;
            while (m.StepAccum >= StepInterval)
            {
                m.StepAccum -= StepInterval;
                var target = TopBlockUnder(g, m.X);
                if (target != null)
                {
                    BlockDamage.DamageBlock(g, target, MeleeDamage, igniteSource: false, killMult: 0.5);
                    g.RaiseEvent(SimEventKind.SkeletonShot, m.X, m.Y);
                }
            }

            if ((m.LifeRemaining -= dt) <= 0) m.Alive = false;
        }
        g.Minions.RemoveAll(m => !m.Alive);
    }

    /// <summary>The topmost (lowest-row) live destructible block in the column under world-X <paramref name="x"/>.</summary>
    private static Block? TopBlockUnder(GameInstance g, double x)
    {
        int col = (int)System.Math.Floor((x - g.Level.Grid.OriginX) / g.Config.CellSize);
        Block? best = null;
        foreach (var b in g.Blocks)
        {
            if (b.Dead || b.Col != col || b.Indestructible) continue;
            if (best == null || b.Row < best.Row) best = b;
        }
        return best;
    }

    /// <summary>Y for a walker perched on the column's rooftop at <paramref name="x"/> (top of board if empty).</summary>
    private static double RoofY(GameInstance g, double x, double height)
    {
        var top = TopBlockUnder(g, x);
        double cell = g.Config.CellSize;
        double roof = top != null
            ? g.Level.Grid.CellCenter(top.Col, top.Row).Y - cell / 2
            : g.Level.Grid.OriginY; // empty column → top of the play field
        return roof - height / 2;
    }
}

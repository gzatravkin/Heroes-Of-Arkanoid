using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>Bone Golem (design §3 fix) — NOT a fat piercing projectile. It's a summoned <b>bodyguard</b>
/// that rises from the paddle and <b>climbs a single column</b>, bulldozing every block in its path, while
/// <b>tanking enemy fire</b>: any hazard that would reach the paddle through the golem's body is soaked
/// instead (it has its own HP pool). It dies when soaked through or when it climbs off the top. §6: leveling
/// adds fire-soak HP (it bodyguards longer).</summary>
internal static class BoneGolemSystem
{
    private const double ClimbSpeed     = 70.0; // px/s upward
    private const int    BaseHp         = 8;    // fire-soak pool
    private const int    HpPerLevel     = 2;
    private const int    BulldozeDamage = 4;    // per bulldoze tick to the block in its path
    private const double BulldozeStep   = 0.15; // cadence so a block dies in 1–2 hits as the golem reaches it

    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        double cell = g.Config.CellSize;
        int col     = (int)System.Math.Floor((g.Paddle.Center.X - g.Level.Grid.OriginX) / cell);
        col         = System.Math.Clamp(col, 0, g.Level.Grid.Cols - 1);
        int hp      = BaseHp + (g.SpellLevel(def.Id) - 1) * HpPerLevel;
        var m = new Minion
        {
            Id     = g._nextMinionId++,
            Kind   = "golem",
            X      = g.Level.Grid.CellCenter(col, 0).X, // snap to the column so it bulldozes cleanly
            Y      = g.Paddle.Center.Y - g.Paddle.Height / 2 - cell, // rises just above the paddle
            Width  = cell * 0.9,
            Height = cell * 1.4,
            Hp     = hp,
            MaxHp  = hp,
        };
        g.Minions.Add(m);
        g.RaiseEvent(SimEventKind.SpellCast, m.X, m.Y);
        g._log.Log(g.TickCount, "spell", "golem", $"summon col={col} hp={hp}");
    }

    internal static void Update(GameInstance g, double dt)
    {
        if (g.Minions.Count == 0) return;
        double cell = g.Config.CellSize;

        foreach (var m in g.Minions)
        {
            if (!m.Alive || m.Kind != "golem") continue;

            // Climb the column.
            m.Y -= ClimbSpeed * dt;

            // Bulldoze the block its head is entering (column blocks fall to the bodyguard).
            m.StepAccum += dt;
            if (m.StepAccum >= BulldozeStep)
            {
                m.StepAccum -= BulldozeStep;
                // 3-wide bulldoze (balance 2026-06-16): the golem shoulders through the head cell AND its two
                // horizontal neighbours, clearing a swath rather than a single column.
                double headY = m.Y - m.Height / 2;
                int hcol = (int)System.Math.Floor((m.X - g.Level.Grid.OriginX) / cell);
                int hrow = (int)System.Math.Floor((headY - g.Level.Grid.OriginY) / cell);
                bool hitAny = false;
                for (int dc = -1; dc <= 1; dc++)
                {
                    var nb = g.BlockAt(hcol + dc, hrow);
                    if (nb != null && !nb.Dead && !nb.Indestructible)
                    {
                        BlockDamage.DamageBlock(g, nb, BulldozeDamage, igniteSource: false, killMult: 0.5);
                        hitAny = true;
                    }
                }
                if (hitAny) g.RaiseEvent(SimEventKind.SkeletonShot, m.X, m.Y);
            }

            // Tank enemy fire: soak any hazard overlapping the golem's body (it shields the paddle).
            foreach (var hz in g.Hazards)
            {
                if (!hz.Alive) continue;
                if (System.Math.Abs(hz.Pos.X - m.X) > m.Width / 2 + hz.Radius) continue;
                if (System.Math.Abs(hz.Pos.Y - m.Y) > m.Height / 2 + hz.Radius) continue;
                m.Hp -= System.Math.Max(1, hz.Damage);
                hz.Alive = false;
                g.RaiseEvent(SimEventKind.BarrierHit, hz.Pos.X, hz.Pos.Y);
            }
            g.Hazards.RemoveAll(h => !h.Alive);

            // Die when soaked through or once it climbs off the top of the board.
            if (m.Hp <= 0 || m.Y < -cell) m.Alive = false;
        }
        g.Minions.RemoveAll(m => !m.Alive);
    }

}

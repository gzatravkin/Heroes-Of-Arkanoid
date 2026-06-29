using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;

namespace Arkanoid.Core.Sim.Systems;

/// <summary>Concussion Charge (design §3 rework of Rocket) — the Engineer's UTILITY blast: it deals NO
/// damage. Instead it detonates around the paddle and KNOCKS BACK every ball in range (a save — a
/// pit-bound ball is shoved away from the blast, i.e. back up the board) and YANKS nearby falling
/// pickups toward the paddle so you can collect them. Identity: rescue + retrieve, not damage.</summary>
internal static class ConcussionSystem
{
    private const double BonusYankSpeed = 240.0;

    internal static void Cast(GameInstance g, SpellDef def)
    {
        if (!SpellSystem.Spend(g, def.ManaCost, def.Id)) return;
        var center = g.Paddle.Center;
        // §6: the blast scales per level (Concussion deals no damage, so "blast scale" = radius).
        double baseR  = def.AoeRadius > 0 ? def.AoeRadius : 180.0;
        double ballR  = baseR + (g.SpellLevel(def.Id) - 1) * def.AoeRadiusPerLevel; // knockback radius
        double yankR  = ballR * 1.6;                                                // wider pull for pickups

        int knocked = 0, yanked = 0;
        // Rescue: shove each ball in range AWAY from the blast (a low blast under a falling ball
        // pushes it back up the board), preserving its speed. No damage to anything.
        foreach (var b in g.Balls)
        {
            if (!b.Alive) continue;
            var off = b.Pos - center;
            if (off.Length > ballR) continue;
            double speed = b.Vel.Length;
            if (speed < 1e-3) speed = g.Config.BallSpeed;
            var dir = off.Length > 1e-3 ? off.Normalized() : new Vec2(0, -1);
            var vel = dir * speed;
            // A "rescue" must always carry the ball UP the board, even for a ball below the paddle.
            if (vel.Y > 0) vel = new Vec2(vel.X, -vel.Y);
            b.Vel = vel;
            knocked++;
        }
        // Yank: redirect nearby falling pickups toward the paddle so the blast retrieves them.
        foreach (var bon in g.Bonuses)
        {
            if (!bon.Alive) continue;
            if ((bon.Pos - center).Length > yankR) continue;
            var dir = (center - bon.Pos);
            if (dir.Length > 1e-3) bon.Vel = dir.Normalized() * BonusYankSpeed;
            yanked++;
        }

        g.RaiseEvent(SimEventKind.Explosion, center.X, center.Y);
        g.RaiseEvent(SimEventKind.SpellCast, center.X, center.Y);
        g._log.Log(g.TickCount, "spell", "concussion", $"knockedBalls={knocked} yankedPickups={yanked} radius={ballR:0}");
    }
}

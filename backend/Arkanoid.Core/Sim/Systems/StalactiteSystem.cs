using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Caverns stalactites (original <c>UnionOfSticks</c> / Goblin <c>StalaktitCreation</c>):
/// a ceiling block that detaches into a falling hazard the moment a ball passes beneath
/// its column — so lingering under a stalactite gets you hit. Reuses the Hazards pipeline.
/// </summary>
internal static class StalactiteSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead || !blk.Stalactite) continue;
            var bc = g.Level.Grid.CellCenter(blk.Col, blk.Row);

            // Already armed (shaking): count down the telegraph, then detach.
            if (blk.StalArmTimer >= 0)
            {
                blk.StalArmTimer -= dt;
                if (blk.StalArmTimer <= 0) Drop(g, blk, bc);
                else g.MarkBlocksDirty(); // keep the shaking flag live in the snapshot
                continue;
            }

            // Trigger: a ball passes beneath this column → start the shake telegraph (don't drop instantly).
            bool ballBeneath = g.Balls.Any(b => b.Alive
                && System.Math.Abs(b.Pos.X - bc.X) < g.Config.CellSize
                && b.Pos.Y > bc.Y);
            if (ballBeneath)
            {
                blk.StalArmTimer = g.Config.Enemies.StalactiteArmDelay;
                g.MarkBlocksDirty();
                g.RaiseEvent(SimEventKind.Stalactite, bc.X, bc.Y); // arm cue
            }
        }
    }

    private static void Drop(GameInstance g, Block blk, Vec2 origin)
    {
        blk.Dead = true;
        g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = origin,
            Vel    = new Vec2(0, g.Config.Enemies.StalactiteFallSpeed),
            Damage = g.Config.Enemies.HazardDamage,
            Radius = g.Config.Enemies.HazardRadius * 1.4,
            Alive  = true,
            Kind     = "stalactite",
            Behavior = HazardBehavior.Stalactite,
        });
        g.RaiseEvent(SimEventKind.Stalactite, origin.X, origin.Y);
    }

    /// <summary>Boss-drop variant: scatter <paramref name="count"/> stalactites across the top.</summary>
    internal static void BossDrop(GameInstance g, int count)
    {
        double boardW = g.Level.Grid.Width;
        for (int i = 0; i < count; i++)
        {
            double x = g.Rng.Range(g.Config.CellSize, boardW - g.Config.CellSize);
            g.Hazards.Add(new Projectile
            {
                Id     = g._nextHazardId++,
                Pos    = new Vec2(x, g.Config.BoardOriginY + g.Config.CellSize),
                Vel    = new Vec2(0, g.Config.Enemies.StalactiteFallSpeed),
                Damage = g.Config.Enemies.HazardDamage,
                Radius = g.Config.Enemies.HazardRadius * 1.4,
                Alive  = true,
                Kind     = "stalactite",
            Behavior = HazardBehavior.Stalactite,
            });
        }
        g._log.Log(g.TickCount, "stalactite", "boss drop", $"count={count}");
    }
}

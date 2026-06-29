namespace Arkanoid.Core.Sim.Systems;

/// <summary>Fire-Mage per-tick updates: firewall rise and damage.</summary>
internal static partial class SpellSystem
{
    internal static void UpdateFireWalls(GameInstance g, double dt)
    {
        foreach (var wall in g.FireWalls)
        {
            if (!wall.Alive) continue;
            wall.Y           -= wall.RiseSpeed * dt;
            wall.Accumulator += dt;
            while (wall.Accumulator >= wall.DamageInterval)
            {
                foreach (var blk in g.Blocks)
                {
                    if (blk.Dead) continue;
                    var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                    if (c.Y >= wall.Y - wall.BandHalfHeight &&
                        c.Y <= wall.Y + wall.BandHalfHeight)
                    {
                        BlockDamage.DamageBlock(g, blk, wall.DamagePerTick, igniteSource: false, killMult: 0.5);
                        // Fire wall leaves blocks burning briefly — a short, NON-spreading burn so the
                        // full-width sweep doesn't compound into a board nuke.
                        BurnSystem.LightBlock(g, blk, 0, duration: 1.0, noSpread: true);
                        g.RaiseEvent(SimEventKind.Burn, c.X, c.Y);
                    }
                }
                wall.Accumulator -= wall.DamageInterval;
            }
            wall.LifeRemaining -= dt;
            if (wall.LifeRemaining <= 0 || wall.Y < -g.Config.CellSize)
                wall.Alive = false;
        }
        g.FireWalls.RemoveAll(w => !w.Alive);
    }
}

using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Witchland Cauldron (docs/11 — the Economy-axis enemy): while a cauldron is alive
/// it siphons the player's mana into itself; killing it refunds everything it stole
/// (BlockDamage handles the refund on death). The bubbling visual is client-side.
/// </summary>
internal static class CauldronSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead || !blk.Cauldron) continue;
            var siphon = System.Math.Min(g.ManaValue, g.Config.CauldronSiphonPerSec * dt);
            if (siphon <= 0) continue;
            g.ManaValue -= siphon;
            blk.StoredMana += siphon;
        }
    }
}

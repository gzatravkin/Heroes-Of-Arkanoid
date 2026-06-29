using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Kill-triggered relic effects.  Called from BlockDamage on every block death,
/// following the same hook pattern as ReviverSystem.OnBlockDestroyed.
/// </summary>
internal static class RelicSystem
{
    internal static void OnBlockDestroyed(GameInstance g, Vec2 c)
    {
        if (Modifiers.HasSplitShot(g) && ++g._killsSinceSplit >= Modifiers.SplitShotCadence(g))
        {
            g._killsSinceSplit = 0;
            BonusSystem.SpawnExtraBall(g);
            g.RaiseEvent(SimEventKind.SplitShot, c.X, c.Y);
        }
        if (Modifiers.HasSouljar(g) && ++g._killsSinceSouljar >= Modifiers.SouljarCadence(g))
        {
            g._killsSinceSouljar = 0;
            g.Crystals++;
            g._log.Log(g.TickCount, "relic", "souljar crystal", $"crystals={g.Crystals}");
        }
    }
}

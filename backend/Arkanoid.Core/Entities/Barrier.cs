namespace Arkanoid.Core.Entities;

/// <summary>
/// A short-lived horizontal line above the paddle. Shared plumbing for two bespoke spells with
/// distinct behavior, selected per-instance by the flags below (SpellSystem.ClassSpells.UpdateBarriers):
///   - Paladin Shield (LEGACY, reverted 2026-06-16): intercepts descending enemy hazards and fires
///     them back up as the player's own projectiles. Does not touch the ball.
///   - Fire Wall (redesign 2026-07-01): intercepts descending enemy hazards and destroys them
///     outright (no counter-bolt); a descending ball crossing it bounces back upward AND is
///     imbued with Ignite.
/// </summary>
public sealed class Barrier
{
    public int Id { get; init; }
    /// <summary>Y coordinate of the barrier line.</summary>
    public double Y { get; set; }
    /// <summary>Center X of the barrier.</summary>
    public double CenterX { get; set; }
    /// <summary>Full width of the barrier.</summary>
    public double Width { get; set; }
    public double LifeRemaining { get; set; }
    public bool Alive { get; set; } = true;
    /// <summary>Visual identity for the renderer ("shield" | "firewall") — the two spells must not
    /// look alike despite sharing this entity's collision plumbing.</summary>
    public string Kind { get; init; } = "shield";

    /// <summary>Paladin Shield: an intercepted hazard converts into an upward "shieldbolt" player projectile.</summary>
    public bool ReflectsHazardsAsBolts { get; init; }
    /// <summary>Fire Wall: an intercepted hazard is destroyed outright — no counter-attack bolt.</summary>
    public bool DestroysHazards { get; init; }
    /// <summary>Fire Wall: a descending ball crossing the line bounces back upward instead of passing through.</summary>
    public bool ReflectsBall { get; init; }
    /// <summary>Fire Wall: a ball crossing the line is imbued with Ignite (its next block hit lights it).</summary>
    public bool IgnitesBallOnCross { get; init; }
}

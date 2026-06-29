namespace Arkanoid.Core.Entities;

/// <summary>
/// Engineer Containment Field (§3, reworked Radiation): a timed AoE that melts blocks within a radius
/// periodically and — when <see cref="Suppresses"/> — silences any enemy emitter caught inside it.
/// </summary>
public sealed class Zone
{
    public int Id { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
    public double LifeRemaining { get; set; }
    public double Accumulator { get; set; }
    public bool Alive { get; set; } = true;
    public int DamagePerTick { get; set; }
    public double DamageInterval { get; set; }
    /// <summary>True for the Containment Field — emitters inside it cannot fire. (Keeps a future
    /// benign zone spell from silently suppressing emitters.)</summary>
    public bool Suppresses { get; set; }
}

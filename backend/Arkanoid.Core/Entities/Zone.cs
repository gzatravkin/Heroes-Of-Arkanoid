namespace Arkanoid.Core.Entities;

/// <summary>
/// Engineer Radiation: a timed AoE damage zone that damages blocks within a radius periodically.
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
}

namespace Arkanoid.Core.Entities;

public sealed class FireWall
{
    public int Id { get; init; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double LifeRemaining { get; set; }
    public double Accumulator { get; set; }
    public bool Alive { get; set; } = true;

    /// <summary>Damage applied to blocks in the fire band per tick (set from SpellDef at cast time).</summary>
    public int DamagePerTick { get; set; } = 1;
    /// <summary>Upward rise speed in px/s.</summary>
    public double RiseSpeed { get; set; } = 90;
    /// <summary>Seconds between damage ticks.</summary>
    public double DamageInterval { get; set; } = 0.4;
    /// <summary>Half-height of the damage band in pixels.</summary>
    public double BandHalfHeight { get; set; } = 18;
}

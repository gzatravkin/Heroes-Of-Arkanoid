using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

/// <summary>
/// Fire-Mage Phoenix: a visible damaging entity that ORBITS a ball (its own position,
/// distinct from the ball each tick) and burns blocks it sweeps over. See the Fire Mage spec.
/// </summary>
public sealed class Phoenix
{
    public int    Id;
    public Vec2   Pos;
    /// <summary>The ball it orbits (re-targets to any live ball if this one drains).</summary>
    public int    TargetBallId;
    /// <summary>Current orbit angle in radians.</summary>
    public double Angle;
    /// <summary>Orbit distance from the target ball.</summary>
    public double OrbitRadius;
    /// <summary>Orbit angular speed (radians/sec).</summary>
    public double AngularSpeed;
    /// <summary>Radius around its own position within which it damages blocks.</summary>
    public double HitRadius;
    public int    Damage;
    public double DamageInterval;
    public double DamageAccum;
    /// <summary>Seconds of life remaining.</summary>
    public double Lifetime;
}

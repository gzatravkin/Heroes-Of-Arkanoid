using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

public sealed class Projectile
{
    public int Id { get; init; }
    public Vec2 Pos;
    public Vec2 Vel;
    public int Damage;
    public double Radius;
    public bool Alive = true;
    /// <summary>Visual/behaviour kind: "fireball" | "turret" | "spear" | "rocket" | "skeleton_bullet" (default empty = fireball)</summary>
    public string Kind = "";
    /// <summary>Spear: passes through and damages up to this many blocks before dying.</summary>
    public int PiercingHitsLeft = 0;
    /// <summary>Rocket: true means the projectile steers toward the nearest alive block each tick.</summary>
    public bool Homing = false;
    /// <summary>Rocket: AoE radius when this projectile kills a block (0 = none).</summary>
    public double AoeRadius = 0;
    /// <summary>Bat carrier hazard: id of the ball it is dragging toward the drain (0 = none).</summary>
    public int CarriedBallId = 0;
    /// <summary>Stalactite hazard: block ids already damaged while falling through (one hit each).</summary>
    public HashSet<int>? HitBlockIds;
}

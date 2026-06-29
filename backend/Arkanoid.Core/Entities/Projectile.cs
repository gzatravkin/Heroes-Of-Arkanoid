using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

/// <summary>Mutually-exclusive special behaviour for hazard-list projectiles. Replaces string-Kind dispatch.</summary>
public enum HazardBehavior
{
    None,       // plain falling hazard (boss missiles, emitter shots)
    Bat,        // grabs and carries a ball toward the drain
    WitchGrab,  // boss witch-hand: snags the ball and throws it down
    Stalactite, // falls through blocks, accumulating hit-block ids before landing
    Cart,       // rolls horizontally along the paddle row
}

public sealed class Projectile
{
    public int Id { get; init; }
    public Vec2 Pos;
    public Vec2 Vel;
    public int Damage;
    public double Radius;
    public bool Alive = true;
    /// <summary>Visual kind tag for the renderer: "fireball" | "turret" | "spear" | "rocket" | "skeleton_bullet" | "allybolt" | …</summary>
    public string Kind = "";
    /// <summary>Special behaviour for hazard-list entries. None = plain falling projectile.</summary>
    public HazardBehavior Behavior = HazardBehavior.None;
    /// <summary>Spear: passes through and damages up to this many blocks before dying.</summary>
    public int PiercingHitsLeft = 0;
    /// <summary>Rocket: true means the projectile steers toward the nearest alive block each tick.</summary>
    public bool Homing = false;
    /// <summary>Rocket: AoE radius when this projectile kills a block (0 = none).</summary>
    public double AoeRadius = 0;
    /// <summary>Rocket: damage dealt to blocks within AoeRadius on impact (0 = use primary Damage).</summary>
    public int AoeDamage = 0;
    /// <summary>Rocket: steering force applied toward the nearest block each tick.</summary>
    public double HomingStrength = 0;
    /// <summary>Rocket: maximum speed after homing acceleration (0 = no clamp).</summary>
    public double MaxSpeed = 0;
    /// <summary>Bat carrier hazard: id of the ball it is dragging toward the drain (0 = none).</summary>
    public int CarriedBallId = 0;
    /// <summary>Stalactite hazard: block ids already damaged while falling through (one hit each).</summary>
    public HashSet<int>? HitBlockIds;
    /// <summary>Witch grab-hand: seconds left holding the ball before the throw.</summary>
    public double StateTimer = 0;
    /// <summary>Telegraph/warm-up seconds: while &gt; 0 the hazard is INERT — it doesn't move, collide, or
    /// despawn (it just shows as a warning). Used by the cart so it can't appear on the paddle with no warning.</summary>
    public double Warmup = 0;
}

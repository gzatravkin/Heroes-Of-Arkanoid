using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

public sealed class Ball
{
    public int Id { get; init; }
    public Vec2 Pos;
    public Vec2 Vel;
    public double Radius;
    public bool Alive = true;
    public int IgniteHitsLeft = 0;     // >0 means imbued with Ignite
    /// <summary>Ticks remaining before this ball can be warped by a teleporter again.</summary>
    public int TeleportCooldown = 0;
}

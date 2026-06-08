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
}

using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

public sealed class Paddle
{
    public Vec2 Center;
    public double Width;
    public double Height;
    /// <summary>mod_grip paddle mod: extra max deflect angle in degrees (docs/04 §4.4).</summary>
    public double DeflectAngleBonusDeg;
}

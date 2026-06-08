namespace Arkanoid.Core.Entities;

public sealed class FireWall
{
    public int Id { get; init; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double LifeRemaining { get; set; }
    public double Accumulator { get; set; }
    public bool Alive { get; set; } = true;
}

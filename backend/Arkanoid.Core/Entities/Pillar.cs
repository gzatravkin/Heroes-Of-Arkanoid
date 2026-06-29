namespace Arkanoid.Core.Entities;

/// <summary>Lance of Dawn (§3): a temporary SOLID pillar the player drops to bank shots off. The ball
/// reflects off its faces (it's indestructible while it lasts); enables trick angles. Pure deflector —
/// deals no damage.</summary>
public sealed class Pillar
{
    public int    Id            { get; init; }
    public double CenterX       { get; set; }
    public double CenterY       { get; set; }
    public double Width         { get; set; }
    public double Height        { get; set; }
    public double LifeRemaining { get; set; }
    public bool   Alive         { get; set; } = true;
}

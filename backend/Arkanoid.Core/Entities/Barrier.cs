namespace Arkanoid.Core.Entities;

/// <summary>
/// Paladin Shield: a short-lived horizontal barrier just above the paddle.
/// A ball moving downward crossing its Y within its X-span reflects upward.
/// </summary>
public sealed class Barrier
{
    public int Id { get; init; }
    /// <summary>Y coordinate of the barrier line.</summary>
    public double Y { get; set; }
    /// <summary>Center X of the barrier.</summary>
    public double CenterX { get; set; }
    /// <summary>Full width of the barrier.</summary>
    public double Width { get; set; }
    public double LifeRemaining { get; set; }
    public bool Alive { get; set; } = true;
}

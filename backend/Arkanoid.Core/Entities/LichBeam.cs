namespace Arkanoid.Core.Entities;

/// <summary>Lich's Gaze (§3): a slow lighthouse beam anchored at the paddle that SWEEPS an arc, cursing
/// every block its ray crosses. Cursed blocks take bonus damage from the ball. Purely a curse delivery —
/// the beam itself deals no damage.</summary>
public sealed class LichBeam
{
    public int    Id            { get; init; }
    public double OriginX       { get; set; }   // anchored to the paddle each tick
    public double OriginY       { get; set; }
    public double Angle         { get; set; }   // current ray angle (radians; -PI/2 = straight up)
    public double Length        { get; set; }   // ray reach
    public double LifeRemaining { get; set; }
    public double TotalLife     { get; init; }  // for computing the sweep progress
    public double StartAngle    { get; init; }
    public double EndAngle      { get; init; }
}

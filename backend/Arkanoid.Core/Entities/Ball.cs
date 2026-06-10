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
    public int DecayHitsLeft  = 0;     // >0 means imbued with Decay (necromancer)
    /// <summary>Ticks remaining before this ball can be warped by a teleporter again.</summary>
    public int TeleportCooldown = 0;
    /// <summary>Ghost phase (Witchland portal): a ghost ball passes through normal blocks and
    /// instead collides with ghost (ballPhases) blocks. Toggled by a Portal block.</summary>
    public bool Ghost = false;

    // --- Bat carry (Witchland): a bat carrier hazard drags the ball toward the drain. ---
    public int GrabberId = 0;   // the carrier hazard's Id (0 = free)

    // --- G2 ball cores ---
    /// <summary>Ghost core: remaining free phase-through hits this serve.</summary>
    public int PhasesLeft = 0;
    /// <summary>Echo core: armed after a paddle deflect — next block hit deals bonus damage.</summary>
    public bool EchoArmed = false;
}

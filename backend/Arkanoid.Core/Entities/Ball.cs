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
    /// <summary>Necromancer Raise helper-ball — a summoned skeleton minion (renders necromantic green).</summary>
    public bool Summoned = false;

    // --- Bat carry (Witchland): a bat carrier hazard drags the ball toward the drain. ---
    public int GrabberId = 0;   // the carrier hazard's Id (0 = free)

    // --- G2 ball cores ---
    /// <summary>Ghost core: remaining free phase-through hits this serve.</summary>
    public int PhasesLeft = 0;
    /// <summary>Echo core: armed after a paddle deflect — next block hit deals bonus damage.</summary>
    public bool EchoArmed = false;

    // --- §1 Cards (per-ball trigger state) ---
    /// <summary>Bank Shot (§1): wall bounces accumulated since the last block hit. Adds banked damage to the
    /// next block hit, then resets to 0 on any block hit.</summary>
    public int BankCharge = 0;
    /// <summary>Dead Center (§1): armed by a PERFECT deflect — the next (first) block hit deals a burst.</summary>
    public bool DeadCenterArmed = false;
    /// <summary>Hot Hand (§1): highest combo milestone (Count/5) this ball has reached — drives its growth.
    /// Persists across paddle touches; resets only when a fresh ball is served.</summary>
    public int HotHandMilestone = 0;
    /// <summary>Redline (§1): seconds since this ball last touched the paddle — ramps its speed + damage.</summary>
    public double SincePaddle = 0;
    /// <summary>Spin-Loaded (§2): angular spin (rad/sec) imparted by an edge paddle hit; curves the ball, decays.</summary>
    public double Spin = 0;
    /// <summary>Twin Soul Core (§2): this is one of the two tethered twin balls (each weaker; tether slices between them).</summary>
    public bool Twin = false;
    /// <summary>Fission Core (§2): this ball splits every Nth kill and re-fuses on a catch into a bigger ball.</summary>
    public bool Fission = false;

    // --- Holy Echo spell ---
    /// <summary>Holy Echo (Paladin spell): this ball is a temporary holy echo — deals 50% damage,
    /// no imbues, cannot spawn further echoes, expires after EchoTimer reaches zero.</summary>
    public bool IsHolyEcho = false;
    /// <summary>Seconds remaining before this holy echo ball disappears.</summary>
    public double HolyEchoTimer = 0;
}

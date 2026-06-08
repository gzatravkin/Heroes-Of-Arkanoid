namespace Arkanoid.Core.Sim;

/// <summary>Every tunable number lives here (CLAUDE.md: no magic numbers in logic).</summary>
public sealed class SimConfig
{
    public double CellSize { get; init; } = 32;
    public double BoardOriginX { get; init; } = 0;
    public double BoardOriginY { get; init; } = 0;

    public double TickHz { get; init; } = 60;
    public double FixedDt => 1.0 / TickHz;

    public double BallRadius { get; init; } = 8;
    public double BallSpeed { get; init; } = 360;     // units/sec
    public double MinVerticalRatio { get; init; } = 0.30; // "no shallow angle" clamp

    public double PaddleWidth { get; init; } = 96;
    public double PaddleHeight { get; init; } = 16;
    // INVARIANT: keep < acos(MinVerticalRatio) (~72.5deg at 0.30) so paddle deflection
    // can never produce a sub-MinVerticalRatio ("shallow") trajectory on its own.
    public double PaddleMaxDeflectAngleDeg { get; init; } = 60;

    public int StartLives { get; init; } = 3;   // HP, enemy damage (M3+)
    public int StartBalls { get; init; } = 3;   // spare balls (drains)
    public int BallDamage { get; init; } = 1;

    public double ManaMax { get; init; } = 100;
    public double ManaRegenPerSec { get; init; } = 12;
    public double ManaPerKill { get; init; } = 4;
    public double ManaPerfectDeflectBonus { get; init; } = 8;
    public double PerfectDeflectBand { get; init; } = 0.18; // |t| < band counts as "perfect"

    public double IgniteCost { get; init; } = 0;     // imbue is cheap/free (anti-Wizorb)
    public int    IgniteHits { get; init; } = 4;
    public double FireballCost { get; init; } = 20;
    public double FireballSpeed { get; init; } = 420;
    public int    FireballDamage { get; init; } = 2;

    public double FireWallCost { get; init; } = 30;
    public double FireWallRiseSpeed { get; init; } = 90;   // units/sec upward
    public double FireWallLifetime { get; init; } = 3.0;   // seconds
    public double FireWallDamageInterval { get; init; } = 0.4; // seconds between damage ticks
    public int    FireWallDamage { get; init; } = 1;
    public double FireWallBandHalfHeight { get; init; } = 18; // band reaches +-this around its Y

    public double TurretCost { get; init; } = 25;
    public double TurretDuration { get; init; } = 5.0;     // seconds active
    public double TurretFireInterval { get; init; } = 0.35;
    public int    TurretDamage { get; init; } = 1;
    public double TurretBulletSpeed { get; init; } = 460;

    // --- Biome block mechanics ---
    /// <summary>Ticks a ball must wait before it can be teleported again after a warp.</summary>
    public int TeleportCooldownTicks { get; init; } = 18;

    // --- Spell level scaling ---
    public int    FireballDamagePerLevel { get; init; } = 1;
    public int    IgniteHitsPerLevel     { get; init; } = 1;
    public int    FireWallDamagePerLevel { get; init; } = 1;
    public double TurretDurationPerLevel { get; init; } = 1.0; // seconds added per level above 1

    // --- Relic magnitudes ---
    public int    GlassCannonDamageBonus { get; init; } = 1;
    public int    FlintToughThreshold { get; init; } = 3;
    public int    FlintBonus { get; init; } = 1;
    public int    PyroclasmChip { get; init; } = 2;        // vs default 1
    public double ManaBatteryBonus { get; init; } = 50;
    public double ManaBatteryRegenMult { get; init; } = 1.6;

    public static SimConfig Default { get; } = new();
}

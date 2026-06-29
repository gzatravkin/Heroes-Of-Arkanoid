namespace Arkanoid.Core.Sim;

/// <summary>
/// Compile-checked discriminant for all domain events raised during simulation.
/// Mapped to camelCase wire strings in <see cref="Net.Snapshot"/> — client contract unchanged.
/// </summary>
public enum SimEventKind
{
    // ── Spells ──────────────────────────────────────────────────────────────
    SpellCast,
    /// <summary>A cast that could not take effect (e.g. Fire Wall while the ball rests on the paddle,
    /// or Conflagration on an empty board) — drives a small "dud" cue so it's never a silent no-op.</summary>
    SpellFizzle,
    Ignite,
    Burn,
    Decay,
    Lightning,
    Radiation,
    Phoenix,
    SkeletonShot,
    TurretShot,
    Penetration,
    Judgement,
    Frost,
    // ── Ball / blocks ────────────────────────────────────────────────────────
    BlockDestroyed,
    Explosion,
    Deflect,
    /// <summary>A critical hit (stat engine). Payload = the crit damage dealt.</summary>
    Crit,
    /// <summary>A perfect (centre-band) paddle deflect — the skill reward worth juicing.</summary>
    PerfectDeflect,
    GhostPortal,
    Teleport,
    // ── Enemies ──────────────────────────────────────────────────────────────
    EnemyShot,
    AllyShot,
    BatGrab,
    BatRelease,
    Cart,
    Stalactite,
    Corrupt,
    DeathMark,
    // ── Boss ─────────────────────────────────────────────────────────────────
    BossTelegraph,
    BossAttack,
    /// <summary>Payload = new phase number (1/2/3).</summary>
    BossPhase,
    BossHop,
    FistTelegraph,
    FistSlam,
    WitchGrabCast,
    WitchGrab,
    WitchThrow,
    WitchGrabPopped,
    SeraphVase,
    SeraphAdd,
    VaseShatter,
    VaseLevelUp,
    // ── Lava ─────────────────────────────────────────────────────────────────
    LavaCreep,
    LavaRetract,
    LavaDrain,
    // ── Bonuses / pickups ────────────────────────────────────────────────────
    BonusCaught,
    SplitShot,
    // ── Player ───────────────────────────────────────────────────────────────
    PlayerHit,
    ShieldSave,
    ShieldBlock,
    Shield,
    BarrierHit,
    SecondWind,
    ManaRefund,
    // ── Level ────────────────────────────────────────────────────────────────
    LevelWon,
    LevelLost,
    TimeUp,
    Overrun,
    // ── Pacing / floors ──────────────────────────────────────────────────────
    Descend,
    FloorDown,
    // ── Reviver ──────────────────────────────────────────────────────────────
    Revive,
    ReviveCancelled,
    // ── Misc ─────────────────────────────────────────────────────────────────
    Altar,
    MidasCrystals,
}

namespace Arkanoid.Core.Sim;

/// <summary>
/// Compile-checked discriminant for all domain events raised during simulation.
/// Mapped to camelCase wire strings in <see cref="Net.Snapshot"/> — client contract unchanged.
/// </summary>
public enum SimEventKind
{
    // ── Spells ──────────────────────────────────────────────────────────────
    SpellCast,
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

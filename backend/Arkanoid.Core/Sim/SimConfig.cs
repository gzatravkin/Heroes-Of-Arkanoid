namespace Arkanoid.Core.Sim;

// ---------------------------------------------------------------------------
// Nested config groups — separating player-facing, enemy, boss, and pickup
// knobs that would otherwise share one flat namespace.
// ---------------------------------------------------------------------------

/// <summary>Enemy-block mechanics: emitters, stat statues, bombs, lava, bats, etc.</summary>
public sealed class EnemiesConfig
{
    public int    TeleportCooldownTicks { get; init; } = 18;
    public double DefaultEmitInterval  { get; init; } = 2.5;
    public double EmitTelegraphWindow  { get; init; } = 0.5;
    public double HazardSpeed          { get; init; } = 210;
    public int    HazardDamage         { get; init; } = 1;
    public double HazardRadius         { get; init; } = 8;
    public int    BombDamage           { get; init; } = 3;
    public double StalactiteFallSpeed  { get; init; } = 260;
    /// <summary>Telegraph: a stalactite shakes this long after the ball passes under before it detaches
    /// and falls, so a low-hanging one isn't an instant unavoidable hit (2026-06-15).</summary>
    public double StalactiteArmDelay   { get; init; } = 0.35;
    public int    StalactiteBlockDamage{ get; init; } = 1;
    public double ReviveDelay          { get; init; } = 4.0;
    public double WindMasterForce      { get; init; } = 900;
    public double WindMasterRadius     { get; init; } = 110;
    /// <summary>Seconds between a ShieldStatue's protective pulses.</summary>
    public double ShieldStatueInterval { get; init; } = 3.5;
    /// <summary>ShieldStatue influence radius in cells (Chebyshev).</summary>
    public int    ShieldStatueRadius   { get; init; } = 2;
    /// <summary>Seconds a block stays damage-immune after a ShieldStatue pulse.</summary>
    public double StatueImmunityDuration { get; init; } = 2.5;
    public double BatCarrySpeed        { get; init; } = 70;
    public double BatFlyAwaySpeed      { get; init; } = 140;
    /// <summary>LEGACY bat (reverted 2026-06-16): seconds it HOLDS the ball before releasing it + rewarding
    /// the player (risk→reward, not a drain threat).</summary>
    public double BatHoldTime          { get; init; } = 3.0;
    public int    AllyBoltDamage       { get; init; } = 1;
    public int    CorruptDamage        { get; init; } = 1;
    public double VaseLevelHaste       { get; init; } = 0.35;
    public double VaseKillManaPerLevel { get; init; } = 4.0;
    public double CauldronSiphonPerSec { get; init; } = 6.0;
    public double LavaCreepInterval    { get; init; } = 6.0;
    public int    LavaCreepMax         { get; init; } = 6;
    public double LavaDrainInterval    { get; init; } = 3.0;
    public double AltarAllyDuration    { get; init; } = 8.0;
    public double CartSpeed            { get; init; } = 150;
    public double CartInterval         { get; init; } = 4.0;
    // Cart telegraph (2026-06-15): the cart appears at the edge and sits INERT (visible warning, no damage)
    // this long before it starts rolling, so it can't spawn on the paddle row with no way to dodge.
    public double CartTelegraph        { get; init; } = 0.8;
}

/// <summary>Boss combat mechanics: patterns, phases, signature attacks.</summary>
public sealed class BossConfig
{
    public double AttackInterval     { get; init; } = 2.4;
    public double HazardSpeed        { get; init; } = 210;
    public int    HazardDamage       { get; init; } = 1;
    public double HazardRadius       { get; init; } = 9;
    // Level-UX rework (2026-06-15, Option 1 "fair projectiles"): no live homing — aimed boss shots now fall
    // straight from the boss's position (predictable, dodgeable). Boss variety comes from movement + the
    // random-column rain + the fixed-angle spread + phase cadence, not from sniping the paddle. Tunable.
    public double HazardAimStrength  { get; init; } = 0.0;
    public int    FistDamage         { get; init; } = 1;
    public int    FistBlockDamage    { get; init; } = 1;
    public int    GoblinHopOffset    { get; init; } = 2;
    public double WitchGrabSpeed     { get; init; } = 160;
    public double WitchThrowDelay    { get; init; } = 1.2;
    public double WitchThrowSpeedMult{ get; init; } = 1.4;
    public int    SeraphMaxAdds      { get; init; } = 2;
    public int    SeraphAddHp        { get; init; } = 3;
    public double SeraphVaseFuse     { get; init; } = 8.0;
    public double Phase2Threshold    { get; init; } = 0.60;
    public double Phase3Threshold    { get; init; } = 0.30;
    public double Phase2AttackInterval { get; init; } = 1.8;
    public double Phase3AttackInterval { get; init; } = 1.2;
    public double TelegraphDuration  { get; init; } = 0.5;
    public int    SpreadCount        { get; init; } = 3;
    public double SpreadHalfAngleDeg { get; init; } = 50.0;
    public int    RainCount          { get; init; } = 2;
    public double SummonSpeedMult    { get; init; } = 1.6;
    public double SummonAimStrength  { get; init; } = 0.0;   // no homing (see HazardAimStrength note)
}

/// <summary>Fire-Mage burn mechanics: ignited blocks burn as a DoT and spread ring-by-ring over time.</summary>
public sealed class FireConfig
{
    // Ignite redesign (owner-approved 2026-06-16): ignite is a SLOW, limited DoT you set and then survive.
    // An ignite-imbued deflect LIGHTS the block (no direct damage); it then burns 1 dmg every BurnInterval,
    // and the fire CREEPS one block every SpreadInterval, capped to (SpreadBlocksBase + spell level) blocks per
    // seed. Per-block total burn damage is capped at min(BurnDamageCap, BurnDamageBase + spell level).
    /// <summary>Seconds between burn-damage ticks (slow DoT — owner: ~7s per 1 dmg).</summary>
    public double BurnInterval    { get; init; } = 7.0;
    /// <summary>Damage dealt per burn tick.</summary>
    public int    BurnDamage      { get; init; } = 1;
    /// <summary>Seconds between fire creeping one block further along the chain.</summary>
    public double SpreadInterval  { get; init; } = 2.5;
    /// <summary>Total burning blocks per ignite seed = this + spell level (level 1 → 3 blocks).</summary>
    public int    SpreadBlocksBase { get; init; } = 2;
    /// <summary>Per-block total burn damage = min(BurnDamageCap, this + spell level) (level 1 → 3).</summary>
    public int    BurnDamageBase  { get; init; } = 2;
    /// <summary>Hard cap on a single block's total burn damage regardless of spell level.</summary>
    public int    BurnDamageCap   { get; init; } = 6;
    /// <summary>Fallback burn seconds for callers that pass an explicit non-spreading burn (e.g. the firewall).</summary>
    public double BurnDuration    { get; init; } = 7.0;
}

/// <summary>Pickup drop mechanics: bonuses and power-ups that fall from blocks.</summary>
public sealed class PickupsConfig
{
    public double DropChance    { get; init; } = 0.12;
    public double FallSpeed          { get; init; } = 130;
    public double EffectDuration     { get; init; } = 6.0;
    public double WidePaddleBonus    { get; init; } = 24;
    public double SlowBallFactor     { get; init; } = 0.55;
    public double ManaSurgeAmount    { get; init; } = 30;
    public double CatchHalfH         { get; init; } = 10;
    public double CatchHalfW         { get; init; } = 14;
    public int    CoinsCrystals      { get; init; } = 10;
    /// <summary>Gold granted by the coins/treasure pickup (docs/04 §5 in-run spending currency).</summary>
    public int    CoinsGold          { get; init; } = 5;
    public double SpecialDropChance  { get; init; } = 0.25;
    public double WideDuration    { get; init; } = 15.0;
    public double FireshotDuration{ get; init; } = 10.0;
}

/// <summary>Every tunable number lives here (CLAUDE.md: no magic numbers in logic).</summary>
public sealed class SimConfig
{
    public double CellSize { get; init; } = 32;
    public double BoardOriginX { get; init; } = 0;
    public double BoardOriginY { get; init; } = 0;

    public double TickHz { get; init; } = 60;
    public double FixedDt => 1.0 / TickHz;

    public double BallRadius { get; init; } = 5;
    public double BallSpeed { get; init; } = 306;
    /// <summary>Difficulty rework (2026-06-16): the ball accelerates over time from BallSpeed up to
    /// BallSpeedMax, reaching the cap after BallAccelSeconds of Playing time (classic-Arkanoid feel).
    /// Replaces the old +5%/20-brick ramp. New Game+ adds NgSpeedPerTier to both ends.</summary>
    public double BallSpeedMax { get; init; } = 638;
    public double BallAccelSeconds { get; init; } = 60;
    public double MinVerticalRatio { get; init; } = 0.30;

    // Level-UX rework (2026-06-15, Option 1): paddle shrunk from 96 (3 cells, 37.5% of an 8-col board)
    // to 64 (2 cells, 25%) so the player has real lateral room to dodge enemy fire.
    // Difficulty rework (2026-06-16, Super-Hardcore profile): paddle 64→52 (smaller, ~20% of board).
    public double PaddleWidth { get; init; } = 52;
    public double PaddleHeight { get; init; } = 16;
    public double PaddleMaxDeflectAngleDeg { get; init; } = 60;

    public int StartHp { get; init; } = 3;
    /// <summary>Seconds of damage immunity (i-frames) after a hit. 3.0 let you face-tank; cut to 1.5
    /// (still generous vs the genre ~0.5s norm) — enough to not double-hit on one hazard (2026-06-16).</summary>
    public double DamageImmunity { get; init; } = 1.5;
    public int StartBalls { get; init; } = 2;
    public int BallDamage { get; init; } = 1;

    // Difficulty rework (2026-06-16): mana is now a real budget — regen 14→4/s (full ≈25s), kill 4→1.
    public double ManaMax { get; init; } = 100;
    public double ManaRegenPerSec { get; init; } = 4;
    public double ManaPerKill { get; init; } = 1;
    public double ManaPerfectDeflectBonus { get; init; } = 8;
    public double PerfectDeflectBand { get; init; } = 0.18;

    // --- Decay spread mechanics (ball interaction, not spell cast params) ---
    public int    DecaySpreadChip    { get; init; } = 2;
    public int    DecaySpreadRange   { get; init; } = 2;

    // --- Character passives ---
    public double EngineerRegenMult       { get; init; } = 1.5;
    public double NecromancerKillManaMult { get; init; } = 2.0;

    // --- Nested config groups (enemy mechanics, boss combat, pickups, fire) ---
    public EnemiesConfig Enemies { get; init; } = new();
    public BossConfig    Boss    { get; init; } = new();
    public PickupsConfig Pickups { get; init; } = new();
    public FireConfig    Fire    { get; init; } = new();

    public static SimConfig Default { get; } = new();
}

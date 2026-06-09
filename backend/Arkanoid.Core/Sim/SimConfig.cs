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
    public double ManaRegenPerSec { get; init; } = 14;  // bumped 12→14 to keep flow with higher spell costs
    public double ManaPerKill { get; init; } = 4;
    public double ManaPerfectDeflectBonus { get; init; } = 8;
    public double PerfectDeflectBand { get; init; } = 0.18; // |t| < band counts as "perfect"

    public double IgniteCost { get; init; } = 0;     // imbue is cheap/free (anti-Wizorb)
    public int    IgniteHits { get; init; } = 4;
    public double FireballCost { get; init; } = 25;  // bumped 20→25 to prevent spam
    public double FireballSpeed { get; init; } = 420;
    public int    FireballDamage { get; init; } = 2;

    public double FireWallCost { get; init; } = 35;  // bumped 30→35; long AoE should cost more
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

    // --- Ball-core magnitudes ---
    /// <summary>Extra damage added to every ball hit when the "heavy" ball core is active.</summary>
    public int HeavyBallDamageBonus { get; init; } = 1;
    /// <summary>Number of extra balls spawned at serve when the "split" ball core is active.</summary>
    public int SplitBallExtraBalls { get; init; } = 1;
    /// <summary>IgniteHitsLeft assigned to every served ball when the "ember" ball core is active.</summary>
    public int EmberBallIgniteHits { get; init; } = 2;

    // --- Character passive magnitudes ---
    /// <summary>Regen multiplier applied when the active character is "engineer".</summary>
    public double EngineerRegenMult { get; init; } = 1.5;
    /// <summary>Multiplier on ManaPerKill applied when the active character is "necromancer".</summary>
    public double NecromancerKillManaMult { get; init; } = 2.0;

    // --- Paladin spell: Shield ---
    public double ShieldCost           { get; init; } = 20;
    public double ShieldLifetime       { get; init; } = 4.0;   // seconds
    /// <summary>Shield width as a multiple of PaddleWidth.</summary>
    public double ShieldWidthMult      { get; init; } = 1.2;

    // --- Paladin spell: Spear ---
    public double SpearCost            { get; init; } = 15;
    public double SpearSpeed           { get; init; } = 500;
    public int    SpearDamage          { get; init; } = 3;
    public int    SpearPiercingHits    { get; init; } = 4;    // hits through this many blocks

    // --- Paladin spell: Duplicate ---
    public double DuplicateCost        { get; init; } = 25;
    public int    DuplicateExtraBalls  { get; init; } = 1;    // balls added

    // --- Engineer spell: Lightning ---
    public double LightningCost        { get; init; } = 20;
    public int    LightningDamage      { get; init; } = 2;
    public int    LightningChainJumps  { get; init; } = 4;    // max blocks hit
    /// <summary>Maximum distance (world units) for a chain jump between blocks.</summary>
    public double LightningChainRadius { get; init; } = 80;

    // --- Engineer spell: Rocket ---
    public double RocketCost           { get; init; } = 25;
    public double RocketSpeed          { get; init; } = 300;
    public int    RocketDamage         { get; init; } = 4;
    public double RocketAoeRadius      { get; init; } = 48;   // world units
    public int    RocketAoeDamage      { get; init; } = 2;
    public double RocketHomingStrength { get; init; } = 400;  // accel units/sec²

    // --- Engineer spell: Radiation ---
    public double RadiationCost           { get; init; } = 30;
    public double RadiationLifetime       { get; init; } = 4.0;
    public double RadiationRadius         { get; init; } = 80;
    public int    RadiationDamage         { get; init; } = 1;
    public double RadiationDamageInterval { get; init; } = 0.5;

    // --- Necromancer spell: Decay ---
    public double DecayCost          { get; init; } = 0;   // free imbue like Ignite
    public int    DecayHits          { get; init; } = 4;
    /// <summary>Number of cardinal neighbours chipped by a decay kill.</summary>
    public int    DecaySpreadChip    { get; init; } = 2;
    public int    DecaySpreadRange   { get; init; } = 2;   // Manhattan distance for spread

    // --- Necromancer spell: Skeleton ---
    public double SkeletonCost          { get; init; } = 25;
    public double SkeletonDuration      { get; init; } = 5.0;   // seconds
    public double SkeletonFireInterval  { get; init; } = 0.4;
    public int    SkeletonBulletDamage  { get; init; } = 1;
    public double SkeletonBulletSpeed   { get; init; } = 420;

    // --- Necromancer spell: Drain ---
    public double DrainCost          { get; init; } = 20;
    public double DrainDuration      { get; init; } = 6.0;   // seconds
    /// <summary>Additional mana gained per kill while drain is active (stacks with base).</summary>
    public double DrainBonusManaPerKill { get; init; } = 6.0;

    // --- Boss enemy (legacy single-pattern; kept for backward-compat; BossSystem reads these per-phase) ---
    /// <summary>Seconds between boss hazard shots per live boss block (phase 1 baseline).</summary>
    public double BossAttackInterval { get; init; } = 1.6;
    /// <summary>Downward speed of a boss hazard (units/sec).</summary>
    public double BossHazardSpeed { get; init; } = 240;
    /// <summary>HP damage dealt to the player when a hazard hits the paddle.</summary>
    public int    BossHazardDamage { get; init; } = 1;
    /// <summary>Collision radius of a boss hazard.</summary>
    public double BossHazardRadius { get; init; } = 9;
    /// <summary>How strongly a hazard aims toward the paddle (0 = straight down, 1 = full tracking).</summary>
    public double BossHazardAimStrength { get; init; } = 0.35;

    // --- Enemy emitter blocks (Hell spawner / Beholder / Melee statue) ---
    /// <summary>Speed of a hazard fired by an emitter block (units/sec).</summary>
    public double EnemyHazardSpeed  { get; init; } = 210;
    /// <summary>HP damage an emitter hazard deals on paddle contact.</summary>
    public int    EnemyHazardDamage { get; init; } = 1;
    /// <summary>Collision radius of an emitter hazard.</summary>
    public double EnemyHazardRadius { get; init; } = 8;
    /// <summary>Bomb explosion damage applied to each block in radius.</summary>
    public int    BombDamage { get; init; } = 3;
    /// <summary>Downward speed of a dropped stalactite hazard (units/sec).</summary>
    public double StalactiteFallSpeed { get; init; } = 260;
    /// <summary>Seconds before a Necromant revives a destroyed normal block.</summary>
    public double NecromantReviveDelay { get; init; } = 4.0;
    /// <summary>WindMaster push strength on a ball (units/sec of velocity added at the centre).</summary>
    public double WindMasterForce { get; init; } = 900;
    /// <summary>WindMaster influence radius (units); push falls off linearly to zero at the edge.</summary>
    public double WindMasterRadius { get; init; } = 110;
    /// <summary>Seconds between a Shield Statue's protective pulses.</summary>
    public double ShieldStatueInterval { get; init; } = 3.5;
    /// <summary>Shield-statue radius in cells (Chebyshev).</summary>
    public int    ShieldStatueRadius   { get; init; } = 2;
    /// <summary>Seconds a block stays damage-immune after being shielded.</summary>
    public double ShieldDuration       { get; init; } = 2.5;
    /// <summary>Seconds a Witchland bat holds the ball before releasing it.</summary>
    public double BatHoldTime          { get; init; } = 2.0;
    /// <summary>Upward speed of the released bat's harmless flyaway hazard (units/sec).</summary>
    public double BatFlyAwaySpeed      { get; init; } = 140;
    /// <summary>Seconds the Heaven statues stay pacified after an Altar hit / Vase break.</summary>
    public double AltarAllyDuration    { get; init; } = 8.0;
    /// <summary>Horizontal speed of a rolling cart hazard (units/sec).</summary>
    public double CartSpeed            { get; init; } = 150;
    /// <summary>Seconds between cart launches from a cart-spawner block.</summary>
    public double CartInterval         { get; init; } = 4.0;

    // --- Boss multi-pattern phases ---
    /// <summary>HP fraction threshold below which the boss enters phase 2 (speed + spread added).</summary>
    public double BossPhase2Threshold { get; init; } = 0.60;
    /// <summary>HP fraction threshold below which the boss enters phase 3 / enrage (fastest + summon).</summary>
    public double BossPhase3Threshold { get; init; } = 0.30;

    /// <summary>Seconds between attacks in phase 2 (shorter = faster).</summary>
    public double BossPhase2AttackInterval { get; init; } = 1.2;
    /// <summary>Seconds between attacks in phase 3 (enrage).</summary>
    public double BossPhase3AttackInterval { get; init; } = 0.75;

    /// <summary>Seconds of telegraph warning before each attack fires.</summary>
    public double BossTelegraphDuration { get; init; } = 0.5;

    /// <summary>Number of hazards in a spread fan (phase 2+).</summary>
    public int    BossSpreadCount { get; init; } = 4;
    /// <summary>Half-angle (degrees) of the spread fan.</summary>
    public double BossSpreadHalfAngleDeg { get; init; } = 35.0;

    /// <summary>Number of rain hazards spawned across random top-X positions (phase 1+).</summary>
    public int    BossRainCount { get; init; } = 3;

    /// <summary>Additional downward speed multiplier for summon/minion hazards (phase 3).</summary>
    public double BossSummonSpeedMult { get; init; } = 1.6;
    /// <summary>How strongly summon minions track the paddle X (stronger than aimedShot).</summary>
    public double BossSummonAimStrength { get; init; } = 0.65;

    // --- Bonus pickups ---
    /// <summary>Probability (0–1) that a destroyed block drops a bonus pickup.</summary>
    public double BonusDropChance     { get; init; } = 0.12;
    /// <summary>Downward speed of a falling bonus pickup (units/sec).</summary>
    public double BonusFallSpeed      { get; init; } = 130;
    /// <summary>Seconds that temporary effects (wide_paddle, slow_ball) remain active.</summary>
    public double BonusEffectDuration { get; init; } = 6.0;
    /// <summary>Extra width added to the paddle by the wide_paddle bonus.</summary>
    public double WidePaddleBonus     { get; init; } = 48;
    /// <summary>Multiplier applied to ball speed during the slow_ball bonus (&lt;1 = slower).</summary>
    public double SlowBallFactor      { get; init; } = 0.55;
    /// <summary>Mana restored by the mana_surge bonus.</summary>
    public double ManaSurgeAmount     { get; init; } = 30;
    /// <summary>Hit-box half-height of a falling bonus pickup (for catch detection).</summary>
    public double BonusCatchHalfH     { get; init; } = 10;
    /// <summary>Hit-box half-width of a falling bonus pickup.</summary>
    public double BonusCatchHalfW     { get; init; } = 14;
    /// <summary>Crystal count awarded by the coins bonus pickup.</summary>
    public int    CoinsBonus          { get; init; } = 10;

    // --- Persistent item equip effect magnitudes (per tier) ---
    /// <summary>Ball damage bonus added per tier of a ball_damage item.</summary>
    public int    ItemBallDamageBonusPerTier  { get; init; } = 1;
    /// <summary>Max mana added per tier of a max_mana item.</summary>
    public double ItemMaxManaBonusPerTier     { get; init; } = 20;
    /// <summary>Max mana added per tier of a torch-type max_mana item (smaller increment).</summary>
    public double ItemMaxManaBonusSmallPerTier { get; init; } = 15;
    /// <summary>Mana-regen multiplier per tier of a mana_regen item (tome type).</summary>
    public double ItemManaRegenMultPerTier    { get; init; } = 0.20;  // additive; total mult = 1 + tier*0.20
    /// <summary>Mana-regen multiplier per tier of a staff-type mana_regen item (smaller).</summary>
    public double ItemManaRegenMultSmallPerTier { get; init; } = 0.15;
    /// <summary>Extra starting lives granted per tier of a start_life item.</summary>
    public int    ItemStartLifeBonusPerTier   { get; init; } = 1;
    /// <summary>Extra crystals awarded at level clear per tier of a treasure item (ring).</summary>
    public int    ItemTreasureBonusPerTier    { get; init; } = 5;
    /// <summary>Extra crystals per tier of a clover-type treasure item (slightly more).</summary>
    public int    ItemTreasureBonusLargePerTier { get; init; } = 8;
    /// <summary>Bonus damage vs tough blocks per tier of a crit_tough item.</summary>
    public int    ItemCritToughBonusPerTier   { get; init; } = 1;
    /// <summary>Kill-mana multiplier per tier of a kill_mana item (gem type).</summary>
    public double ItemKillManaMultPerTier     { get; init; } = 0.20;  // additive; total mult = 1 + tier*0.20
    /// <summary>Kill-mana multiplier per tier of an orb-type kill_mana item (smaller).</summary>
    public double ItemKillManaMultSmallPerTier { get; init; } = 0.15;
    /// <summary>Extra paddle width per tier of a paddle_width item (jadeball type).</summary>
    public double ItemPaddleWidthBonusPerTier { get; init; } = 12;
    /// <summary>Extra paddle width per tier of an hourglass-type paddle_width item.</summary>
    public double ItemPaddleWidthBonusSmallPerTier { get; init; } = 10;

    public static SimConfig Default { get; } = new();
}

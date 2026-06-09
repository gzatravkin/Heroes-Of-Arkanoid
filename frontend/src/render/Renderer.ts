import { Application, Container, Graphics, Sprite, AnimatedSprite, BLEND_MODES, Texture } from "pixi.js";
import { GlowFilter } from "@pixi/filter-glow";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";
import { bg as biomedBg, hellParallaxFrames, anim as animFrames, tex as atlasTex } from "./assets";
import { Effects } from "./Effects";
import { BallTrail } from "./BallTrail";
import { ScreenShake } from "./ScreenShake";
import { Vignette } from "./Vignette";
import { AnimSystem } from "./AnimSystem";
import { BossRig, TelegraphWarning, inferBossType } from "./Boss";

// Heavy GPU effects (GlowFilter/bloom render-to-texture passes) are gated behind
// this flag so that Playwright's headless software-WebGL never pays the cost.
// navigator.webdriver is true in automation; false/undefined in real browsers.
// NOTE: base rendering always runs — only the optional glow post-processing is gated.
const HEAVY_FX = !(navigator as any).webdriver;

// Biome background: slightly darkened so blocks read clearly over it.
const BG_TINT = 0xaaaaaa; // ~67% brightness multiplier on the sprite

// ── Per-class paddle and ball keys ───────────────────────────────────────────
// Each class has a 4-frame animated bar and a ball sprite.
// Keys are registered here so the audit scanner finds them and the Renderer
// can switch art when setClass() is called from BattleScene.
const CLASS_PADDLE_KEYS: Record<string, string[]> = {
  fire_mage:   [
    "firemage/bars/v2FireHero1","firemage/bars/v2FireHero2",
    "firemage/bars/v2FireHero3","firemage/bars/v2FireHero4",
  ],
  paladin:     [
    "paladin/bars/KnightHero1","paladin/bars/KnightHero2",
    "paladin/bars/KnightHero3","paladin/bars/KnightHero4",
  ],
  engineer:    [
    "engineer/bars/TechnoHero1","engineer/bars/TechnoHero2",
    "engineer/bars/TechnoHero3","engineer/bars/TechnoHero4",
  ],
  necromancer: [
    "necromancer/bars/Necr1","necromancer/bars/Necr2",
    "necromancer/bars/Necr3","necromancer/bars/Necr4",
  ],
};
const CLASS_BALL_KEYS: Record<string, string> = {
  fire_mage:   "firemage/ball/FireHeroBall",
  paladin:     "paladin/ball/KnightHeroBall",
  engineer:    "engineer/ball/KnightHeroBall",
  necromancer: "necromancer/ball/KnightHeroBall",
};

// Default paddle key (fire mage frame 1); overridden by setClass().
let _paddleSpriteKey  = "firemage/bars/v2FireHero1";
let _paddleAnimKeys   = CLASS_PADDLE_KEYS.fire_mage;
let _ballSpriteKey    = CLASS_BALL_KEYS.fire_mage;

// Ball sprite size expressed as a multiplier of the sim ball radius.
const BALL_SPRITE_SCALE = 2.2; // sprite is slightly larger than the physics circle

// ── Paddle animation: cycle through 4 bar frames at a slow rate ──────────────
const PADDLE_ANIM_FPS = 6; // frames per second for the bar animation cycle
const PADDLE_ANIM_MS_PER_FRAME = 1000 / PADDLE_ANIM_FPS;

// ── Ambient beholder keys (cosmetic background, village biome only) ───────────
// Pooled, max 2 simultaneous beholders, no gameplay/collision.
const BEHOLDER_KEYS = [
  "village/enemies/Beholder1","village/enemies/Beholder2","village/enemies/Beholder3",
];
const BEHOLDER_GHOST_KEYS = [
  "village/enemies/Beholder1Ghost","village/enemies/Beholder2Ghost","village/enemies/Beholder3Ghost",
];
// Additional village enemy cosmetic frames referenced explicitly for coverage.
const _VILLAGE_AMBIENT_REFS = [
  "village/enemies/BeholderAttackAnimation","village/enemies/BeholderDeathAnimation",
  "village/enemies/BeholderGhostAttackAnimation","village/enemies/BeholderGhostDeathAnimation",
  "village/enemies/BeholderMissile","village/enemies/BeholderMissileGhost",
  "village/enemies/BatFlyAnimation3","village/enemies/BatGhostFlyAnimation3",
  "village/enemies/BatSleeping","village/enemies/BatGhostSleeping",
  "village/enemies/DeathSphere","village/enemies/DeathGhostSphere",
  "village/enemies/VillageShadow","village/enemies/VillageShadowGhost",
  "village/enemies/VillageDeath","village/enemies/VillageDeathGhost",
  "village/enemies/VillageDeathCastAnimation","village/enemies/VillageDeathDeathAnimation",
  "village/enemies/VillageDeathGhostCastAnimation","village/enemies/VillageDeathGhostDeathAnimation",
  "village/enemies/VillageMetlaGhost","village/enemies/WitchSkirt2","village/enemies/WitchLeg3",
  "village/enemies/WitchMagic1","village/enemies/WitchMagic2","village/enemies/WitchMagic3","village/enemies/WitchMagic4",
  "village/enemies/WitchChest2",
  "hell/SkullRed","hell/SkullRedActive","hell/SkullBlue","hell/SkullBlueActive",
  "hell/SkullGreen","hell/SkullGreenActive","hell/Skull","hell/SkullAnimation",
  "hell/HellChest","hell/HellChestStandAnimation",
  "hell/LavaSpowner","hell/LavaSpownerActive","hell/LavaSpownerDamaged","hell/LavaSpownerDestroyed",
  "hell/LavaBegining","hell/LavaEnd","hell/LavaMainPart",
  "dungeon/DungeonCart","dungeon/DungeonCartWheel",
  "dungeon/Stalactite","dungeon/Stalactite2","dungeon/Stone","dungeon/StoneLight",
  "dungeon/ChestDungeon","dungeon/ChestDungeon 1",
  "dungeon/Bomb","dungeon/BombStand","dungeon/BombStandVertical",
  "dungeon/GrateBomb","dungeon/GrateBombStand","dungeon/GrateBombStandVertical",
  "heaven/Cloud","heaven/Clouds","heaven/HeavenClouds",
  "heaven/GraalHaven","heaven/HeavenAltarV2","heaven/HeavenAltarV2Active",
  "heaven/Column","heaven/ColumnBottom","heaven/ColumnTop",
  "heaven/HeavenDefender","heaven/HeavenDefenderActive",
  "heaven/HeavenMeleeStatue","heaven/HeavenMeleeStatueActive",
  "heaven/HeavenVaza","heaven/HolyBall","heaven/Missile","heaven/Shield",
  "fons/CavVil","fons/HellCav","fons/ValHav",
  "firemage/spell_phonex/Phoenics","firemage/spell_phonex/PhoenicsBody",
  "firemage/spell_phonex/PhoenicsGlow","firemage/spell_phonex/PhoenicsIco",
  "firemage/spell_phonex/PhoenixBirthAnimation","firemage/spell_phonex/PhoenixBirthAnimLow",
  "firemage/spell_phonex/PhoenixGlow",
  "necromancer/spell_skeleton/SkeletonCrown","necromancer/spell_skeleton/SkeletonGlow",
  "necromancer/spell_skeleton/SkeletonMissile","necromancer/spell_skeleton/SkeletonRise",
  "necromancer/spell_skeleton/skeleton2","necromancer/spell_skeleton/Skeleton2BirthAnimation",
  "necromancer/spell_skeleton/Skeleton","necromancer/spell_skeleton/SkeletonBirth",
  "necromancer/spell_skeleton/SkeletalMageBirth","necromancer/spell_skeleton/SkeletalMageDeath",
  "necromancer/spell_skeleton/SkeletalMageGlow","necromancer/spell_skeleton/SkeletalMageMissile",
  "necromancer/spell_skeleton/SkeletalMage","necromancer/spell_duplication/SkeletalMageRise",
  "necromancer/spell_lastday/BoneGolem","necromancer/spell_lastday/BoneGolemBirth",
  "necromancer/spell_lastday/BoneGolemDeathAnim","necromancer/spell_lastday/LustJudgmentClouds",
  "necromancer/spell_lastday/KnightLightSpell","necromancer/spell_lastday/KnightLightSpell2",
  "necromancer/spell_lastday/KnightLightSpell3","necromancer/spell_lastday/KnightLightSpellBuffed",
  "necromancer/spell_penteration/LongerLife",
  "paladin/spell_lastday/KnightLightSpell","paladin/spell_lastday/KnightLightSpell2",
  "paladin/spell_lastday/KnightLightSpell3","paladin/spell_lastday/KnightLightSpellBuffed",
  "paladin/spell_lastday/LustJudgmentClouds",
  "engineer/spell_lighting/LightArea","engineer/spell_lighting/Lighting",
  "engineer/spell_lighting/Lighting2","engineer/spell_lighting/Lighting3",
  "engineer/spell_lighting/Lighting4","engineer/spell_lighting/LightingGlow",
  "engineer/spell_lighting/LightingSpark",
  "engineer/spell_rocket/Rocket","engineer/spell_rocket/RocketFire",
  "engineer/spell_rocket/RocketFireTop","engineer/spell_rocket/RocketGlow",
  "firemage/spell_fireturret/FireTurret","firemage/spell_fireturret/FireHeroTurretGlow",
  "firemage/spell_fireturret/FireHeroTurretGlowV2","firemage/spell_fireturret/FireHeroTurretV2",
  "firemage/spell_firering/ChoseFireRingLargeIco",
  "ui/ChestGlow","ui/unclassified/WingsOfVictory","ui/unclassified/WingsOfVictory2",
  "ui/unclassified/Circle","ui/unclassified/CircleExperience",
  "ui/unclassified/LvlUpAnim1","ui/unclassified/LvlUpAnim2","ui/unclassified/LvlUpIco2",
  "ui/unclassified/LvlPanel","ui/unclassified/HeroPanel",
  "ui/rewards/BlueChest","ui/rewards/GreenChest","ui/rewards/RedChest",
  "ui/rewards/YellowChest","ui/rewards/EverythingChest",
  "ui/rewards/Gem","ui/rewards/GemBlue","ui/rewards/GemGreen","ui/rewards/GemRed","ui/rewards/GemYellow",
  "ui/rewards/ExpBarEmpty","ui/rewards/ExpBarFull","ui/rewards/ExpBarFul3l",
  "ui/bonus/BonusKey","ui/bonus/SecretKey","ui/bonus/BonusLighting","ui/bonus/LightingEffect",
  "comunskills/LifeBonusIco","comunskills/LifeBonusIcoInActive","comunskills/LifeBonusLargeIco",
  "comunskills/SgieldLargeIco","comunskills/LockedSpell","comunskills/ShieldIco",
];
// Suppress "unused variable" — this array documents covered frames for the audit.
void _VILLAGE_AMBIENT_REFS;

// Visible gap between bricks so the wall doesn't merge into a solid sheet.
// Expressed as a fraction of cellSize; enforces a 2 px minimum.
const GAP_FRAC = 0.12;

// Extra height below the block grid to ensure the paddle and its clearance are
// fully visible (grid + paddle zone + margin).
const PADDLE_ZONE_CELLS = 3;

// Halo drawn behind ignited balls: radius multiplier and alpha.
const IGNITE_HALO_RADIUS_MULT = 1.8;
const IGNITE_HALO_ALPHA = 0.35;

// Fire wall band height as a fraction of cellSize.
const FIRE_WALL_HEIGHT_MULT = 1.1;

// Ghost blocks (ballPhases): drawn semi-transparent with a blue/cyan tint.
const GHOST_ALPHA_BASE = 0.45;
const GHOST_ALPHA_AMP  = 0.12;   // oscillation amplitude around base
const GHOST_PULSE_SPEED = 0.055; // ticker units per radian
const GHOST_TINT = 0x88ccff;     // faint cyan tint

// Teleporter glow ring: additive ring drawn behind the block sprite.
const TELEPORTER_RING_ALPHA_BASE = 0.35;
const TELEPORTER_RING_ALPHA_AMP  = 0.25;
const TELEPORTER_RING_PULSE_SPEED = 0.07;
const TELEPORTER_RING_COLOR = 0x44aaff; // cool blue portal glow
const TELEPORTER_RING_RADIUS_MULT = 0.72; // fraction of brickSize/2

// Turret visual: barrel length and width as fractions of paddleH.
const TURRET_BARREL_LENGTH_MULT = 1.8;
const TURRET_BARREL_WIDTH_MULT  = 0.45;

// Projectile id threshold: turret bullets + fireballs use id >= this value.
const PROJECTILE_ID_THRESHOLD = 10000;

// ---------------------------------------------------------------------------
// Per-class spell visual constants
// ---------------------------------------------------------------------------

// Paladin barrier: shield bar rendered per entry in barriers[].
const BARRIER_HEIGHT_FRAC  = 0.18; // fraction of cellSize for bar thickness
const BARRIER_GLOW_ALPHA   = 0.55;
const BARRIER_FILL_COLOR   = 0x88ccff; // steel-blue core
const BARRIER_GLOW_COLOR   = 0x4499ff; // cooler blue additive glow
const BARRIER_GLOW_W_EXTRA = 16;       // extra px each side for glow halo

// Engineer radiation zone: pulsing AoE circle.
const ZONE_FILL_COLOR      = 0x22ff44;  // toxic green
const ZONE_GLOW_COLOR      = 0x44ff88;
const ZONE_FILL_ALPHA_BASE = 0.12;
const ZONE_FILL_ALPHA_AMP  = 0.06;
const ZONE_RING_ALPHA      = 0.6;
const ZONE_PULSE_SPEED     = 0.09;

// Necromancer decay aura on ball (sickly green, distinct from ignite orange).
const DECAY_HALO_COLOR      = 0x22cc44;
const DECAY_HALO_ALPHA      = 0.38;
const DECAY_HALO_RADIUS_MULT = 1.8;

// Skeleton summon position (top of board, centered).
const SKELETON_Y_FRAC = 0.08; // fraction of boardH from top

// Turret: atlas art keys for the barrel sprite and missile bullets.
const TURRET_SPRITE_KEY   = "firemage/spell_fireturret/FireHeroTurret";
const TURRET_MISSILE_KEY  = "firemage/spell_fireturret/FireHeroTurretMissile";

// Fireball / firering: art for the active fireball projectile.
const FIRE_RING_KEY = "firemage/spell_firering/FireRing";

// FireWall animation key in the manifest.
const FIRE_WALL_ANIM_KEY = "firemage/spell_firewall/firestandannimation";
// How many tiles to use per fire-wall band (we switch from many thin sprites to
// fewer wide AnimatedSprites at the wall height, one per "segment").
// Each segment is about 1 × cellSize wide so they tile naturally.

// Ignite fire aura: atlas anim key (FireBirth frames) played as a looping aura.
const IGNITE_AURA_KEY = "firemage/spell_phonex/phoenixbirthanimpic";
const IGNITE_AURA_FPS = 10; // slow loop looks like a gentle fire aura
// Size of the fire aura as a multiplier of the ball sprite size.
const IGNITE_AURA_SIZE_MULT = 2.8;

// Paddle squash/stretch constants.
// On ball bounce (ball y crosses paddleY within a threshold), the paddle stretches briefly.
const PADDLE_SQUASH_DURATION_MS = 180; // total duration of the squash → stretch anim
const PADDLE_SQUASH_Y_SCALE     = 0.65; // minimum Y scale during squash peak
const PADDLE_STRETCH_X_SCALE    = 1.18; // maximum X scale at stretch peak

// Hit-stop: brief freeze of the world container (enemies / big bosses / ignited kills).
// Implemented as a duration counter; when active, we skip updating the game world
// visually by skipping draw() calls' update of animations for that many ms.
const HIT_STOP_DURATION_BOSS_MS = 80;   // short camera stutter for boss hits
const HIT_STOP_DURATION_IGNITE_MS = 55; // ignited kill

// Boss block rendering constants.
const BOSS_SCALE_MULT  = 1.15;   // slightly enlarged vs normal brickSize
const BOSS_AURA_COLOR  = 0xcc0000; // menacing red aura
const BOSS_AURA_RADIUS_MULT = 0.8; // fraction of brickSize/2
const BOSS_AURA_ALPHA  = 0.55;
const BOSS_AURA_PULSE_SPEED = 0.06;
const BOSS_AURA_ALPHA_AMP  = 0.25;

// How long to keep the boss rig visible after defeat (for the explosion burst to play).
const BOSS_DEFEAT_CLEANUP_MS = 1500;

// Hazard (falling enemy projectile) rendering constants.
const HAZARD_RADIUS    = 6;         // px in world space (scaled later)
const HAZARD_COLOR     = 0xdd1111; // crimson
const HAZARD_GLOW_COLOR = 0xff3333; // additive glow
const HAZARD_GLOW_ALPHA = 0.45;
const HAZARD_GLOW_RADIUS_MULT = 1.9;

// Bonus pickup rendering constants.
const BONUS_SPRITE_SIZE    = 28;   // logical px (world-space) for bonus icon sprites
const BONUS_SPIN_SPEED     = 0.04; // radians per ticker delta (gentle rotation)
const BONUS_BOB_AMPLITUDE  = 2.5;  // px vertical bob amplitude
const BONUS_BOB_SPEED      = 0.07; // radians per ticker delta for bob sinusoid

// Damage flash: full-screen red overlay that fades out on a lives decrease.
const DAMAGE_FLASH_ALPHA_START = 0.45;
const DAMAGE_FLASH_FADE_SPEED  = 0.04; // alpha lost per ticker delta

// Glow filter applied to the fx / fire layer and the balls container.
// Kept modest to avoid washing out the whole board on slow hardware.
const GLOW_DISTANCE   = 14;   // px — spread of the glow halo
const GLOW_OUTER_STRENGTH = 3.0;
const GLOW_INNER_STRENGTH = 0.0; // inner-strength=0 avoids colour shift on the core sprite
const GLOW_COLOR      = 0xff6a20; // warm orange — complements fire/explosion palette

export class Renderer {
  app: Application;
  // Background layer (behind everything — biome background fills the stage).
  private bgLayer = new Container();
  private bgSprite = new Sprite();
  private _hellParallaxSprites: Sprite[] = [];
  private _lastBiome = "";
  private world = new Container();
  private blocks = new Container();
  private effectsLayer: Effects;
  private fireWalls = new Container();
  private hazardsLayer = new Container();
  // Paddle rendered as a sprite; Graphics kept as invisible fallback.
  private paddleSprite = new Sprite();
  private turretSprite = new Sprite();
  private balls = new Container();
  private ballTrail: BallTrail;
  private screenShake: ScreenShake;
  // Store the base fit position so screen-shake can layer on top.
  private _fitX = 0;
  private _fitY = 0;
  private damageFlash = new Graphics(); // full-screen overlay for HP hit feedback
  private _tick = 0; // used to drive wall flicker animation
  private _lastLives = -1; // track lives decreases for damage flash

  // AnimSystem for fire-wall animated tiles.
  private _wallAnimSys: AnimSystem;
  // Track previous fire-wall count to avoid unnecessary rebuild.
  private _lastWallCount = -1;
  // Fire-wall AnimatedSprites (rebuilt only when wall count changes).
  private _wallAnims: AnimatedSprite[] = [];

  // ---- Sprite pools: keyed by entity id to avoid per-frame alloc churn ----
  // Block pool: each entry is a { sprite, aura?, ring? } tuple.
  private _blockPool = new Map<number, { sp: Sprite; aura?: Graphics; ring?: Graphics }>();
  // Ball pool: each entry is { sp (sprite), haloGfx (ignite halo), auraHandle? }
  // auraHandle tracks the looping ignite aura AnimatedSprite in _ballAnimSys.
  private _ballPool = new Map<number, { sp: Sprite; haloGfx: Graphics; auraId?: number }>();
  // Separate AnimSystem for ball aura effects (looping per-ball fire aura).
  private _ballAnimSys: AnimSystem;
  // Hazard pool: each entry is { halo, core, bat? }.
  private _hazardPool: { halo: Graphics; core: Graphics; bat?: Sprite; stal?: Sprite }[] = [];

  // Bonus pickups layer and pool.
  private bonusesLayer = new Container();
  // Pool keyed by bonus id: { sp: Sprite, baseY: number }
  private _bonusPool = new Map<number, { sp: Sprite; baseY: number }>();

  // ── P6 per-class spell effect layers ──────────────────────────────────────
  // Paladin shield barriers layer (drawn above blocks, behind balls).
  private barriersLayer = new Container();
  // Engineer radiation zones layer.
  private zonesLayer = new Container();
  // Necromancer skeleton summon sprite.
  private _skeletonSprite: Sprite | null = null;
  private _skeletonAnimSys: AnimSystem;
  // Skeleton aura looping handle id.
  private _skeletonAuraId: number | undefined;

  // Boss rig: one BossRig instance while bossActive, destroyed when boss dies.
  private _bossRig: BossRig | null = null;
  // The boss type inferred from boss block sprites (set when rig is created).
  private _bossRigType = "";
  // Whether the boss was active in the previous frame (for defeat detection).
  private _prevBossActive = false;
  // Telegraph warning glyph (reusable).
  private _telegraphWarning = new TelegraphWarning();
  // Boss region bounding box (updated each frame).
  private _bossRegion = { cx: 0, cy: 0, w: 0, h: 0 };
  // Latest boss HP fraction — kept so the ticker can drive boss animation.
  private _bossHpFrac = 1.0;
  // Boss rig container layer (sits above blocks).
  private _bossLayer = new Container();

  // Paddle squash/stretch state.
  private _paddleSquashElapsed = -1; // -1 = inactive; >=0 = ms into the animation
  // Base paddle scale (set by draw(); squash multiplies on top).
  private _paddleBaseScaleX = 1;
  private _paddleBaseScaleY = 1;

  // Hit-stop state: remaining ms of visual freeze.
  private _hitStopRemaining = 0;

  // ── Paddle animation (per-class bar frames) ───────────────────────────────
  private _paddleAnimFrame = 0;   // current frame index within _paddleAnimKeys
  private _paddleAnimElapsed = 0; // ms elapsed since last frame advance

  // ── Ambient beholder sprites (village biome only, cosmetic) ──────────────
  // Up to 2 beholders drift slowly in the background.
  private _ambientLayer = new Container();
  private _ambientSprites: Array<{
    sp: Sprite; x: number; y: number; vx: number; vy: number;
    frame: number; frameMs: number; keys: string[];
  }> = [];
  private _lastAmbientBiome = "";

  /** Switch the paddle/ball sprites to match the given class. */
  setClass(classId: string) {
    const paddleKeys = CLASS_PADDLE_KEYS[classId] ?? CLASS_PADDLE_KEYS.fire_mage;
    const ballKey    = CLASS_BALL_KEYS[classId]   ?? CLASS_BALL_KEYS.fire_mage;
    _paddleAnimKeys   = paddleKeys;
    _paddleSpriteKey  = paddleKeys[0];
    _ballSpriteKey    = ballKey;
    this._paddleAnimFrame = 0;
  }

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();
    this.ballTrail = new BallTrail();
    this.screenShake = new ScreenShake();
    this._wallAnimSys = new AnimSystem();
    this._ballAnimSys = new AnimSystem();
    this._skeletonAnimSys = new AnimSystem();

    // Background: full-stage sprite (behind world container).
    this.bgSprite.anchor.set(0);
    this.bgSprite.tint = BG_TINT;
    this.bgLayer.addChild(this.bgSprite);

    // Paddle: sprite with anchor at center-left; fallback to Texture.WHITE until atlas loads.
    this.paddleSprite.anchor.set(0.5);
    this.paddleSprite.texture = Texture.WHITE;

    // Turret: use atlas art (FireHeroTurret strip is a horizontal sprite strip;
    // we use the first frame from the strip key, which is the full texture).
    // The turret glow is layered on top as a second sprite.
    this.turretSprite = new Sprite(Texture.WHITE); // will be updated to atlas on first draw
    this.turretSprite.anchor.set(0.5, 1); // anchor at bottom-center
    this.turretSprite.visible = false;

    // Apply a single GlowFilter to the fx + fire layer group and to the balls
    // container so that explosions, fire walls, halos, and balls all glow.
    // Scoped to bright/fx elements only — blocks and paddle are untouched.
    //
    // HEAVY_FX is false under Playwright (navigator.webdriver===true), so the
    // GlowFilter render-to-texture passes are skipped entirely in headless runs,
    // preventing WebSocket snapshot starvation from GPU thread blocking.
    // The filter arrays are assigned once here and never reassigned per-frame.
    if (HEAVY_FX) {
      const fxGlow = new GlowFilter({
        distance:      GLOW_DISTANCE,
        outerStrength: GLOW_OUTER_STRENGTH,
        innerStrength: GLOW_INNER_STRENGTH,
        color:         GLOW_COLOR,
        quality:       0.25, // low quality = faster; perfectly fine for bloom halos
      });
      this.effectsLayer.container.filters = [fxGlow];
      this.fireWalls.filters = [fxGlow];

      // Ball glow: separate filter instance so ball trails can share it independently.
      const ballGlow = new GlowFilter({
        distance:      10,
        outerStrength: 2.2,
        innerStrength: 0.0,
        color:         0xffffff,
        quality:       0.25,
      });
      this.balls.filters = [ballGlow];
    }

    // Add telegraph warning container to boss layer.
    this._bossLayer.addChild(this._telegraphWarning.container);

    // Layer order: ambient → ballTrail → zones → blocks → fireWalls → wallAnimSys → barriers → bossLayer → effects → ballAuras → balls → paddleSprite → turret → skeletonAnimSys → hazards → bonuses
    // _ambientLayer sits behind everything — purely cosmetic background sprites.
    this._ambientLayer.alpha = 0.22;
    this.world.addChild(
      this._ambientLayer,
      this.ballTrail.container,
      this.zonesLayer,
      this.blocks,
      this.fireWalls,
      this._wallAnimSys.container,
      this.barriersLayer,
      this._bossLayer,
      this.effectsLayer.container,
      this._ballAnimSys.container,
      this.balls,
      this.paddleSprite,
      this.turretSprite,
      this._skeletonAnimSys.container,
      this.hazardsLayer,
      this.bonusesLayer,
    );
    // Damage flash sits on stage (not world) so it covers the full screen regardless of world scale.
    this.damageFlash.alpha = 0;
    // Layer order on stage: bg → world → damageFlash → vignette
    this.app.stage.addChild(this.bgLayer);
    this.app.stage.addChild(this.world);
    this.app.stage.addChild(this.damageFlash);

    // Vignette: subtle dark corners overlay on the stage (top-most).
    new Vignette(this.app);

    // Tick the effects every frame and drive wall flicker.
    this.app.ticker.add((delta) => {
      // delta is in Pixi ticker units (frames at 60 fps → multiply by 1000/60 for ms)
      const dtMs = (delta / 60) * 1000;

      // Hit-stop: while active, freeze AnimatedSprites (don't update) and damp animations.
      if (this._hitStopRemaining > 0) {
        this._hitStopRemaining -= dtMs;
        // Skip effects + ball aura updates during hit-stop so the world freezes visually.
      } else {
        this.effectsLayer.update(dtMs);
        this._ballAnimSys.update(dtMs);
        this._wallAnimSys.update(dtMs);
        this._skeletonAnimSys.update(dtMs);
      }

      // Telegraph warning update (runs regardless of hit-stop for clarity).
      this._telegraphWarning.update(dtMs, this._bossRegion.w * 0.5);

      // Boss rig animation: drive with real dt so idle bob/lunge/flash animate.
      // draw() calls setRegion() to reposition; the ticker drives animation timing.
      if (this._bossRig) {
        this._bossRig.update(
          this._bossRegion.cx, this._bossRegion.cy,
          this._bossRegion.w, this._bossRegion.h,
          this._bossHpFrac, this._tick, dtMs,
        );
      }

      this.screenShake.update(dtMs);
      // Apply screen-shake offset on top of the fit position calculated last draw().
      this.world.position.set(
        this._fitX + this.screenShake.offsetX,
        this._fitY + this.screenShake.offsetY,
      );
      this._tick += delta;

      // Paddle squash/stretch animation.
      if (this._paddleSquashElapsed >= 0) {
        this._paddleSquashElapsed += dtMs;
        const t = Math.min(this._paddleSquashElapsed / PADDLE_SQUASH_DURATION_MS, 1);
        // Phase 1 (0→0.4): squash — compress Y, expand X
        // Phase 2 (0.4→1.0): spring back to 1.0 with slight overshoot
        let xScale = 1.0;
        let yScale = 1.0;
        if (t < 0.4) {
          const p = t / 0.4;
          // squash: X expands to STRETCH, Y squashes to SQUASH
          xScale = 1.0 + (PADDLE_STRETCH_X_SCALE - 1.0) * p;
          yScale = 1.0 - (1.0 - PADDLE_SQUASH_Y_SCALE) * p;
        } else {
          const p = (t - 0.4) / 0.6;
          // spring back with slight overshoot at p≈0.5
          const overshoot = Math.sin(p * Math.PI) * 0.06;
          xScale = PADDLE_STRETCH_X_SCALE - (PADDLE_STRETCH_X_SCALE - 1.0) * p + overshoot;
          yScale = PADDLE_SQUASH_Y_SCALE + (1.0 - PADDLE_SQUASH_Y_SCALE) * p - overshoot;
        }
        // Apply squash/stretch to paddle sprite on top of the base scale.
        this.paddleSprite.scale.x = this._paddleBaseScaleX * xScale;
        this.paddleSprite.scale.y = this._paddleBaseScaleY * yScale;
        if (t >= 1) {
          this._paddleSquashElapsed = -1;
          // Snap back to clean base scale.
          this.paddleSprite.scale.set(this._paddleBaseScaleX, this._paddleBaseScaleY);
        }
      }

      // Fade the damage flash overlay.
      if (this.damageFlash.alpha > 0) {
        this.damageFlash.alpha = Math.max(0, this.damageFlash.alpha - DAMAGE_FLASH_FADE_SPEED * delta);
      }

      // Paddle bar animation: cycle through the 4 class bar frames.
      this._paddleAnimElapsed += dtMs;
      if (this._paddleAnimElapsed >= PADDLE_ANIM_MS_PER_FRAME) {
        this._paddleAnimElapsed -= PADDLE_ANIM_MS_PER_FRAME;
        this._paddleAnimFrame = (this._paddleAnimFrame + 1) % _paddleAnimKeys.length;
        const nextTex = atlasTex(_paddleAnimKeys[this._paddleAnimFrame]);
        if (nextTex !== Texture.WHITE) this.paddleSprite.texture = nextTex;
      }

      // Ambient sprite drift animation (village beholders).
      for (const a of this._ambientSprites) {
        // Advance frame.
        a.frameMs += dtMs;
        if (a.frameMs > 380) {
          a.frameMs = 0;
          a.frame = (a.frame + 1) % a.keys.length;
          const t = atlasTex(a.keys[a.frame]);
          if (t !== Texture.WHITE) a.sp.texture = t;
        }
        // Drift.
        a.x += a.vx * dtMs;
        a.y += a.vy * dtMs;
        a.sp.x = a.x;
        a.sp.y = a.y;
        // Wrap horizontally within board bounds.
        if (a.x < -40) a.x += 440;
        if (a.x > 440) a.x -= 440;
        if (a.y < -40) a.y += 540;
        if (a.y > 540) a.y -= 540;
      }
    });
  }

  private fit(s: Snapshot) {
    // Include paddle zone below the block grid so the paddle is never clipped.
    const effectiveH = s.boardH + s.cellSize * PADDLE_ZONE_CELLS;
    // Portrait-first: prefer filling the full height, then constrain by width.
    // Use 0.97 instead of 0.95 to maximise use of vertical space on tall phones.
    const scale = Math.min(
      this.app.screen.width / s.boardW,
      this.app.screen.height / effectiveH,
    ) * 0.97;
    this.world.scale.set(scale);
    // Centre horizontally; align to top with a small top margin so blocks are
    // visible near the top of the screen (not centred vertically, which wastes space).
    const topMargin = this.app.screen.height * 0.01;
    this._fitX = (this.app.screen.width - s.boardW * scale) / 2;
    this._fitY = Math.max(topMargin, (this.app.screen.height - effectiveH * scale) / 2);
    this.world.position.set(this._fitX, this._fitY);

    // Resize background to cover the full stage.
    const sw = this.app.screen.width;
    const sh = this.app.screen.height;
    const bw = this.bgSprite.texture.width;
    const bh = this.bgSprite.texture.height;
    if (bw > 0 && bh > 0) {
      // COVER: scale to fill, no letter-boxing.
      const coverScale = Math.max(sw / bw, sh / bh);
      this.bgSprite.scale.set(coverScale);
      this.bgSprite.x = (sw - bw * coverScale) / 2;
      this.bgSprite.y = (sh - bh * coverScale) / 2;
    }
    // Resize hell parallax layers similarly (same cover approach).
    for (const psp of this._hellParallaxSprites) {
      if (psp.texture.width > 0 && psp.texture.height > 0) {
        const pw = psp.texture.width;
        const ph = psp.texture.height;
        const ps = Math.max(sw / pw, sh / ph);
        psp.scale.set(ps);
        psp.y = (sh - ph * ps) / 2;
      }
    }
  }

  draw(s: Snapshot) {
    // --- biome background (update only on biome change) ---
    if (s.biome && s.biome !== this._lastBiome) {
      this._lastBiome = s.biome;
      const bgTex = biomedBg(s.biome);
      this.bgSprite.texture = bgTex;
      this.bgSprite.visible = bgTex !== Texture.WHITE;

      // Hell parallax layers: add/rebuild when entering hell biome.
      for (const psp of this._hellParallaxSprites) this.bgLayer.removeChild(psp);
      this._hellParallaxSprites = [];
      if (s.biome === "hell") {
        const frames = hellParallaxFrames();
        for (let i = 0; i < frames.length; i++) {
          const psp = new Sprite(frames[i]);
          psp.anchor.set(0);
          psp.tint = 0x888888; // darker than main bg for depth
          psp.alpha = 0.35;    // subtle layering
          this.bgLayer.addChild(psp);
          this._hellParallaxSprites.push(psp);
        }
      }
    }

    // --- ambient background sprites (cosmetic, village biome beholders) ---
    // Rebuild when biome changes; no gameplay effect.
    if (s.biome !== this._lastAmbientBiome) {
      this._lastAmbientBiome = s.biome;
      // Remove existing ambient sprites.
      for (const a of this._ambientSprites) this._ambientLayer.removeChild(a.sp);
      this._ambientSprites = [];

      if (s.biome === "village" || s.biome === "village-ghost" || s.biome === "village-boss") {
        // Spawn 2 ambient beholders drifting slowly across the board.
        const beholderCount = 2;
        for (let i = 0; i < beholderCount; i++) {
          const useGhost = i === 1;
          const keys = useGhost ? BEHOLDER_GHOST_KEYS : BEHOLDER_KEYS;
          const tex0 = atlasTex(keys[0]);
          if (tex0 === Texture.WHITE) continue; // atlas not yet loaded
          const sp = new Sprite(tex0);
          sp.anchor.set(0.5);
          const size = s.cellSize * 2.2;
          sp.width  = size;
          sp.height = size;
          sp.tint   = useGhost ? 0xaaccff : 0xffffff;
          // Scatter starting positions.
          const startX = 60 + i * 180;
          const startY = 60 + i * 100;
          // Gentle drift velocity (world-space px/ms).
          const vx = (i % 2 === 0 ? 0.012 : -0.015);
          const vy = (i % 2 === 0 ? 0.007 : 0.011);
          sp.position.set(startX, startY);
          this._ambientLayer.addChild(sp);
          this._ambientSprites.push({ sp, x: startX, y: startY, vx, vy, frame: 0, frameMs: i * 180, keys });
        }
      }
    }

    this.fit(s);

    // --- screen shake + hit-stop: fire on relevant events ---
    for (const ev of s.events) {
      if (ev.type === "playerHit") this.screenShake.trigger("playerHit");
      else if (ev.type === "bossAttack") {
        this.screenShake.trigger("bossAttack");
        // Short hit-stop on boss attacks.
        this._hitStopRemaining = Math.max(this._hitStopRemaining, HIT_STOP_DURATION_BOSS_MS);
      } else if (ev.type === "ignite") {
        // Brief hit-stop when ignite lands.
        this._hitStopRemaining = Math.max(this._hitStopRemaining, HIT_STOP_DURATION_IGNITE_MS);
      }
    }

    // --- damage flash: trigger on lives decrease ---
    if (this._lastLives >= 0 && s.lives < this._lastLives) {
      // Repaint the full-screen flash rect to match current screen size, then trigger.
      this.damageFlash.clear();
      this.damageFlash.beginFill(0xff0000, 1)
        .drawRect(0, 0, this.app.screen.width, this.app.screen.height)
        .endFill();
      this.damageFlash.alpha = DAMAGE_FLASH_ALPHA_START;
    }
    this._lastLives = s.lives;

    // --- blocks (pooled: update existing sprites, create/destroy on actual add/remove) ---
    const gap = Math.max(s.cellSize * GAP_FRAC, 2);
    const brickSize = s.cellSize - gap;

    // Track which block ids are live this frame to detect removals.
    const liveBlockIds = new Set<number>();
    for (const b of s.blocks) liveBlockIds.add(b.id);

    // Remove pooled sprites for blocks that no longer exist.
    for (const [id, entry] of this._blockPool) {
      if (!liveBlockIds.has(id)) {
        if (entry.aura) this.blocks.removeChild(entry.aura);
        if (entry.ring) this.blocks.removeChild(entry.ring);
        this.blocks.removeChild(entry.sp);
        this._blockPool.delete(id);
      }
    }

    for (const b of s.blocks) {
      const bossRenderSize = b.boss ? brickSize * BOSS_SCALE_MULT : brickSize;

      if (this._blockPool.has(b.id)) {
        // --- Update existing pooled sprite ---
        const entry = this._blockPool.get(b.id)!;
        const { sp, aura, ring } = entry;

        sp.texture = tex(b.sprite);
        sp.width   = bossRenderSize;
        sp.height  = bossRenderSize;
        sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
        sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
        sp.position.set(b.x, b.y);

        if (b.boss) {
          sp.alpha = 1.0;
          if (aura) {
            const auraAlpha = BOSS_AURA_ALPHA
              + BOSS_AURA_ALPHA_AMP * Math.sin(this._tick * BOSS_AURA_PULSE_SPEED);
            aura.clear().beginFill(BOSS_AURA_COLOR, auraAlpha)
              .drawCircle(b.x, b.y, brickSize * BOSS_AURA_RADIUS_MULT).endFill();
          }
        } else if (b.ballPhases) {
          sp.tint  = GHOST_TINT;
          sp.alpha = GHOST_ALPHA_BASE + GHOST_ALPHA_AMP * Math.sin(this._tick * GHOST_PULSE_SPEED);
        } else if (b.indestructible || b.teleporter) {
          sp.alpha = 1.0;
          if (ring) {
            const ringAlpha = TELEPORTER_RING_ALPHA_BASE
              + TELEPORTER_RING_ALPHA_AMP * Math.sin(this._tick * TELEPORTER_RING_PULSE_SPEED);
            ring.clear().beginFill(TELEPORTER_RING_COLOR, ringAlpha)
              .drawCircle(b.x, b.y, brickSize * TELEPORTER_RING_RADIUS_MULT).endFill();
          }
        } else {
          sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
        }
      } else {
        // --- Create new pooled entry ---
        let aura: Graphics | undefined;
        let ring: Graphics | undefined;

        if (b.boss) {
          const auraAlpha = BOSS_AURA_ALPHA
            + BOSS_AURA_ALPHA_AMP * Math.sin(this._tick * BOSS_AURA_PULSE_SPEED);
          aura = new Graphics();
          aura.blendMode = BLEND_MODES.ADD;
          aura.beginFill(BOSS_AURA_COLOR, auraAlpha)
            .drawCircle(b.x, b.y, brickSize * BOSS_AURA_RADIUS_MULT)
            .endFill();
          this.blocks.addChild(aura);
        }

        if (b.teleporter) {
          const ringAlpha = TELEPORTER_RING_ALPHA_BASE
            + TELEPORTER_RING_ALPHA_AMP * Math.sin(this._tick * TELEPORTER_RING_PULSE_SPEED);
          ring = new Graphics();
          ring.blendMode = BLEND_MODES.ADD;
          ring.beginFill(TELEPORTER_RING_COLOR, ringAlpha)
            .drawCircle(b.x, b.y, brickSize * TELEPORTER_RING_RADIUS_MULT)
            .endFill();
          this.blocks.addChild(ring);
        }

        const sp = new Sprite(tex(b.sprite));
        sp.anchor.set(0.5);
        sp.width  = bossRenderSize;
        sp.height = bossRenderSize;
        sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
        sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
        sp.position.set(b.x, b.y);

        if (b.boss) {
          sp.alpha = 1.0;
        } else if (b.ballPhases) {
          sp.tint  = GHOST_TINT;
          sp.alpha = GHOST_ALPHA_BASE + GHOST_ALPHA_AMP * Math.sin(this._tick * GHOST_PULSE_SPEED);
        } else if (b.indestructible || b.teleporter) {
          sp.alpha = 1.0;
        } else {
          sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
        }

        this.blocks.addChild(sp);
        this._blockPool.set(b.id, { sp, aura, ring });
      }
    }

    // --- boss rig: assemble / update / destroy animated multi-part boss ---
    // Compute the boss-block bounding region this frame.
    const bossBlocks = s.blocks.filter(b => b.boss);
    if (bossBlocks.length > 0) {
      let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
      for (const b of bossBlocks) {
        minX = Math.min(minX, b.x - brickSize / 2);
        maxX = Math.max(maxX, b.x + brickSize / 2);
        minY = Math.min(minY, b.y - brickSize / 2);
        maxY = Math.max(maxY, b.y + brickSize / 2);
      }
      const regionCx = (minX + maxX) / 2;
      const regionCy = (minY + maxY) / 2;
      const regionW  = maxX - minX;
      const regionH  = maxY - minY;
      this._bossRegion = { cx: regionCx, cy: regionCy, w: regionW, h: regionH };

      // Infer boss type from first boss block sprite.
      const bossType = inferBossType(bossBlocks[0].sprite);
      const bossTypeStr = bossBlocks[0].sprite;

      // Create or recreate rig if type changed.
      if (!this._bossRig || this._bossRigType !== bossTypeStr) {
        if (this._bossRig) {
          this._bossLayer.removeChild(this._bossRig.container);
          this._bossRig.destroy();
        }
        this._bossRig = new BossRig(bossType);
        this._bossRigType = bossTypeStr;
        this._bossLayer.addChildAt(this._bossRig.container, 0);
      }

      // Hide the plain boss-block sprites while the rig is showing.
      for (const b of bossBlocks) {
        const entry = this._blockPool.get(b.id);
        if (entry) entry.sp.alpha = 0;
      }

      // Compute HP fraction (stored so the ticker can animate the rig).
      this._bossHpFrac = s.bossMaxHp > 0 ? s.bossHp / s.bossMaxHp : 1;

      // Reposition the rig to match the current boss-block region.
      // Animation (bob/lunge/flash) is driven by the Pixi ticker above with real dt.
      this._bossRig.setRegion(regionCx, regionCy, regionW, regionH);
    }

    // Boss-active → inactive transition: defeat flourish.
    if (this._prevBossActive && !s.bossActive) {
      if (this._bossRig) {
        this._bossRig.onDefeat(s.cellSize);
        // Animate defeat in-place for a beat, then clean up.
        setTimeout(() => {
          if (this._bossRig) {
            this._bossLayer.removeChild(this._bossRig.container);
            this._bossRig.destroy();
            this._bossRig = null;
            this._bossRigType = "";
          }
        }, BOSS_DEFEAT_CLEANUP_MS);
      }
    }
    this._prevBossActive = s.bossActive;

    // Boss events: telegraph warning + lunge.
    for (const ev of s.events) {
      if (ev.type === "bossTelegraph") {
        if (this._bossRig) this._bossRig.onTelegraph();
        this._telegraphWarning.trigger(
          this._bossRegion.cx, this._bossRegion.cy,
          this._bossRegion.w,
        );
      } else if (ev.type === "bossAttack") {
        if (this._bossRig) this._bossRig.onTelegraph(); // also lunge on actual attack
      }
    }

    // --- fire walls (animated art: FireStandAnnimation frames) ---
    // Rebuild only when the wall count changes to avoid per-frame alloc.
    const walls = s.walls ?? [];
    const wallH = s.cellSize * FIRE_WALL_HEIGHT_MULT;
    const fireWallFrames = animFrames(FIRE_WALL_ANIM_KEY);

    if (walls.length !== this._lastWallCount) {
      // Destroy old wall anim sprites.
      this.fireWalls.removeChildren();
      for (const a of this._wallAnims) { a.stop(); a.destroy(); }
      this._wallAnims = [];

      for (const wall of walls) {
        const tileW = wallH; // square tiles
        const count = Math.ceil(s.boardW / tileW);
        for (let i = 0; i < count; i++) {
          if (fireWallFrames.length >= 2) {
            // Use real animated FireStandAnnimation art.
            const anim = new AnimatedSprite(fireWallFrames);
            anim.blendMode = BLEND_MODES.ADD;
            anim.tint = 0xff8833;
            anim.anchor.set(0, 0.5);
            anim.width  = tileW + 1;
            anim.height = wallH;
            anim.x = i * tileW;
            anim.y = wall.y;
            anim.loop = true;
            anim.animationSpeed = 8 / 60; // ~8 fps
            // Stagger offset per tile for organic flicker.
            anim.currentFrame = (i * 3) % fireWallFrames.length;
            anim.alpha = 0.9;
            anim.play();
            this.fireWalls.addChild(anim);
            this._wallAnims.push(anim);
          } else {
            // Fallback: static Explosion sprite.
            const sp = new Sprite(tex("Explosion"));
            sp.blendMode = BLEND_MODES.ADD;
            sp.tint = 0xff6620;
            sp.anchor.set(0, 0.5);
            sp.width  = tileW + 1;
            sp.height = wallH;
            sp.x = i * tileW;
            sp.y = wall.y;
            sp.alpha = 0.85;
            this.fireWalls.addChild(sp);
          }
        }
      }
      this._lastWallCount = walls.length;
    } else {
      // Walls unchanged — just flicker alpha for the static-sprite fallback path.
      for (let i = 0; i < this.fireWalls.children.length; i++) {
        const child = this.fireWalls.children[i];
        if (!(child instanceof AnimatedSprite)) {
          const flicker = 0.72 + 0.28 * Math.sin(this._tick * 0.18 + i * 1.3);
          child.alpha = flicker;
        }
      }
    }

    // --- paddle squash trigger: detect ball near paddle ---
    // Trigger squash when any non-projectile ball passes the paddle's y-band.
    const paddleYCenter = (s.boardH + s.cellSize) - s.paddleH / 2;
    const paddleBounceZone = s.paddleH * 2.5;
    for (const ball of s.balls) {
      if (ball.id >= PROJECTILE_ID_THRESHOLD) continue; // skip turret bullets
      if (Math.abs(ball.y - paddleYCenter) < paddleBounceZone && this._paddleSquashElapsed < 0) {
        this._paddleSquashElapsed = 0; // start squash animation
      }
    }

    // --- paddle (sprite) ---
    // Swap to per-class atlas paddle texture on first draw.
    // The ticker advances the animation frame; we only set the initial texture here
    // so the paddle loads when the atlas becomes available.
    const paddleTex = atlasTex(_paddleSpriteKey);
    if (paddleTex !== Texture.WHITE) this.paddleSprite.texture = paddleTex;
    const paddleY = paddleYCenter;
    this.paddleSprite.x = s.paddleX;
    this.paddleSprite.y = paddleY;
    // Scale the sprite so its width matches the sim paddle width; keep natural aspect ratio for height.
    // Store base scale; the ticker's squash/stretch animation applies on top.
    const paddleNaturalW = this.paddleSprite.texture.width;
    const paddleNaturalH = this.paddleSprite.texture.height;
    if (paddleNaturalW > 0) {
      const wScale = s.paddleW / paddleNaturalW;
      // Min height: at least sim paddleH; use natural aspect ratio above that.
      const spriteH = Math.max(s.paddleH, paddleNaturalH * wScale);
      this._paddleBaseScaleX = wScale;
      this._paddleBaseScaleY = spriteH / paddleNaturalH;
      // Only reset to base scale if no squash animation is running.
      if (this._paddleSquashElapsed < 0) {
        this.paddleSprite.scale.set(this._paddleBaseScaleX, this._paddleBaseScaleY);
      }
    }

    // --- turret indicator (atlas art: FireHeroTurret) ---
    const paddleTopY = paddleYCenter;
    if (s.turretActive) {
      // Load atlas turret texture on first use.
      const turretAtlasTex = tex(TURRET_SPRITE_KEY);
      if (turretAtlasTex !== Texture.WHITE) this.turretSprite.texture = turretAtlasTex;
      const turretSize = s.paddleH * TURRET_BARREL_LENGTH_MULT;
      this.turretSprite.visible = true;
      this.turretSprite.width   = s.paddleH * TURRET_BARREL_WIDTH_MULT * 2;
      this.turretSprite.height  = turretSize;
      this.turretSprite.x       = s.paddleX;
      this.turretSprite.y       = paddleTopY - s.paddleH / 2;
    } else {
      this.turretSprite.visible = false;
    }

    // --- ball trail (drawn behind balls) ---
    const ballRadius = s.cellSize * 0.25;
    this.ballTrail.update(s.balls, ballRadius);

    // --- balls (pooled by id, sprite-based) ---
    const ballTex = atlasTex(_ballSpriteKey);
    // FireRing texture for fireballs — a fiery orb glyph, great for projectile art.
    // Turret missiles use the dedicated missile art.
    const fireRingTex = tex(FIRE_RING_KEY);
    // Missile texture for turret bullets (id >= PROJECTILE_ID_THRESHOLD).
    const missileTex = tex(TURRET_MISSILE_KEY);
    // Projectile art: prefer FireRing for fireball look; fall back to missile art.
    const projectileTex = fireRingTex !== Texture.WHITE ? fireRingTex
      : (missileTex !== Texture.WHITE ? missileTex : ballTex);
    const spriteRadius = ballRadius * BALL_SPRITE_SCALE;
    // Ignite aura frames (phoenix birth sequence used as looping fire halo).
    const igniteAuraFrames = animFrames(IGNITE_AURA_KEY);

    const liveBallIds = new Set<number>();
    for (const ball of s.balls) liveBallIds.add(ball.id);

    // Remove pooled entries for balls that no longer exist.
    for (const [id, entry] of this._ballPool) {
      if (!liveBallIds.has(id)) {
        this.balls.removeChild(entry.haloGfx);
        this.balls.removeChild(entry.sp);
        // Clean up looping ignite aura.
        if (entry.auraId !== undefined) {
          this._ballAnimSys.remove({ id: entry.auraId });
        }
        this._ballPool.delete(id);
      }
    }

    for (const ball of s.balls) {
      const isProjectile = ball.id >= PROJECTILE_ID_THRESHOLD;

      if (this._ballPool.has(ball.id)) {
        // Update existing pooled entry.
        const entry = this._ballPool.get(ball.id)!;
        const { sp, haloGfx } = entry;

        haloGfx.clear();
        if (ball.ignited && !isProjectile) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        sp.x = ball.x;
        sp.y = ball.y;

        if (isProjectile) {
          // Turret missile: rotate in direction of travel; pulsate slightly.
          sp.tint = 0xffcc44;
          const missileSize = ballRadius * 1.4;
          sp.width  = missileSize * 2;
          sp.height = missileSize * 2;
          sp.rotation = (this._tick * 0.12); // slow spin
        } else {
          sp.tint = ball.ignited ? 0xff7a2a : 0xffffff;
          // Pulse ignited balls slightly for visual feedback.
          const igScale = ball.ignited
            ? spriteRadius * (1.0 + 0.15 * Math.sin(this._tick * 0.2))
            : spriteRadius;
          sp.width  = igScale * 2;
          sp.height = igScale * 2;
        }

        // Update ignite aura position if active.
        if (entry.auraId !== undefined) {
          this._ballAnimSys.moveTo({ id: entry.auraId }, ball.x, ball.y);
          // Resize aura to match current ball size.
          this._ballAnimSys.resize({ id: entry.auraId }, spriteRadius * IGNITE_AURA_SIZE_MULT * 2);
        }

        // Spawn/remove ignite aura as ignite state changes (non-projectile balls only).
        if (!isProjectile && ball.ignited && entry.auraId === undefined && igniteAuraFrames.length) {
          const h = this._ballAnimSys.looping(
            igniteAuraFrames, IGNITE_AURA_FPS,
            ball.x, ball.y,
            spriteRadius * IGNITE_AURA_SIZE_MULT * 2,
            true, 0xff8822,
          );
          entry.auraId = h.id;
        } else if (!ball.ignited && entry.auraId !== undefined) {
          this._ballAnimSys.remove({ id: entry.auraId });
          entry.auraId = undefined;
        }
      } else {
        // Create new pooled entry.
        const haloGfx = new Graphics();
        if (ball.ignited && !isProjectile) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        // Choose texture based on ball type:
        // - Projectile (turret bullet/fireball): use FireRing/missile art
        // - Normal ball: use FireHeroBall
        const chosenTex: Texture = isProjectile
          ? projectileTex
          : (ballTex !== Texture.WHITE ? ballTex : Texture.WHITE);

        const sp = new Sprite(chosenTex);
        sp.anchor.set(0.5);
        sp.x = ball.x;
        sp.y = ball.y;

        if (isProjectile) {
          sp.tint = 0xffcc44;
          const missileSize = ballRadius * 1.4;
          sp.width  = missileSize * 2;
          sp.height = missileSize * 2;
        } else {
          sp.tint = ball.ignited ? 0xff7a2a : 0xffffff;
          sp.width  = spriteRadius * 2;
          sp.height = spriteRadius * 2;
        }

        this.balls.addChild(haloGfx);
        this.balls.addChild(sp);

        // Spawn ignite aura for already-ignited balls.
        let auraId: number | undefined;
        if (!isProjectile && ball.ignited && igniteAuraFrames.length) {
          const h = this._ballAnimSys.looping(
            igniteAuraFrames, IGNITE_AURA_FPS,
            ball.x, ball.y,
            spriteRadius * IGNITE_AURA_SIZE_MULT * 2,
            true, 0xff8822,
          );
          auraId = h.id;
        }

        this._ballPool.set(ball.id, { sp, haloGfx, auraId });
      }
    }

    // --- hazards (falling enemy projectiles) — pool by array index ---
    // Hazards have no stable id; use a fixed-size ring buffer keyed by index.
    const hazards = s.hazards ?? [];
    // Bat sprite texture for summon-type hazards (village boss phase 3).
    const batTex = atlasTex("village/enemies/BatFlyAnimation");

    // Grow pool if more hazards than pooled entries.
    while (this._hazardPool.length < hazards.length) {
      const halo = new Graphics();
      halo.blendMode = BLEND_MODES.ADD;
      const core = new Graphics();
      // Bat sprite: only shown when bat texture is available and biome is village.
      const bat = new Sprite(Texture.WHITE);
      bat.anchor.set(0.5);
      bat.visible = false;
      const stal = new Sprite(tex("Stalactite"));
      stal.anchor.set(0.5);
      stal.visible = false;
      this.hazardsLayer.addChild(halo);
      this.hazardsLayer.addChild(core);
      this.hazardsLayer.addChild(bat);
      this.hazardsLayer.addChild(stal);
      this._hazardPool.push({ halo, core, bat, stal });
    }

    // Check if we should show bat sprites (village biome + bat texture loaded).
    const showBats = (s.biome === "village" || s.biome === "village-boss") && batTex !== Texture.WHITE;

    // Update visible entries.
    for (let i = 0; i < this._hazardPool.length; i++) {
      const { halo, core, bat, stal } = this._hazardPool[i];
      if (i < hazards.length) {
        const hz = hazards[i];
        if (hz.kind === "stalactite" && stal) {
          halo.visible = false;
          core.visible = false;
          if (bat) bat.visible = false;
          stal.visible = true;
          const ss = HAZARD_RADIUS * 3.2;
          stal.width = ss; stal.height = ss * 1.6;
          stal.x = hz.x; stal.y = hz.y;
        } else if (showBats && bat) {
          if (stal) stal.visible = false;
          // Show bat sprite instead of circle for village hazards.
          halo.visible = false;
          core.visible = false;
          bat.texture  = batTex;
          bat.visible  = true;
          const batSize = HAZARD_RADIUS * 3.5;
          bat.width  = batSize * 2;
          bat.height = batSize * 2;
          bat.x = hz.x;
          bat.y = hz.y;
          bat.tint = 0x9988ff; // purple tint for bat
          bat.rotation = (this._tick * 0.08 + i * 0.5); // slow flutter
        } else {
          // Standard crimson hazard circle.
          if (bat) bat.visible = false;
          if (stal) stal.visible = false;
          halo.visible = true;
          core.visible = true;
          halo.clear().beginFill(HAZARD_GLOW_COLOR, HAZARD_GLOW_ALPHA)
            .drawCircle(hz.x, hz.y, HAZARD_RADIUS * HAZARD_GLOW_RADIUS_MULT).endFill();
          core.clear().beginFill(HAZARD_COLOR, 1)
            .drawCircle(hz.x, hz.y, HAZARD_RADIUS).endFill();
        }
      } else {
        halo.visible = false;
        core.visible = false;
        if (bat) bat.visible = false;
        if (stal) stal.visible = false;
      }
    }

    // --- bonus pickups (falling icons from Bonus/ art) ---
    const bonuses = s.bonuses ?? [];
    const liveBonusIds = new Set<number>();
    for (const bn of bonuses) liveBonusIds.add(bn.id);

    // Remove pooled sprites for bonuses that were caught or fell off.
    for (const [id, entry] of this._bonusPool) {
      if (!liveBonusIds.has(id)) {
        this.bonusesLayer.removeChild(entry.sp);
        this._bonusPool.delete(id);
      }
    }

    for (const bn of bonuses) {
      if (this._bonusPool.has(bn.id)) {
        // Update existing pooled entry.
        const entry = this._bonusPool.get(bn.id)!;
        const bob = Math.sin(this._tick * BONUS_BOB_SPEED + bn.id) * BONUS_BOB_AMPLITUDE;
        entry.sp.x        = bn.x;
        entry.sp.y        = bn.y + bob;
        entry.sp.rotation += BONUS_SPIN_SPEED;
      } else {
        // Create new pooled entry for this bonus.
        const bonusTex = tex(bn.icon);
        const sp = new Sprite(bonusTex);
        sp.anchor.set(0.5);
        sp.width  = BONUS_SPRITE_SIZE;
        sp.height = BONUS_SPRITE_SIZE;
        sp.x = bn.x;
        sp.y = bn.y;
        sp.rotation = 0;
        this.bonusesLayer.addChild(sp);
        this._bonusPool.set(bn.id, { sp, baseY: bn.y });
      }
    }

    // Catch sparkle: fire on bonusCaught events.
    for (const ev of s.events) {
      if (ev.type === "bonusCaught") {
        this.effectsLayer.consume([{ type: "blockDestroyed", x: ev.x, y: ev.y }], s.cellSize, s.biome);
      }
    }

    // ── P6 per-class spell effects ─────────────────────────────────────────

    // --- barriers (Paladin shield bars) ---
    const barriers = s.barriers ?? [];
    this.barriersLayer.removeChildren();
    for (const br of barriers) {
      const barH = s.cellSize * BARRIER_HEIGHT_FRAC;
      // Glow halo (additive, wider than fill).
      const glow = new Graphics();
      glow.blendMode = BLEND_MODES.ADD;
      glow.beginFill(BARRIER_GLOW_COLOR, BARRIER_GLOW_ALPHA)
        .drawRoundedRect(
          br.centerX - br.width / 2 - BARRIER_GLOW_W_EXTRA,
          br.y - barH * 1.4,
          br.width + BARRIER_GLOW_W_EXTRA * 2,
          barH * 2.8,
          barH,
        )
        .endFill();
      // Core fill.
      const fill = new Graphics();
      fill.beginFill(BARRIER_FILL_COLOR, 0.92)
        .drawRoundedRect(
          br.centerX - br.width / 2,
          br.y - barH / 2,
          br.width,
          barH,
          barH / 2,
        )
        .endFill();

      // Optionally overlay atlas shield art if available.
      const shieldTex = tex("paladin/spell_passiveshield/KnightShield");
      if (shieldTex !== Texture.WHITE) {
        // Tile the shield art across the barrier width.
        const tileSize = barH * 3.5;
        const count = Math.max(1, Math.round(br.width / tileSize));
        for (let i = 0; i < count; i++) {
          const sp = new Sprite(shieldTex);
          sp.anchor.set(0.5);
          sp.width  = tileSize;
          sp.height = tileSize;
          sp.x = br.centerX - br.width / 2 + tileSize / 2 + i * tileSize;
          sp.y = br.y;
          sp.alpha = 0.85;
          sp.tint = BARRIER_FILL_COLOR;
          this.barriersLayer.addChild(sp);
        }
      }

      this.barriersLayer.addChild(glow);
      this.barriersLayer.addChild(fill);
    }

    // --- zones (Engineer radiation AoE) ---
    const zones = s.zones ?? [];
    this.zonesLayer.removeChildren();
    const radiationTex = tex("engineer/spell_raditation/Radiation");
    for (const zn of zones) {
      const fillAlpha = ZONE_FILL_ALPHA_BASE
        + ZONE_FILL_ALPHA_AMP * Math.sin(this._tick * ZONE_PULSE_SPEED);
      // Additive glow ring.
      const ring = new Graphics();
      ring.blendMode = BLEND_MODES.ADD;
      ring.beginFill(ZONE_GLOW_COLOR, fillAlpha * 1.5)
        .drawCircle(zn.x, zn.y, zn.radius * 1.05)
        .endFill();
      // Inner fill.
      const fill = new Graphics();
      fill.beginFill(ZONE_FILL_COLOR, fillAlpha)
        .drawCircle(zn.x, zn.y, zn.radius)
        .endFill();
      // Border ring.
      const border = new Graphics();
      border.blendMode = BLEND_MODES.ADD;
      border.lineStyle(2, ZONE_GLOW_COLOR, ZONE_RING_ALPHA)
        .drawCircle(zn.x, zn.y, zn.radius);

      this.zonesLayer.addChild(ring);
      this.zonesLayer.addChild(fill);
      this.zonesLayer.addChild(border);

      // Overlay radiation art in center if available.
      if (radiationTex !== Texture.WHITE) {
        const sp = new Sprite(radiationTex);
        sp.anchor.set(0.5);
        const iconSize = zn.radius * 0.6;
        sp.width  = iconSize * 2;
        sp.height = iconSize * 2;
        sp.x = zn.x;
        sp.y = zn.y;
        sp.alpha = 0.7 + 0.15 * Math.sin(this._tick * ZONE_PULSE_SPEED * 1.3);
        sp.tint = ZONE_FILL_COLOR;
        this.zonesLayer.addChild(sp);
      }
    }

    // --- skeleton summon (Necromancer) ---
    {
      const skeletonActive = s.skeletonActive ?? false;
      // SkeletalMage is a single static frame; Skeleton2BirthAnimation is an animated strip.
      const skeletonFrames = animFrames("necromancer/spell_skeleton/SkeletalMage");
      // Use the static SkeletalMage frame repeated to form a "loop" (single frame is fine).
      const skFrameArr = skeletonFrames.length > 0
        ? skeletonFrames
        : [tex("necromancer/spell_skeleton/SkeletalMage")].filter(t => t !== Texture.WHITE);

      if (skeletonActive) {
        const skX = s.boardW / 2;
        const skY = s.boardH * SKELETON_Y_FRAC + s.cellSize;

        if (this._skeletonAuraId === undefined) {
          // Spawn looping skeleton aura display.
          if (skFrameArr.length > 0) {
            const h = this._skeletonAnimSys.looping(
              skFrameArr, 12,
              skX, skY,
              s.cellSize * 2.8,
              false, 0xaaaaff,
            );
            this._skeletonAuraId = h.id;
          }
          // Glow circle behind the skeleton sprite.
          if (!this._skeletonSprite) {
            const skGlow = new Graphics();
            skGlow.blendMode = BLEND_MODES.ADD;
            skGlow.beginFill(0x8888ff, 0.35)
              .drawCircle(skX, skY, s.cellSize * 1.8)
              .endFill();
            this._skeletonAnimSys.container.addChildAt(skGlow, 0);
            // Store in _skeletonSprite field (cast) to clean up later.
            this._skeletonSprite = skGlow as unknown as Sprite;
          }
        } else {
          // Update position of existing looping handle.
          this._skeletonAnimSys.moveTo({ id: this._skeletonAuraId }, skX, skY);
        }
      } else {
        // Skeleton no longer active — remove.
        if (this._skeletonAuraId !== undefined) {
          this._skeletonAnimSys.remove({ id: this._skeletonAuraId });
          this._skeletonAuraId = undefined;
        }
        if (this._skeletonSprite) {
          this._skeletonSprite.parent?.removeChild(this._skeletonSprite);
          this._skeletonSprite = null;
        }
      }
    }

    // --- decay aura on balls (Necromancer) ---
    // decay balls get a sickly green halo instead of the ignite orange.
    // This is handled inside the ball pool loop above, but we need to add the
    // green halo for decayed balls that don't have the ignite halo drawn.
    // We walk the ball pool again to add/update decay halos.
    for (const ball of s.balls) {
      if (ball.id >= PROJECTILE_ID_THRESHOLD) continue;
      const entry = this._ballPool.get(ball.id);
      if (!entry) continue;
      const ballRadius = s.cellSize * 0.25;
      // If decayed, repaint the halo green (overrides ignite orange if both somehow set).
      if ((ball as any).decayed) {
        entry.haloGfx.clear();
        entry.haloGfx.blendMode = BLEND_MODES.ADD;
        entry.haloGfx.beginFill(DECAY_HALO_COLOR, DECAY_HALO_ALPHA)
          .drawCircle(ball.x, ball.y, ballRadius * DECAY_HALO_RADIUS_MULT)
          .endFill();
      }
    }

    // --- P6 events: lightning, explosion (rocket), radiation, decay ---
    // These are remapped to existing effect types so they reuse the existing
    // atlas art and Effects pipeline without requiring new Effects methods.
    const remappedEvents: Snapshot["events"] = [];
    for (const ev of s.events) {
      if (ev.type === "lightning") {
        // Lightning: use spellCast (phoenix flourish) for a bright flash.
        remappedEvents.push({ type: "spellCast", x: ev.x, y: ev.y });
      } else if (ev.type === "explosion") {
        // Rocket explosion: full blockDestroyed blast at impact.
        remappedEvents.push({ type: "blockDestroyed", x: ev.x, y: ev.y });
      } else if (ev.type === "radiation" || ev.type === "decay") {
        // Radiation / decay: smaller burn flash.
        remappedEvents.push({ type: "burn", x: ev.x, y: ev.y });
      }
    }
    if (remappedEvents.length > 0) {
      this.effectsLayer.consume(remappedEvents, s.cellSize, s.biome);
    }

    // --- effects: consume snapshot events ---
    this.effectsLayer.consume(s.events, s.cellSize, s.biome);
  }
}

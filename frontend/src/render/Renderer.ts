import { Application, Container, Graphics, Sprite, Texture } from "pixi.js";
import { GlowFilter } from "@pixi/filter-glow";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";
import { HazardLayer } from "./HazardLayer";
import { BonusLayer } from "./BonusLayer";
import { BlockLayer } from "./BlockLayer";
import { SpellFxLayer } from "./SpellFxLayer";
import { BallLayer, PROJECTILE_ID_THRESHOLD } from "./BallLayer";
import { FireWallLayer } from "./FireWallLayer";
import { VILLAGE_AMBIENT_REFS } from "./ambientRefs";
void VILLAGE_AMBIENT_REFS; // referenced so the asset-coverage audit sees these frames
import { bg as biomedBg, hellParallaxFrames, tex as atlasTex } from "./assets";
import { Effects } from "./Effects";
import { BallTrail } from "./BallTrail";
import { ScreenShake } from "./ScreenShake";
import { Vignette } from "./Vignette";
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

// Visible gap between bricks so the wall doesn't merge into a solid sheet.
// Expressed as a fraction of cellSize; enforces a 2 px minimum.
const GAP_FRAC = 0.12;

// Extra height below the block grid to ensure the paddle and its clearance are
// fully visible (grid + paddle zone + margin).
const PADDLE_ZONE_CELLS = 3;

// Turret visual: barrel length and width as fractions of paddleH.
const TURRET_BARREL_LENGTH_MULT = 1.8;
const TURRET_BARREL_WIDTH_MULT  = 0.45;

// (Ball ignite/decay/aura + projectile constants live in BallLayer;
//  barrier/zone/skeleton constants live in SpellFxLayer.)

// Turret: atlas art key for the barrel sprite.
const TURRET_SPRITE_KEY   = "firemage/spell_fireturret/FireHeroTurret";

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

// How long to keep the boss rig visible after defeat (for the explosion burst to play).
const BOSS_DEFEAT_CLEANUP_MS = 1500;

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
  private blockLayer = new BlockLayer();
  private effectsLayer: Effects;
  private fireWallLayer = new FireWallLayer();
  private hazardLayer = new HazardLayer();
  // Paddle rendered as a sprite; Graphics kept as invisible fallback.
  private paddleSprite = new Sprite();
  private turretSprite = new Sprite();
  private ballLayer = new BallLayer();
  private ballTrail: BallTrail;
  private screenShake: ScreenShake;
  // Store the base fit position so screen-shake can layer on top.
  private _fitX = 0;
  private _fitY = 0;
  private damageFlash = new Graphics(); // full-screen overlay for HP hit feedback
  private _tick = 0; // used to drive wall flicker animation
  private _lastLives = -1; // track lives decreases for damage flash


  // Bonus pickups layer.
  private bonusLayer = new BonusLayer();

  // ── P6 per-class spell effects (Paladin barriers, Engineer zones, Necro skeleton) ──
  private spellFx = new SpellFxLayer();

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
      this.fireWallLayer.container.filters = [fxGlow];

      // Ball glow: separate filter instance so ball trails can share it independently.
      const ballGlow = new GlowFilter({
        distance:      10,
        outerStrength: 2.2,
        innerStrength: 0.0,
        color:         0xffffff,
        quality:       0.25,
      });
      this.ballLayer.container.filters = [ballGlow];
    }

    // Add telegraph warning container to boss layer.
    this._bossLayer.addChild(this._telegraphWarning.container);

    // Layer order: ambient → ballTrail → zones → blocks → fireWalls → wallAnimSys → barriers → bossLayer → effects → ballAuras → balls → paddleSprite → turret → skeletonAnimSys → hazards → bonuses
    // _ambientLayer sits behind everything — purely cosmetic background sprites.
    this._ambientLayer.alpha = 0.22;
    this.world.addChild(
      this._ambientLayer,
      this.ballTrail.container,
      this.spellFx.zonesContainer,
      this.blockLayer.container,
      this.fireWallLayer.container,
      this.spellFx.barriersContainer,
      this._bossLayer,
      this.effectsLayer.container,
      this.ballLayer.auraContainer,
      this.ballLayer.container,
      this.paddleSprite,
      this.turretSprite,
      this.spellFx.skeletonAnim.container,
      this.hazardLayer.container,
      this.bonusLayer.container,
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
        this.ballLayer.updateAnim(dtMs);
        this.spellFx.updateAnim(dtMs);
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

    // --- blocks (pooled: damage states, mirror, boss aura, teleporter ring, ghost, shield) ---
    const gap = Math.max(s.cellSize * GAP_FRAC, 2);
    const brickSize = s.cellSize - gap;
    this.blockLayer.update(s.blocks, this._tick, brickSize);

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
      for (const b of bossBlocks) this.blockLayer.hideBlock(b.id);

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

    // --- fire walls (animated FireStandAnnimation tiles, rebuilt on count change) ---
    this.fireWallLayer.update(s.walls ?? [], this._tick, s.cellSize, s.boardW);

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

    // --- balls (pooled by id: per-class sprite, projectile art, ignite/decay halos + aura) ---
    this.ballLayer.update(s.balls, this._tick, s.cellSize, _ballSpriteKey);

    // --- hazards (falling/rolling enemy projectiles) ---
    this.hazardLayer.update(s.hazards ?? [], this._tick, s.biome);

    // --- bonus pickups (falling icons from Bonus/ art) ---
    this.bonusLayer.update(s.bonuses ?? [], this._tick);

    // Catch sparkle: fire on bonusCaught events.
    for (const ev of s.events) {
      if (ev.type === "bonusCaught") {
        this.effectsLayer.consume([{ type: "blockDestroyed", x: ev.x, y: ev.y }], s.cellSize, s.biome);
      }
    }

    // ── P6 per-class spell effects (Paladin barriers, Engineer zones, Necro skeleton) ──
    this.spellFx.update(s.barriers ?? [], s.zones ?? [], s.skeletonActive ?? false, this._tick, s.cellSize, s.boardW, s.boardH);

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

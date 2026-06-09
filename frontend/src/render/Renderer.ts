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
// Paddle sprite: atlas key for the Fire Mage bar (first animation frame).
const PADDLE_SPRITE_KEY = "firemage/bars/v2FireHero1";
// Ball sprite: atlas key for the Fire Mage ball.
const BALL_SPRITE_KEY = "firemage/ball/FireHeroBall";
// Ball sprite size expressed as a multiplier of the sim ball radius.
const BALL_SPRITE_SCALE = 2.2; // sprite is slightly larger than the physics circle

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
  private _hazardPool: { halo: Graphics; core: Graphics; bat?: Sprite }[] = [];

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
  // Boss rig container layer (sits above blocks).
  private _bossLayer = new Container();

  // Paddle squash/stretch state.
  private _paddleSquashElapsed = -1; // -1 = inactive; >=0 = ms into the animation
  // Base paddle scale (set by draw(); squash multiplies on top).
  private _paddleBaseScaleX = 1;
  private _paddleBaseScaleY = 1;

  // Hit-stop state: remaining ms of visual freeze.
  private _hitStopRemaining = 0;

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();
    this.ballTrail = new BallTrail();
    this.screenShake = new ScreenShake();
    this._wallAnimSys = new AnimSystem();
    this._ballAnimSys = new AnimSystem();

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

    // Layer order: ballTrail → blocks → fireWalls → wallAnimSys → bossLayer → effects → ballAuras → balls → paddleSprite → turret → hazards
    this.world.addChild(
      this.ballTrail.container,
      this.blocks,
      this.fireWalls,
      this._wallAnimSys.container,
      this._bossLayer,
      this.effectsLayer.container,
      this._ballAnimSys.container,
      this.balls,
      this.paddleSprite,
      this.turretSprite,
      this.hazardsLayer,
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
      }

      // Telegraph warning update (runs regardless of hit-stop for clarity).
      this._telegraphWarning.update(dtMs, this._bossRegion.w * 0.5);

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

      // Compute HP fraction.
      const hpFrac = s.bossMaxHp > 0 ? s.bossHp / s.bossMaxHp : 1;

      // Update rig transform / animation.
      this._bossRig.update(regionCx, regionCy, regionW, regionH, hpFrac, this._tick, 0);
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
    // Swap to atlas paddle texture on first draw (atlas may not be loaded at construction time).
    const paddleTex = tex(PADDLE_SPRITE_KEY);
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
    const ballTex = tex(BALL_SPRITE_KEY);
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
      this.hazardsLayer.addChild(halo);
      this.hazardsLayer.addChild(core);
      this.hazardsLayer.addChild(bat);
      this._hazardPool.push({ halo, core, bat });
    }

    // Check if we should show bat sprites (village biome + bat texture loaded).
    const showBats = (s.biome === "village" || s.biome === "village-boss") && batTex !== Texture.WHITE;

    // Update visible entries.
    for (let i = 0; i < this._hazardPool.length; i++) {
      const { halo, core, bat } = this._hazardPool[i];
      if (i < hazards.length) {
        const hz = hazards[i];
        if (showBats && bat) {
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
      }
    }

    // --- effects: consume snapshot events ---
    this.effectsLayer.consume(s.events, s.cellSize, s.biome);
  }
}

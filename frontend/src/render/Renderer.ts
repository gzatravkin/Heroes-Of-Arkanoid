import { Application, Container, Graphics } from "pixi.js";
import { GlowFilter } from "@pixi/filter-glow";
import type { Snapshot } from "../net/Connection";
import { HazardLayer } from "./HazardLayer";
import { BonusLayer } from "./BonusLayer";
import { BlockLayer } from "./BlockLayer";
import { SpellFxLayer } from "./SpellFxLayer";
import { BallLayer } from "./BallLayer";
import { FireWallLayer } from "./FireWallLayer";
import { PaddleLayer } from "./PaddleLayer";
import { VILLAGE_AMBIENT_REFS } from "./ambientRefs";
void VILLAGE_AMBIENT_REFS; // referenced so the asset-coverage audit sees these frames
import { BackgroundLayer } from "./BackgroundLayer";
import { Effects } from "./Effects";
import { BallTrail } from "./BallTrail";
import { ScreenShake } from "./ScreenShake";
import { Vignette } from "./Vignette";
import { BossRig, TelegraphWarning, inferBossType } from "./Boss";
import { log } from "../log";
import { consumeSfx } from "../audio/Sfx";
import { setMusicBiome } from "../audio/Music";

// Heavy GPU effects (GlowFilter/bloom render-to-texture passes) are gated behind
// this flag so that Playwright's headless software-WebGL never pays the cost.
// navigator.webdriver is true in automation; false/undefined in real browsers.
// NOTE: base rendering always runs — only the optional glow post-processing is gated.
const HEAVY_FX = !(navigator as any).webdriver;

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

// Default ball key (fire mage); overridden by setClass(). (Paddle keys live in PaddleLayer.)
let _ballSpriteKey    = CLASS_BALL_KEYS.fire_mage;

// Visible gap between bricks so the wall doesn't merge into a solid sheet.
// Expressed as a fraction of cellSize; enforces a 2 px minimum.
const GAP_FRAC = 0.12;

// Extra height below the block grid to ensure the paddle and its clearance are
// fully visible (grid + paddle zone + margin).
const PADDLE_ZONE_CELLS = 3;

// (Paddle/turret + ball/spell constants live in their respective Layer modules.)

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
  // Biome background + Hell parallax + ambient village beholders.
  private background = new BackgroundLayer();
  private world = new Container();
  private blockLayer = new BlockLayer();
  private effectsLayer: Effects;
  private fireWallLayer = new FireWallLayer();
  private hazardLayer = new HazardLayer();
  private paddleLayer = new PaddleLayer();
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

  // Hit-stop state: remaining ms of visual freeze.
  private _hitStopRemaining = 0;

  /** Switch the paddle/ball sprites to match the given class. */
  setClass(classId: string) {
    const paddleKeys = CLASS_PADDLE_KEYS[classId] ?? CLASS_PADDLE_KEYS.fire_mage;
    _ballSpriteKey   = CLASS_BALL_KEYS[classId] ?? CLASS_BALL_KEYS.fire_mage;
    this.paddleLayer.setClass(paddleKeys);
  }

  constructor(host: HTMLElement) {
    // resolution + autoDensity: render at the device pixel ratio (capped at 2)
    // so Windows display scaling (dpr 1.25–1.5) and retina phones get a sharp
    // canvas instead of a stretched 1x buffer (docs/13 §S5).
    this.app = new Application({
      resizeTo: host,
      background: "#0b0b12",
      antialias: true,
      resolution: Math.min(window.devicePixelRatio || 1, 2),
      autoDensity: true,
    });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();
    this.ballTrail = new BallTrail();
    this.screenShake = new ScreenShake();



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

    // Layer order: ambient → ballTrail → zones → blocks → fireWalls → barriers → bossLayer → effects → paddle/turret → ballAuras → balls → skeleton → hazards → bonuses
    this.world.addChild(
      this.background.ambientContainer,
      this.ballTrail.container,
      this.spellFx.zonesContainer,
      this.blockLayer.container,
      this.fireWallLayer.container,
      this.spellFx.barriersContainer,
      this._bossLayer,
      this.effectsLayer.container,
      this.paddleLayer.container,
      // Balls draw over the paddle: the bar art is much taller than the physics
      // band, so a served ball resting on the paddle top must not hide behind it.
      this.ballLayer.auraContainer,
      this.ballLayer.container,
      this.spellFx.skeletonAnim.container,
      this.hazardLayer.container,
      this.bonusLayer.container,
    );
    // Damage flash sits on stage (not world) so it covers the full screen regardless of world scale.
    this.damageFlash.alpha = 0;
    // Layer order on stage: bg → world → damageFlash → vignette
    this.app.stage.addChild(this.background.bgLayer);
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

      // Paddle squash/stretch + bar-frame animation.
      this.paddleLayer.updateAnim(dtMs);

      // Fade the damage flash overlay.
      if (this.damageFlash.alpha > 0) {
        this.damageFlash.alpha = Math.max(0, this.damageFlash.alpha - DAMAGE_FLASH_FADE_SPEED * delta);
      }

      // Ambient sprite drift animation (village beholders).
      this.background.updateAnim(dtMs);
    });
  }

  private fit(s: Snapshot) {
    // Include paddle zone below the block grid so the paddle is never clipped.
    const effectiveH = s.boardH + s.cellSize * PADDLE_ZONE_CELLS;
    // Reserve space at the top for the DOM HUD (HP + lives bars, two 20px bars
    // plus margins) — the playfield used to start at y≈0 and the top brick rows
    // rendered underneath the bars (docs/13 battle audit).
    const HUD_TOP_INSET = 58;
    const availableH = this.app.screen.height - HUD_TOP_INSET;
    // Portrait-first: prefer filling the full height, then constrain by width.
    const scale = Math.min(
      this.app.screen.width / s.boardW,
      availableH / effectiveH,
    ) * 0.97;
    this.world.scale.set(scale);
    // Centre horizontally; align below the HUD band so blocks start clear of it.
    this._fitX = (this.app.screen.width - s.boardW * scale) / 2;
    this._fitY = Math.max(HUD_TOP_INSET, HUD_TOP_INSET + (availableH - effectiveH * scale) / 2);
    this.world.position.set(this._fitX, this._fitY);

    // Resize background + parallax to cover the full stage.
    this.background.resize(this.app.screen.width, this.app.screen.height);
  }

  draw(s: Snapshot) {
    // --- biome background + Hell parallax + ambient village beholders (rebuilt on biome change) ---
    this.background.setBiome(s.biome, s.cellSize);

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
    this.blockLayer.update(s.blocks, this._tick, brickSize, s.windRadius ?? 0);

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
        // Audit log + test hook: which rig art is actually shown for this boss block.
        log("boss", `rig created type=${bossType} sprite=${bossTypeStr}`);
        (window as unknown as { __bossRigType?: string }).__bossRigType = bossType;
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

    // --- paddle + turret (squash trigger, per-class bar sprite, turret indicator) ---
    this.paddleLayer.update(s.paddleX, s.paddleW, s.paddleH, s.boardH, s.cellSize, s.turretActive, s.balls);

    // --- ball trail (drawn behind balls) ---
    const ballRadius = s.cellSize * 0.25;
    this.ballTrail.update(s.balls, ballRadius);

    // --- balls (pooled by id: per-class sprite, projectile art, ignite/decay halos + aura) ---
    this.ballLayer.update(s.balls, this._tick, s.cellSize, _ballSpriteKey);

    // --- hazards (falling/rolling enemy projectiles) ---
    this.hazardLayer.update(s.hazards ?? [], this._tick);

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
    this.effectsLayer.boardH = s.boardH;
    this.effectsLayer.consume(s.events, s.cellSize, s.biome);
    consumeSfx(s.events); // procedural Web Audio cues (G1) — same event stream
    setMusicBiome(s.biome); // per-biome generative ambience (docs/12 briefs)
  }
}

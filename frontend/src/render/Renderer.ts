import { Application, Container, Graphics, Text, BLEND_MODES } from "pixi.js";
import { GlowFilter } from "@pixi/filter-glow";
import type { Snapshot } from "../net/Connection";
import { HazardLayer } from "./HazardLayer";
import { BonusLayer } from "./BonusLayer";
import { PowerUpLayer } from "./PowerUpLayer";
import { BlockLayer } from "./BlockLayer";
import { SpellFxLayer } from "./SpellFxLayer";
import { BallLayer } from "./BallLayer";
import { FireWallLayer } from "./FireWallLayer";
import { PhoenixLayer } from "./PhoenixLayer";
import { DangerVignette } from "./DangerVignette";
import { PaddleLayer } from "./PaddleLayer";
import { VILLAGE_AMBIENT_REFS } from "./ambientRefs";
void VILLAGE_AMBIENT_REFS; // referenced so the asset-coverage audit sees these frames
import { BackgroundLayer } from "./BackgroundLayer";
import { Effects } from "./Effects";
import { BallTrail } from "./BallTrail";
import { ScreenShake } from "./ScreenShake";
import { Vignette } from "./Vignette";
import { BossRig, TelegraphWarning, inferBossType } from "./Boss";
import { SpritePool } from "./EffectSprites";
import { tex as atlasTex } from "./assets";
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

// Per-biome danger-telegraph palette (2026-06-16 effects style pass §2): the lane telegraph re-skins to the
// world — Hell embers, Caverns grit, Witchland ghost-wisps, Heaven light-motes — so threats read the same
// everywhere but match the biome. (haze = faint lane fill, dash = descending particle, glow = pit target.)
const TELEGRAPH_THEMES: Record<string, { haze: number; dash: number; glow: number; glowKey: string }> = {
  hell:    { haze: 0xff6a2a, dash: 0xffc070, glow: 0xff7a2a, glowKey: "firemage/spell_phonex/PhoenixGlow" },
  caverns: { haze: 0xbb9a6a, dash: 0xe0cda0, glow: 0xd8b878, glowKey: "effects/RangeArea" },
  village: { haze: 0x7a5ac0, dash: 0xc4a6ff, glow: 0xa86aff, glowKey: "effects/RangeArea" },
  heaven:  { haze: 0xfff0c0, dash: 0xffe9a8, glow: 0xfff3cf, glowKey: "effects/RangeArea" },
};
function telegraphTheme(biome: string) {
  const base = (biome || "").split("-")[0];
  return TELEGRAPH_THEMES[base === "cavern" ? "caverns" : base] ?? TELEGRAPH_THEMES.hell;
}

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
  private phoenixLayer = new PhoenixLayer();
  private dangerVignette!: DangerVignette;
  private hazardLayer = new HazardLayer();
  readonly paddleLayer = new PaddleLayer();
  private ballLayer = new BallLayer();
  private ballTrail: BallTrail;
  private screenShake: ScreenShake;
  // Store the base fit position so screen-shake can layer on top.
  private _fitX = 0;
  private _fitY = 0;
  private damageFlash = new Graphics(); // full-screen overlay for HP hit feedback
  private _tick = 0; // used to drive wall flicker animation
  private _lastLives = -1; // track lives decreases for damage flash


  // Bonus pickups layer (generic atlas-icon pickups).
  private bonusLayer = new BonusLayer();
  // Power-up falling pickups (coloured circles: wide/multiball/fireshot/manasurge/shield).
  private powerUpLayer = new PowerUpLayer();

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

  // Arena walls — a framed playfield border (Level-UX rework 2026-06-15, Option 1) drawn at the exact
  // left/right/top bounce boundaries so the player can read the lateral limits. Redrawn on fit().
  private _wallGfx = new Graphics();
  private _wallBoardW = -1;   // cache: only redraw when the board size changes
  // Lane telegraph: a pulsing warm column under any CHARGING emitter, marking the column its shot will fall.
  private _laneGfx = new Graphics();
  // Last-brick highlight: gold pulsing outline on the final ≤3 destructible bricks.
  private _dangerOverlay = new Graphics();
  private _lichBeamGfx = new Graphics(); // §3 Lich's Gaze sweeping beam
  private _tetherGfx = new Graphics();   // §2 Twin Soul Core tether
  // Painted (sprite) effects — replace the old procedural Graphics shapes (2026-06-16 effects style pass).
  private _lanePool   = new SpritePool();  // lane telegraph: soft scorch-target glow at the pit
  private _pillarPool = new SpritePool();  // §3 Lance of Dawn: pillars of light (glow sprite)
  private _minionPool = new SpritePool();  // §3 Bonewalker / Bone Golem: real skeleton/golem art
  private _minionBarGfx = new Graphics();  // minion HP / life bars (small UI overlay)
  private _dangerBlocks: { x: number; y: number }[] = [];
  private _dangerBrickSize = 0;

  // Floating score popups: pool of 10 Text objects reused to avoid GC pressure.
  private _floaterPool: Text[] = [];
  // Crit popups use a separate pool with a pre-baked bold/outlined style — mutating a
  // shared Text's style at spawn time triggers a Pixi re-rasterization white-box flash.
  private _critFloaterPool: Text[] = [];
  private _activeFloaters: { text: Text; elapsed: number; scale: number; pool: Text[] }[] = [];
  private _floaterContainer = new Container();
  // Previous-frame block positions — used to detect block disappearances for floater spawning.
  private _prevBlocks = new Map<number, { x: number; y: number; hp: number }>();

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

    // Floating score popup pool: 10 pre-allocated PIXI.Text objects (task 1.3).
    const floaterStyle = {
      fontSize: 14,
      fill: 0xd8a84e,
      fontWeight: "bold" as const,
      dropShadow: true,
      dropShadowDistance: 2,
      dropShadowAlpha: 0.8,
    };
    for (let i = 0; i < 10; i++) {
      const t = new Text("", floaterStyle);
      t.visible = false;
      t.anchor.set(0.5, 1); // anchor at bottom-centre so text rises from block position
      this._floaterPool.push(t);
      this._floaterContainer.addChild(t);
    }

    // Crit popups: bright gold fill + thick deep-red outline so "CRIT N!" reads instantly
    // on every biome (even the dark-red hell background). Pre-baked style → no white-box flash.
    const critStyle = {
      fontSize: 22,
      fill: 0xffd23b,
      fontWeight: "bold" as const,
      stroke: 0x7a0000,
      strokeThickness: 5,
      dropShadow: true,
      dropShadowDistance: 2,
      dropShadowAlpha: 0.85,
    };
    for (let i = 0; i < 10; i++) {
      const t = new Text("", critStyle);
      t.visible = false;
      t.anchor.set(0.5, 1);
      this._critFloaterPool.push(t);
      this._floaterContainer.addChild(t);
    }

    // Layer order: ambient → ballTrail → zones → blocks → dangerOverlay → fireWalls → barriers → bossLayer → effects → paddle/turret → ballAuras → balls → skeleton → hazards → bonuses → powerUps
    this.world.addChild(
      this.background.ambientContainer,
      this._wallGfx,
      this._laneGfx,
      this._lanePool.container,
      this.ballTrail.container,
      this.spellFx.zonesContainer,
      this.blockLayer.container,
      this._lichBeamGfx,
      this._pillarPool.container,
      this._minionPool.container,
      this._minionBarGfx,
      this._tetherGfx,
      this._dangerOverlay,
      this.fireWallLayer.container,
      this.spellFx.barriersContainer,
      this._bossLayer,
      this.effectsLayer.container,
      this.paddleLayer.container,
      // Balls draw over the paddle: the bar art is much taller than the physics
      // band, so a served ball resting on the paddle top must not hide behind it.
      this.ballLayer.auraContainer,
      this.ballLayer.container,
      this.phoenixLayer.container,
      this.spellFx.skeletonAnim.container,
      this.hazardLayer.container,
      this.bonusLayer.container,
      this.powerUpLayer.container,
      this._floaterContainer,  // floating score popups drawn over everything
    );
    // Damage flash sits on stage (not world) so it covers the full screen regardless of world scale.
    this.damageFlash.alpha = 0;
    // Layer order on stage: bg → world → damageFlash → vignette
    this.app.stage.addChild(this.background.bgLayer);
    this.app.stage.addChild(this.world);
    this.app.stage.addChild(this.damageFlash);

    // Vignette: subtle dark corners overlay on the stage (top-most).
    new Vignette(this.app);
    // Danger vignette: red edge pulse when HP is critical (sits above the dark vignette).
    this.dangerVignette = new DangerVignette(this.app);

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

      // Last-brick highlight: gold pulsing outline on final ≤3 destructible bricks.
      if (this._dangerBlocks.length > 0 && this._dangerBrickSize > 0) {
        const pulseAlpha = 0.5 + 0.4 * Math.sin(Date.now() / 300);
        this._dangerOverlay.clear();
        this._dangerOverlay.lineStyle(3, 0xd8a84e, pulseAlpha);
        const half = this._dangerBrickSize / 2;
        for (const b of this._dangerBlocks) {
          this._dangerOverlay.drawRect(b.x - half, b.y - half, this._dangerBrickSize, this._dangerBrickSize);
        }
      } else if (this._dangerBlocks.length === 0) {
        this._dangerOverlay.clear();
      }

      // Floating score popups: rise and fade over 800 ms.
      for (let fi = this._activeFloaters.length - 1; fi >= 0; fi--) {
        const f = this._activeFloaters[fi];
        f.elapsed += dtMs;
        // Rise 40 world-units over 800 ms.
        f.text.y -= 40 * (dtMs / 800);
        f.text.alpha = Math.max(0, 1 - f.elapsed / 800);
        // Pop: overshoot the target scale at spawn, settle within 160 ms.
        const pop = 1 + Math.max(0, 1 - f.elapsed / 160) * 0.6;
        f.text.scale.set(f.scale * pop);
        if (f.elapsed >= 800) {
          f.text.visible = false;
          f.text.alpha = 1;
          f.text.scale.set(1);
          f.pool.push(f.text);
          this._activeFloaters.splice(fi, 1);
        }
      }

      // Ambient sprite drift animation (village beholders).
      this.background.updateAnim(dtMs);
    });
  }

  /** Spawn a floating "+1 ×M" score label rising from world position (wx, wy). */
  private _spawnFloater(wx: number, wy: number, mult: number) {
    if (this._floaterPool.length === 0) return; // pool exhausted — skip
    const t = this._floaterPool.pop()!;
    // Combos escalate: bigger, hotter-coloured popups as the streak climbs.
    t.text = mult >= 4 ? `×${mult} !` : `+1 ×${mult}`;
    t.tint  = mult >= 4 ? 0xff4d2a : mult >= 3 ? 0xff9a3c : 0xffd24e;
    const scale = mult >= 4 ? 1.7 : mult >= 3 ? 1.35 : 1.0;
    t.x = wx;
    t.y = wy;
    t.alpha = 1;
    t.visible = true;
    this._activeFloaters.push({ text: t, elapsed: 0, scale, pool: this._floaterPool });
  }

  /** Crit popup (stat engine): a big red-gold "CRIT N" that pops + rises at the impact point. */
  private _spawnCritFloater(wx: number, wy: number, dmg: number) {
    if (this._critFloaterPool.length === 0) return;
    const t = this._critFloaterPool.pop()!;
    t.text = dmg > 0 ? `CRIT ${dmg}!` : "CRIT!";
    t.x = wx;
    t.y = wy;
    t.alpha = 1;
    t.visible = true;
    this._activeFloaters.push({ text: t, elapsed: 0, scale: 1.9, pool: this._critFloaterPool });
  }

  private fit(s: Snapshot) {
    // Include paddle zone below the block grid so the paddle is never clipped.
    const effectiveH = s.boardH + s.cellSize * PADDLE_ZONE_CELLS;
    // Reserve space at the top for the DOM HUD (HP + lives bars, two 20px bars
    // plus margins) — the playfield used to start at y≈0 and the top brick rows
    // rendered underneath the bars (docs/13 battle audit).
    const HUD_TOP_INSET = 54;     // matches Hud.svelte .hud-bezel height — playfield starts below the top HUD strip
    const HUD_BOTTOM_INSET = 124; // matches Hud.svelte .hud-bottom min-height — playfield ends ABOVE the mana+hotbar
                                  // band so the mana bar never floats over / under the pit (battle HUD fix).
    const availableH = Math.max(1, this.app.screen.height - HUD_TOP_INSET - HUD_BOTTOM_INSET);
    // Portrait-first: prefer filling the available height, then constrain by width.
    const scale = Math.min(
      this.app.screen.width / s.boardW,
      availableH / effectiveH,
    ) * 0.97;
    this.world.scale.set(scale);
    // Centre horizontally; centre vertically inside the band between the top bezel and the bottom HUD strip.
    this._fitX = (this.app.screen.width - s.boardW * scale) / 2;
    this._fitY = Math.max(HUD_TOP_INSET, HUD_TOP_INSET + (availableH - effectiveH * scale) / 2);
    this.world.position.set(this._fitX, this._fitY);

    // Arena walls: frame the playfield at the exact bounce boundaries (x=0, x=boardW) + top.
    // World-space, so it scales/shakes with the board. Only redraw when the board size changes.
    if (this._wallBoardW !== s.boardW) {
      this._wallBoardW = s.boardW;
      const T = 9;                  // wall thickness (world units)
      const playH = effectiveH;     // frame the whole play column (blocks + dodge/paddle zone)
      const g = this._wallGfx;
      g.clear();
      g.beginFill(0x120b06, 0.97);                          // stone body
      g.drawRect(-T, -T, T, playH + T);                     // left
      g.drawRect(s.boardW, -T, T, playH + T);               // right
      g.drawRect(-T, -T, s.boardW + 2 * T, T);              // top
      g.endFill();
      g.beginFill(0xd8a84e, 0.92);                          // gold inner rule = the exact bounce line
      g.drawRect(-2.5, 0, 2.5, playH);                      // left edge
      g.drawRect(s.boardW, 0, 2.5, playH);                  // right edge
      g.drawRect(0, -2.5, s.boardW, 2.5);                   // top edge
      g.endFill();
      g.beginFill(0x6a4a1e, 0.5);                           // subtle bevel under the gold rule
      g.drawRect(-2.5, 0, 1, playH);
      g.drawRect(s.boardW + 1.5, 0, 1, playH);
      g.endFill();
    }

    // Resize background + parallax to cover the full stage.
    this.background.resize(this.app.screen.width, this.app.screen.height);
  }

  draw(s: Snapshot) {
    // --- biome background + Hell parallax + ambient village beholders (rebuilt on biome change) ---
    this.background.setBiome(s.biome, s.cellSize);
    // Dim the backdrop during active play so blocks + ball pop (readability overhaul §D).
    this.background.setPlayDim(s.phase === "Playing");

    this.fit(s);

    // --- screen shake + hit-stop: fire on relevant events ---
    const shakeEnabled = localStorage.getItem("arkanoid_fx") !== "0"
      && !window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    for (const ev of s.events) {
      if (ev.type === "playerHit") { if (shakeEnabled) this.screenShake.trigger("playerHit"); }
      else if (ev.type === "bossAttack") {
        if (shakeEnabled) this.screenShake.trigger("bossAttack");
        // Short hit-stop on boss attacks.
        this._hitStopRemaining = Math.max(this._hitStopRemaining, HIT_STOP_DURATION_BOSS_MS);
      } else if (ev.type === "ignite") {
        // Brief hit-stop when ignite lands.
        this._hitStopRemaining = Math.max(this._hitStopRemaining, HIT_STOP_DURATION_IGNITE_MS);
      } else if (ev.type === "crit") {
        // Crit (design §5.7): a sharp hit-flash/shake so the bigger number lands with weight.
        if (shakeEnabled) this.screenShake.pulse(3.5, 110);
      }
    }

    // --- damage flash: trigger on HP decrease ---
    if (this._lastLives >= 0 && s.hp < this._lastLives) {
      // Repaint the full-screen flash rect to match current screen size, then trigger.
      this.damageFlash.clear();
      this.damageFlash.beginFill(0xff0000, 1)
        .drawRect(0, 0, this.app.screen.width, this.app.screen.height)
        .endFill();
      this.damageFlash.alpha = DAMAGE_FLASH_ALPHA_START;
    }
    this._lastLives = s.hp;

    // --- low-HP danger vignette: pulse red at the edges when critically low ---
    const dangerIntensity = s.phase === "Playing"
      ? (s.hp <= 1 ? 1 : s.hp === 2 ? 0.35 : 0)
      : 0;
    this.dangerVignette.update(dangerIntensity, this._tick, shakeEnabled);

    // --- blocks (pooled: damage states, mirror, boss aura, teleporter ring, ghost, shield) ---
    const gap = Math.max(s.cellSize * GAP_FRAC, 2);
    const brickSize = s.cellSize - gap;
    // Ball phase drives the ghost/physical layer emphasis (Witchland). Use "any/all" so a hittable
    // layer is never dimmed away under mixed-phase multiball.
    const _balls = s.balls ?? [];
    const anyGhostBall  = _balls.some(b => b.ghost);
    const allGhostBalls = _balls.length > 0 && _balls.every(b => b.ghost);
    this.blockLayer.update(s.blocks, this._tick, brickSize, s.windRadius ?? 0, anyGhostBall, allGhostBalls);

    // --- lane telegraph: under each CHARGING emitter, a faint warm haze + a DESCENDING stream of sparks
    // + a pulsing scorch target at the pit — reads as "something is falling down this lane" and fits the
    // fiery look, replacing the old flat orange "laser bar" (readability/feel pass 2026-06-16). ---
    this._laneGfx.clear();
    this._lanePool.begin();
    const playBottom = s.boardH + s.cellSize * PADDLE_ZONE_CELLS;
    const laneW = s.cellSize * 0.6;
    const tt = telegraphTheme(s.biome);
    const laneGlowTex = atlasTex(tt.glowKey);
    for (const b of s.blocks) {
      if (!b.charging) continue;
      const top = b.y + s.cellSize * 0.3;
      const colH = playBottom - top;
      if (colH <= 0) continue;
      // faint biome-tinted haze marks the danger lane without dominating the frame
      this._laneGfx.beginFill(tt.haze, 0.10).drawRect(b.x - laneW / 2, top, laneW, colH).endFill();
      // descending biome particle dashes (animated) — motion reads as "incoming"
      const period = s.cellSize * 1.2;
      const seg    = s.cellSize * 0.5;
      const scroll = (this._tick * 2.6) % period;
      for (let y = top + scroll - period; y < playBottom; y += period) {
        const y0 = Math.max(top, y);
        const y1 = Math.min(playBottom, y + seg);
        if (y1 <= y0) continue;
        this._laneGfx.beginFill(tt.dash, 0.62).drawRect(b.x - 2.5, y0, 5, y1 - y0).endFill();
      }
      // painted pulsing glow target at the pit end (additive) — the unmistakable "dodge here"
      const tp = 0.55 + 0.3 * (0.5 + 0.5 * Math.sin(this._tick * 0.25));
      const g = this._lanePool.next(laneGlowTex);
      g.blendMode = BLEND_MODES.ADD;
      g.tint = tt.glow;
      g.alpha = tp;
      const gs = laneW * (2.4 + 0.25 * Math.sin(this._tick * 0.25));
      g.width = gs; g.height = gs * 0.6;
      g.position.set(b.x, playBottom);
    }
    this._lanePool.end();

    // §3 Lich's Gaze: draw the sweeping beam ray (world-space; over blocks, under FX).
    this._lichBeamGfx.clear();
    if (s.lichBeam) {
      const lb = s.lichBeam;
      const ex = lb.x + Math.cos(lb.angle) * lb.len;
      const ey = lb.y + Math.sin(lb.angle) * lb.len;
      this._lichBeamGfx.blendMode = BLEND_MODES.ADD; // energy beam reads as additive light, not a flat line
      this._lichBeamGfx.lineStyle(20, 0x6a20b0, 0.12).moveTo(lb.x, lb.y).lineTo(ex, ey); // wide soft glow
      this._lichBeamGfx.lineStyle(10, 0x9040d0, 0.30).moveTo(lb.x, lb.y).lineTo(ex, ey); // mid glow
      this._lichBeamGfx.lineStyle(3,  0xe6c0ff, 0.95).moveTo(lb.x, lb.y).lineTo(ex, ey); // bright core
    }

    // §2 Twin Soul Core: draw the slicing tether between the two twin balls.
    this._tetherGfx.clear();
    if (s.twinTether) {
      const t = s.twinTether;
      this._tetherGfx.blendMode = BLEND_MODES.ADD;
      this._tetherGfx.lineStyle(12, 0x2090c0, 0.14).moveTo(t.x1, t.y1).lineTo(t.x2, t.y2); // wide soft glow
      this._tetherGfx.lineStyle(6,  0x66e0ff, 0.30).moveTo(t.x1, t.y1).lineTo(t.x2, t.y2); // mid glow
      this._tetherGfx.lineStyle(2,  0xd0f4ff, 0.95).moveTo(t.x1, t.y1).lineTo(t.x2, t.y2); // bright core
    }

    // §3 Lance of Dawn: pillars of light — soft additive glow sprites instead of flat rects.
    this._pillarPool.begin();
    const pillarTex = atlasTex("effects/RangeArea");
    for (const p of s.pillars ?? []) {
      const glow = this._pillarPool.next(pillarTex);
      glow.blendMode = BLEND_MODES.ADD; glow.tint = 0xffe9a8; glow.alpha = 0.45;
      glow.width = p.w * 1.9; glow.height = p.h * 1.05; glow.position.set(p.x, p.y);
      const core = this._pillarPool.next(pillarTex);
      core.blendMode = BLEND_MODES.ADD; core.tint = 0xfff6e0; core.alpha = 0.95;
      core.width = p.w * 0.9; core.height = p.h; core.position.set(p.x, p.y);
    }
    this._pillarPool.end();

    // §3 Necromancer summons: Bonewalker (skeleton) and Bone Golem — drawn with their real sprite art
    // instead of bone-coloured rectangles (2026-06-16 effects style pass). Bars stay as a small overlay.
    this._minionPool.begin();
    this._minionBarGfx.clear();
    const skelTex  = atlasTex("necromancer/spell_skeleton/Skeleton");
    const golemTex = atlasTex("necromancer/spell_lastday/BoneGolem");
    for (const m of s.minions ?? []) {
      const isGolem = m.kind === "golem";
      const t = isGolem ? golemTex : skelTex;
      const sp = this._minionPool.next(t);
      sp.anchor.set(0.5, 1); // feet on the ground line
      const targetH = m.h * (isGolem ? 1.9 : 2.2); // a bit larger than the footprint so the art reads
      const aspect = (t.width || 1) / (t.height || 1);
      sp.height = targetH;
      sp.width  = targetH * aspect;
      sp.position.set(m.x, m.y + m.h / 2);
      // bars (HP for the golem, walk-duration for the bonewalker)
      const bw = m.w + 4;
      if (isGolem && m.maxHp > 0) {
        const frac = Math.max(0, m.hp / m.maxHp), by = m.y - m.h / 2 - m.h * 0.34;
        this._minionBarGfx.beginFill(0x000000, 0.5).drawRect(m.x - bw / 2, by, bw, 3).endFill();
        this._minionBarGfx.beginFill(0x8be08b, 0.95).drawRect(m.x - bw / 2, by, bw * frac, 3).endFill();
      } else if (!isGolem) {
        const lf = m.lifeFrac ?? 0;
        if (lf > 0) {
          const by = m.y - m.h / 2 - 6;
          this._minionBarGfx.beginFill(0x000000, 0.5).drawRect(m.x - bw / 2, by, bw, 3).endFill();
          this._minionBarGfx.beginFill(0xe8d27a, 0.95).drawRect(m.x - bw / 2, by, bw * lf, 3).endFill();
        }
      }
    }
    this._minionPool.end();

    // --- floating score popups: detect destroyed blocks, spawn floater when combo > 1 ---
    const combo = s.comboMultiplier ?? 1;
    if (this._prevBlocks.size > 0 && combo > 1) {
      const currentIds = new Set(s.blocks.map(b => b.id));
      let destroyedThisFrame = false;
      for (const [id, pos] of this._prevBlocks) {
        if (!currentIds.has(id)) {
          this._spawnFloater(pos.x, pos.y, combo);
          destroyedThisFrame = true;
        }
      }
      // Combo punch: a tiny, escalating screen-shake (and a micro hit-stop at max combo).
      if (destroyedThisFrame && shakeEnabled) {
        this.screenShake.pulse(Math.min(1.2 + combo * 0.9, 5), 90);
        if (combo >= 4) this._hitStopRemaining = Math.max(this._hitStopRemaining, 35);
      }
    }
    // Chip spark: a small spark wherever a surviving block lost HP — tactile per-hit
    // feedback on every ball/projectile contact. Burning DoT is excluded (it has its own fire FX).
    for (const b of s.blocks) {
      const prev = this._prevBlocks.get(b.id);
      if (prev && b.hp < prev.hp && b.hp > 0 && !b.burning) {
        this.effectsLayer.hitSpark(b.x, b.y, s.cellSize);
      }
    }
    // Update prev-blocks map for next frame.
    this._prevBlocks.clear();
    for (const b of s.blocks) {
      this._prevBlocks.set(b.id, { x: b.x, y: b.y, hp: b.hp });
    }

    // --- last-brick highlight: track the final ≤3 destructible bricks for the pulsing overlay ---
    const destructible = s.blocks.filter(b => !b.indestructible && b.hp > 0);
    this._dangerBrickSize = brickSize;
    this._dangerBlocks = (destructible.length <= 3 && destructible.length > 0)
      ? destructible.map(b => ({ x: b.x, y: b.y }))
      : [];

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
    // Phoenix entities (orbit the ball + burn blocks). A born phoenix gets a one-time
    // birth flourish — the ONLY place the phoenix art fires (never on a generic cast).
    const bornPhoenixes = this.phoenixLayer.update(s.phoenixes ?? [], s.cellSize);
    for (const b of bornPhoenixes) this.effectsLayer.spawnPhoenixBirth(b.x, b.y, s.cellSize);

    // --- paddle + turret (squash trigger, per-class bar sprite, turret indicator) ---
    this.paddleLayer.update(s.paddleX, s.paddleW, s.paddleH, s.boardH, s.cellSize, s.turretActive, s.balls, s.paddleInvuln);
    // Drive the bar-frame from mana ratio so power-state art matches game state.
    this.paddleLayer.setMana(s.mana / (s.manaMax || 100));

    // --- ball trail (drawn behind balls) ---
    const ballRadius = s.cellSize * 0.15625; // matches BallLayer.BALL_RADIUS_FRAC
    this.ballTrail.update(s.balls, ballRadius);

    // --- balls (pooled by id: per-class sprite, projectile art, ignite/decay halos + aura) ---
    this.ballLayer.update(s.balls, s.projectiles ?? [], this._tick, s.cellSize, _ballSpriteKey, s.biome);

    // --- hazards (falling/rolling enemy projectiles) ---
    this.hazardLayer.update(s.hazards ?? [], this._tick);

    // --- bonus pickups (falling icons from Bonus/ art) ---
    this.bonusLayer.update(s.bonuses ?? [], this._tick);
    // --- power-up pickups (coloured circles: wide/multiball/fireshot/manasurge/shield) ---
    this.powerUpLayer.update(s.bonuses ?? [], this._tick);

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
      if (ev.type === "explosion") {
        // Rocket explosion: full blockDestroyed blast at impact.
        remappedEvents.push({ type: "blockDestroyed", x: ev.x, y: ev.y });
      } else if (ev.type === "radiation" || ev.type === "decay") {
        // Radiation / decay: smaller burn flash.
        remappedEvents.push({ type: "burn", x: ev.x, y: ev.y });
      } else if (ev.type === "crit") {
        // Crit (stat engine): a punchy "CRIT N" floater + a bright gold impact spark.
        // A non-killing crit must NOT shatter the block, so we use the debris-free gold
        // burst (perfectDeflect), not blockDestroyed. The real block-death blast still
        // fires on its own event when the crit is lethal.
        this._spawnCritFloater(ev.x, ev.y, ev.extra ?? 0);
        remappedEvents.push({ type: "perfectDeflect", x: ev.x, y: ev.y });
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

import { Application, Container, Graphics, Sprite, BLEND_MODES, Texture } from "pixi.js";
import { GlowFilter } from "@pixi/filter-glow";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";
import { Effects } from "./Effects";
import { BallTrail } from "./BallTrail";
import { ScreenShake } from "./ScreenShake";
import { Vignette } from "./Vignette";

// Heavy GPU effects (GlowFilter/bloom render-to-texture passes) are gated behind
// this flag so that Playwright's headless software-WebGL never pays the cost.
// navigator.webdriver is true in automation; false/undefined in real browsers.
// NOTE: base rendering always runs — only the optional glow post-processing is gated.
const HEAVY_FX = !(navigator as any).webdriver;

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

// Note: turret bullets (id >= 10000) render via the normal ball path — no special branch needed.

// Boss block rendering constants.
const BOSS_SCALE_MULT  = 1.15;   // slightly enlarged vs normal brickSize
const BOSS_AURA_COLOR  = 0xcc0000; // menacing red aura
const BOSS_AURA_RADIUS_MULT = 0.8; // fraction of brickSize/2
const BOSS_AURA_ALPHA  = 0.55;
const BOSS_AURA_PULSE_SPEED = 0.06;
const BOSS_AURA_ALPHA_AMP  = 0.25;

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
  private world = new Container();
  private blocks = new Container();
  private effectsLayer: Effects;
  private fireWalls = new Container();
  private hazardsLayer = new Container();
  private paddle = new Graphics();
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

  // ---- Sprite pools: keyed by entity id to avoid per-frame alloc churn ----
  // Block pool: each entry is a { sprite, aura?, ring? } tuple.
  private _blockPool = new Map<number, { sp: Sprite; aura?: Graphics; ring?: Graphics }>();
  // Ball pool: each entry is { gfx (ball circle), haloGfx (ignite halo) }.
  private _ballPool = new Map<number, { gfx: Graphics; haloGfx: Graphics }>();
  // Hazard pool: each entry is { halo, core }.
  private _hazardPool: { halo: Graphics; core: Graphics }[] = [];

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();
    this.ballTrail = new BallTrail();
    this.screenShake = new ScreenShake();

    // Try to load the turret sprite; fall back to Graphics if it fails.
    this.turretSprite = Sprite.from("/art/FireHeroTurret.png");
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

    // Layer order: ballTrail → blocks → fireWalls → effects → balls → paddle → turret → hazards
    this.world.addChild(
      this.ballTrail.container,
      this.blocks,
      this.fireWalls,
      this.effectsLayer.container,
      this.balls,
      this.paddle,
      this.turretSprite,
      this.hazardsLayer,
    );
    // Damage flash sits on stage (not world) so it covers the full screen regardless of world scale.
    this.damageFlash.alpha = 0;
    this.app.stage.addChild(this.world);
    this.app.stage.addChild(this.damageFlash);

    // Vignette: subtle dark corners overlay on the stage (top-most).
    new Vignette(this.app);

    // Tick the effects every frame and drive wall flicker.
    this.app.ticker.add((delta) => {
      // delta is in Pixi ticker units (frames at 60 fps → multiply by 1000/60 for ms)
      const dtMs = (delta / 60) * 1000;
      this.effectsLayer.update(dtMs);
      this.screenShake.update(dtMs);
      // Apply screen-shake offset on top of the fit position calculated last draw().
      this.world.position.set(
        this._fitX + this.screenShake.offsetX,
        this._fitY + this.screenShake.offsetY,
      );
      this._tick += delta;
      // Animate the alpha flicker on each fire-wall tile every frame.
      for (let i = 0; i < this.fireWalls.children.length; i++) {
        const child = this.fireWalls.children[i];
        const flicker = 0.72 + 0.28 * Math.sin(this._tick * 0.18 + i * 1.3);
        child.alpha = flicker;
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
    const scale = Math.min(
      this.app.screen.width / s.boardW,
      this.app.screen.height / effectiveH,
    ) * 0.95;
    this.world.scale.set(scale);
    // Store the base fit position — screen-shake will add its offset on top each tick.
    this._fitX = (this.app.screen.width - s.boardW * scale) / 2;
    this._fitY = (this.app.screen.height - effectiveH * scale) / 2;
    this.world.position.set(this._fitX, this._fitY);
  }

  draw(s: Snapshot) {
    this.fit(s);

    // --- screen shake: fire on relevant events ---
    for (const ev of s.events) {
      if (ev.type === "playerHit") this.screenShake.trigger("playerHit");
      else if (ev.type === "bossAttack") this.screenShake.trigger("bossAttack");
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

    // --- fire walls ---
    this.fireWalls.removeChildren();
    const wallH = s.cellSize * FIRE_WALL_HEIGHT_MULT;
    for (const wall of (s.walls ?? [])) {
      // Tile Explosion.png across the board width to form the flame band.
      const explosionTex: Texture = tex("Explosion");
      const tileW = wallH; // square tiles looks good
      const count  = Math.ceil(s.boardW / tileW);
      for (let i = 0; i < count; i++) {
        const sp = new Sprite(explosionTex);
        sp.blendMode = BLEND_MODES.ADD;
        sp.tint    = 0xff6620; // orange-red tint
        sp.anchor.set(0, 0.5);
        sp.width   = tileW + 1;  // +1 to avoid hairline gaps
        sp.height  = wallH;
        sp.x       = i * tileW;
        sp.y       = wall.y;
        // Initial alpha; the ticker loop will flicker it each frame.
        sp.alpha   = 0.85;
        this.fireWalls.addChild(sp);
      }
    }

    // --- paddle ---
    this.paddle.clear();
    this.paddle.beginFill(0x7fd1ff).drawRect(
      s.paddleX - s.paddleW / 2,
      (s.boardH + s.cellSize) - s.paddleH / 2,
      s.paddleW,
      s.paddleH,
    ).endFill();

    // --- turret indicator ---
    const paddleTopY = (s.boardH + s.cellSize) - s.paddleH / 2;
    if (s.turretActive) {
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

    // --- balls (pooled by id) ---
    const liveBallIds = new Set<number>();
    for (const ball of s.balls) liveBallIds.add(ball.id);

    // Remove pooled entries for balls that no longer exist.
    for (const [id, entry] of this._ballPool) {
      if (!liveBallIds.has(id)) {
        this.balls.removeChild(entry.haloGfx);
        this.balls.removeChild(entry.gfx);
        this._ballPool.delete(id);
      }
    }

    for (const ball of s.balls) {
      if (this._ballPool.has(ball.id)) {
        // Update existing pooled Graphics objects (redraw is cheap — no alloc).
        const { gfx, haloGfx } = this._ballPool.get(ball.id)!;

        haloGfx.clear();
        if (ball.ignited) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        gfx.clear();
        if (ball.ignited) {
          gfx.beginFill(0xff7a2a, IGNITE_HALO_ALPHA)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }
        gfx.beginFill(ball.ignited ? 0xff7a2a : 0xffffff)
          .drawCircle(ball.x, ball.y, ballRadius)
          .endFill();
      } else {
        // Create new pooled entry.
        const haloGfx = new Graphics();
        if (ball.ignited) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        const gfx = new Graphics();
        if (ball.ignited) {
          gfx.beginFill(0xff7a2a, IGNITE_HALO_ALPHA)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }
        gfx.beginFill(ball.ignited ? 0xff7a2a : 0xffffff)
          .drawCircle(ball.x, ball.y, ballRadius)
          .endFill();

        this.balls.addChild(haloGfx);
        this.balls.addChild(gfx);
        this._ballPool.set(ball.id, { gfx, haloGfx });
      }
    }

    // --- hazards (falling enemy projectiles) — pool by array index ---
    // Hazards have no stable id; use a fixed-size ring buffer keyed by index.
    const hazards = s.hazards ?? [];

    // Grow pool if more hazards than pooled entries.
    while (this._hazardPool.length < hazards.length) {
      const halo = new Graphics();
      halo.blendMode = BLEND_MODES.ADD;
      const core = new Graphics();
      this.hazardsLayer.addChild(halo);
      this.hazardsLayer.addChild(core);
      this._hazardPool.push({ halo, core });
    }

    // Update visible entries.
    for (let i = 0; i < this._hazardPool.length; i++) {
      const { halo, core } = this._hazardPool[i];
      if (i < hazards.length) {
        const hz = hazards[i];
        halo.visible = true;
        core.visible = true;
        halo.clear().beginFill(HAZARD_GLOW_COLOR, HAZARD_GLOW_ALPHA)
          .drawCircle(hz.x, hz.y, HAZARD_RADIUS * HAZARD_GLOW_RADIUS_MULT).endFill();
        core.clear().beginFill(HAZARD_COLOR, 1)
          .drawCircle(hz.x, hz.y, HAZARD_RADIUS).endFill();
      } else {
        halo.visible = false;
        core.visible = false;
      }
    }

    // --- effects: consume snapshot events ---
    this.effectsLayer.consume(s.events, s.cellSize);
  }
}

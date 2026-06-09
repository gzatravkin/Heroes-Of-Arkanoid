/**
 * Boss.ts — Assembled animated multi-part boss rigs.
 *
 * Infers boss type from the boss-block sprite key (DemonBody → Demon,
 * GoblinBody → Goblin, WitchChest → Witch) and builds a layered Container
 * of part sprites over the boss-block region.
 *
 * Features:
 *  - Idle animation: subtle bob (body), hand sway (hands), head bob offset
 *  - Attack tell: lunge/flash on bossTelegraph / bossAttack events
 *  - HP-driven tint: boss darkens as HP drops
 *  - Defeat flourish: flash + explosion burst when boss dies
 *
 * Atlas keys used:
 *   Demon : hell/DemonBody, hell/DemonFace, hell/DemonFace2, hell/DemonFaceGlow,
 *            hell/DemonHand1, hell/DemonHand2, hell/DemonHand3
 *   Goblin: dungeon/GoblinBody, dungeon/GoblinHead, dungeon/GoblinHand1,
 *            dungeon/GoblinHand2, dungeon/GoblinHand3, dungeon/GoblinLeg1,
 *            dungeon/GoblinLeg2, dungeon/GoblinPants, dungeon/GoblinPlecho
 *   Witch : village/enemies/WitchChest, village/enemies/WitchHead1,
 *            village/enemies/WitchHand1, village/enemies/WitchHand2,
 *            village/enemies/WitchHand3, village/enemies/WitchLeg1,
 *            village/enemies/WitchLeg2, village/enemies/WitchSkirt,
 *            village/enemies/WitchMetla
 */

import {
  Container, Sprite, Graphics, BLEND_MODES, Texture
} from "pixi.js";
import { tex as atlasTex } from "./assets";
import { AnimSystem } from "./AnimSystem";
import { animStrip } from "./assets";

// ── Timing constants ─────────────────────────────────────────────────────────

// Idle bob: body oscillates vertically at this angular speed (ticker units).
const IDLE_BOB_SPEED   = 0.04;
const IDLE_BOB_AMOUNT  = 0.025; // fraction of bossH

// Idle sway: hands and accessories oscillate horizontally.
const IDLE_SWAY_SPEED  = 0.03;
const IDLE_SWAY_AMOUNT = 0.04;  // fraction of bossW

// Attack tell: how long the lunge-forward flash lasts (ms).
const ATTACK_LUNGE_DURATION_MS = 400;
// Lunge scale-up factor for the tell.
const ATTACK_LUNGE_SCALE = 1.22;
// Red flash alpha on telegraph.
const TELEGRAPH_FLASH_ALPHA = 0.7;

// HP colour thresholds for boss tint.
// Above 0.66: full bright.  0.33–0.66: orange tint.  Below 0.33: red tint.
const HP_TINT_LOW  = 0.33;
const HP_TINT_MID  = 0.66;
const HP_COLOR_LOW = 0xdd4422;  // angry red
const HP_COLOR_MID = 0xff9944;  // orange

// How many boss explosion bursts on defeat.
const DEFEAT_BURST_COUNT = 5;

// Explosion strip atlas key (same as Effects.ts).
const EXPLOSION_KEY = "effects/Explosion";
const EXPLOSION_FPS = 18;

// ── Type definitions ──────────────────────────────────────────────────────────

export type BossType = "Demon" | "Goblin" | "Witch" | "Unknown";

/** Infer the boss type from the sprite key on a boss block. */
export function inferBossType(spriteKey: string): BossType {
  if (spriteKey.includes("Demon")) return "Demon";
  if (spriteKey.includes("Goblin")) return "Goblin";
  if (spriteKey.includes("Witch")) return "Witch";
  return "Unknown";
}

/** Label shown in the boss HP bar. */
export function bossLabel(type: BossType): string {
  switch (type) {
    case "Demon":  return "DEMON LORD";
    case "Goblin": return "GOBLIN KING";
    case "Witch":  return "THE WITCH";
    default:       return "BOSS";
  }
}

// ── Boss rig ──────────────────────────────────────────────────────────────────

interface RigPart {
  sprite: Sprite;
  /** Base offset from rig center as fraction of (bossW, bossH). */
  relX: number;
  relY: number;
  /** Natural size as fraction of bossH. */
  scale: number;
  /** Is this part a "hand/sway" part (oscillates side-to-side)? */
  sway: boolean;
  /** Is this part a "body/bob" part (oscillates up-down with the body)? */
  bob: boolean;
}

export class BossRig {
  readonly container: Container;
  private body: Container;    // inner container that bob-translates
  private parts: RigPart[] = [];
  private flashGfx: Graphics; // red flash overlay
  private type: BossType;
  private _attackLunge = -1;  // ms elapsed into lunge anim, -1=inactive
  private _animSys: AnimSystem;
  // Stored region (set by setRegion each snapshot, read by update each tick).
  _regionW = 0;
  _regionH = 0;

  constructor(type: BossType) {
    this.type = type;
    this.container = new Container();
    this.body = new Container();
    this.container.addChild(this.body);

    this.flashGfx = new Graphics();
    this.flashGfx.blendMode = BLEND_MODES.ADD;
    this.flashGfx.alpha = 0;
    this.container.addChild(this.flashGfx);

    this._animSys = new AnimSystem();
    this.container.addChild(this._animSys.container);

    this._buildRig(type);
  }

  // ── Part construction ────────────────────────────────────────────────────

  private _buildRig(type: BossType) {
    switch (type) {
      case "Demon":  this._buildDemon();  break;
      case "Goblin": this._buildGoblin(); break;
      case "Witch":  this._buildWitch();  break;
      default:       this._buildFallback(); break;
    }
  }

  /**
   * Demon rig: body center, face above body, glow overlay, three hands arranged as arms.
   * Parts used: DemonBody (7), DemonFace (8), DemonFace2 (face alt), DemonFaceGlow (9),
   *             DemonHand1 (left arm up), DemonHand2 (right arm), DemonHand3 (claw).
   * Total: 7 sprites.
   */
  private _buildDemon() {
    this._addPart("hell/DemonBody",      0.0,  0.1,  0.90, false, true);
    this._addPart("hell/DemonFace",      0.0, -0.18, 0.55, false, true);
    this._addPart("hell/DemonFaceGlow",  0.0, -0.18, 0.55, false, true);  // additive glow on face
    this._addPart("hell/DemonHand1",    -0.52, 0.0,  0.38, true,  true);  // left arm
    this._addPart("hell/DemonHand2",     0.52, 0.0,  0.38, true,  true);  // right arm
    this._addPart("hell/DemonHand3",    -0.60, 0.28, 0.32, true,  false); // lower-left claw
    this._addPart("hell/DemonFace2",     0.0, -0.18, 0.42, false, true);  // alt face layer
    // Apply additive blend to glow part only
    const glowPart = this.parts[2];
    if (glowPart) {
      glowPart.sprite.blendMode = BLEND_MODES.ADD;
      glowPart.sprite.alpha = 0.6;
    }
  }

  /**
   * Goblin rig: body center, head above, pants below body, shoulder plates,
   *             two hands at sides, two legs at bottom.
   * Parts used: GoblinBody (10), GoblinHead (11), GoblinHand1 (12), GoblinHand2 (13),
   *             GoblinHand3 (14), GoblinLeg1 (15), GoblinLeg2 (16),
   *             GoblinPants (17), GoblinPlecho (18).
   * Total: 9 sprites.
   */
  private _buildGoblin() {
    this._addPart("dungeon/GoblinPants",  0.0,  0.35, 0.50, false, true);  // lower body/pants
    this._addPart("dungeon/GoblinBody",   0.0,  0.0,  0.70, false, true);  // torso
    this._addPart("dungeon/GoblinPlecho", 0.0, -0.05, 0.58, false, true);  // shoulder armor
    this._addPart("dungeon/GoblinHead",   0.0, -0.35, 0.50, false, true);  // head
    this._addPart("dungeon/GoblinHand1", -0.50,  0.05, 0.32, true,  true); // left hand
    this._addPart("dungeon/GoblinHand2",  0.50,  0.05, 0.32, true,  true); // right hand
    this._addPart("dungeon/GoblinHand3", -0.55, -0.05, 0.28, true,  true); // extra left
    this._addPart("dungeon/GoblinLeg1",  -0.22,  0.62, 0.30, true,  false); // left leg
    this._addPart("dungeon/GoblinLeg2",   0.22,  0.62, 0.30, true,  false); // right leg
  }

  /**
   * Witch rig: chest center, head above, skirt below, hands at sides,
   *            broom (metla) accessory, legs at bottom.
   * Parts used: WitchChest (10), WitchHead1 (11), WitchHand1 (12), WitchHand2 (13),
   *             WitchHand3 (14), WitchLeg1 (15), WitchLeg2 (16), WitchSkirt (17),
   *             WitchMetla (18).
   * Total: 9 sprites.
   */
  private _buildWitch() {
    this._addPart("village/enemies/WitchSkirt",  0.0,  0.38, 0.50, false, true);  // skirt/bottom
    this._addPart("village/enemies/WitchChest",  0.0,  0.05, 0.65, false, true);  // torso
    this._addPart("village/enemies/WitchHead1",  0.0, -0.32, 0.48, false, true);  // head+hat
    this._addPart("village/enemies/WitchHand1", -0.48,  0.0,  0.30, true,  true); // left arm
    this._addPart("village/enemies/WitchHand2",  0.48,  0.0,  0.30, true,  true); // right arm
    this._addPart("village/enemies/WitchHand3", -0.52,  0.15, 0.25, true,  false); // extra hand
    this._addPart("village/enemies/WitchLeg1",  -0.18,  0.65, 0.28, true,  false); // left leg
    this._addPart("village/enemies/WitchLeg2",   0.18,  0.65, 0.28, true,  false); // right leg
    this._addPart("village/enemies/WitchMetla",  0.52,  0.18, 0.35, true,  true);  // broom
  }

  /** Fallback: single body sprite when type is unknown. */
  private _buildFallback() {
    this._addPart("hell/DemonBody", 0.0, 0.0, 0.9, false, true);
  }

  private _addPart(
    key: string,
    relX: number, relY: number,
    scale: number,
    sway: boolean, bob: boolean,
  ) {
    const texture = atlasTex(key);
    const sp = new Sprite(texture);
    sp.anchor.set(0.5);
    // Tint fallback (Texture.WHITE) parts invisible until atlas loads.
    if (texture === Texture.WHITE) sp.alpha = 0;
    this.body.addChild(sp);
    this.parts.push({ sprite: sp, relX, relY, scale, sway, bob });
  }

  // ── Public API ────────────────────────────────────────────────────────────

  /** Destroy and clean up. */
  destroy() {
    this.container.destroy({ children: true });
  }

  /** Boss type label. */
  get bossType(): BossType { return this.type; }

  /**
   * Advance animation state and position the rig.
   * Called from the Pixi ticker with real delta-ms so animations play at true speed.
   *
   * @param cx   World-space center X of the boss region
   * @param cy   World-space center Y of the boss region
   * @param w    World-space width of the boss region
   * @param h    World-space height of the boss region
   * @param hpFrac 0..1 HP fraction (1 = full, 0 = dead)
   * @param tick  Ticker frame counter for animations
   * @param dtMs  Real delta-time in ms (must be >0 for animations to move)
   */
  update(cx: number, cy: number, w: number, h: number, hpFrac: number, tick: number, dtMs: number) {
    this.container.position.set(cx, cy);

    // Determine per-part size in world space (h is the "rig height" reference).
    const rigH = h;

    // Compute bob offset.
    const bobAmt = rigH * IDLE_BOB_AMOUNT * Math.sin(tick * IDLE_BOB_SPEED);

    // Compute attack lunge scale multiplier.
    let lungeScale = 1.0;
    if (this._attackLunge >= 0) {
      this._attackLunge += dtMs;
      const t = Math.min(this._attackLunge / ATTACK_LUNGE_DURATION_MS, 1);
      // Quick grow → fast shrink back: peak at t≈0.3
      const peak = Math.sin(t * Math.PI);
      lungeScale = 1.0 + (ATTACK_LUNGE_SCALE - 1.0) * peak;
      if (t >= 1) this._attackLunge = -1;
    }

    // Apply HP-based tint.
    let hpTint = 0xffffff;
    if (hpFrac < HP_TINT_LOW) hpTint = HP_COLOR_LOW;
    else if (hpFrac < HP_TINT_MID) hpTint = HP_COLOR_MID;

    // Flash overlay alpha fades toward 0 each frame.
    this.flashGfx.alpha = Math.max(0, this.flashGfx.alpha - 0.04 * (dtMs / 16.67));

    // Update each part.
    for (const part of this.parts) {
      const sp = part.sprite;

      // Re-check alpha: if texture loaded this frame, make visible.
      if (sp.texture !== Texture.WHITE && sp.alpha === 0 && !sp.blendMode) {
        sp.alpha = 1;
      }

      // Compute natural size.
      const naturalW = sp.texture.width  || 1;
      const naturalH = sp.texture.height || 1;
      const naturalDim = Math.max(naturalW, naturalH);
      const targetSize = rigH * part.scale * lungeScale;
      sp.scale.set(targetSize / naturalDim);

      // Position = relX * w + idle sway offset, relY * h + bob offset.
      const swayAmt = part.sway
        ? w * IDLE_SWAY_AMOUNT * Math.sin(tick * IDLE_SWAY_SPEED + part.relX * 2)
        : 0;
      const bobOffset = part.bob ? bobAmt : 0;

      sp.x = part.relX * w * 0.5 + swayAmt;
      sp.y = part.relY * rigH * 0.5 + bobOffset;

      // Apply HP tint (skip glow/additive parts).
      if (sp.blendMode !== BLEND_MODES.ADD) {
        sp.tint = hpTint;
      }
    }

    // Resize flash gfx to cover rig — use an ellipse for a natural body-glow shape.
    this.flashGfx.clear();
    if (this.flashGfx.alpha > 0.01) {
      // Outer halo ring (additive ellipse — looks like a pulsing energy burst).
      this.flashGfx.beginFill(0xff2200, 0.55)
        .drawEllipse(0, 0, w * 0.75, rigH * 0.7)
        .endFill();
      // Inner core: brighter, smaller.
      this.flashGfx.beginFill(0xff8844, 0.65)
        .drawEllipse(0, 0, w * 0.42, rigH * 0.42)
        .endFill();
    }

    // Drive AnimSystem.
    this._animSys.update(dtMs);
  }

  /**
   * Update the region the rig is anchored to (called from draw() per snapshot).
   * Does NOT advance animation time — the ticker calls update() for that.
   */
  setRegion(cx: number, cy: number, w: number, h: number) {
    this.container.position.set(cx, cy);
    // Store for use when the ticker calls update().
    this._regionW = w;
    this._regionH = h;
  }

  /** Call on bossTelegraph or bossAttack event — triggers the lunge + flash. */
  onTelegraph() {
    this._attackLunge = 0; // start lunge animation
    this.flashGfx.alpha = TELEGRAPH_FLASH_ALPHA;
  }

  /** Call when the boss is defeated — plays burst explosions. */
  onDefeat(cellSize: number) {
    const exFrames = animStrip(EXPLOSION_KEY, EXPLOSION_FPS);
    if (!exFrames.length) return;
    for (let i = 0; i < DEFEAT_BURST_COUNT; i++) {
      const offsetX = (Math.random() - 0.5) * cellSize * 3;
      const offsetY = (Math.random() - 0.5) * cellSize * 3;
      const delay = i * 120; // stagger by 120ms
      // Use AnimSystem's container via direct oneShot calls.
      // We schedule with a brief timeout to stagger the bursts.
      setTimeout(() => {
        this._animSys.oneShot(
          exFrames, EXPLOSION_FPS,
          offsetX, offsetY,
          cellSize * (1.5 + Math.random() * 2),
          true, 0xff8844,
        );
        // Extra flash.
        this.flashGfx.alpha = 0.6;
      }, delay);
    }
  }
}

// ── Telegraph warning glyph ───────────────────────────────────────────────────

/**
 * A reusable warning indicator drawn at a given world position.
 * Displayed on bossTelegraph events.
 */
export class TelegraphWarning {
  readonly container: Container;
  private gfx: Graphics;
  private _elapsed = 0;
  readonly duration = 600; // ms

  constructor() {
    this.container = new Container();
    this.gfx = new Graphics();
    this.gfx.blendMode = BLEND_MODES.ADD;
    this.container.addChild(this.gfx);
    this.container.visible = false;
  }

  /** Trigger the warning at world-space (x, y). */
  trigger(x: number, y: number, size: number) {
    this.container.position.set(x, y);
    this.container.visible = true;
    this._elapsed = 0;
    this._redraw(size, 1.0);
  }

  update(dtMs: number, size: number) {
    if (!this.container.visible) return;
    this._elapsed += dtMs;
    const t = this._elapsed / this.duration;
    if (t >= 1) {
      this.container.visible = false;
      return;
    }
    // Pulse: 3 rapid pulses then fade.
    const pulse = Math.abs(Math.sin(t * Math.PI * 4));
    const alpha = (1 - t) * pulse;
    this._redraw(size, alpha);
  }

  private _redraw(size: number, alpha: number) {
    const r = size * 0.55;
    this.gfx.clear();

    // Soft outer glow halo (wide, low-alpha additive fill).
    this.gfx.lineStyle(0).beginFill(0xff2200, alpha * 0.18)
      .drawCircle(0, 0, r * 1.5)
      .endFill();

    // Outer ring — double-stroked for readability.
    this.gfx.lineStyle(5, 0xff0000, alpha * 0.45)
      .drawCircle(0, 0, r);
    this.gfx.lineStyle(2.5, 0xff8800, alpha * 0.9)
      .drawCircle(0, 0, r);

    // Exclamation shaft (tall rect).
    this.gfx.lineStyle(0).beginFill(0xffcc00, alpha * 0.95)
      .drawRoundedRect(-r * 0.1, -r * 0.60, r * 0.2, r * 0.52, r * 0.05)
      .endFill();
    // Exclamation dot.
    this.gfx.beginFill(0xffcc00, alpha * 0.95)
      .drawCircle(0, r * 0.36, r * 0.13)
      .endFill();
  }
}

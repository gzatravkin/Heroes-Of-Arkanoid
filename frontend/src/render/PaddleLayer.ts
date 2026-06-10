import { Container, Sprite, Texture } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";
import { PROJECTILE_ID_THRESHOLD } from "./BallLayer";

// Paddle + turret rendering, extracted from Renderer. Owns both sprites (in one
// container for z-order), the per-class bar-frame animation, and the squash/stretch
// bounce animation. update() positions them from the snapshot; updateAnim() drives
// the bar cycling + squash timeline.

// Paddle bar animation: cycle through 4 class bar frames at a slow rate.
const PADDLE_ANIM_FPS = 6;
const PADDLE_ANIM_MS_PER_FRAME = 1000 / PADDLE_ANIM_FPS;

// Turret visual: barrel length and width as fractions of paddleH.
const TURRET_BARREL_LENGTH_MULT = 1.8;
const TURRET_BARREL_WIDTH_MULT  = 0.45;
const TURRET_SPRITE_KEY = "firemage/spell_fireturret/FireHeroTurret";

// Paddle squash/stretch: on ball bounce the paddle stretches briefly.
const PADDLE_SQUASH_DURATION_MS = 180; // total duration of the squash → stretch anim
const PADDLE_SQUASH_Y_SCALE     = 0.65; // minimum Y scale during squash peak
const PADDLE_STRETCH_X_SCALE    = 1.18; // maximum X scale at stretch peak

interface BallLike { id: number; y: number }

export class PaddleLayer {
  readonly container = new Container();
  // The original bar art is a HALF sprite (the left wing, cut at the centre gem) —
  // the legacy game mirrored it. We render two mirrored halves around the centre;
  // drawing the half once, stretched, was the old asymmetric-paddle bug.
  private leftHalf  = new Sprite();
  private rightHalf = new Sprite();
  private turretSprite = new Sprite();

  // Per-class bar art keys (default fire mage; overridden by setClass).
  private _spriteKey = "firemage/bars/v2FireHero1";
  private _animKeys: string[] = [
    "firemage/bars/v2FireHero1", "firemage/bars/v2FireHero2",
    "firemage/bars/v2FireHero3", "firemage/bars/v2FireHero4",
  ];
  private _animFrame = 0;
  private _animElapsed = 0;

  // Squash/stretch state.
  private _squashElapsed = -1; // -1 = inactive; >=0 = ms into the animation
  private _baseScaleX = 1;
  private _baseScaleY = 1;
  // Previous y per ball id — squash triggers on band ENTRY, not presence.
  private _prevBallY = new Map<number, number>();

  constructor() {
    // Both halves anchor on their inner (cut) edge so they meet at the centre.
    this.leftHalf.anchor.set(1, 0.5);
    this.leftHalf.texture = Texture.WHITE;
    this.rightHalf.anchor.set(1, 0.5); // mirrored via negative X scale
    this.rightHalf.texture = Texture.WHITE;
    this.turretSprite = new Sprite(Texture.WHITE);
    this.turretSprite.anchor.set(0.5, 1); // anchor at bottom-center
    this.turretSprite.visible = false;
    this.container.addChild(this.leftHalf, this.rightHalf, this.turretSprite);
  }

  /** Switch the paddle bar art to the given class's 4-frame strip. */
  setClass(paddleKeys: string[]): void {
    this._animKeys  = paddleKeys;
    this._spriteKey = paddleKeys[0];
    this._animFrame = 0;
  }

  update(
    paddleX: number, paddleW: number, paddleH: number,
    boardH: number, cellSize: number, turretActive: boolean, balls: BallLike[],
  ): void {
    // --- paddle squash trigger: a ball ENTERING the paddle y-band from above ---
    // Edge-triggered on the band boundary. The old level-trigger restarted the
    // squash every snapshot while a served ball rested on the paddle, so the
    // bar pulsed forever (docs/13: "constantly changing size").
    const paddleYCenter = (boardH + cellSize) - paddleH / 2;
    const paddleBounceZone = paddleH * 2.5;
    const zoneTop = paddleYCenter - paddleBounceZone;
    const seen = new Set<number>();
    for (const ball of balls) {
      if (ball.id >= PROJECTILE_ID_THRESHOLD) continue; // skip turret bullets
      seen.add(ball.id);
      const prevY = this._prevBallY.get(ball.id);
      if (prevY !== undefined && prevY < zoneTop && ball.y >= zoneTop && this._squashElapsed < 0) {
        this._squashElapsed = 0; // start squash animation
      }
      this._prevBallY.set(ball.id, ball.y);
    }
    // Drop stale entries so the map doesn't grow over a long level.
    for (const id of this._prevBallY.keys()) {
      if (!seen.has(id)) this._prevBallY.delete(id);
    }

    // --- paddle (two mirrored halves of the half-sprite bar art) ---
    // Swap to per-class atlas paddle texture on first draw (ticker advances frames).
    const paddleTex = atlasTex(this._spriteKey);
    if (paddleTex !== Texture.WHITE) {
      this.leftHalf.texture  = paddleTex;
      this.rightHalf.texture = paddleTex;
    }
    this.leftHalf.position.set(paddleX, paddleYCenter);
    this.rightHalf.position.set(paddleX, paddleYCenter);
    // Scale is anchored to the FIRST animation frame, not the current one: the
    // 4-frame strips vary in width (fire mage: 240→369 px at a constant 171 px
    // height — the wings flare outward). Per-frame width compensation made the
    // bar's rendered height pulse 1.5× at 6 fps (docs/13: "constantly changing
    // size"). With one uniform scale, height stays constant and wide frames
    // flare past the collision width as the art intends.
    const baseTex = atlasTex(this._animKeys[0]);
    const baseW = baseTex !== Texture.WHITE ? baseTex.width  : this.leftHalf.texture.width;
    const baseH = baseTex !== Texture.WHITE ? baseTex.height : this.leftHalf.texture.height;
    if (baseW > 1) {
      const wScale  = (paddleW / 2) / baseW;
      const spriteH = Math.max(paddleH, baseH * wScale);
      this._baseScaleX = wScale;
      this._baseScaleY = spriteH / baseH;
      // Only reset to base scale if no squash animation is running.
      if (this._squashElapsed < 0) this.applyScale(1, 1);
    }

    // --- turret indicator (atlas art: FireHeroTurret) ---
    if (turretActive) {
      const turretAtlasTex = tex(TURRET_SPRITE_KEY);
      if (turretAtlasTex !== Texture.WHITE) this.turretSprite.texture = turretAtlasTex;
      this.turretSprite.visible = true;
      this.turretSprite.width   = paddleH * TURRET_BARREL_WIDTH_MULT * 2;
      this.turretSprite.height  = paddleH * TURRET_BARREL_LENGTH_MULT;
      this.turretSprite.x       = paddleX;
      this.turretSprite.y       = paddleYCenter - paddleH / 2;
    } else {
      this.turretSprite.visible = false;
    }
  }

  /** Apply scale multipliers to both halves (right half mirrors via negative X). */
  private applyScale(xMult: number, yMult: number): void {
    this.leftHalf.scale.set(this._baseScaleX * xMult, this._baseScaleY * yMult);
    this.rightHalf.scale.set(-this._baseScaleX * xMult, this._baseScaleY * yMult);
  }

  /** Drives the squash/stretch timeline + the bar-frame cycling each frame. */
  updateAnim(dtMs: number): void {
    // Paddle squash/stretch animation.
    if (this._squashElapsed >= 0) {
      this._squashElapsed += dtMs;
      const t = Math.min(this._squashElapsed / PADDLE_SQUASH_DURATION_MS, 1);
      // Phase 1 (0→0.4): squash — compress Y, expand X. Phase 2 (0.4→1.0): spring back.
      let xScale = 1.0;
      let yScale = 1.0;
      if (t < 0.4) {
        const p = t / 0.4;
        xScale = 1.0 + (PADDLE_STRETCH_X_SCALE - 1.0) * p;
        yScale = 1.0 - (1.0 - PADDLE_SQUASH_Y_SCALE) * p;
      } else {
        const p = (t - 0.4) / 0.6;
        const overshoot = Math.sin(p * Math.PI) * 0.06;
        xScale = PADDLE_STRETCH_X_SCALE - (PADDLE_STRETCH_X_SCALE - 1.0) * p + overshoot;
        yScale = PADDLE_SQUASH_Y_SCALE + (1.0 - PADDLE_SQUASH_Y_SCALE) * p - overshoot;
      }
      this.applyScale(xScale, yScale);
      if (t >= 1) {
        this._squashElapsed = -1;
        this.applyScale(1, 1);
      }
    }

    // Paddle bar animation: cycle through the 4 class bar frames.
    this._animElapsed += dtMs;
    if (this._animElapsed >= PADDLE_ANIM_MS_PER_FRAME) {
      this._animElapsed -= PADDLE_ANIM_MS_PER_FRAME;
      this._animFrame = (this._animFrame + 1) % this._animKeys.length;
      const nextTex = atlasTex(this._animKeys[this._animFrame]);
      if (nextTex !== Texture.WHITE) {
        this.leftHalf.texture  = nextTex;
        this.rightHalf.texture = nextTex;
      }
    }
  }
}

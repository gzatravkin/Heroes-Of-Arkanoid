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
  private paddleSprite = new Sprite();
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

  constructor() {
    this.paddleSprite.anchor.set(0.5);
    this.paddleSprite.texture = Texture.WHITE;
    this.turretSprite = new Sprite(Texture.WHITE);
    this.turretSprite.anchor.set(0.5, 1); // anchor at bottom-center
    this.turretSprite.visible = false;
    this.container.addChild(this.paddleSprite, this.turretSprite);
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
    // --- paddle squash trigger: detect a non-projectile ball passing the paddle y-band ---
    const paddleYCenter = (boardH + cellSize) - paddleH / 2;
    const paddleBounceZone = paddleH * 2.5;
    for (const ball of balls) {
      if (ball.id >= PROJECTILE_ID_THRESHOLD) continue; // skip turret bullets
      if (Math.abs(ball.y - paddleYCenter) < paddleBounceZone && this._squashElapsed < 0) {
        this._squashElapsed = 0; // start squash animation
      }
    }

    // --- paddle (sprite) ---
    // Swap to per-class atlas paddle texture on first draw (ticker advances frames).
    const paddleTex = atlasTex(this._spriteKey);
    if (paddleTex !== Texture.WHITE) this.paddleSprite.texture = paddleTex;
    this.paddleSprite.x = paddleX;
    this.paddleSprite.y = paddleYCenter;
    // Scale so the sprite width matches the sim paddle; keep natural aspect for height.
    const paddleNaturalW = this.paddleSprite.texture.width;
    const paddleNaturalH = this.paddleSprite.texture.height;
    if (paddleNaturalW > 0) {
      const wScale = paddleW / paddleNaturalW;
      const spriteH = Math.max(paddleH, paddleNaturalH * wScale);
      this._baseScaleX = wScale;
      this._baseScaleY = spriteH / paddleNaturalH;
      // Only reset to base scale if no squash animation is running.
      if (this._squashElapsed < 0) {
        this.paddleSprite.scale.set(this._baseScaleX, this._baseScaleY);
      }
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
      this.paddleSprite.scale.x = this._baseScaleX * xScale;
      this.paddleSprite.scale.y = this._baseScaleY * yScale;
      if (t >= 1) {
        this._squashElapsed = -1;
        this.paddleSprite.scale.set(this._baseScaleX, this._baseScaleY);
      }
    }

    // Paddle bar animation: cycle through the 4 class bar frames.
    this._animElapsed += dtMs;
    if (this._animElapsed >= PADDLE_ANIM_MS_PER_FRAME) {
      this._animElapsed -= PADDLE_ANIM_MS_PER_FRAME;
      this._animFrame = (this._animFrame + 1) % this._animKeys.length;
      const nextTex = atlasTex(this._animKeys[this._animFrame]);
      if (nextTex !== Texture.WHITE) this.paddleSprite.texture = nextTex;
    }
  }
}

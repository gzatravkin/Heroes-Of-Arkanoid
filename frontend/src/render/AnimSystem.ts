/**
 * AnimSystem.ts — Pooled one-shot and looping AnimatedSprite system.
 *
 * Usage:
 *   const sys = new AnimSystem(parentContainer);
 *   sys.oneShot(frames, fps, x, y, size);   // plays once, auto-removes
 *   const id = sys.looping(frames, fps, x, y, size); // loops until removed
 *   sys.remove(id);
 *   sys.update(dtMs); // call each frame
 */

import { Container, AnimatedSprite, BLEND_MODES, Rectangle, Texture } from "pixi.js";

// Max simultaneous one-shot animations. Lowered 48→16 in the readability overhaul (2026-06-16 §E): a
// field-clear used to fire dozens of overlapping additive explosions at once → a white-out the ball
// vanished into. 16 keeps impact punchy without burying the playfield.
const MAX_ONE_SHOTS = 16;

// Blend mode used for additive fire/explosion effects.
const ADDITIVE = BLEND_MODES.ADD;

export interface AnimHandle { id: number }

interface OneShotEntry {
  anim: AnimatedSprite;
  /** elapsed ms */
  elapsed: number;
  /** total duration ms */
  duration: number;
}

interface LoopEntry {
  id: number;
  anim: AnimatedSprite;
}

let _nextId = 1;

export class AnimSystem {
  readonly container: Container;
  private oneShots: OneShotEntry[] = [];
  private loops = new Map<number, LoopEntry>();

  constructor(parent?: Container) {
    this.container = new Container();
    if (parent) parent.addChild(this.container);
  }

  /**
   * Slice a horizontal sprite strip into individual frame Textures.
   * The strip is assumed to contain square frames:
   *   frameSize = texture.height
   *   frameCount = round(texture.width / texture.height)
   * Returns the sliced array, or an empty array if the texture is invalid.
   */
  static sliceStrip(texture: Texture): Texture[] {
    if (!texture || texture === Texture.EMPTY || texture === Texture.WHITE) return [];
    const { width, height } = texture;
    if (height <= 0) return [];
    const frameSize = height;
    const frameCount = Math.max(1, Math.round(width / frameSize));
    if (frameCount === 1) return [texture];
    const base = texture.baseTexture;
    // The texture's frame gives the top-left origin within the atlas page.
    const ox = texture.frame.x;
    const oy = texture.frame.y;
    const frames: Texture[] = [];
    for (let i = 0; i < frameCount; i++) {
      frames.push(new Texture(base, new Rectangle(ox + i * frameSize, oy, frameSize, frameSize)));
    }
    return frames;
  }

  /**
   * Play a one-shot animation at (x, y) in world space.
   * `size` is the desired display size in world units.
   * `additive` uses ADD blend mode (good for fire/explosions).
   * `tint` optionally tints the sprite.
   */
  oneShot(
    frames: Texture[],
    fps: number,
    x: number, y: number,
    size: number,
    additive = true,
    tint = 0xffffff,
  ) {
    if (!frames.length) return;
    // Throttle: drop oldest one-shot if we're at the cap.
    if (this.oneShots.length >= MAX_ONE_SHOTS) {
      const oldest = this.oneShots.shift()!;
      this.container.removeChild(oldest.anim);
      oldest.anim.destroy();
    }

    const anim = new AnimatedSprite(frames);
    anim.anchor.set(0.5);
    anim.position.set(x, y);
    anim.blendMode = additive ? ADDITIVE : BLEND_MODES.NORMAL;
    anim.tint = tint;
    anim.loop = false;
    anim.animationSpeed = fps / 60; // Pixi animSpeed is in frames-per-ticker-frame at 60fps
    // Scale to desired size using the natural frame dimensions.
    const naturalSize = Math.max(frames[0].width, 1);
    anim.scale.set(size / naturalSize);
    anim.play();

    const duration = (frames.length / fps) * 1000;
    this.container.addChild(anim);
    this.oneShots.push({ anim, elapsed: 0, duration });
  }

  /**
   * Spawn a looping animation. Returns an AnimHandle to remove it later.
   */
  looping(
    frames: Texture[],
    fps: number,
    x: number, y: number,
    size: number,
    additive = true,
    tint = 0xffffff,
  ): AnimHandle {
    const id = _nextId++;
    if (!frames.length) return { id };

    const anim = new AnimatedSprite(frames);
    anim.anchor.set(0.5);
    anim.position.set(x, y);
    anim.blendMode = additive ? ADDITIVE : BLEND_MODES.NORMAL;
    anim.tint = tint;
    anim.loop = true;
    anim.animationSpeed = fps / 60;
    const naturalSize = Math.max(frames[0].width, 1);
    anim.scale.set(size / naturalSize);
    anim.play();

    this.container.addChild(anim);
    this.loops.set(id, { id, anim });
    return { id };
  }

  /** Remove a looping animation by handle. */
  remove(handle: AnimHandle) {
    const entry = this.loops.get(handle.id);
    if (!entry) return;
    this.container.removeChild(entry.anim);
    entry.anim.stop();
    entry.anim.destroy();
    this.loops.delete(handle.id);
  }

  /** Move a looping animation to a new position. */
  moveTo(handle: AnimHandle, x: number, y: number) {
    const entry = this.loops.get(handle.id);
    if (entry) entry.anim.position.set(x, y);
  }

  /** Resize a looping animation. size is in world units. */
  resize(handle: AnimHandle, size: number) {
    const entry = this.loops.get(handle.id);
    if (!entry) return;
    const naturalSize = Math.max(entry.anim.width / entry.anim.scale.x, 1);
    entry.anim.scale.set(size / naturalSize);
  }

  /** Call every ticker frame with delta in ms. */
  update(dtMs: number) {
    const done: OneShotEntry[] = [];
    for (const e of this.oneShots) {
      e.elapsed += dtMs;
      const t = Math.min(e.elapsed / e.duration, 1);
      // Fade out in the final 30% of the animation.
      e.anim.alpha = t > 0.7 ? 1 - (t - 0.7) / 0.3 : 1;
      if (t >= 1) done.push(e);
    }
    for (const e of done) {
      this.container.removeChild(e.anim);
      e.anim.stop();
      e.anim.destroy();
      this.oneShots.splice(this.oneShots.indexOf(e), 1);
    }
  }

  /** Remove all active animations (call on level reset). */
  clear() {
    for (const e of this.oneShots) {
      this.container.removeChild(e.anim);
      e.anim.destroy();
    }
    this.oneShots = [];
    for (const [, e] of this.loops) {
      this.container.removeChild(e.anim);
      e.anim.stop();
      e.anim.destroy();
    }
    this.loops.clear();
  }
}

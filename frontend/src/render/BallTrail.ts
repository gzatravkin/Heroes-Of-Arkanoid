import { Container, Graphics } from "pixi.js";

// Number of historical positions kept per ball.
const TRAIL_LENGTH = 7;
// Alpha of the oldest trail dot (newest is always 1 → fades toward oldest).
const TRAIL_ALPHA_MIN = 0.04;
// Trail dot radius as a fraction of the ball radius.
const TRAIL_DOT_FRAC = 0.72;

// Colors for normal vs ignited ball trails.
const TRAIL_COLOR_NORMAL  = 0x88ccff; // cool blue-white
const TRAIL_COLOR_IGNITED = 0xff6a00; // hot orange

interface TrailEntry {
  x: number;
  y: number;
  ignited: boolean;
}

export class BallTrail {
  readonly container: Container;
  /** Map from ball id → circular buffer of recent positions. */
  private history = new Map<number, TrailEntry[]>();

  constructor() {
    this.container = new Container();
  }

  /**
   * Call once per snapshot after positions are known.
   * `balls` is the snapshot ball array; `ballRadius` is the radius in world units.
   * Clears and redraws the trail graphics.
   */
  update(
    balls: { id: number; x: number; y: number; ignited: boolean }[],
    ballRadius: number,
  ) {
    // Build a set of live ids so we can prune stale entries.
    const liveIds = new Set(balls.map((b) => b.id));

    // Prune dead balls.
    for (const id of this.history.keys()) {
      if (!liveIds.has(id)) this.history.delete(id);
    }

    // Append current position for each live ball.
    for (const ball of balls) {
      if (!this.history.has(ball.id)) this.history.set(ball.id, []);
      const buf = this.history.get(ball.id)!;
      buf.push({ x: ball.x, y: ball.y, ignited: ball.ignited });
      // Keep only the most recent TRAIL_LENGTH entries.
      if (buf.length > TRAIL_LENGTH) buf.splice(0, buf.length - TRAIL_LENGTH);
    }

    // Redraw all trail dots.
    this.container.removeChildren();
    const dotRadius = ballRadius * TRAIL_DOT_FRAC;

    for (const [, buf] of this.history) {
      const len = buf.length;
      // Skip the last entry (index len-1) — that is the current ball position
      // which Renderer already draws; start from len-2 going backward.
      for (let i = len - 2; i >= 0; i--) {
        const entry = buf[i];
        // t=0 → oldest visible dot; t=1 → just before the ball.
        const t = i / Math.max(len - 1, 1);
        const alpha = TRAIL_ALPHA_MIN + (1 - TRAIL_ALPHA_MIN) * t;
        const scale = 0.35 + 0.65 * t; // shrink toward the tail
        const color = entry.ignited ? TRAIL_COLOR_IGNITED : TRAIL_COLOR_NORMAL;

        const g = new Graphics();
        g.beginFill(color, alpha)
          .drawCircle(entry.x, entry.y, dotRadius * scale)
          .endFill();
        this.container.addChild(g);
      }
    }
  }

  /** Call when the level resets / ball list changes drastically. */
  clear() {
    this.history.clear();
    this.container.removeChildren();
  }
}

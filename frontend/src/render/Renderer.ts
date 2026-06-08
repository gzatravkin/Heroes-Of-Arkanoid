import { Application, Container, Graphics, Sprite, BLEND_MODES } from "pixi.js";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";
import { Effects } from "./Effects";

// Visible gap between bricks so the wall doesn't merge into a solid sheet.
// Expressed as a fraction of cellSize; enforces a 2 px minimum.
const GAP_FRAC = 0.12;

// Extra height below the block grid to ensure the paddle and its clearance are
// fully visible (grid + paddle zone + margin).
const PADDLE_ZONE_CELLS = 3;

// Halo drawn behind ignited balls: radius multiplier and alpha.
const IGNITE_HALO_RADIUS_MULT = 1.8;
const IGNITE_HALO_ALPHA = 0.35;

export class Renderer {
  app: Application;
  private world = new Container();
  private blocks = new Container();
  private effectsLayer: Effects;
  private paddle = new Graphics();
  private balls = new Container();

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();

    // Layer order: blocks → effects → balls → paddle (effects behind balls)
    this.world.addChild(
      this.blocks,
      this.effectsLayer.container,
      this.balls,
      this.paddle,
    );
    this.app.stage.addChild(this.world);

    // Tick the effects every frame.
    this.app.ticker.add((delta) => {
      // delta is in Pixi ticker units (frames at 60 fps → multiply by 1000/60 for ms)
      const dtMs = (delta / 60) * 1000;
      this.effectsLayer.update(dtMs);
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
    this.world.position.set(
      (this.app.screen.width - s.boardW * scale) / 2,
      (this.app.screen.height - effectiveH * scale) / 2,
    );
  }

  draw(s: Snapshot) {
    this.fit(s);

    // --- blocks ---
    const gap = Math.max(s.cellSize * GAP_FRAC, 2);
    const brickSize = s.cellSize - gap;
    this.blocks.removeChildren();
    for (const b of s.blocks) {
      const sp = new Sprite(tex(b.sprite));
      sp.anchor.set(0.5);
      sp.width = brickSize;
      sp.height = brickSize;
      sp.position.set(b.x, b.y);
      sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
      this.blocks.addChild(sp);
    }

    // --- paddle ---
    this.paddle.clear();
    this.paddle.beginFill(0x7fd1ff).drawRect(
      s.paddleX - s.paddleW / 2,
      (s.boardH + s.cellSize) - s.paddleH / 2,
      s.paddleW,
      s.paddleH,
    ).endFill();

    // --- balls ---
    this.balls.removeChildren();
    for (const ball of s.balls) {
      const ballRadius = s.cellSize * 0.25;
      const g = new Graphics();

      if (ball.ignited) {
        // Draw an additive halo behind the ball to signal the ignite status.
        g.beginFill(0xff7a2a, IGNITE_HALO_ALPHA)
          .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
          .endFill();
        // The halo is drawn with additive blend; use a child container for the
        // normal ball circle on top so it composites cleanly.
        const haloGfx = new Graphics();
        haloGfx.blendMode = BLEND_MODES.ADD;
        haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
          .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
          .endFill();
        this.balls.addChild(haloGfx);
      }

      g.beginFill(ball.ignited ? 0xff7a2a : 0xffffff)
        .drawCircle(ball.x, ball.y, ballRadius)
        .endFill();
      this.balls.addChild(g);
    }

    // --- effects: consume snapshot events ---
    this.effectsLayer.consume(s.events, s.cellSize);
  }
}

import { Application, Container, Graphics, Sprite, BLEND_MODES, Texture } from "pixi.js";
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

// Fire wall band height as a fraction of cellSize.
const FIRE_WALL_HEIGHT_MULT = 1.1;

// Turret visual: barrel length and width as fractions of paddleH.
const TURRET_BARREL_LENGTH_MULT = 1.8;
const TURRET_BARREL_WIDTH_MULT  = 0.45;

// Note: turret bullets (id >= 10000) render via the normal ball path — no special branch needed.

export class Renderer {
  app: Application;
  private world = new Container();
  private blocks = new Container();
  private effectsLayer: Effects;
  private fireWalls = new Container();
  private paddle = new Graphics();
  private turretSprite = new Sprite();
  private balls = new Container();
  private _tick = 0; // used to drive wall flicker animation

  constructor(host: HTMLElement) {
    this.app = new Application({ resizeTo: host, background: "#0b0b12", antialias: true });
    host.appendChild(this.app.view as HTMLCanvasElement);

    this.effectsLayer = new Effects();

    // Try to load the turret sprite; fall back to Graphics if it fails.
    this.turretSprite = Sprite.from("/art/FireHeroTurret.png");
    this.turretSprite.anchor.set(0.5, 1); // anchor at bottom-center
    this.turretSprite.visible = false;

    // Layer order: blocks → fireWalls → effects → balls → paddle → turret
    this.world.addChild(
      this.blocks,
      this.fireWalls,
      this.effectsLayer.container,
      this.balls,
      this.paddle,
      this.turretSprite,
    );
    this.app.stage.addChild(this.world);

    // Tick the effects every frame and drive wall flicker.
    this.app.ticker.add((delta) => {
      // delta is in Pixi ticker units (frames at 60 fps → multiply by 1000/60 for ms)
      const dtMs = (delta / 60) * 1000;
      this.effectsLayer.update(dtMs);
      this._tick += delta;
      // Animate the alpha flicker on each fire-wall tile every frame.
      for (let i = 0; i < this.fireWalls.children.length; i++) {
        const child = this.fireWalls.children[i];
        const flicker = 0.72 + 0.28 * Math.sin(this._tick * 0.18 + i * 1.3);
        child.alpha = flicker;
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

import { Container, Sprite, BLEND_MODES } from "pixi.js";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";

interface Particle {
  sprite: Sprite;
  /** base scale (set at spawn) */
  baseScale: number;
  /** total lifetime in ms */
  life: number;
  /** elapsed ms */
  elapsed: number;
  /** multiplier at t=0 */
  scaleStart: number;
  /** multiplier at t=1 */
  scaleEnd: number;
}

export class Effects {
  readonly container: Container;
  private particles: Particle[] = [];

  constructor() {
    this.container = new Container();
  }

  /** Spawn effects for all events in the snapshot. Call once per snapshot. */
  consume(events: Snapshot["events"], cellSize: number) {
    for (const ev of events) {
      if (ev.type === "blockDestroyed") {
        this.spawnExplosion(ev.x, ev.y, cellSize);
      } else if (ev.type === "burn") {
        this.spawnBurn(ev.x, ev.y, cellSize);
      } else if (ev.type === "ignite" || ev.type === "spellCast") {
        this.spawnFlash(ev.x, ev.y, cellSize);
      }
    }
  }

  private addParticle(
    sp: Sprite,
    x: number, y: number,
    sizeInWorld: number,
    life: number,
    scaleStart: number, scaleEnd: number,
  ) {
    sp.anchor.set(0.5);
    sp.blendMode = BLEND_MODES.ADD;
    sp.position.set(x, y);
    // Use uniform scale; sizeInWorld is the desired world-unit size at t=0.
    const baseScale = sizeInWorld / Math.max(sp.texture.width, 1);
    sp.scale.set(baseScale * scaleStart);
    sp.alpha = 1;
    this.container.addChild(sp);
    this.particles.push({ sprite: sp, baseScale, life, elapsed: 0, scaleStart, scaleEnd });
  }

  private spawnExplosion(x: number, y: number, cellSize: number) {
    const sp = new Sprite(tex("Explosion"));
    this.addParticle(sp, x, y, cellSize * 1.4, 280, 1.0, 1.6);
  }

  private spawnBurn(x: number, y: number, cellSize: number) {
    const sp = new Sprite(tex("Explosion"));
    sp.tint = 0xff6600;
    this.addParticle(sp, x, y, cellSize * 0.7, 180, 1.0, 1.4);
  }

  private spawnFlash(x: number, y: number, cellSize: number) {
    const sp = new Sprite(tex("Explosion"));
    sp.tint = 0xffffff;
    this.addParticle(sp, x, y, cellSize * 0.5, 120, 1.0, 1.3);
  }

  /** Call every ticker frame with the delta in ms. */
  update(dtMs: number) {
    const dead: Particle[] = [];
    for (const p of this.particles) {
      p.elapsed += dtMs;
      const t = Math.min(p.elapsed / p.life, 1);
      p.sprite.alpha = 1 - t;
      const scaleMult = p.scaleStart + (p.scaleEnd - p.scaleStart) * t;
      p.sprite.scale.set(p.baseScale * scaleMult);
      if (t >= 1) dead.push(p);
    }
    for (const p of dead) {
      this.container.removeChild(p.sprite);
      this.particles.splice(this.particles.indexOf(p), 1);
    }
  }
}

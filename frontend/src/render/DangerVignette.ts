import { Application, Graphics } from "pixi.js";

// A red edge vignette that pulses when the player is in danger (critically low HP).
// Classic "you're about to die" feedback — builds tension without obscuring the playfield.
const INNER_FRAC = 0.40; // red starts further out than the dark vignette
const RED = 0xcc1414;

export class DangerVignette {
  private gfx = new Graphics();

  constructor(app: Application) {
    app.stage.addChild(this.gfx);
    this.gfx.alpha = 0;
    app.renderer.on("resize", () => this.rebuild(app));
    this.rebuild(app);
  }

  private rebuild(app: Application) {
    const w = app.screen.width, h = app.screen.height;
    const cx = w / 2, cy = h / 2;
    this.gfx.clear();
    const STEPS = 10;
    const innerR = Math.min(w, h) * INNER_FRAC;
    const outerR = Math.sqrt(cx * cx + cy * cy);
    for (let i = 0; i < STEPS; i++) {
      const t = (i + 1) / STEPS;
      const r = innerR + (outerR - innerR) * t;
      const a = (t * t) * 0.9; // quadratic falloff, transparent at centre
      const rx = Math.max(cx - r, 0), ry = Math.max(cy - r, 0);
      const rw = Math.min(r * 2, w), rh = Math.min(r * 2, h);
      this.gfx.beginFill(RED, a / STEPS).drawRect(rx, ry, rw, rh).endFill();
    }
  }

  /** intensity 0..1 (0 = hidden). `pulse` true → throb; false → steady (reduced-motion). */
  update(intensity: number, tick: number, pulse: boolean) {
    if (intensity <= 0) { this.gfx.alpha = 0; return; }
    const throb = pulse ? 0.55 + 0.45 * Math.sin(tick * 0.18) : 0.7;
    this.gfx.alpha = intensity * throb;
  }
}

import { Application, Graphics } from "pixi.js";

// How much of the screen the vignette covers (0 = nothing, 1 = full width/height).
// The darkened region starts at INNER_FRAC from centre and reaches the edges.
const INNER_FRAC = 0.55;   // fraction of the smaller screen dimension — inner bright circle
const OUTER_ALPHA = 0.52;  // opacity at the very corner

/**
 * Subtle full-screen vignette drawn as a dark radial overlay.
 * Uses a Graphics-based approximation (several concentric rectangles fading
 * from fully transparent in the centre to semi-opaque black at the edges).
 *
 * This avoids a ShaderFilter dependency and is extremely cheap.
 */
export class Vignette {
  private gfx: Graphics;

  constructor(app: Application) {
    this.gfx = new Graphics();
    // Sit on top of everything else on the stage.
    app.stage.addChild(this.gfx);

    // Rebuild when the renderer resizes.
    app.renderer.on("resize", () => this.rebuild(app));
    this.rebuild(app);
  }

  private rebuild(app: Application) {
    const w = app.screen.width;
    const h = app.screen.height;
    const cx = w / 2;
    const cy = h / 2;

    this.gfx.clear();

    // Draw concentric rings fading from transparent centre to dark edge.
    // We use a radial approach: layer several filled rects with SUBTRACT-like
    // blending — but Pixi normal alpha blend with black rectangles at decreasing
    // radius does the job visually without needing special blend modes.
    const STEPS = 12;
    const innerR = Math.min(w, h) * INNER_FRAC;
    const outerR = Math.sqrt(cx * cx + cy * cy); // corner distance

    for (let i = 0; i < STEPS; i++) {
      // t=0 → inner (transparent), t=1 → outer (dark)
      const t = (i + 1) / STEPS;
      const r = innerR + (outerR - innerR) * t;
      const alpha = OUTER_ALPHA * (t * t); // quadratic falloff — barely visible in centre

      // Clip rect around the circle.
      const rx = Math.max(cx - r, 0);
      const ry = Math.max(cy - r, 0);
      const rw = Math.min(r * 2, w);
      const rh = Math.min(r * 2, h);

      this.gfx.beginFill(0x000000, alpha / STEPS)
        .drawRect(rx, ry, rw, rh)
        .endFill();
    }

    // Solid dark corners: draw the four corner triangles at moderate opacity.
    const cornerAlpha = OUTER_ALPHA * 0.45;
    this.gfx.beginFill(0x000000, cornerAlpha)
      .drawRect(0, 0, cx * 0.28, cy * 0.28)          // top-left
      .drawRect(w - cx * 0.28, 0, cx * 0.28, cy * 0.28)       // top-right
      .drawRect(0, h - cy * 0.28, cx * 0.28, cy * 0.28)       // bottom-left
      .drawRect(w - cx * 0.28, h - cy * 0.28, cx * 0.28, cy * 0.28) // bottom-right
      .endFill();
  }
}

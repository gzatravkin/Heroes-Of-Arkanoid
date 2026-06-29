import { Container, Sprite, Texture } from "pixi.js";

/**
 * SpritePool — a tiny per-frame pooled-sprite container for world-space effects that used to be drawn as
 * procedural `Graphics` (minions, pillars, telegraph glows, …). Usage each frame:
 *
 *   pool.begin();
 *   const sp = pool.next(tex);  sp.position.set(x, y);  sp.width = w; ...
 *   pool.end();                 // hides any sprites not used this frame
 *
 * Sprites are anchored centre by default and reset (alpha/tint/rotation/scale) on each `next()`.
 */
export class SpritePool {
  readonly container = new Container();
  private pool: Sprite[] = [];
  private used = 0;

  begin(): void { this.used = 0; }

  next(tex: Texture): Sprite {
    let sp = this.pool[this.used];
    if (!sp) {
      sp = new Sprite();
      sp.anchor.set(0.5);
      this.container.addChild(sp);
      this.pool.push(sp);
    }
    sp.texture = tex;
    sp.visible = true;
    sp.alpha = 1;
    sp.tint = 0xffffff;
    sp.rotation = 0;
    sp.scale.set(1, 1);
    this.used++;
    return sp;
  }

  end(): void {
    for (let i = this.used; i < this.pool.length; i++) this.pool[i].visible = false;
  }

  /** Size a centre-anchored sprite to a target width/height in world units (keeps the art undistorted if h omitted). */
  static fit(sp: Sprite, w: number, h?: number): void {
    const tw = sp.texture.width || 1;
    const th = sp.texture.height || 1;
    sp.width = w;
    sp.height = h ?? (w * (th / tw));
  }
}

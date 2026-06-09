import { Container, Sprite, AnimatedSprite, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { anim as animFrames } from "./assets";

// Fire-wall (Fire Mage) rendering: a band of animated FireStandAnnimation tiles
// rebuilt only when the wall count changes, with an alpha-flicker fallback for the
// static-sprite path. Owns its display container; extracted from Renderer.

// Fire wall band height as a fraction of cellSize.
const FIRE_WALL_HEIGHT_MULT = 1.1;
// FireWall animation key in the manifest.
const FIRE_WALL_ANIM_KEY = "firemage/spell_firewall/firestandannimation";

interface Wall { y: number; width: number }

export class FireWallLayer {
  readonly container = new Container();
  private _wallAnims: AnimatedSprite[] = [];
  private _lastWallCount = -1;

  update(walls: Wall[], tick: number, cellSize: number, boardW: number): void {
    const wallH = cellSize * FIRE_WALL_HEIGHT_MULT;
    const fireWallFrames = animFrames(FIRE_WALL_ANIM_KEY);

    if (walls.length !== this._lastWallCount) {
      // Destroy old wall anim sprites.
      this.container.removeChildren();
      for (const a of this._wallAnims) { a.stop(); a.destroy(); }
      this._wallAnims = [];

      for (const wall of walls) {
        const tileW = wallH; // square tiles
        const count = Math.ceil(boardW / tileW);
        for (let i = 0; i < count; i++) {
          if (fireWallFrames.length >= 2) {
            // Use real animated FireStandAnnimation art.
            const anim = new AnimatedSprite(fireWallFrames);
            anim.blendMode = BLEND_MODES.ADD;
            anim.tint = 0xff8833;
            anim.anchor.set(0, 0.5);
            anim.width  = tileW + 1;
            anim.height = wallH;
            anim.x = i * tileW;
            anim.y = wall.y;
            anim.loop = true;
            anim.animationSpeed = 8 / 60; // ~8 fps
            // Stagger offset per tile for organic flicker.
            anim.currentFrame = (i * 3) % fireWallFrames.length;
            anim.alpha = 0.9;
            anim.play();
            this.container.addChild(anim);
            this._wallAnims.push(anim);
          } else {
            // Fallback: static Explosion sprite.
            const sp = new Sprite(tex("Explosion"));
            sp.blendMode = BLEND_MODES.ADD;
            sp.tint = 0xff6620;
            sp.anchor.set(0, 0.5);
            sp.width  = tileW + 1;
            sp.height = wallH;
            sp.x = i * tileW;
            sp.y = wall.y;
            sp.alpha = 0.85;
            this.container.addChild(sp);
          }
        }
      }
      this._lastWallCount = walls.length;
    } else {
      // Walls unchanged — just flicker alpha for the static-sprite fallback path.
      for (let i = 0; i < this.container.children.length; i++) {
        const child = this.container.children[i];
        if (!(child instanceof AnimatedSprite)) {
          const flicker = 0.72 + 0.28 * Math.sin(tick * 0.18 + i * 1.3);
          child.alpha = flicker;
        }
      }
    }
  }
}

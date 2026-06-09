import { Container, Sprite, Texture } from "pixi.js";
import { bg as biomedBg, hellParallaxFrames, tex as atlasTex } from "./assets";

// Biome background, Hell parallax layers, and cosmetic village "beholder" ambient
// sprites. Extracted from Renderer. Exposes two containers: `bgLayer` (added to the
// stage, behind the world) and `ambientContainer` (added to the world, behind play).

// Biome background: slightly darkened so blocks read clearly over it.
const BG_TINT = 0xaaaaaa; // ~67% brightness multiplier on the sprite

// Ambient beholder keys (cosmetic background, village biome only). Pooled, max 2.
const BEHOLDER_KEYS = [
  "village/enemies/Beholder1", "village/enemies/Beholder2", "village/enemies/Beholder3",
];
const BEHOLDER_GHOST_KEYS = [
  "village/enemies/Beholder1Ghost", "village/enemies/Beholder2Ghost", "village/enemies/Beholder3Ghost",
];

interface Ambient {
  sp: Sprite; x: number; y: number; vx: number; vy: number;
  frame: number; frameMs: number; keys: string[];
}

export class BackgroundLayer {
  readonly bgLayer = new Container();        // stage-level (behind world)
  readonly ambientContainer = new Container(); // world-level (behind play)

  private bgSprite = new Sprite();
  private _hellParallaxSprites: Sprite[] = [];
  private _lastBiome = "";
  private _ambientSprites: Ambient[] = [];
  private _lastAmbientBiome = "";

  constructor() {
    this.bgSprite.anchor.set(0);
    this.bgSprite.tint = BG_TINT;
    this.bgLayer.addChild(this.bgSprite);
    this.ambientContainer.alpha = 0.22; // purely cosmetic
  }

  /** Rebuild the biome background, Hell parallax, and ambient sprites on biome change. */
  setBiome(biome: string, cellSize: number): void {
    // --- biome background (update only on biome change) ---
    if (biome && biome !== this._lastBiome) {
      this._lastBiome = biome;
      const bgTex = biomedBg(biome);
      this.bgSprite.texture = bgTex;
      this.bgSprite.visible = bgTex !== Texture.WHITE;

      // Hell parallax layers: add/rebuild when entering hell biome.
      for (const psp of this._hellParallaxSprites) this.bgLayer.removeChild(psp);
      this._hellParallaxSprites = [];
      if (biome === "hell") {
        const frames = hellParallaxFrames();
        for (let i = 0; i < frames.length; i++) {
          const psp = new Sprite(frames[i]);
          psp.anchor.set(0);
          psp.tint = 0x888888; // darker than main bg for depth
          psp.alpha = 0.35;    // subtle layering
          this.bgLayer.addChild(psp);
          this._hellParallaxSprites.push(psp);
        }
      }
    }

    // --- ambient background sprites (cosmetic, village biome beholders) ---
    if (biome !== this._lastAmbientBiome) {
      this._lastAmbientBiome = biome;
      for (const a of this._ambientSprites) this.ambientContainer.removeChild(a.sp);
      this._ambientSprites = [];

      if (biome === "village" || biome === "village-ghost" || biome === "village-boss") {
        // Spawn 2 ambient beholders drifting slowly across the board.
        const beholderCount = 2;
        for (let i = 0; i < beholderCount; i++) {
          const useGhost = i === 1;
          const keys = useGhost ? BEHOLDER_GHOST_KEYS : BEHOLDER_KEYS;
          const tex0 = atlasTex(keys[0]);
          if (tex0 === Texture.WHITE) continue; // atlas not yet loaded
          const sp = new Sprite(tex0);
          sp.anchor.set(0.5);
          const size = cellSize * 2.2;
          sp.width  = size;
          sp.height = size;
          sp.tint   = useGhost ? 0xaaccff : 0xffffff;
          // Scatter starting positions.
          const startX = 60 + i * 180;
          const startY = 60 + i * 100;
          // Gentle drift velocity (world-space px/ms).
          const vx = (i % 2 === 0 ? 0.012 : -0.015);
          const vy = (i % 2 === 0 ? 0.007 : 0.011);
          sp.position.set(startX, startY);
          this.ambientContainer.addChild(sp);
          this._ambientSprites.push({ sp, x: startX, y: startY, vx, vy, frame: 0, frameMs: i * 180, keys });
        }
      }
    }
  }

  /** COVER-scale the background + parallax to fill the stage (called from fit()). */
  resize(screenW: number, screenH: number): void {
    const bw = this.bgSprite.texture.width;
    const bh = this.bgSprite.texture.height;
    if (bw > 0 && bh > 0) {
      const coverScale = Math.max(screenW / bw, screenH / bh);
      this.bgSprite.scale.set(coverScale);
      this.bgSprite.x = (screenW - bw * coverScale) / 2;
      this.bgSprite.y = (screenH - bh * coverScale) / 2;
    }
    for (const psp of this._hellParallaxSprites) {
      if (psp.texture.width > 0 && psp.texture.height > 0) {
        const pw = psp.texture.width;
        const ph = psp.texture.height;
        const ps = Math.max(screenW / pw, screenH / ph);
        psp.scale.set(ps);
        psp.y = (screenH - ph * ps) / 2;
      }
    }
  }

  /** Advance the ambient sprite drift + frame cycling each frame. */
  updateAnim(dtMs: number): void {
    for (const a of this._ambientSprites) {
      // Advance frame.
      a.frameMs += dtMs;
      if (a.frameMs > 380) {
        a.frameMs = 0;
        a.frame = (a.frame + 1) % a.keys.length;
        const t = atlasTex(a.keys[a.frame]);
        if (t !== Texture.WHITE) a.sp.texture = t;
      }
      // Drift.
      a.x += a.vx * dtMs;
      a.y += a.vy * dtMs;
      a.sp.x = a.x;
      a.sp.y = a.y;
      // Wrap horizontally within board bounds.
      if (a.x < -40) a.x += 440;
      if (a.x > 440) a.x -= 440;
      if (a.y < -40) a.y += 540;
      if (a.y > 540) a.y -= 540;
    }
  }
}

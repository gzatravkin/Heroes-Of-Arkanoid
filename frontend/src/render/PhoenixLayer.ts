import { Container, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex as atlasTex } from "./assets";

// Fire-Mage Phoenix rendering: a visible phoenix bird per entity, positioned at its own
// orbiting world coordinate (distinct from the ball). Pooled by phoenix id. A soft glow
// sits behind the bird so it reads against the dark biomes.

const PHOENIX_BODY_KEY = "firemage/spell_phonex/Phoenics";
const PHOENIX_GLOW_KEY = "firemage/spell_phonex/PhoenixGlow";
const PHOENIX_SIZE_MULT = 2.1;  // body diameter as a multiple of cellSize
const GLOW_SIZE_MULT    = 3.2;
const GLOW_TINT         = 0xffa838; // warm gold-orange so the bird reads against the dark-orange Hell field
const GLOW_ALPHA_BASE   = 0.78;
const GLOW_ALPHA_AMP    = 0.18;     // gentle pulse → reads as a living fire entity trailing the ball

interface PhoenixDto { id: number; x: number; y: number; angle: number }

export class PhoenixLayer {
  readonly container = new Container();
  private pool = new Map<number, { body: Sprite; glow: Sprite }>();

  /** Returns the world positions of phoenixes that were spawned this frame, so the renderer
   *  can play a one-time birth flourish for each (the only place the phoenix-birth art fires). */
  update(phoenixes: PhoenixDto[], cellSize: number): { x: number; y: number }[] {
    const size = cellSize * PHOENIX_SIZE_MULT;
    const glowSize = cellSize * GLOW_SIZE_MULT;
    const bodyTex = atlasTex(PHOENIX_BODY_KEY);
    const glowTex = atlasTex(PHOENIX_GLOW_KEY);
    const born: { x: number; y: number }[] = [];

    const live = new Set(phoenixes.map(p => p.id));
    for (const [id, entry] of this.pool) {
      if (!live.has(id)) {
        this.container.removeChild(entry.glow);
        this.container.removeChild(entry.body);
        entry.glow.destroy();
        entry.body.destroy();
        this.pool.delete(id);
      }
    }

    for (const ph of phoenixes) {
      let entry = this.pool.get(ph.id);
      if (!entry) {
        born.push({ x: ph.x, y: ph.y });
        const glow = new Sprite(glowTex !== Texture.WHITE ? glowTex : Texture.WHITE);
        glow.anchor.set(0.5);
        glow.blendMode = BLEND_MODES.ADD;
        glow.tint = GLOW_TINT;
        glow.alpha = GLOW_ALPHA_BASE;
        const body = new Sprite(bodyTex !== Texture.WHITE ? bodyTex : Texture.WHITE);
        body.anchor.set(0.5);
        this.container.addChild(glow);
        this.container.addChild(body);
        entry = { body, glow };
        this.pool.set(ph.id, entry);
      }
      const { body, glow } = entry;
      body.x = glow.x = ph.x;
      body.y = glow.y = ph.y;
      body.width = body.height = size;
      glow.width = glow.height = glowSize;
      // Gentle per-phoenix pulse so it reads as a living flame trailing the ball.
      glow.alpha = GLOW_ALPHA_BASE + GLOW_ALPHA_AMP * Math.sin(performance.now() / 170 + ph.id * 1.7);
      // Face the direction of travel along the orbit (d/dt x = -sin(angle)).
      const facingLeft = Math.sin(ph.angle) > 0;
      body.scale.x = Math.abs(body.scale.x) * (facingLeft ? -1 : 1);
    }
    return born;
  }
}

import { Container, Sprite } from "pixi.js";
import { tex } from "./textures";

// Falling bonus-pickup icons. Owns its own display layer + id-keyed pool.
const BONUS_SPRITE_SIZE   = 28;   // world-space px
const BONUS_SPIN_SPEED    = 0.04; // radians per tick
const BONUS_BOB_AMPLITUDE = 2.5;  // px vertical bob
const BONUS_BOB_SPEED     = 0.07; // radians per tick for the bob sinusoid

interface BonusDto { id: number; x: number; y: number; type: string; icon: string }

export class BonusLayer {
  readonly container = new Container();
  private pool = new Map<number, Sprite>();

  update(bonuses: BonusDto[], tick: number): void {
    // Power-up types (powerup_*) are rendered by PowerUpLayer — skip them here.
    bonuses = bonuses.filter(b => !b.type?.startsWith("powerup_"));
    const live = new Set(bonuses.map(b => b.id));
    for (const [id, sp] of this.pool) {
      if (!live.has(id)) { this.container.removeChild(sp); this.pool.delete(id); }
    }
    for (const bn of bonuses) {
      const sp = this.pool.get(bn.id);
      if (sp) {
        const bob = Math.sin(tick * BONUS_BOB_SPEED + bn.id) * BONUS_BOB_AMPLITUDE;
        sp.x = bn.x;
        sp.y = bn.y + bob;
        sp.rotation += BONUS_SPIN_SPEED;
      } else {
        const next = new Sprite(tex(bn.icon));
        next.anchor.set(0.5);
        next.width = next.height = BONUS_SPRITE_SIZE;
        next.x = bn.x; next.y = bn.y; next.rotation = 0;
        this.container.addChild(next);
        this.pool.set(bn.id, next);
      }
    }
  }
}

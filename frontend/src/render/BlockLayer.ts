import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";

// Block rendering (pooled): damage states, mirror flags, boss aura, teleporter ring,
// ghost pulse, shield flash. Owns its own display layer; extracted from Renderer.

const BOSS_SCALE_MULT       = 1.15;
const BOSS_AURA_COLOR       = 0xcc0000;
const BOSS_AURA_RADIUS_MULT = 0.8;
const BOSS_AURA_ALPHA       = 0.55;
const BOSS_AURA_PULSE_SPEED = 0.06;
const BOSS_AURA_ALPHA_AMP   = 0.25;

const GHOST_ALPHA_BASE  = 0.45;
const GHOST_ALPHA_AMP   = 0.12;
const GHOST_PULSE_SPEED = 0.055;
const GHOST_TINT        = 0x88ccff;

const TELEPORTER_RING_ALPHA_BASE  = 0.35;
const TELEPORTER_RING_ALPHA_AMP   = 0.25;
const TELEPORTER_RING_PULSE_SPEED = 0.07;
const TELEPORTER_RING_COLOR       = 0x44aaff;
const TELEPORTER_RING_RADIUS_MULT = 0.72;

// Damage states (A3): below DAMAGE_THRESHOLD of max HP, swap to the "destroyed/cracked" frame.
const DAMAGE_THRESHOLD = 0.6;
const BLOCK_DAMAGED: Record<string, string> = {
  HellStandart:     "hell/StandartHellDestroyed",
  HellStandart2:    "hell/StandartHell2Destroyed",
  DungeonStandart:  "dungeon/DungeonStandartDestroyed",
  DungeonStandart2: "dungeon/DungeonStandart2Destroyed",
  VillageStandart:  "village/blocks/VillageStandartDestroyed",
  VillageStandart2: "village/blocks/VillageStandart2Destroyed",
  StandartHaven:    "heaven/StandartHavenDestroyed",
  Standart2Haven:   "heaven/Standart2HavenDestroyed",
};

interface BlockDto {
  id: number; x: number; y: number; hp: number; maxHp: number; sprite: string;
  boss?: boolean; ballPhases: boolean; indestructible: boolean; teleporter: boolean;
  flipX?: boolean; flipY?: boolean; shielded?: boolean;
}

export class BlockLayer {
  readonly container = new Container();
  private pool = new Map<number, { sp: Sprite; aura?: Graphics; ring?: Graphics }>();

  /** Block texture with damage states: swaps to the cracked frame near death. */
  private blockTex(b: { sprite: string; hp: number; maxHp: number }): Texture {
    const dmgKey = BLOCK_DAMAGED[b.sprite];
    if (dmgKey && b.maxHp > 0 && b.hp / b.maxHp < DAMAGE_THRESHOLD) {
      const t = atlasTex(dmgKey);
      if (t !== Texture.WHITE) return t;
    }
    return tex(b.sprite);
  }

  /** Hide a block's plain sprite (e.g. while the animated boss rig covers it). */
  hideBlock(id: number): void {
    const e = this.pool.get(id);
    if (e) e.sp.alpha = 0;
  }

  update(blocks: BlockDto[], tick: number, brickSize: number): void {
    const live = new Set<number>();
    for (const b of blocks) live.add(b.id);

    // Remove sprites for blocks that no longer exist.
    for (const [id, entry] of this.pool) {
      if (!live.has(id)) {
        if (entry.aura) this.container.removeChild(entry.aura);
        if (entry.ring) this.container.removeChild(entry.ring);
        this.container.removeChild(entry.sp);
        this.pool.delete(id);
      }
    }

    for (const b of blocks) {
      const size = b.boss ? brickSize * BOSS_SCALE_MULT : brickSize;
      const existing = this.pool.get(b.id);

      if (existing) {
        const { sp, aura, ring } = existing;
        sp.texture = this.blockTex(b);
        sp.width = size; sp.height = size;
        sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
        sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
        sp.position.set(b.x, b.y);

        if (b.boss) {
          sp.alpha = 1.0;
          if (aura) {
            const a = BOSS_AURA_ALPHA + BOSS_AURA_ALPHA_AMP * Math.sin(tick * BOSS_AURA_PULSE_SPEED);
            aura.clear().beginFill(BOSS_AURA_COLOR, a)
              .drawCircle(b.x, b.y, brickSize * BOSS_AURA_RADIUS_MULT).endFill();
          }
        } else if (b.ballPhases) {
          sp.tint = GHOST_TINT;
          sp.alpha = GHOST_ALPHA_BASE + GHOST_ALPHA_AMP * Math.sin(tick * GHOST_PULSE_SPEED);
        } else if (b.indestructible || b.teleporter) {
          sp.alpha = 1.0;
          if (ring) {
            const a = TELEPORTER_RING_ALPHA_BASE + TELEPORTER_RING_ALPHA_AMP * Math.sin(tick * TELEPORTER_RING_PULSE_SPEED);
            ring.clear().beginFill(TELEPORTER_RING_COLOR, a)
              .drawCircle(b.x, b.y, brickSize * TELEPORTER_RING_RADIUS_MULT).endFill();
          }
        } else {
          sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);
          sp.tint = b.shielded ? 0x66ddff : 0xffffff; // cyan flash while shielded
        }
      } else {
        let aura: Graphics | undefined;
        let ring: Graphics | undefined;

        if (b.boss) {
          const a = BOSS_AURA_ALPHA + BOSS_AURA_ALPHA_AMP * Math.sin(tick * BOSS_AURA_PULSE_SPEED);
          aura = new Graphics();
          aura.blendMode = BLEND_MODES.ADD;
          aura.beginFill(BOSS_AURA_COLOR, a).drawCircle(b.x, b.y, brickSize * BOSS_AURA_RADIUS_MULT).endFill();
          this.container.addChild(aura);
        }
        if (b.teleporter) {
          const a = TELEPORTER_RING_ALPHA_BASE + TELEPORTER_RING_ALPHA_AMP * Math.sin(tick * TELEPORTER_RING_PULSE_SPEED);
          ring = new Graphics();
          ring.blendMode = BLEND_MODES.ADD;
          ring.beginFill(TELEPORTER_RING_COLOR, a).drawCircle(b.x, b.y, brickSize * TELEPORTER_RING_RADIUS_MULT).endFill();
          this.container.addChild(ring);
        }

        const sp = new Sprite(this.blockTex(b));
        sp.anchor.set(0.5);
        sp.width = size; sp.height = size;
        sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
        sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
        sp.position.set(b.x, b.y);

        if (b.boss) sp.alpha = 1.0;
        else if (b.ballPhases) {
          sp.tint = GHOST_TINT;
          sp.alpha = GHOST_ALPHA_BASE + GHOST_ALPHA_AMP * Math.sin(tick * GHOST_PULSE_SPEED);
        } else if (b.indestructible || b.teleporter) sp.alpha = 1.0;
        else sp.alpha = 0.4 + 0.6 * (b.hp / b.maxHp);

        this.container.addChild(sp);
        this.pool.set(b.id, { sp, aura, ring });
      }
    }
  }
}

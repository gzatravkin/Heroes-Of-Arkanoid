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
  // Enemy blocks crack too (docs/11 polish):
  ColumnTop:        "heaven/ColumnTopDestroyed",
  Column:           "heaven/ColumnDamaged",
  ColumnBottom:     "heaven/ColumnBottomDestroyed",
  WindMaster2:      "heaven/WindMaster2Destroyed",
};

// Beholder shows its damage through the original 3-tier art (docs/11: beholder tiers).
const BEHOLDER_TIER2 = "village/enemies/Beholder2"; // below 2/3 HP
const BEHOLDER_TIER3 = "village/enemies/Beholder3"; // below 1/3 HP
const BEHOLDER_T2_FRAC = 2 / 3;
const BEHOLDER_T3_FRAC = 1 / 3;

// Pacified statues/altar swap to the original *Active art (docs/11 R5 — convert is visible).
const ALLIED_VARIANT: Record<string, string> = {
  HeavenMeleeStatue: "heaven/HeavenMeleeStatueActive",
  HeavenDefender:    "heaven/HeavenDefenderActive",
};
const ALTAR_SPRITE        = "HeavenAltarV2";
const ALTAR_ACTIVE        = "heaven/HeavenAltarV2Active";

// Telegraph flash (docs/11 R2): emitter about to fire pulses warm.
const CHARGE_TINT        = 0xffdd66;
const CHARGE_PULSE_TICKS = 8; // tint toggles every N ticks

// WindMaster aura (docs/11: the push radius must be visible).
const WIND_AURA_KEY        = "heaven/WindMasterV2Circle";
const WIND_SPRITE          = "WindMaster2";
const WIND_AURA_ALPHA_BASE = 0.22;
const WIND_AURA_ALPHA_AMP  = 0.10;
const WIND_AURA_PULSE      = 0.05;

// Vase-levelled statues glow warm so the risk the player took is readable.
const LEVELED_TINT = 0xffd9a0;

// Cauldron bubbles by cycling the original Kotelok frames (the siphon is visible).
const CAULDRON_FRAMES = ["village/blocks/Kotelok1", "village/blocks/Kotelok2", "village/blocks/Kotelok3"];
const CAULDRON_FRAME_TICKS = 14;
// Lava spawner pulses to its Active frame on the same cadence feel.
const LAVA_SPAWNER_ACTIVE = "hell/LavaSpownerActive";
const LAVA_SPAWNER_PULSE_TICKS = 24;

interface BlockDto {
  id: number; x: number; y: number; hp: number; maxHp: number; sprite: string;
  boss?: boolean; ballPhases: boolean; indestructible: boolean; teleporter: boolean;
  flipX?: boolean; flipY?: boolean; shielded?: boolean;
  charging?: boolean; allied?: boolean; level?: number;
}

export class BlockLayer {
  readonly container = new Container();
  private pool = new Map<number, { sp: Sprite; aura?: Graphics; ring?: Graphics; wind?: Sprite }>();

  /** Block texture: allied *Active art, beholder damage tiers, cracked frames near death. */
  private blockTex(b: BlockDto, anyAllied: boolean, tick = 0): Texture {
    // Cauldron: bubble through the Kotelok frames.
    if (b.sprite === "Kotelok1") {
      const frame = CAULDRON_FRAMES[Math.floor(tick / CAULDRON_FRAME_TICKS) % CAULDRON_FRAMES.length];
      const t = atlasTex(frame);
      if (t !== Texture.WHITE) return t;
    }
    // Lava spawner: pulse to the Active frame.
    if (b.sprite === "LavaSpowner" && (tick % (LAVA_SPAWNER_PULSE_TICKS * 2)) < LAVA_SPAWNER_PULSE_TICKS) {
      const t = atlasTex(LAVA_SPAWNER_ACTIVE);
      if (t !== Texture.WHITE) return t;
    }
    // Pacified statues (and the altar, while its blessing holds) show the Active art.
    const activeKey = b.allied ? ALLIED_VARIANT[b.sprite]
      : (anyAllied && b.sprite === ALTAR_SPRITE) ? ALTAR_ACTIVE : undefined;
    if (activeKey) {
      const t = atlasTex(activeKey);
      if (t !== Texture.WHITE) return t;
    }
    // Beholder communicates damage through its 3-tier art.
    if (b.sprite === "Beholder1" && b.maxHp > 0) {
      const frac = b.hp / b.maxHp;
      const tierKey = frac < BEHOLDER_T3_FRAC ? BEHOLDER_TIER3
        : frac < BEHOLDER_T2_FRAC ? BEHOLDER_TIER2 : undefined;
      if (tierKey) {
        const t = atlasTex(tierKey);
        if (t !== Texture.WHITE) return t;
      }
    }
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

  update(blocks: BlockDto[], tick: number, brickSize: number, windRadius = 0): void {
    const live = new Set<number>();
    let anyAllied = false;
    for (const b of blocks) {
      live.add(b.id);
      if (b.allied) anyAllied = true;
    }

    // Remove sprites for blocks that no longer exist.
    for (const [id, entry] of this.pool) {
      if (!live.has(id)) {
        if (entry.aura) this.container.removeChild(entry.aura);
        if (entry.ring) this.container.removeChild(entry.ring);
        if (entry.wind) this.container.removeChild(entry.wind);
        this.container.removeChild(entry.sp);
        this.pool.delete(id);
      }
    }

    for (const b of blocks) {
      const size = b.boss ? brickSize * BOSS_SCALE_MULT : brickSize;
      const existing = this.pool.get(b.id);

      if (existing) {
        const { sp, aura, ring, wind } = existing;
        sp.texture = this.blockTex(b, anyAllied, tick);
        sp.width = size; sp.height = size;
        sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
        sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
        sp.position.set(b.x, b.y);

        if (wind) {
          const wa = WIND_AURA_ALPHA_BASE + WIND_AURA_ALPHA_AMP * Math.sin(tick * WIND_AURA_PULSE);
          wind.alpha = wa;
          wind.width = wind.height = windRadius * 2;
          wind.position.set(b.x, b.y);
          wind.rotation = tick * WIND_AURA_PULSE;
        }

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
          // Telegraph pulse beats shield flash beats levelled glow beats neutral.
          sp.tint = b.charging && (tick % (CHARGE_PULSE_TICKS * 2)) < CHARGE_PULSE_TICKS
            ? CHARGE_TINT
            : b.shielded ? 0x66ddff
            : (b.level ?? 0) > 0 ? LEVELED_TINT : 0xffffff;
        }
      } else {
        let aura: Graphics | undefined;
        let ring: Graphics | undefined;
        let wind: Sprite | undefined;

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
        if (b.sprite === WIND_SPRITE && windRadius > 0) {
          const auraTex = atlasTex(WIND_AURA_KEY);
          if (auraTex !== Texture.WHITE) {
            wind = new Sprite(auraTex);
            wind.anchor.set(0.5);
            wind.blendMode = BLEND_MODES.ADD;
            wind.alpha = WIND_AURA_ALPHA_BASE;
            wind.width = wind.height = windRadius * 2;
            wind.position.set(b.x, b.y);
            this.container.addChild(wind);
          }
        }

        const sp = new Sprite(this.blockTex(b, anyAllied, tick));
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
        this.pool.set(b.id, { sp, aura, ring, wind });
      }
    }
  }
}

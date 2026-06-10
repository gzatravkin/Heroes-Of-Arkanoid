import { Container, Graphics, Sprite, TilingSprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex, animStrip } from "./assets";

// Block rendering (pooled): damage states, mirror flags, boss aura, teleporter ring,
// ghost pulse, shield flash — and NATIVE-ASPECT sizing (the original art is not
// square: bricks are ~1.36:1, walls ~2.2:1, statues portrait). Sizing modes:
//   contain — fit inside the cell keeping aspect (bricks get mortar gaps, skulls slim)
//   wall    — TilingSprite filling the cell with undistorted repeating stone
//   stand   — full cell width, natural height, feet on the cell floor (statues)
//   hang    — full cell width, natural height, hanging from the cell ceiling
//   fill    — stretch to the cell (columns: keeps stack continuity)

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

// Damage states (A3): below DAMAGE_THRESHOLD of max HP, swap to cracked art.
// NOTE: the "*Destroyed" assets are ANIMATION STRIPS (e.g. 388×34) — they must be
// sliced via animStrip and sampled, never drawn whole (that was the squashed-
// garbage near-death look). DAMAGE_FRAME picks an early-crack frame.
const DAMAGE_THRESHOLD = 0.6;
const DAMAGE_FRAME     = 1;
const BLOCK_DAMAGED: Record<string, string> = {
  HellStandart:     "hell/StandartHellDestroyed",
  HellStandart2:    "hell/StandartHell2Destroyed",
  DungeonStandart:  "dungeon/DungeonStandartDestroyed",
  DungeonStandart2: "dungeon/DungeonStandart2Destroyed",
  VillageStandart:  "village/blocks/VillageStandartDestroyed",
  VillageStandart2: "village/blocks/VillageStandart2Destroyed",
  StandartHaven:    "heaven/StandartHavenDestroyed",
  Standart2Haven:   "heaven/Standart2HavenDestroyed",
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
const ALTAR_SPRITE = "HeavenAltarV2";
const ALTAR_ACTIVE = "heaven/HeavenAltarV2Active";

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

// Cauldron: the Kotelok assets are 7-frame bubbling STRIPS — cycle real frames.
const CAULDRON_STRIP_KEY   = "village/blocks/Kotelok1";
const CAULDRON_FRAME_TICKS = 9;
// Lava spawner pulses to its Active frame.
const LAVA_SPAWNER_ACTIVE = "hell/LavaSpownerActive";
const LAVA_SPAWNER_PULSE_TICKS = 24;

// stand/hang sprites may overflow their cell, but no further than this — taller
// figures scale down uniformly so they never hide a whole neighbouring brick.
const MAX_OVERFLOW = 1.75;

type SizeMode = "contain" | "wall" | "stand" | "hang" | "fill";
const SIZE_MODES: Record<string, SizeMode> = {
  // Structural walls: tile undistorted stone to seal channels.
  HellInvulnerable:    "wall",
  DungeonInvulnerable: "wall",
  InvulnerableHaven:   "wall",
  LavaMainPart:        "wall",
  // Columns stretch so the stack stays continuous.
  ColumnTop: "fill", Column: "fill", ColumnBottom: "fill",
  // Figures stand on the cell floor at natural height.
  HeavenMeleeStatue: "stand", HeavenDefender: "stand",
  BatSleeping: "stand", HeavenVaza: "stand", Kotelok1: "stand",
  // Stalactites hang from the cell ceiling.
  Stalactite: "hang",
  // default: contain
};

interface BlockDto {
  id: number; x: number; y: number; hp: number; maxHp: number; sprite: string;
  boss?: boolean; ballPhases: boolean; indestructible: boolean; teleporter: boolean;
  flipX?: boolean; flipY?: boolean; shielded?: boolean;
  charging?: boolean; allied?: boolean; level?: number;
}

interface Entry { sp: Sprite | TilingSprite; aura?: Graphics; ring?: Graphics; wind?: Sprite }

export class BlockLayer {
  readonly container = new Container();
  private pool = new Map<number, Entry>();
  private _cauldronFrames: Texture[] | null = null;

  /** Block texture: allied *Active art, beholder damage tiers, cracked frames near death. */
  private blockTex(b: BlockDto, anyAllied: boolean, tick: number): Texture {
    // Cauldron: bubble through the real Kotelok strip frames.
    if (b.sprite === "Kotelok1") {
      this._cauldronFrames ??= animStrip(CAULDRON_STRIP_KEY);
      const frames = this._cauldronFrames;
      if (frames.length > 1)
        return frames[Math.floor(tick / CAULDRON_FRAME_TICKS) % frames.length];
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
    // Cracked frame near death — sliced from the destroy STRIP, never drawn whole.
    const dmgKey = BLOCK_DAMAGED[b.sprite];
    if (dmgKey && b.maxHp > 0 && b.hp / b.maxHp < DAMAGE_THRESHOLD) {
      const frames = animStrip(dmgKey);
      if (frames.length > DAMAGE_FRAME) return frames[DAMAGE_FRAME];
      if (frames.length === 1 && frames[0].width / Math.max(frames[0].height, 1) < 2)
        return frames[0]; // a genuine single cracked frame
    }
    return tex(b.sprite);
  }

  /** Size + position the sprite by its sizing mode, preserving flip signs. */
  private applySizing(sp: Sprite | TilingSprite, b: BlockDto, size: number): void {
    const mode: SizeMode = b.boss ? "fill" : (SIZE_MODES[b.sprite] ?? "contain");
    const nw = sp.texture.width  || 1;
    const nh = sp.texture.height || 1;
    const aspect = nh / nw;

    let w = size, h = size, y = b.y;
    if (sp instanceof TilingSprite) {
      // wall: tile the texture at cell width, undistorted, sealing the cell.
      sp.width = size; sp.height = size;
      const s = size / nw;
      sp.tileScale.set(s, s);
      sp.position.set(b.x, b.y);
      return;
    }
    switch (mode) {
      case "contain":
        if (aspect <= 1) { w = size; h = size * aspect; }
        else { h = size; w = size / aspect; }
        break;
      case "stand": // feet on the cell floor; may rise above the cell (capped)
        w = size; h = size * aspect;
        if (h > size * MAX_OVERFLOW) { h = size * MAX_OVERFLOW; w = h / aspect; }
        y = b.y + size / 2 - h / 2;
        break;
      case "hang": // hanging from the cell ceiling; may reach below (capped)
        w = size; h = size * aspect;
        if (h > size * MAX_OVERFLOW) { h = size * MAX_OVERFLOW; w = h / aspect; }
        y = b.y - size / 2 + h / 2;
        break;
      case "fill":
      default:
        break;
    }
    sp.width = w; sp.height = h;
    sp.scale.x = Math.abs(sp.scale.x) * (b.flipX ? -1 : 1);
    sp.scale.y = Math.abs(sp.scale.y) * (b.flipY ? -1 : 1);
    sp.position.set(b.x, y);
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
      let entry = this.pool.get(b.id);

      if (!entry) {
        let aura: Graphics | undefined;
        let ring: Graphics | undefined;
        let wind: Sprite | undefined;

        if (b.boss) {
          aura = new Graphics();
          aura.blendMode = BLEND_MODES.ADD;
          this.container.addChild(aura);
        }
        if (b.teleporter) {
          ring = new Graphics();
          ring.blendMode = BLEND_MODES.ADD;
          this.container.addChild(ring);
        }
        if (b.sprite === WIND_SPRITE && windRadius > 0) {
          const auraTex = atlasTex(WIND_AURA_KEY);
          if (auraTex !== Texture.WHITE) {
            wind = new Sprite(auraTex);
            wind.anchor.set(0.5);
            wind.blendMode = BLEND_MODES.ADD;
            wind.alpha = WIND_AURA_ALPHA_BASE;
            this.container.addChild(wind);
          }
        }

        const texture = this.blockTex(b, anyAllied, tick);
        const isWall = !b.boss && SIZE_MODES[b.sprite] === "wall";
        const sp = isWall
          ? new TilingSprite(texture, size, size)
          : new Sprite(texture);
        sp.anchor.set(0.5);
        this.container.addChild(sp);
        entry = { sp, aura, ring, wind };
        this.pool.set(b.id, entry);
      }

      const { sp, aura, ring, wind } = entry;
      const texture = this.blockTex(b, anyAllied, tick);
      if (sp.texture !== texture) sp.texture = texture;
      this.applySizing(sp, b, size);

      if (wind) {
        wind.alpha = WIND_AURA_ALPHA_BASE + WIND_AURA_ALPHA_AMP * Math.sin(tick * WIND_AURA_PULSE);
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
        sp.tint = 0xffffff;
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
    }
  }
}

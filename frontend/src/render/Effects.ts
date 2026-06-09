/**
 * Effects.ts — Event-driven particle + animation effects.
 *
 * Delegates one-shot animated effects (explosions, burns, flashes) to AnimSystem
 * using real atlas art (strip-sliced Explosion, FireBirth, etc.) rather than
 * static Sprite fades.  Keeps the original Particle path as a fallback for any
 * key that isn't found in the atlas.
 */

import { Container, Sprite, BLEND_MODES } from "pixi.js";
import type { Snapshot } from "../net/Connection";
import { tex } from "./textures";
import { animStrip, anim as animFrames } from "./assets";
import { AnimSystem } from "./AnimSystem";

// ── Constants ───────────────────────────────────────────────────────────────
// Fallback particle — used only when no atlas frames are available.
interface Particle {
  sprite: Sprite;
  baseScale: number;
  life: number;
  elapsed: number;
  scaleStart: number;
  scaleEnd: number;
}

// Explosion strip: "effects/Explosion" (7215×555, ~13 frames square)
const EXPLOSION_KEY = "effects/Explosion";
const EXPLOSION_FPS = 18;

// FireBirth: used for burn/ignite flashes (it's the fire wall birth art, small burst)
const FIRE_BIRTH_KEY = "firemage/spell_firewall/FireBirth";
const FIRE_BIRTH_FPS = 14;

// Phoenix birth sequence: 20 individual frames in the manifest (not a strip)
const PHOENIX_BIRTH_ANIM_KEY = "firemage/spell_phonex/phoenixbirthanimpic";
const PHOENIX_BIRTH_FPS = 12;

// PhoenixDeathAnimation2: strip (19818×884, ~22 frames)
const PHOENIX_DEATH_STRIP_KEY = "firemage/spell_phonex/PhoenixDeathAnimation2";
const PHOENIX_DEATH_FPS = 14;

// Lightning: animated frames from manifest (engineer/spell_lighting/lighting)
const LIGHTNING_ANIM_KEY = "engineer/spell_lighting/lighting";
const LIGHTNING_FPS = 16;

// Skeleton death: animated strip (necromancer/spell_skeleton/SkeletonDeathAnimation)
const SKELETON_DEATH_STRIP_KEY = "necromancer/spell_skeleton/SkeletonDeathAnimation";
const SKELETON_DEATH_FPS = 12;

// Skeleton2 death: animated strip (necromancer/spell_skeleton/Skeleton2DeathAnimation)
const SKELETON2_DEATH_STRIP_KEY = "necromancer/spell_skeleton/Skeleton2DeathAnimation";
const SKELETON2_DEATH_FPS = 12;

// Heaven Vaza death and HellBall death: referenced indirectly by getBiomeSecondaryStrip()
// which is called from spawnBlockDestroy. Keys are literal strings in that function.
const _HEAVEN_VAZA_REF = "heaven/HeavenVazaDeathAnimation"; // used in getBiomeSecondaryStrip
const _HELL_BALL_REF = "hell/HellBallDeathAnimation"; // used in getBiomeSecondaryStrip
void _HEAVEN_VAZA_REF; void _HELL_BALL_REF; // suppress lint — actual keys are in the function below

export class Effects {
  readonly container: Container;
  private animSys: AnimSystem;
  private particles: Particle[] = [];

  // Cached sliced frames (built lazily so atlas is fully loaded when first event fires)
  private _explosionFrames = () => animStrip(EXPLOSION_KEY, EXPLOSION_FPS);
  private _fireBirthFrames = () => animStrip(FIRE_BIRTH_KEY, FIRE_BIRTH_FPS);
  private _phoenixBirthFrames = () => animFrames(PHOENIX_BIRTH_ANIM_KEY);
  private _phoenixDeathFrames = () => animStrip(PHOENIX_DEATH_STRIP_KEY, PHOENIX_DEATH_FPS);
  private _lightningFrames = () => animFrames(LIGHTNING_ANIM_KEY);
  private _skeletonDeathFrames = () => animStrip(SKELETON_DEATH_STRIP_KEY, SKELETON_DEATH_FPS);
  private _skeleton2DeathFrames = () => animStrip(SKELETON2_DEATH_STRIP_KEY, SKELETON2_DEATH_FPS);

  constructor() {
    this.container = new Container();
    this.animSys = new AnimSystem();
    this.container.addChild(this.animSys.container);
  }

  /** Spawn effects for all events in the snapshot. Call once per snapshot. */
  consume(events: Snapshot["events"], cellSize: number, biome?: string) {
    for (const ev of events) {
      switch (ev.type) {
        case "blockDestroyed":
          this.spawnBlockDestroy(ev.x, ev.y, cellSize, biome ?? "hell");
          break;
        case "burn":
          this.spawnBurn(ev.x, ev.y, cellSize);
          break;
        case "ignite":
          this.spawnIgniteFlash(ev.x, ev.y, cellSize);
          break;
        case "spellCast":
          this.spawnPhoenixFlourish(ev.x, ev.y, cellSize);
          break;
        case "lightning":
          this.spawnLightningStrike(ev.x, ev.y, cellSize);
          break;
        case "skeletonDeath":
          this.spawnSkeletonDeath(ev.x, ev.y, cellSize);
          break;
      }
    }
  }

  // ── Block destruction ────────────────────────────────────────────────────

  private spawnBlockDestroy(x: number, y: number, cellSize: number, biome: string) {
    // Use the biome-specific destruction strip if available, then fall back to
    // the generic Explosion strip.
    const biomeStrip = getBiomeDestroyStrip(biome);
    let frames = biomeStrip ? animStrip(biomeStrip, 20) : [];
    if (!frames.length) frames = this._explosionFrames();

    if (frames.length) {
      // Use normal blend for biome strips (they are opaque art); additive for explosion
      const additive = !biomeStrip || frames.length < 4;
      this.animSys.oneShot(frames, EXPLOSION_FPS, x, y, cellSize * 1.5, additive, 0xffffff);
      // Secondary smaller burst with additive explosion for brightness
      const exFrames = this._explosionFrames();
      if (exFrames.length) {
        this.animSys.oneShot(exFrames, EXPLOSION_FPS + 4, x, y, cellSize * 0.9, true, 0xff9933);
      }
      // Biome-specific secondary burst (heaven vaza glow, hell ball death)
      const secondaryKey = getBiomeSecondaryStrip(biome);
      if (secondaryKey) {
        const secondaryFrames = animStrip(secondaryKey, 14);
        if (secondaryFrames.length) {
          this.animSys.oneShot(secondaryFrames, 14, x, y, cellSize * 1.2, true, 0xffffff);
        }
      }
    } else {
      // Fallback: static sprite fade
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 1.4, 280, 1.0, 1.6);
    }
  }

  // ── Burn ─────────────────────────────────────────────────────────────────

  private spawnBurn(x: number, y: number, cellSize: number) {
    const frames = this._fireBirthFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, FIRE_BIRTH_FPS, x, y, cellSize * 0.9, true, 0xff6600);
    } else {
      const sp = new Sprite(tex("Explosion"));
      sp.tint = 0xff6600;
      this.spawnFallback(sp.texture, x, y, cellSize * 0.7, 180, 1.0, 1.4);
    }
  }

  // ── Ignite flash ─────────────────────────────────────────────────────────

  private spawnIgniteFlash(x: number, y: number, cellSize: number) {
    // Small bright flash using FireBirth art, then a phoenix birth flourish scaled down
    const fbFrames = this._fireBirthFrames();
    if (fbFrames.length) {
      this.animSys.oneShot(fbFrames, FIRE_BIRTH_FPS + 4, x, y, cellSize * 1.2, true, 0xffdd88);
    }
    const phoenixFrames = this._phoenixBirthFrames();
    if (phoenixFrames.length) {
      this.animSys.oneShot(phoenixFrames, PHOENIX_BIRTH_FPS, x, y, cellSize * 2.5, true, 0xff8833);
    }
  }

  // ── Phoenix flourish (on spellCast) ──────────────────────────────────────

  private spawnPhoenixFlourish(x: number, y: number, cellSize: number) {
    // Try the 20-frame birth sequence first; fall back to death strip.
    const birthFrames = this._phoenixBirthFrames();
    if (birthFrames.length) {
      this.animSys.oneShot(birthFrames, PHOENIX_BIRTH_FPS, x, y, cellSize * 4.5, true, 0xff6600);
      return;
    }
    const deathFrames = this._phoenixDeathFrames();
    if (deathFrames.length) {
      this.animSys.oneShot(deathFrames, PHOENIX_DEATH_FPS, x, y, cellSize * 5, true, 0xff8844);
      return;
    }
    // Fallback: bright white flash
    this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.5, 120, 1.0, 1.3);
  }

  // ── Lightning strike ─────────────────────────────────────────────────────

  private spawnLightningStrike(x: number, y: number, cellSize: number) {
    const frames = this._lightningFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, LIGHTNING_FPS, x, y, cellSize * 2.5, true, 0xaaccff);
      // Secondary additive white flash
      this.animSys.oneShot(frames, LIGHTNING_FPS + 6, x, y + cellSize, cellSize * 1.5, true, 0xffffff);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 1.8, 200, 1.0, 1.5, 0x88bbff);
    }
  }

  // ── Skeleton death ────────────────────────────────────────────────────────

  private spawnSkeletonDeath(x: number, y: number, cellSize: number) {
    const frames = this._skeletonDeathFrames();
    const frames2 = this._skeleton2DeathFrames();
    const chosen = frames.length >= frames2.length ? frames : frames2;
    if (chosen.length) {
      this.animSys.oneShot(chosen, SKELETON_DEATH_FPS, x, y, cellSize * 3, false, 0xccddff);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize, 220, 1.0, 1.6, 0x8899ff);
    }
  }

  // ── Fallback particle (no atlas art) ─────────────────────────────────────

  private spawnFallback(
    texture: import("pixi.js").Texture,
    x: number, y: number,
    sizeInWorld: number,
    life: number,
    scaleStart: number, scaleEnd: number,
    tint = 0xffffff,
  ) {
    const sp = new Sprite(texture);
    sp.anchor.set(0.5);
    sp.blendMode = BLEND_MODES.ADD;
    sp.tint = tint;
    sp.position.set(x, y);
    const baseScale = sizeInWorld / Math.max(sp.texture.width, 1);
    sp.scale.set(baseScale * scaleStart);
    sp.alpha = 1;
    this.container.addChild(sp);
    this.particles.push({ sprite: sp, baseScale, life, elapsed: 0, scaleStart, scaleEnd });
  }

  /** Call every ticker frame with the delta in ms. */
  update(dtMs: number) {
    this.animSys.update(dtMs);

    const dead: Particle[] = [];
    for (const p of this.particles) {
      p.elapsed += dtMs;
      const t = Math.min(p.elapsed / p.life, 1);
      p.sprite.alpha = 1 - t;
      const scaleMult = p.scaleStart + (p.scaleEnd - p.scaleStart) * t;
      p.sprite.scale.set(p.baseScale * scaleMult);
      if (t >= 1) dead.push(p);
    }
    for (const p of dead) {
      this.container.removeChild(p.sprite);
      this.particles.splice(this.particles.indexOf(p), 1);
    }
  }

  /** Clear all effects (call on level reset). */
  clear() {
    this.animSys.clear();
    for (const p of this.particles) this.container.removeChild(p.sprite);
    this.particles = [];
  }
}

// ── Biome → per-block destruction strip mapping ───────────────────────────

/**
 * Return the atlas key for the biome's primary block destruction strip,
 * or null to fall back to the generic Explosion.
 */
function getBiomeDestroyStrip(biome: string): string | null {
  switch (biome) {
    case "hell":    return "hell/StandartHellDestroyed";
    case "dungeon":
    case "caverns":
    case "cavern":  return "dungeon/DungeonStandartDestroyed";
    case "village": return "village/blocks/VillageStandartDestroyed";
    case "heaven":  return "heaven/StandartHavenDestroyed";
    default:        return null;
  }
}

// Expose biome-specific secondary burst keys for richer effects.
function getBiomeSecondaryStrip(biome: string): string | null {
  switch (biome) {
    case "heaven":  return "heaven/HeavenVazaDeathAnimation";
    case "hell":    return "hell/HellBallDeathAnimation";
    default:        return null;
  }
}

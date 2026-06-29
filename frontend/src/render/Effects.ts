/**
 * Effects.ts — Event-driven particle + animation effects.
 *
 * Delegates one-shot animated effects (explosions, burns, flashes) to AnimSystem
 * using real atlas art (strip-sliced Explosion, FireBirth, etc.) rather than
 * static Sprite fades.  Keeps the original Particle path as a fallback for any
 * key that isn't found in the atlas.
 */

import { Container, Graphics, Sprite, AnimatedSprite, Texture, BLEND_MODES } from "pixi.js";
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

// Block destruction: the biome crack-strip plays as a snappy shatter; a single small biome-tinted
// spark adds punch. (Previously THREE overlapping bursts of different scale/blend stacked into an
// incoherent mess — plus the per-biome "secondary" wrongly played the vaza/hell-ball death on every
// brick. One shatter + one spark reads cleanly.)
const BLOCK_SHATTER_FPS = 30;
const BIOME_SPARK_TINT: Record<string, number> = {
  hell: 0xff8844, caverns: 0x88ccff, cavern: 0x88ccff, dungeon: 0x88ccff,
  village: 0x99dd66, heaven: 0xffe9b0,
};
function biomeSparkTint(biome: string): number { return BIOME_SPARK_TINT[biome] ?? 0xffd9a0; }

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

// Necromant death-mark: a DeathSphere hovers over the marked corpse until the
// revive fires (or is cancelled by killing the necromant). docs/11 — makes the
// race visible and tells the player WHICH cells are coming back.
const DEATH_SPHERE_KEY        = "village/enemies/DeathSphere";
const DEATH_SPHERE_SIZE_FRAC  = 0.8;  // of cellSize
const DEATH_SPHERE_ALPHA_BASE = 0.65;
const DEATH_SPHERE_ALPHA_AMP  = 0.25;
const DEATH_SPHERE_PULSE_HZ   = 2.5;

// Demon fist column flashes (docs/11 boss verbs): warning amber, slam red.
const FIST_TELEGRAPH_COLOR = 0xffaa33;
const FIST_TELEGRAPH_MS    = 700;
const FIST_SLAM_MS         = 350;
const JUDGEMENT_COLOR      = 0xffd24a; // Paladin Last Day gold
const FIST_COLUMN_ALPHA    = 0.32;

interface ColumnFlash { gfx: Graphics; life: number; elapsed: number }
// Transient effect sprites with optional vertical slam (y0→y1, ease-in) + tail fade. Used by the Demon
// fist (a hellfire pillar + a clawed hand crashing down). 2026-06-16 effects style pass.
interface FxSprite { sp: Sprite; life: number; elapsed: number; fade: number; y0?: number; y1?: number }
// Demon fist SLAM: a column of hellfire (the fire-wall stand animation, stretched tall).
const FIRE_PILLAR_KEY = "firemage/spell_firewall/firestandannimation";

export class Effects {
  readonly container: Container;
  private animSys: AnimSystem;
  private particles: Particle[] = [];
  // Death-mark spheres keyed by rounded cell position.
  private deathMarks = new Map<string, Sprite>();
  private _markClock = 0;
  private columnFlashes: ColumnFlash[] = [];
  private _fxSprites: FxSprite[] = [];
  /** Board height in world units — set by the renderer each snapshot for column flashes. */
  boardH = 0;

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
        case "perfectDeflect":
          this.spawnPerfectDeflect(ev.x, ev.y, cellSize);
          break;
        case "burn":
          this.spawnBurn(ev.x, ev.y, cellSize);
          break;
        case "ignite":
          this.spawnIgniteFlash(ev.x, ev.y, cellSize);
          break;
        case "spellCast":
          this.spawnCastFlash(ev.x, ev.y, cellSize, biome ?? "hell");
          break;
        case "spellFizzle":
          this.spawnFizzle(ev.x, ev.y, cellSize);
          break;
        case "lightning":
          this.spawnLightningStrike(ev.x, ev.y, cellSize);
          break;
        case "skeletonDeath":
          this.spawnSkeletonDeath(ev.x, ev.y, cellSize);
          break;
        case "ghostPortal":
          this.spawnPhaseFlash(ev.x, ev.y, cellSize);
          break;
        case "deathMark":
          this.addDeathMark(ev.x, ev.y, cellSize);
          break;
        case "revive":
        case "reviveCancelled":
          this.removeDeathMark(ev.x, ev.y);
          break;
        case "fistTelegraph":
          this.spawnColumnFlash(ev.x, cellSize, FIST_TELEGRAPH_COLOR, FIST_TELEGRAPH_MS);
          break;
        case "fistSlam":
          this.spawnFirePillar(ev.x, cellSize);
          this.spawnBlockDestroy(ev.x, this.boardH * 0.5, cellSize, biome ?? "hell");
          break;
        case "judgement": // Paladin Last Day: a golden column smite
          this.spawnColumnFlash(ev.x, cellSize, JUDGEMENT_COLOR, FIST_SLAM_MS);
          break;
      }
    }
  }

  /** A column of fire at world x — the Demon fist's warning/impact. A soft funnel that brightens and
   *  widens toward a glowing pool at the floor (where the fist lands), not a flat full-height bar
   *  (readability/feel pass 2026-06-16). */
  private spawnColumnFlash(x: number, cellSize: number, color: number, lifeMs: number) {
    const gfx = new Graphics();
    gfx.blendMode = BLEND_MODES.ADD;
    const h = Math.max(this.boardH, cellSize);
    const steps = 9;
    for (let i = 0; i < steps; i++) {
      const t = i / (steps - 1);                       // 0 top → 1 floor
      const a = FIST_COLUMN_ALPHA * (0.12 + 0.88 * t * t); // brightens toward the impact
      const w = cellSize * (0.45 + 0.65 * t);          // widens toward the impact
      gfx.beginFill(color, a).drawRect(x - w / 2, h * (i / steps), w, h / steps + 1).endFill();
    }
    // bright impact pool at the floor — the unmistakable "here it lands"
    gfx.beginFill(color, Math.min(0.9, FIST_COLUMN_ALPHA * 1.6))
      .drawEllipse(x, h, cellSize * 0.85, cellSize * 0.45).endFill();
    this.container.addChild(gfx);
    this.columnFlashes.push({ gfx, life: lifeMs, elapsed: 0 });
  }

  /** Demon fist SLAM: a pillar of hellfire crashes down the column with a clawed hand + a floor impact —
   *  painted art (fire-wall frames + DemonHand sprite + Explosion), replacing the old flat red bar. */
  private spawnFirePillar(x: number, cellSize: number): void {
    const h = Math.max(this.boardH, cellSize);
    // 1. Pillar of hellfire down the column.
    const frames = animFrames(FIRE_PILLAR_KEY);
    if (frames.length >= 2) {
      const fire = new AnimatedSprite(frames);
      fire.anchor.set(0.5, 1);
      fire.blendMode = BLEND_MODES.ADD;
      fire.loop = true;
      fire.animationSpeed = 18 / 60;
      fire.tint = 0xffd9a0;
      fire.width = cellSize * 1.7;
      fire.height = h * 1.02;
      fire.position.set(x, h);
      fire.play();
      this.container.addChild(fire);
      this._fxSprites.push({ sp: fire, life: 620, elapsed: 0, fade: 0.35 });
    }
    // 2. Clawed hand crashing down the column (ease-in slam).
    const handTex = tex("hell/DemonHand3");
    if (handTex !== Texture.WHITE) {
      const hand = new Sprite(handTex);
      hand.anchor.set(0.5, 0.5);
      const hw = cellSize * 2.4;
      hand.width = hw;
      hand.height = hw * ((handTex.height || 1) / (handTex.width || 1));
      hand.position.set(x, -cellSize);
      this.container.addChild(hand);
      this._fxSprites.push({ sp: hand, life: 420, elapsed: 0, fade: 0.65, y0: -cellSize, y1: h - cellSize * 0.4 });
    }
    // 3. Floor impact burst.
    const spark = this._explosionFrames();
    if (spark.length) this.animSys.oneShot(spark, EXPLOSION_FPS + 6, x, h - cellSize * 0.3, cellSize * 1.5, true, 0xffcaa0);
    else this.spawnFallback(tex("Explosion"), x, h - cellSize * 0.3, cellSize * 1.4, 240, 0.5, 1.8, 0xffcaa0);
  }

  // ── Necromant death-mark spheres ──────────────────────────────────────────

  private static markKey(x: number, y: number) { return `${Math.round(x)},${Math.round(y)}`; }

  private addDeathMark(x: number, y: number, cellSize: number) {
    const key = Effects.markKey(x, y);
    if (this.deathMarks.has(key)) return;
    const texture = tex(DEATH_SPHERE_KEY); // direct atlas key via the legacy resolver
    const sp = new Sprite(texture);
    sp.anchor.set(0.5);
    sp.blendMode = BLEND_MODES.ADD;
    sp.position.set(x, y);
    const size = cellSize * DEATH_SPHERE_SIZE_FRAC;
    const dim = Math.max(sp.texture.width, sp.texture.height, 1);
    sp.scale.set(size / dim);
    sp.alpha = DEATH_SPHERE_ALPHA_BASE;
    this.container.addChild(sp);
    this.deathMarks.set(key, sp);
  }

  private removeDeathMark(x: number, y: number) {
    const key = Effects.markKey(x, y);
    const sp = this.deathMarks.get(key);
    if (!sp) return;
    this.container.removeChild(sp);
    this.deathMarks.delete(key);
  }

  // ── Block destruction ────────────────────────────────────────────────────

  private spawnBlockDestroy(x: number, y: number, cellSize: number, biome: string) {
    const tint = biomeSparkTint(biome);
    const biomeStrip = getBiomeDestroyStrip(biome);
    const shatter = biomeStrip ? animStrip(biomeStrip) : [];

    if (shatter.length >= 2) {
      // Primary: the block visibly shatters using its biome art (opaque → normal blend, snappy).
      this.animSys.oneShot(shatter, BLOCK_SHATTER_FPS, x, y, cellSize * 1.1, false, 0xffffff);
      // Secondary: one small biome-tinted spark for punch — brief, and clearly subordinate.
      const spark = this._explosionFrames();
      if (spark.length) this.animSys.oneShot(spark, EXPLOSION_FPS + 6, x, y, cellSize * 0.45, true, tint);
    } else {
      // No biome shatter art → a single clean additive poof, biome-tinted.
      const ex = this._explosionFrames();
      if (ex.length) this.animSys.oneShot(ex, EXPLOSION_FPS, x, y, cellSize * 1.05, true, tint);
      else this.spawnFallback(tex("Explosion"), x, y, cellSize * 1.1, 260, 1.0, 1.5, tint);
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
    // A small bright FIRE flash where a block ignites — fire art only. (It used to also
    // overlay a phoenix birth animation; the Phoenix belongs to the Phoenix spell alone.)
    const fbFrames = this._fireBirthFrames();
    if (fbFrames.length) {
      this.animSys.oneShot(fbFrames, FIRE_BIRTH_FPS + 4, x, y, cellSize * 1.2, true, 0xffdd88);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.7, 150, 0.8, 1.4, 0xffaa44);
    }
  }

  // ── Generic spell-cast flash (on the shared `spellCast` event) ────────────
  // A small, neutral, biome-tinted puff that simply marks that *a* spell fired.
  // Deliberately subtle and identity-free: `spellCast` is a SHARED event raised by ~15
  // systems (every player spell AND several enemy abilities — Bonewalker, Bone Golem,
  // Ashfall…), so it must NOT carry any one spell's look. (It used to fire a giant
  // Phoenix flourish here — that bloomed a phoenix on EVERY cast. The Phoenix's identity
  // belongs to the Phoenix spell alone: its orbiting `PhoenixLayer` entity + its fire
  // trail via `burn` events, plus a one-time birth flourish wired to the real entity
  // spawn — see `spawnPhoenixBirth`.)
  private spawnCastFlash(x: number, y: number, cellSize: number, biome: string) {
    const tint = biomeSparkTint(biome);
    const frames = this._explosionFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, EXPLOSION_FPS + 6, x, y, cellSize * 0.8, true, tint);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.7, 150, 0.6, 1.4, tint);
    }
  }

  // ── Spell-fizzle "dud" cue ────────────────────────────────────────────────
  // A small COOL-grey puff when a cast couldn't fire (Fire Wall while the ball is on the paddle,
  // Conflagration on an empty board). Cool tint (not warm like spawnCastFlash) reads as "nothing
  // happened", so a blocked cast is never a silent no-op.
  private spawnFizzle(x: number, y: number, cellSize: number) {
    const frames = this._explosionFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, EXPLOSION_FPS, x, y, cellSize * 0.5, true, 0x8a93a8);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.45, 180, 0.5, 1.2, 0x8a93a8);
    }
  }

  // ── Phoenix BIRTH flourish — ONLY ever called for a real Phoenix entity spawn ─
  // Driven by PhoenixLayer detecting a newly-spawned phoenix id (NOT by spellCast), so the
  // phoenix art appears only when the Phoenix spell actually summons its bird.
  spawnPhoenixBirth(x: number, y: number, cellSize: number) {
    const birthFrames = this._phoenixBirthFrames();
    if (birthFrames.length) {
      this.animSys.oneShot(birthFrames, PHOENIX_BIRTH_FPS, x, y, cellSize * 3, true, 0xff8a3a);
      return;
    }
    const deathFrames = this._phoenixDeathFrames();
    if (deathFrames.length) {
      this.animSys.oneShot(deathFrames, PHOENIX_DEATH_FPS, x, y, cellSize * 3, true, 0xff8844);
      return;
    }
    this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.8, 160, 0.8, 1.5, 0xff8a3a);
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

  /** A bright gold burst rewarding a perfect (centre-band) paddle deflect. */
  private spawnPerfectDeflect(x: number, y: number, cellSize: number) {
    const frames = this._explosionFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, EXPLOSION_FPS + 8, x, y, cellSize * 0.7, true, 0xffe060);
    }
    this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.5, 150, 0.6, 1.6, 0xfff2a0);
  }

  /** Witchland portal: a quick spectral burst as the ball flips phase (normal ⇄ ghost). */
  private spawnPhaseFlash(x: number, y: number, cellSize: number) {
    const frames = this._explosionFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, EXPLOSION_FPS + 6, x, y, cellSize * 1.1, true, 0x9b6bff);
    }
    this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.8, 200, 0.5, 1.9, 0x9b6bff);
  }

  /** A small bright spark when the ball chips (but doesn't destroy) a block — tactile per-hit feedback. */
  hitSpark(x: number, y: number, cellSize: number) {
    const frames = this._explosionFrames();
    if (frames.length) {
      this.animSys.oneShot(frames, EXPLOSION_FPS + 6, x, y, cellSize * 0.55, true, 0xfff0b0);
    } else {
      this.spawnFallback(tex("Explosion"), x, y, cellSize * 0.5, 110, 1.0, 1.3, 0xfff0b0);
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
    // Budget (readability overhaul 2026-06-16 §E): cap concurrent additive particles so a flurry of
    // casts/kills can't white-out the field — recycle the oldest when full.
    const PARTICLE_BUDGET = 14;
    while (this.particles.length >= PARTICLE_BUDGET) {
      const old = this.particles.shift()!;
      this.container.removeChild(old.sprite);
    }
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

    // Fade out the fist column flashes.
    for (let i = this.columnFlashes.length - 1; i >= 0; i--) {
      const cf = this.columnFlashes[i];
      cf.elapsed += dtMs;
      const t = cf.elapsed / cf.life;
      if (t >= 1) {
        this.container.removeChild(cf.gfx);
        this.columnFlashes.splice(i, 1);
      } else {
        cf.gfx.alpha = 1 - t;
      }
    }

    // Fist effect sprites: ease-in vertical slam + tail fade, then remove.
    for (let i = this._fxSprites.length - 1; i >= 0; i--) {
      const f = this._fxSprites[i];
      f.elapsed += dtMs;
      const t = Math.min(f.elapsed / f.life, 1);
      if (f.y0 !== undefined && f.y1 !== undefined) f.sp.y = f.y0 + (f.y1 - f.y0) * (t * t); // accelerate down
      f.sp.alpha = t > f.fade ? 1 - (t - f.fade) / (1 - f.fade) : 1;
      if (t >= 1) {
        this.container.removeChild(f.sp);
        if (f.sp instanceof AnimatedSprite) f.sp.stop();
        f.sp.destroy();
        this._fxSprites.splice(i, 1);
      }
    }

    // Pulse the death-mark spheres so they read as "pending", not debris.
    this._markClock += dtMs / 1000;
    const markAlpha = DEATH_SPHERE_ALPHA_BASE
      + DEATH_SPHERE_ALPHA_AMP * Math.sin(this._markClock * Math.PI * DEATH_SPHERE_PULSE_HZ);
    for (const sp of this.deathMarks.values()) sp.alpha = markAlpha;

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
    for (const sp of this.deathMarks.values()) this.container.removeChild(sp);
    this.deathMarks.clear();
    for (const cf of this.columnFlashes) this.container.removeChild(cf.gfx);
    this.columnFlashes = [];
    for (const f of this._fxSprites) { this.container.removeChild(f.sp); f.sp.destroy(); }
    this._fxSprites = [];
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

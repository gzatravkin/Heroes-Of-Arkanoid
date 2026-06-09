import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { anim as animFrames, tex as atlasTex } from "./assets";
import { AnimSystem } from "./AnimSystem";

// Ball rendering (pooled by id): per-class ball sprite, turret/fireball projectile
// art, ignite halo + looping fire aura, ghost tint, and Necromancer decay halo.
// Owns the balls display container and a dedicated AnimSystem for the fire auras.
// Extracted from Renderer.

// Projectile id threshold: turret bullets + fireballs use id >= this value.
export const PROJECTILE_ID_THRESHOLD = 10000;

const BALL_SPRITE_SCALE = 2.2; // sprite is slightly larger than the physics circle
const BALL_RADIUS_FRAC  = 0.25; // ball radius as a fraction of cellSize

// Halo drawn behind ignited balls.
const IGNITE_HALO_RADIUS_MULT = 1.8;
const IGNITE_HALO_ALPHA = 0.35;

// Ignite fire aura: atlas anim key (FireBirth frames) played as a looping aura.
const IGNITE_AURA_KEY = "firemage/spell_phonex/phoenixbirthanimpic";
const IGNITE_AURA_FPS = 10; // slow loop looks like a gentle fire aura
const IGNITE_AURA_SIZE_MULT = 2.8; // aura size as a multiplier of the ball sprite size

// Necromancer decay aura on ball (sickly green, distinct from ignite orange).
const DECAY_HALO_COLOR       = 0x22cc44;
const DECAY_HALO_ALPHA       = 0.38;
const DECAY_HALO_RADIUS_MULT = 1.8;

// Fireball / firering: art for the active fireball projectile.
const FIRE_RING_KEY      = "firemage/spell_firering/FireRing";
const TURRET_MISSILE_KEY = "firemage/spell_fireturret/FireHeroTurretMissile";

interface BallDto { id: number; x: number; y: number; ignited: boolean; decayed?: boolean; ghost?: boolean }

export class BallLayer {
  readonly container = new Container();      // ball sprites + halos
  readonly auraContainer: Container;          // looping fire auras (separate z-slot)
  private auraSys = new AnimSystem();
  private pool = new Map<number, { sp: Sprite; haloGfx: Graphics; auraId?: number }>();

  constructor() {
    this.auraContainer = this.auraSys.container;
  }

  /** The looping fire auras are AnimatedSprites — advance them each frame. */
  updateAnim(dtMs: number): void {
    this.auraSys.update(dtMs);
  }

  update(balls: BallDto[], tick: number, cellSize: number, ballSpriteKey: string): void {
    const ballRadius   = cellSize * BALL_RADIUS_FRAC;
    const spriteRadius = ballRadius * BALL_SPRITE_SCALE;

    const ballTex     = atlasTex(ballSpriteKey);
    const fireRingTex = tex(FIRE_RING_KEY);
    const missileTex  = tex(TURRET_MISSILE_KEY);
    // Projectile art: prefer FireRing for fireball look; fall back to missile art.
    const projectileTex = fireRingTex !== Texture.WHITE ? fireRingTex
      : (missileTex !== Texture.WHITE ? missileTex : ballTex);
    const igniteAuraFrames = animFrames(IGNITE_AURA_KEY);

    const liveBallIds = new Set<number>();
    for (const ball of balls) liveBallIds.add(ball.id);

    // Remove pooled entries for balls that no longer exist.
    for (const [id, entry] of this.pool) {
      if (!liveBallIds.has(id)) {
        this.container.removeChild(entry.haloGfx);
        this.container.removeChild(entry.sp);
        if (entry.auraId !== undefined) this.auraSys.remove({ id: entry.auraId });
        this.pool.delete(id);
      }
    }

    for (const ball of balls) {
      const isProjectile = ball.id >= PROJECTILE_ID_THRESHOLD;

      if (this.pool.has(ball.id)) {
        // Update existing pooled entry.
        const entry = this.pool.get(ball.id)!;
        const { sp, haloGfx } = entry;

        haloGfx.clear();
        if (ball.ignited && !isProjectile) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        sp.x = ball.x;
        sp.y = ball.y;

        if (isProjectile) {
          // Turret missile: rotate in direction of travel; pulsate slightly.
          sp.tint = 0xffcc44;
          const missileSize = ballRadius * 1.4;
          sp.width  = missileSize * 2;
          sp.height = missileSize * 2;
          sp.rotation = (tick * 0.12); // slow spin
        } else {
          sp.tint = ball.ignited ? 0xff7a2a : (ball.ghost ? 0xaa88ff : 0xffffff);
          // Pulse ignited balls slightly for visual feedback.
          const igScale = ball.ignited
            ? spriteRadius * (1.0 + 0.15 * Math.sin(tick * 0.2))
            : spriteRadius;
          sp.width  = igScale * 2;
          sp.height = igScale * 2;
        }

        // Update ignite aura position if active.
        if (entry.auraId !== undefined) {
          this.auraSys.moveTo({ id: entry.auraId }, ball.x, ball.y);
          this.auraSys.resize({ id: entry.auraId }, spriteRadius * IGNITE_AURA_SIZE_MULT * 2);
        }

        // Spawn/remove ignite aura as ignite state changes (non-projectile balls only).
        if (!isProjectile && ball.ignited && entry.auraId === undefined && igniteAuraFrames.length) {
          const h = this.auraSys.looping(
            igniteAuraFrames, IGNITE_AURA_FPS,
            ball.x, ball.y,
            spriteRadius * IGNITE_AURA_SIZE_MULT * 2,
            true, 0xff8822,
          );
          entry.auraId = h.id;
        } else if (!ball.ignited && entry.auraId !== undefined) {
          this.auraSys.remove({ id: entry.auraId });
          entry.auraId = undefined;
        }
      } else {
        // Create new pooled entry.
        const haloGfx = new Graphics();
        if (ball.ignited && !isProjectile) {
          haloGfx.blendMode = BLEND_MODES.ADD;
          haloGfx.beginFill(0xff5500, IGNITE_HALO_ALPHA * 0.8)
            .drawCircle(ball.x, ball.y, ballRadius * IGNITE_HALO_RADIUS_MULT)
            .endFill();
        }

        // Projectile (turret bullet/fireball) uses FireRing/missile art; normal ball uses FireHeroBall.
        const chosenTex: Texture = isProjectile
          ? projectileTex
          : (ballTex !== Texture.WHITE ? ballTex : Texture.WHITE);

        const sp = new Sprite(chosenTex);
        sp.anchor.set(0.5);
        sp.x = ball.x;
        sp.y = ball.y;

        if (isProjectile) {
          sp.tint = 0xffcc44;
          const missileSize = ballRadius * 1.4;
          sp.width  = missileSize * 2;
          sp.height = missileSize * 2;
        } else {
          sp.tint = ball.ignited ? 0xff7a2a : (ball.ghost ? 0xaa88ff : 0xffffff);
          sp.width  = spriteRadius * 2;
          sp.height = spriteRadius * 2;
        }

        this.container.addChild(haloGfx);
        this.container.addChild(sp);

        // Spawn ignite aura for already-ignited balls.
        let auraId: number | undefined;
        if (!isProjectile && ball.ignited && igniteAuraFrames.length) {
          const h = this.auraSys.looping(
            igniteAuraFrames, IGNITE_AURA_FPS,
            ball.x, ball.y,
            spriteRadius * IGNITE_AURA_SIZE_MULT * 2,
            true, 0xff8822,
          );
          auraId = h.id;
        }

        this.pool.set(ball.id, { sp, haloGfx, auraId });
      }
    }

    // Necromancer decay aura: repaint the halo green for decayed balls.
    for (const ball of balls) {
      if (ball.id >= PROJECTILE_ID_THRESHOLD) continue;
      const entry = this.pool.get(ball.id);
      if (!entry) continue;
      if ((ball as any).decayed) {
        entry.haloGfx.clear();
        entry.haloGfx.blendMode = BLEND_MODES.ADD;
        entry.haloGfx.beginFill(DECAY_HALO_COLOR, DECAY_HALO_ALPHA)
          .drawCircle(ball.x, ball.y, ballRadius * DECAY_HALO_RADIUS_MULT)
          .endFill();
      }
    }
  }
}

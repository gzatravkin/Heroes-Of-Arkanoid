import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";
import { AnimSystem } from "./AnimSystem";

// Ball rendering (pooled by id): per-class ball sprite + a strict "ball-first" readability treatment —
// a dark crisp OUTLINE ring (every biome) so the ball separates from any background, a bright HUE-LOCKED
// core so the ball is always the single brightest point on screen, and STATE shown by a thin ring + a
// modest glow only (never by recolouring or engulfing the ball). Owns the balls display container.
// (Readability overhaul 2026-06-16 — docs/2026-06-16-effects-clarity-proposal.md §A.)

// The ball renders a notch LARGER than its physics circle so it's easy to track in motion (the old
// "too huge" was the removed 2.8× fire aura, not the ball itself — so there's headroom). Visual radius =
// physics radius × this. (owner 2026-06-16)
const BALL_VISUAL_MULT  = 1.4;
const BALL_RADIUS_FRAC  = 0.15625; // ball radius as a fraction of cellSize (matches SimConfig.BallRadius 5/32)

// Ball-first treatment.
const BALL_OUTLINE_COLOR = 0x0a0a12; // dark crisp edge — contrasts with fire/orange AND bright heaven
const BALL_OUTLINE_ALPHA = 0.72;
const BALL_CORE_COLOR    = 0xffffff; // hue-LOCKED bright nucleus (never changes with state)
const BALL_CORE_ALPHA    = 0.92;

// State is communicated by a thin ring + a MODEST glow (not a giant aura).
const IGNITE_RING   = 0xff7a2a;
const GHOST_RING    = 0x9b6bff;
const DECAY_RING    = 0x22cc44;
const STATE_GLOW_ALPHA = 0.28; // modest — was a 2.8× looping fire aura that merged the ball into the field

// Ghost phase (Witchland portal): swap to the spectral BallGhost sprite so the phased ball reads distinctly.
const GHOST_BALL_KEY = "village/BallGhost";

// Fireball / firering: art for the active fireball projectile.
const FIRE_RING_KEY      = "firemage/spell_firering/FireRing";
const TURRET_MISSILE_KEY = "firemage/spell_fireturret/FireHeroTurretMissile";

// Per-kind projectile art (legacy art wired 2026-06-16): each spell projectile uses its own drawn sprite,
// oriented to its travel direction, instead of a single generic gold missile for everything.
const PROJ_ART: Record<string, { key: string; sizeMult: number }> = {
  rocket: { key: "engineer/spell_rocket/Rocket",      sizeMult: 2.4 },
  spear:  { key: "paladin/spell_spear/KnightChain",   sizeMult: 2.8 },
  turret: { key: "firemage/spell_fireturret/FireHeroTurretMissile", sizeMult: 1.4 },
};

interface BallDto { id: number; x: number; y: number; ignited: boolean; decayed?: boolean; ghost?: boolean; summoned?: boolean; radiusScale?: number }
interface ProjectileDto { id: number; x: number; y: number; kind: string }

export class BallLayer {
  readonly container = new Container();      // ball sprites + outline/core graphics
  readonly auraContainer: Container;          // kept for z-order compatibility (no longer used for ball auras)
  private auraSys = new AnimSystem();
  private ballPool = new Map<number, { sp: Sprite; glow: Graphics; core: Graphics }>();
  private projPool = new Map<number, { sp: Sprite; px: number; py: number; art: boolean }>();

  constructor() {
    this.auraContainer = this.auraSys.container;
  }

  updateAnim(dtMs: number): void {
    this.auraSys.update(dtMs);
  }

  /** Modest state glow drawn BEHIND the sprite (ignite/ghost/decay). Never engulfs the ball. */
  private paintGlow(glow: Graphics, ball: BallDto, br: number): void {
    glow.clear();
    const c = ball.decayed ? DECAY_RING : ball.ghost ? GHOST_RING : ball.ignited ? IGNITE_RING : 0;
    if (!c) return;
    glow.blendMode = BLEND_MODES.ADD;
    glow.beginFill(c, STATE_GLOW_ALPHA).drawCircle(ball.x, ball.y, br * 1.5).endFill();
  }

  /** Dark outline ring + bright hue-locked core + thin state ring, drawn IN FRONT of the sprite. */
  private paintCore(core: Graphics, ball: BallDto, br: number): void {
    core.clear();
    // Dark crisp outline at the ball edge — guarantees separation on every biome.
    core.lineStyle(Math.max(1, br * 0.30), BALL_OUTLINE_COLOR, BALL_OUTLINE_ALPHA);
    core.drawCircle(ball.x, ball.y, br * 1.02);
    // Bright hue-locked nucleus — the single brightest point on screen.
    core.lineStyle(0);
    core.beginFill(BALL_CORE_COLOR, BALL_CORE_ALPHA).drawCircle(ball.x, ball.y, br * 0.42).endFill();
    // Thin state ring just outside the ball.
    const ring = ball.ignited ? IGNITE_RING : ball.ghost ? GHOST_RING : ball.decayed ? DECAY_RING : 0;
    if (ring) {
      core.lineStyle(Math.max(1, br * 0.22), ring, 0.95);
      core.drawCircle(ball.x, ball.y, br * 1.32);
    }
  }

  update(balls: BallDto[], projectiles: ProjectileDto[], tick: number, cellSize: number, ballSpriteKey: string, _biome = ""): void {
    const ballRadius   = cellSize * BALL_RADIUS_FRAC;

    const ballTex      = atlasTex(ballSpriteKey);
    const ghostBallTex = atlasTex(GHOST_BALL_KEY);
    const fireRingTex = tex(FIRE_RING_KEY);
    const missileTex  = tex(TURRET_MISSILE_KEY);
    const projectileTex = fireRingTex !== Texture.WHITE ? fireRingTex
      : (missileTex !== Texture.WHITE ? missileTex : ballTex);

    // ── Real balls ────────────────────────────────────────────────────────────
    const liveBallIds = new Set(balls.map(b => b.id));
    for (const [id, entry] of this.ballPool) {
      if (!liveBallIds.has(id)) {
        this.container.removeChild(entry.glow);
        this.container.removeChild(entry.sp);
        this.container.removeChild(entry.core);
        this.ballPool.delete(id);
      }
    }

    for (const ball of balls) {
      const rs = ball.radiusScale ?? 1;
      const br = ballRadius * rs;
      const vr = br * BALL_VISUAL_MULT; // visual radius (sprite + outline/core), a notch over the physics circle
      let entry = this.ballPool.get(ball.id);
      if (!entry) {
        const glow = new Graphics();
        const sp = new Sprite(Texture.WHITE);
        sp.anchor.set(0.5);
        const core = new Graphics();
        this.container.addChild(glow);
        this.container.addChild(sp);
        this.container.addChild(core);
        entry = { sp, glow, core };
        this.ballPool.set(ball.id, entry);
      }
      const { sp, glow, core } = entry;

      // Hue-LOCK the sprite: a phased ball uses the distinct BallGhost art; the summoned skeleton minion
      // keeps its green identity; otherwise the ball is never recoloured (state lives in the ring/glow).
      const useGhostArt = !!ball.ghost && ghostBallTex !== Texture.WHITE;
      sp.texture = useGhostArt ? ghostBallTex : (ballTex !== Texture.WHITE ? ballTex : Texture.WHITE);
      sp.tint = ball.summoned ? 0x88ffaa : 0xffffff;
      sp.x = ball.x;
      sp.y = ball.y;
      sp.width = vr * 2;
      sp.height = vr * 2;

      this.paintGlow(glow, ball, vr);
      this.paintCore(core, ball, vr);
    }

    // ── Projectiles (turret bullets, fireballs, golems, skeleton bolts) ───────
    const liveProjIds = new Set(projectiles.map(p => p.id));
    for (const [id, entry] of this.projPool) {
      if (!liveProjIds.has(id)) {
        this.container.removeChild(entry.sp);
        this.projPool.delete(id);
      }
    }

    const missileSize = ballRadius * 1.4;
    for (const proj of projectiles) {
      const art = PROJ_ART[proj.kind];
      let entry = this.projPool.get(proj.id);
      if (!entry) {
        const artTex = art ? atlasTex(art.key) : Texture.WHITE;
        const useArt = art != null && artTex !== Texture.WHITE;
        const sp = new Sprite(useArt ? artTex : projectileTex);
        sp.anchor.set(0.5);
        if (!useArt) sp.tint = 0xffcc44;
        this.container.addChild(sp);
        entry = { sp, px: proj.x, py: proj.y, art: useArt };
        this.projPool.set(proj.id, entry);
      }
      const { sp } = entry;
      const sz = missileSize * (entry.art && art ? art.sizeMult : 2);
      sp.x = proj.x;
      sp.y = proj.y;
      sp.width  = sz;
      sp.height = sz;
      if (entry.art) {
        // Orient drawn projectiles (rocket/spear/turret) to their travel direction.
        const dx = proj.x - entry.px, dy = proj.y - entry.py;
        if (dx * dx + dy * dy > 0.04) sp.rotation = Math.atan2(dy, dx) + Math.PI / 2;
      } else {
        sp.rotation = tick * 0.12; // generic projectiles spin
      }
      entry.px = proj.x;
      entry.py = proj.y;
    }
  }
}

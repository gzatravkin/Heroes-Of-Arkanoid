import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { bg as biomedBg, hellParallaxFrames, tex as atlasTex } from "./assets";

// Biome background, Hell parallax layers, cosmetic ambient sprites, and the
// per-biome atmosphere kits (docs/12): Hell embers, Caverns dust, Witchland fog
// shadows, Heaven clouds + light motes. Exposes two containers: `bgLayer` (stage,
// behind the world) and `ambientContainer` (world, behind play).

// Biome background: slightly darkened so blocks read clearly over it.
const BG_TINT = 0xaaaaaa; // ~67% brightness multiplier on the sprite
// During active play the background is dimmed harder (cool/dark) so blocks + ball pop off a calm
// backdrop (readability overhaul 2026-06-16 §D). Restored to BG_TINT when not Playing.
const PLAY_BG_TINT = 0x55555f;
const PLAY_PARALLAX_ALPHA = 0.20;
const IDLE_PARALLAX_ALPHA = 0.35;

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

// ── Atmosphere kits (docs/12) ────────────────────────────────────────────────
// All particles live in ambientContainer (global alpha 0.22) so they can never
// compete with playfield readability — the docs/12 restraint rule.
const WRAP_W = 440; // world-space wrap bounds (matches the ambient sprite wrap)
const WRAP_H = 540;

// Particle counts deliberately low (readability overhaul 2026-06-16): rising embers were the #1 thing
// competing with the ball for the eye, so the field carries only a sparse hint of atmosphere.
const HELL_EMBER_COUNT   = 6;
const HELL_EMBER_COLOR   = 0xffaa44;
const CAVERN_DUST_COUNT  = 8;
const CAVERN_DUST_COLOR  = 0xbbaa99;
const HEAVEN_MOTE_COUNT  = 6;
const HEAVEN_MOTE_COLOR  = 0xfff4cc;
const HEAVEN_CLOUD_KEYS  = ["heaven/Cloud", "heaven/Clouds", "heaven/HeavenClouds"];
const VILLAGE_SHADOW_KEY = "village/enemies/VillageShadow";

interface MoteParticle {
  node: Graphics | Sprite;
  x: number; y: number; vx: number; vy: number;
  /** phase offset for the sway/flicker sine */
  phase: number;
}

export class BackgroundLayer {
  readonly bgLayer = new Container();        // stage-level (behind world)
  readonly ambientContainer = new Container(); // world-level (behind play)

  private bgSprite = new Sprite();
  private _hellParallaxSprites: Sprite[] = [];
  private _lastBiome = "";
  private _ambientSprites: Ambient[] = [];
  private _lastAmbientBiome = "";
  private _motes: MoteParticle[] = [];
  private _moteClock = 0;

  constructor() {
    this.bgSprite.anchor.set(0);
    this.bgSprite.tint = BG_TINT;
    this.bgLayer.addChild(this.bgSprite);
    this.ambientContainer.alpha = 0.12; // purely cosmetic — kept very low so it never competes with the ball
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
      this._buildAtmosphere(biome, cellSize);

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

  /** Dim the background harder during active play so blocks + ball pop (readability overhaul §D). */
  setPlayDim(playing: boolean): void {
    this.bgSprite.tint = playing ? PLAY_BG_TINT : BG_TINT;
    const pa = playing ? PLAY_PARALLAX_ALPHA : IDLE_PARALLAX_ALPHA;
    for (const psp of this._hellParallaxSprites) psp.alpha = pa;
  }

  /** Build the biome's atmosphere kit (docs/12): embers / dust / fog shadows / clouds. */
  private _buildAtmosphere(biome: string, cellSize: number): void {
    for (const m of this._motes) this.ambientContainer.removeChild(m.node);
    this._motes = [];
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const base = biome.split("-")[0]; // "village-boss" → "village"

    const addDot = (color: number, r: number, vx: number, vy: number, additive: boolean) => {
      const g = new Graphics();
      g.beginFill(color, 1).drawCircle(0, 0, r).endFill();
      if (additive) g.blendMode = BLEND_MODES.ADD;
      const x = Math.random() * WRAP_W;
      const y = Math.random() * WRAP_H;
      g.position.set(x, y);
      this.ambientContainer.addChild(g);
      this._motes.push({ node: g, x, y, vx, vy, phase: Math.random() * Math.PI * 2 });
    };

    const addSprite = (key: string, size: number, alpha: number, vx: number, tint?: number) => {
      const t = atlasTex(key);
      if (t === Texture.WHITE) return;
      const sp = new Sprite(t);
      sp.anchor.set(0.5);
      const dim = Math.max(t.width, t.height, 1);
      sp.scale.set(size / dim);
      sp.alpha = alpha;
      if (tint !== undefined) sp.tint = tint;
      const x = Math.random() * WRAP_W;
      const y = Math.random() * (WRAP_H * 0.5); // upper half — keeps the paddle zone clean
      sp.position.set(x, y);
      this.ambientContainer.addChild(sp);
      this._motes.push({ node: sp, x, y, vx, vy: 0, phase: Math.random() * Math.PI * 2 });
    };

    switch (base) {
      case "hell": // rising embers
        for (let i = 0; i < HELL_EMBER_COUNT; i++)
          addDot(HELL_EMBER_COLOR, 1.5 + Math.random() * 1.5, 0, -(0.01 + Math.random() * 0.02), true);
        break;
      case "cavern":
      case "caverns": // falling dust motes
        for (let i = 0; i < CAVERN_DUST_COUNT; i++)
          addDot(CAVERN_DUST_COLOR, 1 + Math.random(), 0, 0.005 + Math.random() * 0.008, false);
        break;
      case "village": // drifting shadow silhouettes (fog reads via their slow motion)
        addSprite(VILLAGE_SHADOW_KEY, cellSize * 4, 0.9, 0.008);
        addSprite(VILLAGE_SHADOW_KEY, cellSize * 3, 0.7, -0.012);
        break;
      case "heaven": // drifting clouds + rising light motes
        for (let i = 0; i < HEAVEN_CLOUD_KEYS.length; i++)
          addSprite(HEAVEN_CLOUD_KEYS[i], cellSize * (4 + i * 2), 0.8, 0.006 + i * 0.004);
        for (let i = 0; i < HEAVEN_MOTE_COUNT; i++)
          addDot(HEAVEN_MOTE_COLOR, 1 + Math.random(), 0, -(0.004 + Math.random() * 0.008), true);
        break;
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
    // Atmosphere motes: drift, sway, wrap.
    this._moteClock += dtMs / 1000;
    for (const m of this._motes) {
      m.x += m.vx * dtMs + Math.sin(this._moteClock + m.phase) * 0.06;
      m.y += m.vy * dtMs;
      if (m.y < -10) m.y += WRAP_H + 20;
      if (m.y > WRAP_H + 10) m.y -= WRAP_H + 20;
      if (m.x < -60) m.x += WRAP_W + 120;
      if (m.x > WRAP_W + 60) m.x -= WRAP_W + 120;
      m.node.x = m.x;
      m.node.y = m.y;
    }

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

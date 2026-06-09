import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { anim as animFrames } from "./assets";
import { AnimSystem } from "./AnimSystem";

// Per-class spell-effect rendering (Paladin barriers, Engineer radiation zones,
// Necromancer skeleton summon). Extracted from Renderer. Exposes three display
// containers so the caller can slot each into the correct world z-order, plus an
// updateAnim(dtMs) for the skeleton's looping AnimSystem.

// Paladin barrier: shield bar rendered per entry in barriers[].
const BARRIER_HEIGHT_FRAC  = 0.18; // fraction of cellSize for bar thickness
const BARRIER_GLOW_ALPHA   = 0.55;
const BARRIER_FILL_COLOR   = 0x88ccff; // steel-blue core
const BARRIER_GLOW_COLOR   = 0x4499ff; // cooler blue additive glow
const BARRIER_GLOW_W_EXTRA = 16;       // extra px each side for glow halo

// Engineer radiation zone: pulsing AoE circle.
const ZONE_FILL_COLOR      = 0x22ff44;  // toxic green
const ZONE_GLOW_COLOR      = 0x44ff88;
const ZONE_FILL_ALPHA_BASE = 0.12;
const ZONE_FILL_ALPHA_AMP  = 0.06;
const ZONE_RING_ALPHA      = 0.6;
const ZONE_PULSE_SPEED     = 0.09;

// Skeleton summon position (top of board, centered).
const SKELETON_Y_FRAC = 0.08; // fraction of boardH from top

interface Barrier { y: number; centerX: number; width: number }
interface Zone { x: number; y: number; radius: number }

export class SpellFxLayer {
  readonly barriersContainer = new Container();
  readonly zonesContainer    = new Container();
  readonly skeletonAnim      = new AnimSystem();

  // Necromancer skeleton summon state.
  private _skeletonGlow: Graphics | null = null;
  private _skeletonAuraId: number | undefined;

  /** Skeleton aura is a looping AnimatedSprite — advance it each frame. */
  updateAnim(dtMs: number): void {
    this.skeletonAnim.update(dtMs);
  }

  update(
    barriers: Barrier[], zones: Zone[], skeletonActive: boolean,
    tick: number, cellSize: number, boardW: number, boardH: number,
  ): void {
    this.drawBarriers(barriers, cellSize);
    this.drawZones(zones, tick);
    this.updateSkeleton(skeletonActive, cellSize, boardW, boardH);
  }

  // --- barriers (Paladin shield bars) ---
  private drawBarriers(barriers: Barrier[], cellSize: number): void {
    this.barriersContainer.removeChildren();
    for (const br of barriers) {
      const barH = cellSize * BARRIER_HEIGHT_FRAC;
      // Glow halo (additive, wider than fill).
      const glow = new Graphics();
      glow.blendMode = BLEND_MODES.ADD;
      glow.beginFill(BARRIER_GLOW_COLOR, BARRIER_GLOW_ALPHA)
        .drawRoundedRect(
          br.centerX - br.width / 2 - BARRIER_GLOW_W_EXTRA,
          br.y - barH * 1.4,
          br.width + BARRIER_GLOW_W_EXTRA * 2,
          barH * 2.8,
          barH,
        )
        .endFill();
      // Core fill.
      const fill = new Graphics();
      fill.beginFill(BARRIER_FILL_COLOR, 0.92)
        .drawRoundedRect(
          br.centerX - br.width / 2,
          br.y - barH / 2,
          br.width,
          barH,
          barH / 2,
        )
        .endFill();

      // Optionally overlay atlas shield art if available.
      const shieldTex = tex("paladin/spell_passiveshield/KnightShield");
      if (shieldTex !== Texture.WHITE) {
        // Tile the shield art across the barrier width.
        const tileSize = barH * 3.5;
        const count = Math.max(1, Math.round(br.width / tileSize));
        for (let i = 0; i < count; i++) {
          const sp = new Sprite(shieldTex);
          sp.anchor.set(0.5);
          sp.width  = tileSize;
          sp.height = tileSize;
          sp.x = br.centerX - br.width / 2 + tileSize / 2 + i * tileSize;
          sp.y = br.y;
          sp.alpha = 0.85;
          sp.tint = BARRIER_FILL_COLOR;
          this.barriersContainer.addChild(sp);
        }
      }

      this.barriersContainer.addChild(glow);
      this.barriersContainer.addChild(fill);
    }
  }

  // --- zones (Engineer radiation AoE) ---
  private drawZones(zones: Zone[], tick: number): void {
    this.zonesContainer.removeChildren();
    const radiationTex = tex("engineer/spell_raditation/Radiation");
    for (const zn of zones) {
      const fillAlpha = ZONE_FILL_ALPHA_BASE
        + ZONE_FILL_ALPHA_AMP * Math.sin(tick * ZONE_PULSE_SPEED);
      // Additive glow ring.
      const ring = new Graphics();
      ring.blendMode = BLEND_MODES.ADD;
      ring.beginFill(ZONE_GLOW_COLOR, fillAlpha * 1.5)
        .drawCircle(zn.x, zn.y, zn.radius * 1.05)
        .endFill();
      // Inner fill.
      const fill = new Graphics();
      fill.beginFill(ZONE_FILL_COLOR, fillAlpha)
        .drawCircle(zn.x, zn.y, zn.radius)
        .endFill();
      // Border ring.
      const border = new Graphics();
      border.blendMode = BLEND_MODES.ADD;
      border.lineStyle(2, ZONE_GLOW_COLOR, ZONE_RING_ALPHA)
        .drawCircle(zn.x, zn.y, zn.radius);

      this.zonesContainer.addChild(ring);
      this.zonesContainer.addChild(fill);
      this.zonesContainer.addChild(border);

      // Overlay radiation art in center if available.
      if (radiationTex !== Texture.WHITE) {
        const sp = new Sprite(radiationTex);
        sp.anchor.set(0.5);
        const iconSize = zn.radius * 0.6;
        sp.width  = iconSize * 2;
        sp.height = iconSize * 2;
        sp.x = zn.x;
        sp.y = zn.y;
        sp.alpha = 0.7 + 0.15 * Math.sin(tick * ZONE_PULSE_SPEED * 1.3);
        sp.tint = ZONE_FILL_COLOR;
        this.zonesContainer.addChild(sp);
      }
    }
  }

  // --- skeleton summon (Necromancer) ---
  private updateSkeleton(skeletonActive: boolean, cellSize: number, boardW: number, boardH: number): void {
    // SkeletalMage is a single static frame; Skeleton2BirthAnimation is an animated strip.
    const skeletonFrames = animFrames("necromancer/spell_skeleton/SkeletalMage");
    const skFrameArr = skeletonFrames.length > 0
      ? skeletonFrames
      : [tex("necromancer/spell_skeleton/SkeletalMage")].filter(t => t !== Texture.WHITE);

    if (skeletonActive) {
      const skX = boardW / 2;
      const skY = boardH * SKELETON_Y_FRAC + cellSize;

      if (this._skeletonAuraId === undefined) {
        // Spawn looping skeleton aura display.
        if (skFrameArr.length > 0) {
          const h = this.skeletonAnim.looping(
            skFrameArr, 12,
            skX, skY,
            cellSize * 2.8,
            false, 0xaaaaff,
          );
          this._skeletonAuraId = h.id;
        }
        // Glow circle behind the skeleton sprite.
        if (!this._skeletonGlow) {
          const skGlow = new Graphics();
          skGlow.blendMode = BLEND_MODES.ADD;
          skGlow.beginFill(0x8888ff, 0.35)
            .drawCircle(skX, skY, cellSize * 1.8)
            .endFill();
          this.skeletonAnim.container.addChildAt(skGlow, 0);
          this._skeletonGlow = skGlow;
        }
      } else {
        // Update position of existing looping handle.
        this.skeletonAnim.moveTo({ id: this._skeletonAuraId }, skX, skY);
      }
    } else {
      // Skeleton no longer active — remove.
      if (this._skeletonAuraId !== undefined) {
        this.skeletonAnim.remove({ id: this._skeletonAuraId });
        this._skeletonAuraId = undefined;
      }
      if (this._skeletonGlow) {
        this._skeletonGlow.parent?.removeChild(this._skeletonGlow);
        this._skeletonGlow = null;
      }
    }
  }
}

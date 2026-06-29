import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";

// Hazard (falling/rolling enemy projectile) rendering. Owns its own display layer +
// index-keyed pool; extracted from Renderer to keep that file focused.
//
// Every hazard carries a `kind` tag from the sim and maps to its original art:
//   hellball / beholdermissile / heavenmissile → missile sprites
//   stalactite / cart                          → falling/rolling props
//   witchmagic                                 → WitchMagic1-4 cycle
//   bat                                        → harmless flyaway bat
//   "" (untagged)                              → crimson dot fallback
const HAZARD_RADIUS           = 6;        // px in world space
const HAZARD_COLOR            = 0xdd1111; // crimson
const HAZARD_GLOW_COLOR       = 0xff3333; // additive glow
const HAZARD_GLOW_ALPHA       = 0.45;
const HAZARD_GLOW_RADIUS_MULT = 1.9;

interface HazardDto { x: number; y: number; kind?: string; warming?: boolean }
type Entry = { halo: Graphics; core: Graphics; bat: Sprite; stal: Sprite; magic: Sprite; missile: Sprite };

// The Witch boss casts four distinct magic bolts — cycle the sprites by hazard index.
const WITCH_MAGIC_FRAMES = [
  "village/enemies/WitchMagic1", "village/enemies/WitchMagic2",
  "village/enemies/WitchMagic3", "village/enemies/WitchMagic4",
];
const MAGIC_SIZE_MULT = 4;   // ×HAZARD_RADIUS
const MAGIC_SPIN_SPEED = 0.1; // rad/tick

// Enemy missile art per sim kind (original sprites the legacy game used).
const MISSILE_FRAMES: Record<string, string> = {
  hellball:        "hell/HellBallMissile",
  beholdermissile: "village/enemies/BeholderMissile",
  heavenmissile:   "heaven/Missile",
  witchgrab:       "village/enemies/WitchHand2", // the Witch's grab-hand chasing the ball
};
const MISSILE_SIZE_MULT = 3;     // ×HAZARD_RADIUS
const BAT_FLUTTER_SPEED = 0.25;  // wing-flap wobble (scale.y oscillation)
const BAT_FLUTTER_AMP   = 0.18;

export class HazardLayer {
  readonly container = new Container();
  private pool: Entry[] = [];

  /** Render the current hazards. `tick` drives spin/flutter animation. */
  update(hazards: HazardDto[], tick: number): void {
    // Grow the pool to cover the live hazard count.
    while (this.pool.length < hazards.length) {
      const halo = new Graphics();
      halo.blendMode = BLEND_MODES.ADD;
      const core = new Graphics();
      const bat = new Sprite(Texture.WHITE);
      bat.anchor.set(0.5);
      bat.visible = false;
      const stal = new Sprite(tex("Stalactite"));
      stal.anchor.set(0.5);
      stal.visible = false;
      const magic = new Sprite(Texture.WHITE);
      magic.anchor.set(0.5);
      magic.visible = false;
      const missile = new Sprite(Texture.WHITE);
      missile.anchor.set(0.5);
      missile.visible = false;
      this.container.addChild(halo, core, bat, stal, magic, missile);
      this.pool.push({ halo, core, bat, stal, magic, missile });
    }

    for (let i = 0; i < this.pool.length; i++) {
      const entry = this.pool[i];
      const { halo, core, bat, stal, magic, missile } = entry;
      if (i >= hazards.length) {
        halo.visible = core.visible = bat.visible = stal.visible = magic.visible = missile.visible = false;
        continue;
      }
      const hz = hazards[i];
      halo.visible = core.visible = bat.visible = stal.visible = magic.visible = missile.visible = false;

      if (hz.kind === "witchmagic") {
        const frame = atlasTex(WITCH_MAGIC_FRAMES[i % WITCH_MAGIC_FRAMES.length]);
        if (frame !== Texture.WHITE) {
          magic.visible = true;
          magic.texture = frame;
          const ms = HAZARD_RADIUS * MAGIC_SIZE_MULT;
          magic.width = ms; magic.height = ms;
          magic.x = hz.x; magic.y = hz.y;
          magic.rotation = tick * MAGIC_SPIN_SPEED + i;
          continue;
        }
      } else if (hz.kind === "stalactite" || hz.kind === "cart") {
        stal.visible = true;
        stal.alpha = 1; stal.tint = 0xffffff;
        if (hz.kind === "cart") {
          stal.texture = tex("DungeonCart");
          const cs = HAZARD_RADIUS * 4;
          stal.width = cs * 1.4; stal.height = cs;
        } else {
          stal.texture = tex("Stalactite");
          const ss = HAZARD_RADIUS * 3.2;
          stal.width = ss; stal.height = ss * 1.6;
        }
        stal.x = hz.x; stal.y = hz.y;
        // Telegraph: a warming hazard (e.g. the cart before it rolls) pulses red with a warning halo.
        if (hz.warming) {
          const p = 0.5 + 0.5 * Math.sin(tick * 0.4);
          stal.alpha = 0.4 + 0.5 * p; stal.tint = 0xff6a3a;
          halo.visible = true;
          halo.clear().beginFill(0xff3a1a, 0.22 + 0.26 * p)
            .drawCircle(hz.x, hz.y, HAZARD_RADIUS * 4).endFill();
        }
        continue;
      } else if (hz.kind === "bat") {
        const frame = atlasTex("village/enemies/BatFlyAnimation");
        if (frame !== Texture.WHITE) {
          bat.visible = true;
          bat.texture = frame;
          const batSize = HAZARD_RADIUS * 7;
          bat.width = batSize;
          // Wing-flap flutter: oscillate the height instead of spinning the sprite.
          bat.height = batSize * (1 + BAT_FLUTTER_AMP * Math.sin(tick * BAT_FLUTTER_SPEED + i));
          bat.x = hz.x; bat.y = hz.y;
          continue;
        }
      } else if (hz.kind && MISSILE_FRAMES[hz.kind]) {
        const frame = atlasTex(MISSILE_FRAMES[hz.kind]);
        if (frame !== Texture.WHITE) {
          missile.visible = true;
          missile.texture = frame;
          const ms = HAZARD_RADIUS * MISSILE_SIZE_MULT;
          // Preserve the art's aspect ratio (missiles are taller than wide).
          const aspect = frame.height / Math.max(frame.width, 1);
          missile.width = ms; missile.height = ms * aspect;
          missile.x = hz.x; missile.y = hz.y;
          continue;
        }
      }

      // Fallback for untagged hazards (or missing art): crimson glow dot.
      halo.visible = core.visible = true;
      halo.clear().beginFill(HAZARD_GLOW_COLOR, HAZARD_GLOW_ALPHA)
        .drawCircle(hz.x, hz.y, HAZARD_RADIUS * HAZARD_GLOW_RADIUS_MULT).endFill();
      core.clear().beginFill(HAZARD_COLOR, 1)
        .drawCircle(hz.x, hz.y, HAZARD_RADIUS).endFill();
    }
  }
}

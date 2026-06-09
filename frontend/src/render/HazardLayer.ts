import { Container, Graphics, Sprite, Texture, BLEND_MODES } from "pixi.js";
import { tex } from "./textures";
import { tex as atlasTex } from "./assets";

// Hazard (falling/rolling enemy projectile) rendering. Owns its own display layer +
// index-keyed pool; extracted from Renderer to keep that file focused.
const HAZARD_RADIUS           = 6;        // px in world space
const HAZARD_COLOR            = 0xdd1111; // crimson
const HAZARD_GLOW_COLOR       = 0xff3333; // additive glow
const HAZARD_GLOW_ALPHA       = 0.45;
const HAZARD_GLOW_RADIUS_MULT = 1.9;

interface HazardDto { x: number; y: number; kind?: string }
type Entry = { halo: Graphics; core: Graphics; bat: Sprite; stal: Sprite; magic: Sprite };

// The Witch boss casts four distinct magic bolts — cycle the sprites by hazard index.
const WITCH_MAGIC_FRAMES = [
  "village/enemies/WitchMagic1", "village/enemies/WitchMagic2",
  "village/enemies/WitchMagic3", "village/enemies/WitchMagic4",
];
const MAGIC_SIZE_MULT = 4;   // ×HAZARD_RADIUS
const MAGIC_SPIN_SPEED = 0.1; // rad/tick

export class HazardLayer {
  readonly container = new Container();
  private pool: Entry[] = [];

  /** Render the current hazards. `tick` drives the bat flutter; `biome` gates the bat skin. */
  update(hazards: HazardDto[], tick: number, biome: string): void {
    const batTex = atlasTex("village/enemies/BatFlyAnimation");

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
      this.container.addChild(halo, core, bat, stal, magic);
      this.pool.push({ halo, core, bat, stal, magic });
    }

    const showBats = (biome === "village" || biome === "village-boss") && batTex !== Texture.WHITE;

    for (let i = 0; i < this.pool.length; i++) {
      const { halo, core, bat, stal, magic } = this.pool[i];
      if (i >= hazards.length) {
        halo.visible = core.visible = bat.visible = stal.visible = magic.visible = false;
        continue;
      }
      const hz = hazards[i];

      if (hz.kind === "witchmagic") {
        halo.visible = core.visible = bat.visible = stal.visible = false;
        const frame = atlasTex(WITCH_MAGIC_FRAMES[i % WITCH_MAGIC_FRAMES.length]);
        magic.visible = frame !== Texture.WHITE;
        magic.texture = frame;
        const ms = HAZARD_RADIUS * MAGIC_SIZE_MULT;
        magic.width = ms; magic.height = ms;
        magic.x = hz.x; magic.y = hz.y;
        magic.rotation = tick * MAGIC_SPIN_SPEED + i;
        // Fallback to the crimson core if the magic art is missing.
        if (!magic.visible) {
          core.visible = true;
          core.clear().beginFill(HAZARD_COLOR, 1).drawCircle(hz.x, hz.y, HAZARD_RADIUS).endFill();
        }
        continue;
      }

      magic.visible = false;
      if (hz.kind === "stalactite" || hz.kind === "cart") {
        halo.visible = core.visible = bat.visible = false;
        stal.visible = true;
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
      } else if (showBats) {
        stal.visible = halo.visible = core.visible = false;
        bat.texture = batTex;
        bat.visible = true;
        const batSize = HAZARD_RADIUS * 3.5;
        bat.width = batSize * 2; bat.height = batSize * 2;
        bat.x = hz.x; bat.y = hz.y;
        bat.tint = 0x9988ff;
        bat.rotation = tick * 0.08 + i * 0.5;
      } else {
        bat.visible = stal.visible = false;
        halo.visible = core.visible = true;
        halo.clear().beginFill(HAZARD_GLOW_COLOR, HAZARD_GLOW_ALPHA)
          .drawCircle(hz.x, hz.y, HAZARD_RADIUS * HAZARD_GLOW_RADIUS_MULT).endFill();
        core.clear().beginFill(HAZARD_COLOR, 1)
          .drawCircle(hz.x, hz.y, HAZARD_RADIUS).endFill();
      }
    }
  }
}

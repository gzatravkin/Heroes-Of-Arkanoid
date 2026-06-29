import { Texture } from "pixi.js";
import { tex as atlasTex } from "../../render/assets";
import type { SpellDef } from "../../net/metaApi";

// Spell-icon resolution, extracted from Hud.ts. Resolves a spell's icon through
// a fallback chain: packed atlas frame → legacy /art/ PNG → the spell's initial
// letter (no emojis — the user prefers real art, which usually resolves).

/** Render the spell's icon into `wrap`, trying atlas → legacy art → letter fallback. */
export function buildSpellIcon(wrap: HTMLElement, spell: SpellDef): void {
  const iconKey = spell.icon;
  if (!iconKey) {
    wrap.textContent = getSpellFallback(spell.id);
    return;
  }

  // Try atlas tex (for full atlas paths like "paladin/spell_passiveshield/SpellShieldLargeIco").
  // atlasTex returns Texture.WHITE for unknown keys; check width > 1 to detect valid.
  const atlasFrame = atlasTex(iconKey);
  // NOTE: atlasTex returns the 16×16 Texture.WHITE for unknown keys (e.g. the Fire Mage
  // short keys like "FireBallIco"), which would otherwise draw a blank white square.
  // Exclude WHITE so those fall through to the real /art/ icons below.
  if (atlasFrame && atlasFrame !== Texture.WHITE && atlasFrame.width > 1) {
    // Build an img from the atlas texture using its source image + UV, via a canvas.
    // Render at 2× the slot (64) for crispness, then CSS downscales to 32.
    const SZ = 64;
    const canvas = document.createElement("canvas");
    canvas.width  = SZ;
    canvas.height = SZ;
    const ctx = canvas.getContext("2d");
    if (ctx && (atlasFrame as any).baseTexture?.resource?.source) {
      const src = (atlasFrame as any).baseTexture.resource.source as HTMLImageElement | HTMLCanvasElement;
      const fr = (atlasFrame as any).frame;
      if (fr) {
        // Center-crop the frame to a square. The themed spell icons ("…LargeIco") are WIDE
        // banners (e.g. 597×248) with the art centered; drawing the whole frame into the square
        // slot squashed them into muddy rectangles (docs/13). A centered square crop reads clean.
        const side = Math.min(fr.width, fr.height);
        const sx = fr.x + (fr.width  - side) / 2;
        const sy = fr.y + (fr.height - side) / 2;
        ctx.drawImage(src, sx, sy, side, side, 0, 0, SZ, SZ);
        const img = document.createElement("img");
        img.src = canvas.toDataURL();
        img.alt = spell.name;
        img.style.cssText = "width:32px;height:32px;object-fit:contain;";
        wrap.appendChild(img);
        return;
      }
    }
  }

  // Legacy /art/ path fallback. These are the square Chose*Ico icons copied from
  // the Sprites tree — the old /art/*Ico.png files were letterboxed wide crops
  // that rendered as muddy rectangles in the hotbar (docs/13 battle audit).
  const legacyPaths: Record<string, string> = {
    FireHeroBall:  "/art/SpellIgnite.png",
    FireBallIco:   "/art/SpellFireball.png",
    FireWallIco:   "/art/SpellFirewall.png",
    FireTurretIco: "/art/SpellTurret.png",
  };
  const legacySrc = legacyPaths[iconKey];
  if (legacySrc) {
    const img = document.createElement("img");
    img.src = legacySrc;
    img.alt = spell.name;
    img.style.cssText = "width:32px;height:32px;object-fit:contain;";
    const emoji = getSpellFallback(spell.id);
    img.onerror = () => { img.style.display = "none"; wrap.textContent = emoji; };
    wrap.appendChild(img);
    return;
  }

  // Full atlas key with no exposed frame: fall through to the letter fallback.
  wrap.textContent = getSpellFallback(spell.id);
}

// Non-emoji last-resort fallback when a spell icon can't be resolved: the spell's
// initial letter (the user dislikes emojis; real art is preferred and usually resolves).
export function getSpellFallback(id: string): string {
  return (id[0] ?? "?").toUpperCase();
}

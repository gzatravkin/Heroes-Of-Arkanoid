// HUD value-bar factories, extracted from Hud.ts. Pure DOM builders — no class
// state — for the 3-slice HP / balls / mana / boss bars.
//
// The bar sprites (BattleHPEmpty/MPEmpty) are 220×41 with dark angular end-caps
// baked into the sides. Rendering them as a single stretched image distorts the
// caps; instead we pin the caps to the ends via CSS border-image 9-slice and
// stretch only the middle, with the value fill clipped strictly between the caps.

const BAR_SPRITE_H = 41;   // native sprite height (px)
const BAR_CAP_X    = 16;   // native left/right cap thickness (px)
const BAR_CAP_Y    = 7;    // native top/bottom cap thickness (px)
export const BAR_H      = 22;  // rendered height of value bars (mana/HP/balls) — raised to 22 so caps don't look crushed
export const BOSS_BAR_H = 18;  // rendered height of the boss bar

function el(tag: string, className?: string): HTMLElement {
  const e = document.createElement(tag);
  if (className) e.className = className;
  return e;
}

/**
 * Build a symmetric 3-slice value bar: the empty sprite supplies the frame via
 * border-image (caps pinned to both ends, middle stretched), and a gradient fill
 * is clipped strictly between the caps so it grows left→right without ever
 * touching the caps. `fill.style.width` stays a plain percentage string.
 */
export function buildBar(opts: {
  id: string; fillId: string; width: string; height: number;
  emptySrc: string; gradient: string;
}): { outer: HTMLElement; fill: HTMLElement } {
  const capX = Math.round(BAR_CAP_X * opts.height / BAR_SPRITE_H);
  const capY = Math.round(BAR_CAP_Y * opts.height / BAR_SPRITE_H);

  const outer = el("div");
  outer.id = opts.id;
  outer.style.cssText = `position:relative;width:${opts.width};height:${opts.height}px;`;

  // Frame: empty bar via 9-slice border-image — caps fixed, middle stretched, `fill` draws the interior.
  const track = el("div");
  track.style.cssText = [
    "position:absolute", "inset:0", "box-sizing:border-box",
    "border-style:solid",
    `border-width:${capY}px ${capX}px`,
    `border-image:url('${opts.emptySrc}') ${BAR_CAP_Y} ${BAR_CAP_X} ${BAR_CAP_Y} ${BAR_CAP_X} fill stretch`,
  ].join(";");
  outer.appendChild(track);

  // Fill clip: the interior region strictly between the caps.
  const clip = el("div");
  clip.style.cssText = [
    "position:absolute",
    `left:${capX}px`, `right:${capX}px`, `top:${capY}px`, `bottom:${capY}px`,
    "overflow:hidden", "border-radius:2px",
  ].join(";");
  const fill = el("div");
  fill.id = opts.fillId;
  fill.style.cssText = [
    "position:absolute", "left:0", "top:0", "bottom:0", "width:100%",
    `background:${opts.gradient}`,
    "transition:width var(--dur-normal) linear",
  ].join(";");
  clip.appendChild(fill);
  outer.appendChild(clip);

  return { outer, fill };
}

/** A labelled value bar (icon + count overlay) for the top-left HP / spare-balls. */
export function buildLabelledBar(opts: {
  id: string; fillId: string; labelId: string;
  emptySrc: string; gradient: string; icon: string;
}): { outer: HTMLElement; fill: HTMLElement; label: HTMLElement } {
  const { outer, fill } = buildBar({
    id: opts.id, fillId: opts.fillId,
    width: "118px", height: BAR_H,
    emptySrc: opts.emptySrc, gradient: opts.gradient,
  });
  const label = el("span");
  label.id = opts.labelId;
  label.style.cssText = [
    "position:absolute", "top:50%", "left:8px",
    "transform:translateY(-50%)",
    "display:flex", "align-items:center", "gap:4px",
    "font-size:11px", "color:#fff", "font-weight:700",
    "text-shadow:0 0 4px #000,0 1px 2px #000", "pointer-events:none", "white-space:nowrap",
  ].join(";");
  label.innerHTML =
    `<img src="${opts.icon}" alt="" style="width:13px;height:13px;object-fit:contain;image-rendering:pixelated;">` +
    `<span class="hud-bar-count">0</span>`;
  outer.appendChild(label);
  return { outer, fill, label };
}

export function buildBossBar(): { outer: HTMLElement; fill: HTMLElement; name: HTMLElement } {
  const outer = el("div");
  outer.id = "hud-boss-hp";
  outer.style.cssText = [
    "display:none",
    "position:absolute",
    "top:8px", "left:50%",
    "transform:translateX(-50%)",
    "flex-direction:column",
    "align-items:center",
    "gap:3px",
    "pointer-events:none",
    "z-index:20",
    "min-width:min(260px,72cqw)",
  ].join(";");

  const name = el("div");
  name.id = "hud-boss-name";
  name.style.cssText = [
    "font-size:10px", "font-weight:900",
    "color:#ff6644", "letter-spacing:2px",
    "text-shadow:0 0 6px #ff3300,0 1px 3px #000",
    "text-align:center", "white-space:nowrap",
  ].join(";");
  outer.appendChild(name);

  const { outer: bar, fill } = buildBar({
    id: "hud-boss-bar", fillId: "hud-boss-hp-fill",
    width: "100%", height: BOSS_BAR_H,
    emptySrc: "/ui/BattleHPEmpty.png",
    gradient: "linear-gradient(to right,#880000,#cc2222)",
  });
  outer.appendChild(bar);

  return { outer, fill, name };
}

export function buildManaBar(): HTMLElement {
  const { outer } = buildBar({
    id: "hud-mana", fillId: "hud-mana-fill",
    width: "min(220px,80cqw)", height: BAR_H,
    emptySrc: "/ui/BattleMPEmpty.png",
    gradient: "linear-gradient(to right,#1f9fb8,#5fe6f5)",
  });

  const label = el("span");
  label.id = "hud-mana-text";
  label.style.cssText = [
    "position:absolute", "top:50%", "left:50%",
    "transform:translate(-50%,-50%)",
    "font-size:10px", "color:#fff", "font-weight:600",
    "text-shadow:0 1px 2px #000", "pointer-events:none", "white-space:nowrap", "z-index:1",
  ].join(";");
  outer.appendChild(label);

  return outer;
}

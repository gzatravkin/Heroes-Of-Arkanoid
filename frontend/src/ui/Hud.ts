import type { Snapshot } from "../net/Connection";

// ---------------------------------------------------------------------------
// Spell cost constants (mirrored from backend; used for affordability dimming).
// Ignite is free (costs 0 mana); Fireball costs 20.
// ---------------------------------------------------------------------------
const SPELL_COSTS: Record<string, number> = {
  ignite: 0,
  fireball: 20,
};

interface SpellDef {
  id: string;
  key: string;
  label: string;
  icon: string | null; // path relative to / (public/art/...) or null for emoji
  emoji: string;       // fallback when icon fails to load
}

const SPELLS: SpellDef[] = [
  { id: "ignite",   key: "Q", label: "Ignite",   icon: "/art/FireHeroBall.png", emoji: "🔥" },
  { id: "fireball", key: "E", label: "Fireball", icon: "/art/FireBallIco.png",  emoji: "💥" },
];

// ---------------------------------------------------------------------------
// Hud — a DOM overlay mounted on top of the Pixi canvas.
// pointer-events:none so mouse/touch events pass through to the canvas.
// ---------------------------------------------------------------------------
export class Hud {
  private root: HTMLElement;
  private livesEl: HTMLElement;
  private ballsEl: HTMLElement;
  private manaOuter: HTMLElement;
  private manaFill: HTMLElement;
  private manaText: HTMLElement;
  private spellSlots: Map<string, HTMLElement> = new Map();
  private banner: HTMLElement;

  constructor(host: HTMLElement) {
    this.root = this.createElement("div", "hud-root");
    this.root.style.cssText = [
      "position:absolute", "inset:0", "pointer-events:none",
      "font-family:'Segoe UI',system-ui,sans-serif", "z-index:10",
      "user-select:none",
    ].join(";");

    this.injectStyles();

    // ---- top-left panel: lives + balls ----
    const topLeft = this.createElement("div", "hud-top-left");
    topLeft.style.cssText = "position:absolute;top:10px;left:12px;display:flex;flex-direction:column;gap:6px;";

    this.livesEl = this.createElement("div");
    this.livesEl.id = "hud-lives";
    this.livesEl.dataset.lives = "0";
    topLeft.appendChild(this.livesEl);

    this.ballsEl = this.createElement("div");
    this.ballsEl.id = "hud-balls";
    this.ballsEl.dataset.balls = "0";
    topLeft.appendChild(this.ballsEl);

    // ---- top-right panel: relics placeholder ----
    const topRight = this.createElement("div", "hud-relics");
    topRight.id = "hud-relics";
    topRight.style.cssText = "position:absolute;top:10px;right:12px;min-width:40px;min-height:20px;";

    // ---- bottom-center: mana bar + spell hotbar ----
    const bottomCenter = this.createElement("div", "hud-bottom");
    bottomCenter.style.cssText = [
      "position:absolute", "bottom:12px", "left:50%", "transform:translateX(-50%)",
      "display:flex", "flex-direction:column", "align-items:center", "gap:6px",
    ].join(";");

    this.manaOuter = this.buildManaBar();
    this.manaFill  = this.manaOuter.querySelector("#hud-mana-fill")!;
    this.manaText  = this.manaOuter.querySelector("#hud-mana-text")!;

    const hotbar = this.buildHotbar();

    bottomCenter.appendChild(this.manaOuter);
    bottomCenter.appendChild(hotbar);

    // ---- banner (center) ----
    this.banner = this.createElement("div", "hud-banner");
    this.banner.id = "hud-banner";
    this.banner.style.cssText = [
      "position:absolute", "top:50%", "left:50%",
      "transform:translate(-50%,-50%)",
      "display:none", "padding:18px 48px", "border-radius:8px",
      "font-size:48px", "font-weight:900", "letter-spacing:4px",
      "text-shadow:0 0 20px currentColor",
    ].join(";");

    this.root.appendChild(topLeft);
    this.root.appendChild(topRight);
    this.root.appendChild(bottomCenter);
    this.root.appendChild(this.banner);

    host.style.position = "relative";
    host.appendChild(this.root);
  }

  // -----------------------------------------------------------------------
  update(s: Snapshot) {
    // -- lives --
    const lives = s.lives ?? 0;
    this.livesEl.dataset.lives = String(lives);
    this.livesEl.innerHTML = this.renderIconRow(lives, "/art/HPFull.png", "❤️", "Lives");

    // -- spare balls --
    const balls = s.spareBalls ?? 0;
    this.ballsEl.dataset.balls = String(balls);
    this.ballsEl.innerHTML = this.renderIconRow(balls, "/art/LifeBall.png", "⚪", "Spare balls");

    // -- mana bar --
    const mana    = s.mana    ?? 0;
    const manaMax = s.manaMax ?? 1;
    const pct     = Math.min(1, Math.max(0, mana / manaMax)) * 100;
    this.manaFill.style.width = `${pct}%`;
    this.manaText.textContent = `${Math.round(mana)} / ${Math.round(manaMax)}`;

    // -- spell affordability --
    for (const spell of SPELLS) {
      const el = this.spellSlots.get(spell.id);
      if (!el) continue;
      const cost = SPELL_COSTS[spell.id] ?? 0;
      const canAfford = mana >= cost;
      el.classList.toggle("affordable",   canAfford);
      el.classList.toggle("unaffordable", !canAfford);
    }

    // -- banner --
    if (s.phase === "Won") {
      this.banner.style.display = "block";
      this.banner.className = "hud-banner win";
      this.banner.textContent = "VICTORY";
    } else if (s.phase === "Lost") {
      this.banner.style.display = "block";
      this.banner.className = "hud-banner lose";
      this.banner.textContent = "DEFEAT";
    } else {
      this.banner.style.display = "none";
      this.banner.className = "hud-banner";
    }
  }

  // -----------------------------------------------------------------------
  // Helpers
  // -----------------------------------------------------------------------

  private buildManaBar(): HTMLElement {
    const outer = this.createElement("div");
    outer.id = "hud-mana";
    outer.style.cssText = [
      "width:220px", "background:rgba(0,0,0,0.55)", "border-radius:6px",
      "border:1px solid rgba(80,120,255,0.4)", "padding:3px", "position:relative",
    ].join(";");

    const track = this.createElement("div");
    track.style.cssText = [
      "width:100%", "height:14px", "background:rgba(20,20,50,0.8)",
      "border-radius:4px", "overflow:hidden", "position:relative",
    ].join(";");

    const fill = this.createElement("div");
    fill.id = "hud-mana-fill";
    fill.style.cssText = [
      "height:100%", "width:100%", "border-radius:4px", "transition:width 0.1s linear",
      "background:linear-gradient(90deg,#3355ff 0%,#88aaff 100%)",
    ].join(";");

    const label = this.createElement("span");
    label.id = "hud-mana-text";
    label.style.cssText = [
      "position:absolute", "top:50%", "left:50%",
      "transform:translate(-50%,-50%)",
      "font-size:10px", "color:#dde", "font-weight:600",
      "text-shadow:0 0 4px #000", "pointer-events:none", "white-space:nowrap",
    ].join(";");

    track.appendChild(fill);
    track.appendChild(label);
    outer.appendChild(track);
    return outer;
  }

  private buildHotbar(): HTMLElement {
    const bar = this.createElement("div");
    bar.style.cssText = "display:flex;gap:8px;";

    for (const spell of SPELLS) {
      const slot = this.createElement("div");
      slot.id = `hud-spell-${spell.id}`;
      // start affordable (ignite is free, fireball we'll assess on first update)
      slot.className = "hud-spell-slot affordable";

      // key badge
      const keyBadge = this.createElement("div", "hud-spell-key");
      keyBadge.textContent = spell.key;

      // icon area
      const iconWrap = this.createElement("div", "hud-spell-icon");
      if (spell.icon) {
        const img = document.createElement("img");
        img.src = spell.icon;
        img.alt = spell.label;
        img.style.cssText = "width:32px;height:32px;object-fit:contain;image-rendering:pixelated;";
        img.onerror = () => { img.style.display = "none"; iconWrap.textContent = spell.emoji; };
        iconWrap.appendChild(img);
      } else {
        iconWrap.textContent = spell.emoji;
      }

      // name
      const name = this.createElement("div", "hud-spell-name");
      name.textContent = spell.label;

      slot.appendChild(keyBadge);
      slot.appendChild(iconWrap);
      slot.appendChild(name);

      this.spellSlots.set(spell.id, slot);
      bar.appendChild(slot);
    }

    return bar;
  }

  /** Render a row of icon images (or emoji fallback) with a count. */
  private renderIconRow(count: number, iconSrc: string, emoji: string, label: string): string {
    const iconHtml = `<img
      src="${iconSrc}" alt="${label}"
      style="width:16px;height:16px;object-fit:contain;image-rendering:pixelated;vertical-align:middle;"
      onerror="this.style.display='none';this.insertAdjacentText('afterend','${emoji}')"
    >`;
    const MAX_ICONS = 5;
    const shown = Math.min(count, MAX_ICONS);
    const icons = Array.from({ length: shown }, () => iconHtml).join(" ");
    const overflow = count > MAX_ICONS ? `<span style="font-size:11px;color:#aaa;"> +${count - MAX_ICONS}</span>` : "";
    return `<span style="display:inline-flex;align-items:center;gap:2px;">${icons}${overflow}</span>`;
  }

  private createElement(tag: string, className?: string): HTMLElement {
    const el = document.createElement(tag);
    if (className) el.className = className;
    return el;
  }

  private injectStyles() {
    const id = "hud-styles";
    if (document.getElementById(id)) return;
    const style = document.createElement("style");
    style.id = id;
    style.textContent = `
      .hud-top-left > div {
        background: rgba(0,0,0,0.55);
        border: 1px solid rgba(255,255,255,0.1);
        border-radius: 6px;
        padding: 4px 8px;
        color: #eee;
        font-size: 13px;
        display: inline-flex;
        align-items: center;
        gap: 4px;
        min-width: 70px;
      }
      .hud-spell-slot {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 3px;
        background: rgba(0,0,0,0.6);
        border: 1px solid rgba(255,120,40,0.35);
        border-radius: 7px;
        padding: 5px 8px;
        min-width: 56px;
        transition: opacity 0.15s, border-color 0.15s;
      }
      .hud-spell-slot.affordable {
        opacity: 1;
        border-color: rgba(255,130,50,0.7);
      }
      .hud-spell-slot.unaffordable {
        opacity: 0.4;
        border-color: rgba(100,100,100,0.4);
      }
      .hud-spell-key {
        font-size: 10px;
        font-weight: 700;
        color: #ffcc66;
        line-height: 1;
      }
      .hud-spell-icon {
        font-size: 20px;
        line-height: 1;
        display: flex;
        align-items: center;
        justify-content: center;
        width: 32px;
        height: 32px;
      }
      .hud-spell-name {
        font-size: 9px;
        color: #ccc;
        text-align: center;
        line-height: 1;
      }
      .hud-banner.win {
        background: rgba(10,40,10,0.85);
        border: 2px solid #44ff88;
        color: #44ff88;
      }
      .hud-banner.lose {
        background: rgba(40,5,5,0.85);
        border: 2px solid #ff3333;
        color: #ff3333;
      }
    `;
    document.head.appendChild(style);
  }
}

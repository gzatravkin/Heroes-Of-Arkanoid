import type { Connection } from "../net/Connection";
import type { Snapshot } from "../net/Connection";
import { inferBossType, bossLabel } from "../render/Boss";

// ---------------------------------------------------------------------------
// Spell cost constants (mirrored from backend; used for affordability dimming).
// Ignite is free (costs 0 mana); Fireball costs 20.
// ---------------------------------------------------------------------------
const SPELL_COSTS: Record<string, number> = {
  ignite: 0,
  fireball: 20,
  firewall: 30,
  turret: 25,
};

interface SpellDef {
  id: string;
  key: string;
  label: string;
  icon: string | null; // path relative to / (public/art/...) or null for emoji
  emoji: string;       // fallback when icon fails to load
  cast: (conn: Connection) => void;
}

const SPELLS: SpellDef[] = [
  { id: "ignite",   key: "Q", label: "Ignite",   icon: "/art/FireHeroBall.png",  emoji: "🔥", cast: (c) => c.castIgnite()   },
  { id: "fireball", key: "E", label: "Fireball", icon: "/art/FireBallIco.png",   emoji: "💥", cast: (c) => c.castFireball() },
  { id: "firewall", key: "W", label: "Fire Wall", icon: "/art/FireWallIco.png",  emoji: "🧱", cast: (c) => c.castFireWall() },
  { id: "turret",   key: "R", label: "Turret",   icon: "/art/FireTurretIco.png", emoji: "🔫", cast: (c) => c.castTurret()   },
];

// ---------------------------------------------------------------------------
// Hud — a DOM overlay mounted on top of the Pixi canvas.
// Spell slots are tappable buttons (pointer-events:auto) that cast the spell.
// Other elements are pointer-events:none so mouse/touch pass through to canvas.
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
  private relicsEl: HTMLElement;
  // Boss HP bar elements.
  private bossBarEl: HTMLElement;
  private bossBarFill: HTMLElement;
  private bossNameEl: HTMLElement;

  // Latest snapshot mana, for affordability check on tap.
  private _mana = 0;

  constructor(host: HTMLElement) {
    this.root = this.createElement("div", "hud-root");
    this.root.style.cssText = [
      "position:absolute", "inset:0", "pointer-events:none",
      "font-family:'Segoe UI',system-ui,sans-serif", "z-index:10",
      "user-select:none",
      // Safe-area insets for notched phones.
      "padding:env(safe-area-inset-top,0px) env(safe-area-inset-right,0px) env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px)",
    ].join(";");

    this.injectStyles();

    // ---- top-left panel: lives + balls ----
    // Framed with the HP/life bar arts from Battle Interface
    const topLeft = this.createElement("div", "hud-top-left");
    topLeft.style.cssText = "position:absolute;top:8px;left:8px;display:flex;flex-direction:column;gap:5px;";

    this.livesEl = this.createElement("div");
    this.livesEl.id = "hud-lives";
    this.livesEl.dataset.lives = "0";
    this.livesEl.className = "hud-stat-row";
    topLeft.appendChild(this.livesEl);

    this.ballsEl = this.createElement("div");
    this.ballsEl.id = "hud-balls";
    this.ballsEl.dataset.balls = "0";
    this.ballsEl.className = "hud-stat-row";
    topLeft.appendChild(this.ballsEl);

    // ---- top-right panel: relics row ----
    const topRight = this.createElement("div", "hud-relics");
    topRight.id = "hud-relics";
    topRight.style.cssText = [
      "position:absolute", "top:8px", "right:8px",
      "display:flex", "flex-direction:row", "gap:5px",
      "align-items:center", "min-width:40px", "min-height:20px",
    ].join(";");
    this.relicsEl = topRight;

    // ---- bottom thumb zone: mana bar + spell hotbar ----
    const bottomCenter = this.createElement("div", "hud-bottom");
    bottomCenter.style.cssText = [
      "position:absolute", "bottom:0", "left:0", "right:0",
      "display:flex", "flex-direction:column", "align-items:center", "gap:4px",
      "padding-bottom:max(12px,env(safe-area-inset-bottom,12px))",
      "padding-top:6px",
      "background:linear-gradient(to top,rgba(4,4,12,0.80) 0%,transparent 100%)",
      "pointer-events:none",
    ].join(";");

    this.manaOuter = this.buildManaBar();
    this.manaFill  = this.manaOuter.querySelector("#hud-mana-fill")!;
    this.manaText  = this.manaOuter.querySelector("#hud-mana-text")!;

    const hotbar = this.buildHotbar();

    bottomCenter.appendChild(this.manaOuter);
    bottomCenter.appendChild(hotbar);

    // ---- boss HP bar (top center, only visible when bossActive) ----
    const bossBar = this.buildBossBar();
    this.bossBarEl   = bossBar.outer;
    this.bossBarFill = bossBar.fill;
    this.bossNameEl  = bossBar.name;

    // ---- banner (center) ----
    this.banner = this.createElement("div", "hud-banner");
    this.banner.id = "hud-banner";
    this.banner.style.cssText = [
      "position:absolute", "top:50%", "left:50%",
      "transform:translate(-50%,-50%)",
      "display:none", "padding:18px 48px", "border-radius:8px",
      "font-size:clamp(28px,8vw,48px)", "font-weight:900", "letter-spacing:4px",
      "text-shadow:0 0 20px currentColor",
    ].join(";");

    this.root.appendChild(topLeft);
    this.root.appendChild(topRight);
    this.root.appendChild(this.bossBarEl);
    this.root.appendChild(bottomCenter);
    this.root.appendChild(this.banner);

    host.style.position = "relative";
    host.appendChild(this.root);
  }

  // -----------------------------------------------------------------------
  wireConn(conn: Connection) {
    for (const spell of SPELLS) {
      const el = this.spellSlots.get(spell.id);
      if (!el) continue;
      el.addEventListener("pointerdown", (e) => {
        e.stopPropagation();
        const cost = SPELL_COSTS[spell.id] ?? 0;
        if (this._mana >= cost) spell.cast(conn);
      });
    }
  }

  // -----------------------------------------------------------------------
  update(s: Snapshot) {
    this._mana = s.mana ?? 0;

    // -- lives --
    const lives = s.lives ?? 0;
    this.livesEl.dataset.lives = String(lives);
    this.livesEl.innerHTML = this.renderStatRow(lives, "/ui/BattleHPFull.png", "/art/HPFull.png", "❤️", "Lives");

    // -- spare balls --
    const balls = s.spareBalls ?? 0;
    this.ballsEl.dataset.balls = String(balls);
    this.ballsEl.innerHTML = this.renderStatRow(balls, "/ui/BattleLifeBall.png", "/art/LifeBall.png", "⚪", "Spare balls");

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

    // -- relics --
    this.updateRelics(s.activeRelics ?? []);

    // -- boss HP bar --
    if (s.bossActive && s.bossMaxHp > 0) {
      this.bossBarEl.style.display = "flex";
      const hpPct = Math.min(1, Math.max(0, s.bossHp / s.bossMaxHp)) * 100;
      this.bossBarFill.style.width = `${hpPct}%`;
      // Infer boss name from the first boss block's sprite.
      const bossBlock = s.blocks.find(b => b.boss);
      const bossType = bossBlock ? inferBossType(bossBlock.sprite) : "Unknown";
      this.bossNameEl.textContent = bossLabel(bossType);
      // Tint fill bar based on HP level.
      if (hpPct < 33) {
        this.bossBarFill.style.background = "linear-gradient(to right,#cc2200,#ff4422)";
      } else if (hpPct < 66) {
        this.bossBarFill.style.background = "linear-gradient(to right,#cc6600,#ff9933)";
      } else {
        this.bossBarFill.style.background = "linear-gradient(to right,#880000,#cc2222)";
      }
    } else {
      this.bossBarEl.style.display = "none";
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
  private updateRelics(relics: { id: string; name: string; icon: string }[]) {
    const existing = this.relicsEl.querySelectorAll<HTMLElement>("[data-relic-id]");
    const existingIds = Array.from(existing).map(el => el.dataset.relicId!);
    const newIds = relics.map(r => r.id);
    if (existingIds.join(",") === newIds.join(",")) return;

    this.relicsEl.innerHTML = "";
    for (const relic of relics) {
      const tile = this.createElement("div");
      tile.dataset.relicId = relic.id;
      tile.title = relic.name;
      // Use SpellBarActive as frame for relic tiles
      tile.style.cssText = [
        "width:36px", "height:36px",
        "background:url('/ui/BattleSpellBarActive.png') no-repeat center/cover",
        "display:flex", "align-items:center", "justify-content:center",
        "pointer-events:none",
      ].join(";");

      const iconSrc = `/art/${relic.icon}.png`;
      const img = document.createElement("img");
      img.src = iconSrc;
      img.alt = relic.name;
      img.style.cssText = "width:22px;height:22px;object-fit:contain;image-rendering:pixelated;";
      img.onerror = () => { img.style.display = "none"; tile.textContent = "?"; };
      tile.appendChild(img);

      this.relicsEl.appendChild(tile);
    }
  }

  private buildBossBar(): { outer: HTMLElement; fill: HTMLElement; name: HTMLElement } {
    const outer = this.createElement("div");
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
      "min-width:min(260px,72vw)",
    ].join(";");

    // Boss name label.
    const name = this.createElement("div");
    name.id = "hud-boss-name";
    name.style.cssText = [
      "font-size:10px", "font-weight:900",
      "color:#ff6644", "letter-spacing:2px",
      "text-shadow:0 0 6px #ff3300,0 1px 3px #000",
      "text-align:center", "white-space:nowrap",
    ].join(";");
    outer.appendChild(name);

    // Bar container.
    const barWrap = this.createElement("div");
    barWrap.style.cssText = [
      "position:relative",
      "width:100%", "height:14px",
      "background:rgba(0,0,0,0.7)",
      "border:1px solid #aa1111",
      "border-radius:3px",
      "overflow:hidden",
    ].join(";");
    outer.appendChild(barWrap);

    // Fill.
    const fill = this.createElement("div");
    fill.id = "hud-boss-hp-fill";
    fill.style.cssText = [
      "position:absolute", "left:0", "top:0", "bottom:0",
      "width:100%",
      "background:linear-gradient(to right,#880000,#cc2222)",
      "transition:width 0.15s linear",
    ].join(";");
    barWrap.appendChild(fill);

    return { outer, fill, name };
  }

  private buildManaBar(): HTMLElement {
    const outer = this.createElement("div");
    outer.id = "hud-mana";
    // Use MPFull art as a visual frame reference; overlay custom fill on top
    outer.style.cssText = [
      "position:relative",
      "width:min(220px,80vw)",
      "height:20px",
    ].join(";");

    // Background: empty mana bar art
    const bg = this.createElement("div");
    bg.style.cssText = [
      "position:absolute", "inset:0",
      "background:url('/ui/BattleMPEmpty.png') no-repeat center/100% 100%",
    ].join(";");
    outer.appendChild(bg);

    // Fill: full mana bar art, clipped by percentage
    const fill = this.createElement("div");
    fill.id = "hud-mana-fill";
    fill.style.cssText = [
      "position:absolute", "left:0", "top:0", "bottom:0",
      "width:100%",
      "background:url('/ui/BattleMPFull.png') no-repeat left center/auto 100%",
      "transition:width 0.1s linear",
    ].join(";");
    outer.appendChild(fill);

    const label = this.createElement("span");
    label.id = "hud-mana-text";
    label.style.cssText = [
      "position:absolute", "top:50%", "left:50%",
      "transform:translate(-50%,-50%)",
      "font-size:9px", "color:#fff", "font-weight:600",
      "text-shadow:0 0 4px #000", "pointer-events:none", "white-space:nowrap",
    ].join(";");
    outer.appendChild(label);

    return outer;
  }

  private buildHotbar(): HTMLElement {
    const bar = this.createElement("div");
    bar.style.cssText = "display:flex;gap:6px;pointer-events:none;";

    for (const spell of SPELLS) {
      const slot = this.createElement("div");
      slot.id = `hud-spell-${spell.id}`;
      slot.className = "hud-spell-slot affordable";

      // key badge
      const keyBadge = this.createElement("div", "hud-spell-key");
      keyBadge.textContent = spell.key;

      // icon area — use SpellBar art as the slot frame
      const iconWrap = this.createElement("div", "hud-spell-icon");
      if (spell.icon) {
        const img = document.createElement("img");
        img.src = spell.icon;
        img.alt = spell.label;
        img.style.cssText = "width:28px;height:28px;object-fit:contain;image-rendering:pixelated;";
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

  /** Render a row of icon images (or emoji fallback) with a count.
   *  uiSrc is tried first (battle-interface art), artSrc is fallback. */
  private renderStatRow(count: number, uiSrc: string, artSrc: string, emoji: string, label: string): string {
    const iconHtml = `<img
      src="${uiSrc}" alt="${label}"
      style="width:18px;height:18px;object-fit:contain;image-rendering:pixelated;vertical-align:middle;"
      onerror="this.src='${artSrc}';this.onerror=function(){this.style.display='none';this.insertAdjacentText('afterend','${emoji}')}"
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
      /* Lives/balls stat row — framed with HeroBar-style pill */
      .hud-stat-row {
        background: url('/ui/BattleHeroBar.png') no-repeat center/contain,
                    rgba(0,0,0,0.45);
        border-radius: 20px;
        padding: 3px 10px 3px 8px;
        color: #eee;
        font-size: 12px;
        display: inline-flex;
        align-items: center;
        gap: 3px;
        min-width: 60px;
        min-height: 26px;
      }

      .hud-spell-slot {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
        /* Use SpellBar art as the slot frame background */
        background: url('/ui/BattleSpellBar.png') no-repeat center/100% 100%;
        border: none;
        border-radius: 6px;
        padding: 4px 6px 6px 6px;
        /* ≥44px touch target (WCAG 2.5.5) */
        min-width: 52px;
        min-height: 72px;
        touch-action: manipulation;
        cursor: pointer;
        pointer-events: auto;
        transition: opacity 0.15s, filter 0.15s;
        -webkit-tap-highlight-color: transparent;
      }
      .hud-spell-slot.affordable {
        opacity: 1;
        filter: none;
      }
      .hud-spell-slot.affordable:hover {
        filter: brightness(1.2);
      }
      .hud-spell-slot.unaffordable {
        opacity: 0.4;
        filter: grayscale(0.6);
        cursor: default;
      }
      .hud-spell-slot:active {
        transform: scale(0.93);
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
        width: 28px;
        height: 28px;
      }
      .hud-spell-name {
        font-size: 8px;
        color: #e0c880;
        text-align: center;
        line-height: 1;
        text-shadow: 0 1px 2px rgba(0,0,0,0.9);
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
      #hud-relics [data-relic-id] {
        cursor: default;
      }
      /* Landscape orientation: reduce bottom zone height */
      @media (orientation: landscape) and (max-height: 500px) {
        .hud-spell-slot {
          min-width: 44px;
          min-height: 56px;
          padding: 3px 5px 4px 5px;
        }
        .hud-spell-icon { width: 22px; height: 22px; }
        .hud-spell-icon img { width: 22px !important; height: 22px !important; }
      }
    `;
    document.head.appendChild(style);
  }
}

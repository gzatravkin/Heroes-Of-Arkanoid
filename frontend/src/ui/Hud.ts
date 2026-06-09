import type { Connection } from "../net/Connection";
import type { Snapshot } from "../net/Connection";
import type { SpellDef, ItemDef } from "../net/metaApi";
import { inferBossType, bossLabel } from "../render/Boss";
import { tex as atlasTex } from "../render/assets";

// ---------------------------------------------------------------------------
// Spell cost constants (mirrored from backend; used for affordability dimming).
// ---------------------------------------------------------------------------
const SPELL_COSTS: Record<string, number> = {
  ignite:    0,
  fireball:  20,
  firewall:  30,
  turret:    25,
  shield:    20,
  spear:     25,
  duplicate: 30,
  lightning: 20,
  rocket:    30,
  radiation: 35,
  decay:     0,
  skeleton:  30,
  drain:     20,
};

// Key labels by slot index (0→Q, 1→E, 2→W, 3→R).
const SLOT_KEYS = ["Q", "E", "W", "R"];

// Legacy per-spell DOM ids for Fire Mage (required for existing tests).
const FIRE_MAGE_SLOT_IDS: Record<string, string> = {
  ignite:   "hud-spell-ignite",
  fireball: "hud-spell-fireball",
  firewall: "hud-spell-firewall",
  turret:   "hud-spell-turret",
};

// ---------------------------------------------------------------------------
// Hud — a DOM overlay mounted on top of the Pixi canvas.
// ---------------------------------------------------------------------------
export class Hud {
  private root: HTMLElement;
  private livesEl: HTMLElement;
  private ballsEl: HTMLElement;
  private manaOuter: HTMLElement;
  private manaFill: HTMLElement;
  private manaText: HTMLElement;
  private spellSlots: Map<string, HTMLElement> = new Map();
  private hotbarEl: HTMLElement;
  private banner: HTMLElement;
  private relicsEl: HTMLElement;
  // Boss HP bar elements.
  private bossBarEl: HTMLElement;
  private bossBarFill: HTMLElement;
  private bossNameEl: HTMLElement;
  // Active bonus effects indicator row.
  private effectsEl: HTMLElement;

  // Active spells for the current class (populated by loadSpells).
  private _spells: SpellDef[] = [];
  private _conn: Connection | null = null;
  private _itemsRowEl: HTMLElement | null = null;

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

    this.hotbarEl = this.createElement("div");
    this.hotbarEl.id = "hud-hotbar";
    this.hotbarEl.style.cssText = "display:flex;gap:6px;pointer-events:none;";

    bottomCenter.appendChild(this.manaOuter);
    bottomCenter.appendChild(this.hotbarEl);

    // ---- boss HP bar (top center, only visible when bossActive) ----
    const bossBar = this.buildBossBar();
    this.bossBarEl   = bossBar.outer;
    this.bossBarFill = bossBar.fill;
    this.bossNameEl  = bossBar.name;

    // ---- active bonus effects row (top-left, below lives) ----
    this.effectsEl = this.createElement("div", "hud-effects");
    this.effectsEl.id = "hud-effects";
    this.effectsEl.style.cssText = [
      "position:absolute", "top:72px", "left:8px",
      "display:flex", "flex-direction:row", "gap:4px",
      "align-items:center", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this.effectsEl);

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
  /**
   * Call on battle start to load the selected character's spell kit.
   * Rebuilds the hotbar DOM and wires cast handlers if conn is already set.
   */
  loadSpells(spells: SpellDef[]) {
    this._spells = spells;
    this.rebuildHotbar();
    if (this._conn) this.wireConnHandlers(this._conn);
  }

  wireConn(conn: Connection) {
    this._conn = conn;
    // If spells are already loaded (loadSpells was called first), wire immediately.
    // Otherwise wire after loadSpells is called.
    if (this._spells.length > 0) {
      this.wireConnHandlers(conn);
    } else {
      // Fall back: build Fire Mage hotbar so the HUD is usable even if fetch fails.
      this.loadFireMageFallback(conn);
    }
  }

  // -----------------------------------------------------------------------
  /** Show a compact row of equipped item icons in the top-right area (below relics). */
  loadEquippedItems(items: ItemDef[]) {
    if (items.length === 0) return;

    // Create row if not yet present.
    if (!this._itemsRowEl) {
      this._itemsRowEl = this.createElement("div", "hud-items-row");
      this._itemsRowEl.id = "hud-equipped-items";
      this._itemsRowEl.style.cssText = [
        "position:absolute", "top:52px", "right:8px",
        "display:flex", "flex-direction:row", "gap:4px",
        "align-items:center", "pointer-events:none",
      ].join(";");
      this.root.appendChild(this._itemsRowEl);
    }

    this._itemsRowEl.innerHTML = "";
    for (const item of items) {
      const tile = this.createElement("div");
      tile.title = item.name;
      tile.style.cssText = [
        "width:28px", "height:28px",
        "background:rgba(20,14,6,0.75)",
        "border:1px solid rgba(200,150,30,0.5)",
        "border-radius:4px",
        "display:flex", "align-items:center", "justify-content:center",
      ].join(";");

      const tier = item.ownedTier;
      const suffix = tier > 1 ? String(tier) : "";
      const img = document.createElement("img");
      img.src = `/Sprites/Items/${item.icon}${suffix}.png`;
      img.alt = item.name;
      img.style.cssText = "width:22px;height:22px;object-fit:contain;image-rendering:pixelated;";
      img.onerror = () => { img.src = `/Sprites/Items/${item.icon}.png`; img.onerror = null; };
      tile.appendChild(img);
      this._itemsRowEl.appendChild(tile);
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
    for (const spell of this._spells) {
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
      const bossBlock = s.blocks.find(b => b.boss);
      const bossType = bossBlock ? inferBossType(bossBlock.sprite) : "Unknown";
      this.bossNameEl.textContent = bossLabel(bossType);
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

    // -- active bonus effects --
    this.updateEffects(s);

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
  private loadFireMageFallback(_conn: Connection) {
    // Default spell kit matching Fire Mage from characters.json
    const fallback: SpellDef[] = [
      { id: "ignite",   name: "Ignite",    icon: "FireHeroBall" },
      { id: "fireball", name: "Fireball",  icon: "FireBallIco" },
      { id: "firewall", name: "Fire Wall", icon: "FireWallIco" },
      { id: "turret",   name: "Turret",    icon: "FireTurretIco" },
    ];
    this.loadSpells(fallback);
  }

  private rebuildHotbar() {
    // Clear existing slots.
    this.spellSlots.clear();
    this.hotbarEl.innerHTML = "";

    for (let i = 0; i < this._spells.length; i++) {
      const spell = this._spells[i];
      const key = SLOT_KEYS[i] ?? String(i + 1);

      const slot = this.createElement("div");
      // Use legacy id for Fire Mage spells (for test backwards-compatibility).
      slot.id = FIRE_MAGE_SLOT_IDS[spell.id] ?? `hud-spell-${spell.id}`;
      slot.className = "hud-spell-slot affordable";

      // key badge
      const keyBadge = this.createElement("div", "hud-spell-key");
      keyBadge.textContent = key;

      // icon area
      const iconWrap = this.createElement("div", "hud-spell-icon");
      this.buildSpellIcon(iconWrap, spell);

      // name
      const name = this.createElement("div", "hud-spell-name");
      name.textContent = spell.name;

      slot.appendChild(keyBadge);
      slot.appendChild(iconWrap);
      slot.appendChild(name);

      this.spellSlots.set(spell.id, slot);
      this.hotbarEl.appendChild(slot);
    }
  }

  /**
   * Resolve icon for a spell.
   * Priority: atlas frame (long key) → /art/<key>.png legacy path → emoji fallback.
   */
  private buildSpellIcon(wrap: HTMLElement, spell: SpellDef) {
    const iconKey = spell.icon;
    if (!iconKey) {
      wrap.textContent = "✨";
      return;
    }

    // Try atlas tex (for full atlas paths like "paladin/spell_passiveshield/SpellShieldLargeIco").
    // atlasTex returns Texture.WHITE for unknown keys; check width > 1 to detect valid.
    const atlasFrame = atlasTex(iconKey);
    if (atlasFrame && atlasFrame.width > 1) {
      // Build an img from the atlas texture using its source image + UV.
      // Easiest cross-browser way: render to a canvas and use as dataURL.
      // But since we're in DOM, we can use the sprite canvas extraction.
      // Instead, construct a canvas-based icon.
      const canvas = document.createElement("canvas");
      canvas.width  = 28;
      canvas.height = 28;
      const ctx = canvas.getContext("2d");
      if (ctx && (atlasFrame as any).baseTexture?.resource?.source) {
        const src = (atlasFrame as any).baseTexture.resource.source as HTMLImageElement | HTMLCanvasElement;
        const fr = (atlasFrame as any).frame;
        if (fr) {
          ctx.drawImage(src, fr.x, fr.y, fr.width, fr.height, 0, 0, 28, 28);
          const img = document.createElement("img");
          img.src = canvas.toDataURL();
          img.alt = spell.name;
          img.style.cssText = "width:28px;height:28px;object-fit:contain;image-rendering:pixelated;";
          wrap.appendChild(img);
          return;
        }
      }
    }

    // Legacy /art/ path fallback.
    const legacyPaths: Record<string, string> = {
      FireHeroBall:  "/art/FireHeroBall.png",
      FireBallIco:   "/art/FireBallIco.png",
      FireWallIco:   "/art/FireWallIco.png",
      FireTurretIco: "/art/FireTurretIco.png",
    };
    const legacySrc = legacyPaths[iconKey];
    if (legacySrc) {
      const img = document.createElement("img");
      img.src = legacySrc;
      img.alt = spell.name;
      img.style.cssText = "width:28px;height:28px;object-fit:contain;image-rendering:pixelated;";
      const emoji = getSpellEmoji(spell.id);
      img.onerror = () => { img.style.display = "none"; wrap.textContent = emoji; };
      wrap.appendChild(img);
      return;
    }

    // Full atlas key: try /atlas/ path (may work if build pipeline exposes frames).
    // Fall through to emoji.
    wrap.textContent = getSpellEmoji(spell.id);
  }

  private wireConnHandlers(conn: Connection) {
    for (let i = 0; i < this._spells.length; i++) {
      const spell = this._spells[i];
      const el = this.spellSlots.get(spell.id);
      if (!el) continue;
      const slotIndex = i;
      el.addEventListener("pointerdown", (e) => {
        e.stopPropagation();
        const cost = SPELL_COSTS[spell.id] ?? 0;
        if (this._mana >= cost) conn.castSlot(slotIndex);
      });
    }

    // Desktop keyboard bindings: Q→slot0, E→slot1, W→slot2, R→slot3.
    document.addEventListener("keydown", (e) => {
      if (e.repeat) return;
      const keyMap: Record<string, number> = { q: 0, e: 1, w: 2, r: 3 };
      const slotIdx = keyMap[e.key.toLowerCase()];
      if (slotIdx === undefined || slotIdx >= this._spells.length) return;
      const spell = this._spells[slotIdx];
      const cost = SPELL_COSTS[spell.id] ?? 0;
      if (this._mana >= cost) conn.castSlot(slotIdx);
    });
  }

  // -----------------------------------------------------------------------
  private updateEffects(s: Snapshot) {
    const chips: string[] = [];
    if (s.widePaddleActive) chips.push(`↔️ ${Math.ceil(s.widePaddleTimer ?? 0)}s`);
    if (s.slowBallActive)   chips.push(`🐢 ${Math.ceil(s.slowBallTimer ?? 0)}s`);
    const html = chips.map(c =>
      `<span style="background:rgba(0,0,0,0.65);border:1px solid #66aaff;border-radius:4px;padding:1px 5px;font-size:10px;color:#aaddff;">${c}</span>`
    ).join("");
    this.effectsEl.innerHTML = html;
  }

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

    const name = this.createElement("div");
    name.id = "hud-boss-name";
    name.style.cssText = [
      "font-size:10px", "font-weight:900",
      "color:#ff6644", "letter-spacing:2px",
      "text-shadow:0 0 6px #ff3300,0 1px 3px #000",
      "text-align:center", "white-space:nowrap",
    ].join(";");
    outer.appendChild(name);

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
    outer.style.cssText = [
      "position:relative",
      "width:min(220px,80vw)",
      "height:20px",
    ].join(";");

    const bg = this.createElement("div");
    bg.style.cssText = [
      "position:absolute", "inset:0",
      "background:url('/ui/BattleMPEmpty.png') no-repeat center/100% 100%",
    ].join(";");
    outer.appendChild(bg);

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

  /** Render a row of icon images (or emoji fallback) with a count. */
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

function getSpellEmoji(id: string): string {
  const map: Record<string, string> = {
    ignite:    "🔥",
    fireball:  "💥",
    firewall:  "🧱",
    turret:    "🔫",
    shield:    "🛡️",
    spear:     "🗡️",
    duplicate: "✂️",
    lightning: "⚡",
    rocket:    "🚀",
    radiation: "☢️",
    decay:     "💀",
    skeleton:  "🦴",
    drain:     "🩸",
  };
  return map[id] ?? "✨";
}

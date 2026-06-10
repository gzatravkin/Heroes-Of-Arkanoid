import type { Connection } from "../net/Connection";
import type { Snapshot } from "../net/Connection";
import type { SpellDef, ItemDef } from "../net/metaApi";
import { inferBossType, bossLabel } from "../render/Boss";
import { buildLabelledBar, buildManaBar, buildBossBar } from "./hud/bars";
import { HUD_STYLES } from "./hud/hudStyles";
import { buildSpellIcon } from "./hud/spellIcon";

// ---------------------------------------------------------------------------
// Spell cost constants (mirrored from backend; used for affordability dimming).
// ---------------------------------------------------------------------------
// Mirrored from backend SimConfig — update here whenever SimConfig spell costs change.
const SPELL_COSTS: Record<string, number> = {
  ignite:    0,
  fireball:  25,  // bumped 20→25 (P7a balance pass)
  firewall:  35,  // bumped 30→35 (P7a balance pass)
  turret:    25,
  shield:    20,
  spear:     15,
  duplicate: 25,
  lightning: 20,
  rocket:    25,
  radiation: 30,
  decay:     0,
  skeleton:  25,
  drain:     20,
  // G2c kit-completion spells
  phoenix:     30,
  penetration: 20,
  lastday:     35,
  magnet:      20,
  overload:    25,
  golem:       30,
  mage:        25,
};

// Key labels by slot index (0→Q, 1→E, 2→W, 3→R, 4→T).
const SLOT_KEYS = ["Q", "E", "W", "R", "T"];

// The 3-slice HUD value bars (HP / balls / mana / boss) live in ./hud/bars.

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
  private livesFill: HTMLElement;
  private livesCount: HTMLElement;
  private ballsEl: HTMLElement;
  private ballsFill: HTMLElement;
  private ballsCount: HTMLElement;
  // Running maxima — lives/spare-balls have no fixed cap, so the bar is scaled to
  // the largest value seen this battle (start value = full bar).
  private _maxLives = 1;
  private _maxBalls = 1;
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
  // Objective timer (survive/limit) — docs/12 objective flavors.
  private timerEl!: HTMLElement;

  // Active spells for the current class (populated by loadSpells).
  private _spells: SpellDef[] = [];
  private _conn: Connection | null = null;
  private _itemsRowEl: HTMLElement | null = null;
  // Active power-up panel (top-right; task 1.2).
  private _powerupPanelEl: HTMLElement;
  // Combo badge (top-right, below power-ups; task 1.3).
  private _comboBadgeEl: HTMLElement;
  private _prevComboMult = 1;

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
    // Translucent backing strip so bars read over bright biomes (Rulebook §8).
    topLeft.style.cssText = [
      "position:absolute", "top:8px", "left:8px",
      "display:flex", "flex-direction:column", "gap:5px",
      "background:rgba(10,7,5,.55)", "border-radius:8px", "padding:6px 8px",
    ].join(";");

    const livesBar = buildLabelledBar({
      id: "hud-lives", fillId: "hud-lives-fill", labelId: "hud-lives-label",
      emptySrc: "/ui/BattleHPEmpty.png",
      gradient: "linear-gradient(to right,#cc2a2a,#ff5a4a)",
      icon: "/ui/BonusHP.png",
    });
    this.livesEl = livesBar.outer;
    this.livesEl.dataset.lives = "0";
    this.livesFill = livesBar.fill;
    this.livesCount = livesBar.label.querySelector(".hud-bar-count")!;
    topLeft.appendChild(this.livesEl);

    const ballsBar = buildLabelledBar({
      id: "hud-balls", fillId: "hud-balls-fill", labelId: "hud-balls-label",
      emptySrc: "/ui/BattleMPEmpty.png",
      gradient: "linear-gradient(to right,#1f7fc8,#56d6ff)",
      icon: "/ui/BattleLifeBall.png",
    });
    this.ballsEl = ballsBar.outer;
    this.ballsEl.dataset.balls = "0";
    this.ballsFill = ballsBar.fill;
    this.ballsCount = ballsBar.label.querySelector(".hud-bar-count")!;
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

    this.manaOuter = buildManaBar();
    this.manaFill  = this.manaOuter.querySelector("#hud-mana-fill")!;
    this.manaText  = this.manaOuter.querySelector("#hud-mana-text")!;

    this.hotbarEl = this.createElement("div");
    this.hotbarEl.id = "hud-hotbar";
    this.hotbarEl.style.cssText = "display:flex;gap:6px;pointer-events:none;";

    bottomCenter.appendChild(this.manaOuter);
    bottomCenter.appendChild(this.hotbarEl);

    // ---- boss HP bar (top center, only visible when bossActive) ----
    const bossBar = buildBossBar();
    this.bossBarEl   = bossBar.outer;
    this.bossBarFill = bossBar.fill;
    this.bossNameEl  = bossBar.name;

    // ---- objective timer (top center, under the boss bar; docs/12 objectives) ----
    this.timerEl = this.createElement("div", "hud-timer");
    this.timerEl.id = "hud-timer";
    this.timerEl.style.cssText = [
      "position:absolute", "top:44px", "left:50%", "transform:translateX(-50%)",
      "display:none", "padding:2px 14px", "border-radius:10px",
      "font-size:18px", "font-weight:800", "letter-spacing:1px",
      "background:rgba(0,0,0,0.45)", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this.timerEl);

    // ---- active bonus effects row (top-left, below lives) ----
    this.effectsEl = this.createElement("div", "hud-effects");
    this.effectsEl.id = "hud-effects";
    this.effectsEl.style.cssText = [
      "position:absolute", "top:72px", "left:8px",
      "display:flex", "flex-direction:row", "gap:4px",
      "align-items:center", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this.effectsEl);

    // ---- active power-up panel (top-right, below relics/items; task 1.2) ----
    this._powerupPanelEl = this.createElement("div", "hud-powerups");
    this._powerupPanelEl.id = "hud-powerups";
    this._powerupPanelEl.style.cssText = [
      "position:absolute", "top:90px", "right:8px",
      "display:none", "flex-direction:column", "gap:3px",
      "align-items:flex-end", "pointer-events:none",
    ].join(";");
    this.root.appendChild(this._powerupPanelEl);

    // ---- combo multiplier badge (top-right, below power-up panel; task 1.3) ----
    this._comboBadgeEl = this.createElement("div");
    this._comboBadgeEl.id = "hud-combo";
    this._comboBadgeEl.style.cssText = [
      "position:absolute", "top:155px", "right:8px",
      "display:none",
      "color:var(--gold-bright,#ffc84e)",
      "text-shadow:0 0 8px rgba(255,190,80,0.6)",
      "font-family:var(--font-display,'Segoe UI',system-ui,sans-serif)",
      "font-size:18px", "font-weight:bold",
      "background:rgba(0,0,0,0.65)",
      "border:1px solid rgba(255,190,80,0.4)",
      "border-radius:6px", "padding:3px 10px",
      "pointer-events:none",
    ].join(";");
    this.root.appendChild(this._comboBadgeEl);

    // ---- banner (center) ----
    this.banner = this.createElement("div", "hud-banner");
    this.banner.id = "hud-banner";
    this.banner.style.cssText = [
      "position:absolute", "top:50%", "left:50%",
      "transform:translate(-50%,-50%)",
      "display:none", "padding:18px 48px", "border-radius:8px",
      "font-size:clamp(28px,8cqw,48px)", "font-weight:900", "letter-spacing:4px",
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
      img.src = `/items/${item.icon}${suffix}.png`;
      img.alt = item.name;
      img.style.cssText = "width:22px;height:22px;object-fit:contain;image-rendering:pixelated;";
      img.onerror = () => { img.src = `/items/${item.icon}.png`; img.onerror = null; };
      tile.appendChild(img);
      this._itemsRowEl.appendChild(tile);
    }
  }

  // -----------------------------------------------------------------------
  update(s: Snapshot) {
    this._mana = s.mana ?? 0;

    // -- lives (HP bar) --
    const lives = s.lives ?? 0;
    this._maxLives = Math.max(this._maxLives, lives);
    this.livesEl.dataset.lives = String(lives);
    this.livesFill.style.width = `${(lives / this._maxLives) * 100}%`;
    this.livesCount.textContent = String(lives);

    // -- spare balls bar --
    const balls = s.spareBalls ?? 0;
    this._maxBalls = Math.max(this._maxBalls, balls);
    this.ballsEl.dataset.balls = String(balls);
    this.ballsFill.style.width = `${(balls / this._maxBalls) * 100}%`;
    this.ballsCount.textContent = String(balls);

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

    // -- objective timer (survive = gold "hold out", limit = red countdown) --
    if (s.timerMode && (s.timeLeft ?? 0) >= 0 && s.phase === "Playing") {
      this.timerEl.style.display = "block";
      const t = Math.ceil(s.timeLeft ?? 0);
      const mm = Math.floor(t / 60), ss = (t % 60).toString().padStart(2, "0");
      if (s.timerMode === "survive") {
        this.timerEl.style.color = "#ffd700";
        this.timerEl.textContent = `SURVIVE ${mm}:${ss}`;
      } else {
        this.timerEl.style.color = t <= 10 ? "#ff5544" : "#ffffff";
        this.timerEl.textContent = `TIME ${mm}:${ss}`;
      }
    } else if ((s.floorCount ?? 1) > 1 && s.phase === "Playing") {
      // Multi-floor collapse: show progress through the mine shaft.
      this.timerEl.style.display = "block";
      this.timerEl.style.color = "#ccbbaa";
      this.timerEl.textContent = `FLOOR ${s.floor}/${s.floorCount}`;
    } else {
      this.timerEl.style.display = "none";
    }

    // -- active bonus effects --
    this.updateEffects(s);
    // -- active power-up indicators (top-right panel, task 1.2) --
    this.updatePowerups(s);
    // -- combo multiplier badge (task 1.3) --
    this.updateComboBadge(s);

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

      // Kvadrat-framed inner box: key badge (absolute top-left) + icon
      const frame = this.createElement("div", "hud-spell-frame");

      // Keybind letter chip — absolute top-left inside the Kvadrat frame
      const keyBadge = this.createElement("div", "hud-spell-key");
      keyBadge.textContent = key;

      // icon area (fills the inner tile of the frame)
      const iconWrap = this.createElement("div", "hud-spell-icon");
      buildSpellIcon(iconWrap, spell);

      frame.appendChild(keyBadge);
      frame.appendChild(iconWrap);

      // Spell name label — BELOW the frame, no overlap with icon
      const name = this.createElement("div", "hud-spell-name");
      name.textContent = spell.name;

      slot.appendChild(frame);
      slot.appendChild(name);

      this.spellSlots.set(spell.id, slot);
      this.hotbarEl.appendChild(slot);
    }
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
    if (s.widePaddleActive) chips.push(`↔ ${Math.ceil(s.widePaddleTimer ?? 0)}s`);
    if (s.slowBallActive)   chips.push(`slow ${Math.ceil(s.slowBallTimer ?? 0)}s`);
    const html = chips.map(c =>
      `<span style="background:rgba(0,0,0,0.65);border:1px solid #66aaff;border-radius:4px;padding:1px 5px;font-size:10px;color:#aaddff;">${c}</span>`
    ).join("");
    this.effectsEl.innerHTML = html;
  }

  // -----------------------------------------------------------------------
  /** Active power-up indicators — top-right panel showing collected effects (task 1.2). */
  private updatePowerups(s: Snapshot) {
    const active: { label: string; color: string; timer?: number }[] = [];
    if (s.widePaddleActive)      active.push({ label: "W", color: "#d4aa00", timer: s.widePaddleTimer });
    if ((s as any).fireshotActive) active.push({ label: "F", color: "#ff6600", timer: (s as any).fireshotTimer });
    if ((s as any).shieldActive)   active.push({ label: "◆", color: "#00ddee" });

    if (active.length === 0) {
      this._powerupPanelEl.style.display = "none";
      return;
    }
    this._powerupPanelEl.style.display = "flex";
    this._powerupPanelEl.innerHTML = active.map(({ label, color, timer }) => {
      const t = timer !== undefined ? ` ${Math.ceil(timer)}s` : "";
      return `<div class="hud-powerup-active" style="background:rgba(0,0,0,0.70);border:1px solid ${color};border-radius:5px;padding:2px 7px;font-size:11px;font-weight:700;color:${color};letter-spacing:.5px;">${label}${t}</div>`;
    }).join("");
  }

  // -----------------------------------------------------------------------
  /** Combo multiplier badge — shows ×2/×3/×4 with a pop animation on increase (task 1.3). */
  private updateComboBadge(s: Snapshot) {
    const combo = s.comboMultiplier ?? 1;
    if (combo > 1) {
      if (combo !== this._prevComboMult) {
        // Trigger scale-bounce animation by removing and re-adding the class.
        this._comboBadgeEl.classList.remove("combo-pop");
        // Force reflow so the browser registers the removal before re-adding.
        void this._comboBadgeEl.offsetWidth;
        this._comboBadgeEl.classList.add("combo-pop");
      }
      this._comboBadgeEl.style.display = "block";
      this._comboBadgeEl.textContent = `×${combo}`;
    } else {
      this._comboBadgeEl.style.display = "none";
    }
    this._prevComboMult = combo;
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
    style.textContent = HUD_STYLES;
    document.head.appendChild(style);
  }
}

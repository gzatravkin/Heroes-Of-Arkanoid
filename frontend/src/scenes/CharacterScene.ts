import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";

// Hero icon images per backend icon key
const ICON_FILES: Record<string, string> = {
  FireHeroBall:   "/art/FireHeroBall.png",
  HPFull:         "/art/HPFull.png",
  FireTurretIco:  "/art/FireTurretIco.png",
  MPFull:         "/art/MPFull.png",
};

// Per-class ClassChoice banner art + hero icon
const CLASS_ART: Record<string, { banner: string; ico: string }> = {
  fire_mage:    { banner: "/ui/ClassChoiceMage.png",   ico: "/ui/FireHeroIco.png" },
  paladin:      { banner: "/ui/ClassChoiceKnight.png", ico: "/ui/KnightHeroIco.png" },
  engineer:     { banner: "/ui/ClassChoiceTechno.png", ico: "/ui/TechnoHeroIco.png" },
  necromancer:  { banner: "/ui/ClassChoiceMage.png",   ico: "/ui/NecrHeroIco.png" },
  // fallback: use whatever icon the backend gives
};

function iconSrc(key: string): string {
  return ICON_FILES[key] ?? "/art/ItemGem.png";
}

export function mountCharacters(host: HTMLElement) {
  injectCharacterStyles();

  const root = document.createElement("div");
  root.id = "character-scene";
  root.className = "char-root";

  // Background warm gradient layer
  const bg = document.createElement("div");
  bg.className = "char-bg";
  root.appendChild(bg);

  // Inner content (above bg)
  const inner = document.createElement("div");
  inner.className = "char-inner";

  // ── Top bar: back chip · centered title · symmetry spacer ──────────
  const topbar = document.createElement("div");
  topbar.className = "ui-topbar";

  const backBtn = document.createElement("button");
  backBtn.className = "ui-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  backBtn.addEventListener("click", () => { location.href = "/?scene=menu"; });
  topbar.appendChild(backBtn);

  const h1 = document.createElement("h1");
  h1.textContent = "Choose Character";
  h1.className = "ui-title";
  topbar.appendChild(h1);

  const spacer = document.createElement("div");
  spacer.className = "ui-topbar-spacer";
  topbar.appendChild(spacer);

  inner.appendChild(topbar);

  // ── Content area — flex:1 so card list fills the remaining height ───
  const content = document.createElement("div");
  content.className = "char-content";

  const list = document.createElement("div");
  list.id = "character-list";
  list.className = "char-list";
  content.appendChild(list);

  inner.appendChild(content);
  root.appendChild(inner);
  host.appendChild(root);

  // Characters are EARNED through boss clears (docs/04 §3); hints mirror
  // Rewards.CharacterUnlocks on the backend.
  const UNLOCK_HINTS: Record<string, string> = {
    paladin:     "Defeat the Demon Lord in Hell to unlock",
    engineer:    "Defeat the Goblin King in the Caverns to unlock",
    necromancer: "Defeat the Witch in Witchland to unlock",
  };

  async function render() {
    const data = await metaApi.getCharacters();
    const selectable = data.unlocked.length === 0
      ? data.characters.map(c => c.id)
      : data.unlocked;

    list.innerHTML = "";
    list.setAttribute("data-selected", data.selected ?? "");

    for (const char of data.characters) {
      const isSelected   = char.id === data.selected;
      const isSelectable = selectable.includes(char.id);
      const art = CLASS_ART[char.id];

      const card = document.createElement("div");
      card.setAttribute("data-character", char.id);
      card.className = `char-card ${isSelected ? "selected" : ""} ${isSelectable ? "" : "locked"}`;

      // ── ClassChoice banner strip ────────────────────────────────────
      if (art) {
        const banner = document.createElement("div");
        banner.className = "char-banner";
        banner.style.backgroundImage = `url('${art.banner}')`;

        // Round portrait icon (left edge) — painted art, no pixelated rendering
        const ico = document.createElement("img");
        ico.src = art.ico;
        ico.alt = char.name;
        ico.className = "char-hero-ico";
        banner.appendChild(ico);

        // Hero name inside the banner strip
        const bannerName = document.createElement("span");
        bannerName.textContent = char.name;
        bannerName.className = "char-banner-name";
        banner.appendChild(bannerName);

        card.appendChild(banner);
      } else {
        // Fallback: plain portrait + name when no CLASS_ART entry
        const icon = document.createElement("img");
        icon.src = iconSrc(char.icon);
        icon.className = "char-fallback-ico";
        card.appendChild(icon);
        const nameEl = document.createElement("div");
        nameEl.textContent = char.name;
        nameEl.className = "char-banner-name char-banner-name--standalone";
        card.appendChild(nameEl);
      }

      // ── "SELECTED" gold chip shown below the banner when active ────
      if (isSelected) {
        const chip = document.createElement("div");
        chip.className = "char-selected-chip";
        chip.textContent = "SELECTED";
        card.appendChild(chip);
      }

      // ── Passive description / unlock hint ───────────────────────────
      const passiveEl = document.createElement("div");
      passiveEl.textContent = isSelectable
        ? char.passive
        : (UNLOCK_HINTS[char.id] ?? "Locked");
      passiveEl.className = "char-passive";
      card.appendChild(passiveEl);

      // ── Lock badge (bottom-right corner) for not-yet-earned classes ─
      if (!isSelectable) {
        const lockBadge = document.createElement("div");
        lockBadge.className = "char-lock-badge";
        lockBadge.textContent = "🔒"; // 🔒
        card.appendChild(lockBadge);
      }

      if (isSelectable) {
        card.addEventListener("click", async () => {
          if (char.id === data.selected) return;
          await metaApi.selectCharacter(char.id);
          await render();
        });
      }

      list.appendChild(card);
    }
  }

  render().catch(console.error);
}

// ── Styles ──────────────────────────────────────────────────────────────────

function injectCharacterStyles() {
  const id = "character-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = CHARACTER_CSS;
  document.head.appendChild(style);
}

const CHARACTER_CSS = `
  /* ── Screen root: warm gradient, definite height so nested flex:1 works ── */
  .char-root {
    position: relative;
    /* height (not min-height) gives a DEFINITE value so nested flex:1 propagates */
    height: 100cqh;
    overflow-y: auto;   /* safety scroll on very small screens */
    overflow-x: hidden;
    display: flex;
    flex-direction: column;
    font-family: var(--font-body);
    color: var(--text);
    -webkit-font-smoothing: antialiased;
  }
  .char-bg {
    position: absolute;
    inset: 0;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    pointer-events: none;
    z-index: 0;
  }

  /* ── Inner wrapper: takes char-root's definite height via flex:1 ── */
  .char-inner {
    flex: 1;
    min-height: 0; /* prevent auto-height inflation in flex */
    position: relative;
    z-index: 1;
    display: flex;
    flex-direction: column;
  }

  /* .ui-topbar, .ui-back, .ui-title, .ui-topbar-spacer come from theme.ts */

  /* ── Content area: takes remaining height, card list fills it ── */
  .char-content {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 4px 14px max(env(safe-area-inset-bottom, 0px), 14px);
  }
  .char-list {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    gap: 12px;
    width: 100%;
    max-width: 480px;
  }

  /* ── Card: BarGoods gold-rimmed navy 9-slice panel ── */
  .char-card {
    flex: 1;
    min-height: 0;
    ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 15px")}
    padding: 10px 12px 14px;
    display: flex;
    flex-direction: column;
    position: relative;
    cursor: pointer;
    box-sizing: border-box;
    transition: filter var(--dur-normal), transform var(--dur-normal);
    touch-action: manipulation;
    -webkit-tap-highlight-color: transparent;
  }
  /* Non-selected, unlocked: slightly dimmed so selected stands out */
  .char-card:not(.selected):not(.locked) {
    filter: brightness(0.88);
  }
  /* Selected: gold glow */
  .char-card.selected {
    filter: drop-shadow(0 0 10px rgba(255,190,80,0.6));
  }
  /* Hover states */
  .char-card:hover:not(.locked):not(.selected) {
    filter: brightness(1.1);
    transform: scale(1.01);
  }
  .char-card:hover.selected {
    filter: drop-shadow(0 0 16px rgba(255,190,80,0.85));
  }
  /* Active (press) */
  .char-card:active:not(.locked) {
    transform: scale(0.97);
  }
  .char-card:not(.locked):focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
    border-radius: 4px;
  }
  /* Locked: desaturated ~50%, never blacked out (Rulebook §6) */
  .char-card.locked {
    filter: var(--filter-locked);
    cursor: default;
  }

  /* ── ClassChoice banner strip ── */
  .char-banner {
    position: relative;
    width: 100%;
    height: 72px;
    flex-shrink: 0;
    /* Left portion of banner art at natural aspect; right edge gradient fade */
    background-size: auto 100%;
    background-position: left center;
    background-repeat: no-repeat;
    border-radius: 4px;
    display: flex;
    align-items: center;
    margin-bottom: 8px;
    /* Fade right edge: cleaner than hard overflow: hidden clip */
    -webkit-mask-image: linear-gradient(90deg, black 72%, transparent 100%);
    mask-image: linear-gradient(90deg, black 72%, transparent 100%);
  }

  /* Round portrait icon — painted art, never pixelated (Rulebook §4) */
  .char-hero-ico {
    position: absolute;
    left: 2px;
    top: 50%;
    transform: translateY(-50%);
    width: 68px;
    height: 68px;
    object-fit: contain;
    z-index: 2;
    filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7));
  }

  .char-banner-name {
    position: absolute;
    left: 76px;
    right: 28px;
    font-family: var(--font-display);
    font-size: 16px;
    font-weight: 700;
    color: var(--gold-bright);
    text-shadow: 0 1px 3px rgba(0,0,0,0.95), 0 0 10px rgba(200,140,30,0.35);
    letter-spacing: 0.06em;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  /* Standalone name for fallback path (no banner) */
  .char-banner-name--standalone {
    position: static;
    display: block;
    margin: 6px 0 10px;
    font-size: 17px;
  }

  /* ── "SELECTED" gold chip ── */
  .char-selected-chip {
    display: inline-flex;
    align-self: flex-start;
    align-items: center;
    justify-content: center;
    font-family: var(--font-display);
    font-size: 9px;
    font-weight: 900;
    letter-spacing: 0.16em;
    color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    padding: 2px 10px;
    margin-bottom: 6px;
    border: 1px solid var(--gold);
    border-radius: 3px;
    background: rgba(216,168,78,0.18);
    min-height: 20px;
  }

  /* ── Passive description / unlock hint ── */
  .char-passive {
    font-size: 12px;
    color: var(--text-dim);
    line-height: 1.4;
    padding: 0 4px;
    margin-top: auto;
  }

  /* ── Lock badge: bottom-right corner of locked cards ── */
  .char-lock-badge {
    position: absolute;
    bottom: 11px;
    right: 13px;
    font-size: 15px;
    opacity: 0.75;
    line-height: 1;
    pointer-events: none;
  }

  /* ── Fallback portrait (no CLASS_ART entry) ── */
  .char-fallback-ico {
    width: 64px;
    height: 64px;
    object-fit: contain;
    display: block;
    margin: 0 auto 8px;
    filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));
  }

  /* Desktop: letterboxed — slightly larger composition */
  @container (min-width: 480px) {
    .char-list  { max-width: 520px; }
    .char-banner { height: 80px; }
    .char-hero-ico { width: 74px; height: 74px; }
  }
`;

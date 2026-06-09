import { metaApi } from "../net/metaApi";
import { css } from "./battle/overlays";

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
  paladin:      { banner: "/ui/ClassChoiceKnight.png",  ico: "/ui/KnightHeroIco.png" },
  engineer:     { banner: "/ui/ClassChoiceTechno.png",  ico: "/ui/TechnoHeroIco.png" },
  necromancer:  { banner: "/ui/ClassChoiceMage.png",    ico: "/ui/NecrHeroIco.png" },
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

  // Background
  const bg = document.createElement("div");
  bg.className = "char-bg";
  root.appendChild(bg);

  // Inner content
  const inner = document.createElement("div");
  inner.className = "char-inner";

  // Title
  const h1 = document.createElement("h1");
  h1.textContent = "Choose Character";
  h1.className = "char-title";
  inner.appendChild(h1);

  const sub = document.createElement("p");
  sub.textContent = "Your passive ability applies for every level.";
  sub.className = "char-sub";
  inner.appendChild(sub);

  // Character list
  const list = document.createElement("div");
  list.id = "character-list";
  list.className = "char-list";
  inner.appendChild(list);

  // Back link
  const back = document.createElement("a");
  back.href = "/?scene=menu";
  back.textContent = "← Back to Menu";
  back.className = "char-back";
  inner.appendChild(back);

  root.appendChild(inner);
  host.appendChild(root);

  async function render() {
    const data = await metaApi.getCharacters();
    const selectable = data.unlocked.length === 0
      ? data.characters.map(c => c.id)
      : data.unlocked;

    list.innerHTML = "";
    list.setAttribute("data-selected", data.selected ?? "");

    for (const char of data.characters) {
      const isSelected = char.id === data.selected;
      const isSelectable = selectable.includes(char.id);
      const art = CLASS_ART[char.id];

      const card = document.createElement("div");
      card.setAttribute("data-character", char.id);
      if (isSelected) card.classList.add("selected");
      card.className = `char-card ${isSelected ? "selected" : ""} ${isSelectable ? "" : "locked"}`;

      // ClassChoice banner background (the horizontal bar with circle left icon)
      if (art) {
        const banner = document.createElement("div");
        banner.className = "char-banner";
        banner.style.backgroundImage = `url('${art.banner}')`;

        // Hero portrait icon (the round coin/badge portrait on the left of the banner)
        const ico = document.createElement("img");
        ico.src = art.ico;
        ico.alt = char.name;
        ico.className = "char-hero-ico";
        banner.appendChild(ico);

        // Name inside banner
        const bannerName = document.createElement("span");
        bannerName.textContent = char.name;
        bannerName.className = "char-banner-name";
        banner.appendChild(bannerName);

        // Selected gold frame overlay
        if (isSelected) {
          const sel = document.createElement("div");
          sel.className = "char-selected-frame";
          banner.appendChild(sel);
        }

        card.appendChild(banner);
      } else {
        // Fallback: plain layout with backend icon
        const icon = document.createElement("img");
        icon.src = iconSrc(char.icon);
        css(icon, { width: "48px", height: "48px", imageRendering: "pixelated" });
        card.appendChild(icon);
        const nameEl = document.createElement("div");
        nameEl.textContent = char.name;
        css(nameEl, { fontSize: "15px", fontWeight: "700", color: isSelected ? "#cc99ff" : "#aabbff" });
        card.appendChild(nameEl);
      }

      // Passive text
      const passiveEl = document.createElement("div");
      passiveEl.textContent = char.passive;
      passiveEl.className = "char-passive";
      card.appendChild(passiveEl);

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

function injectCharacterStyles() {
  const id = "character-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .char-root {
      position: relative;
      min-height: 100vh;
      overflow-x: hidden;
      font-family: sans-serif;
    }
    .char-bg {
      position: fixed;
      inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(40,20,60,0.6) 0%, transparent 60%),
        linear-gradient(180deg, #100a1a 0%, #080510 50%, #040308 100%);
      z-index: 0;
    }
    .char-inner {
      position: relative;
      z-index: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: max(env(safe-area-inset-top,0px), 24px) 16px max(env(safe-area-inset-bottom,0px), 24px);
      gap: 0;
    }
    .char-title {
      margin: 0 0 6px 0;
      font-size: 1.8rem;
      letter-spacing: 0.08em;
      color: #e8d8b0;
      text-shadow: 0 0 16px rgba(200,150,50,0.5), 0 2px 4px rgba(0,0,0,0.8);
    }
    .char-sub {
      margin: 0 0 24px 0;
      color: #8899cc;
      font-size: 0.9rem;
      letter-spacing: 0.04em;
      text-align: center;
    }
    .char-list {
      display: flex;
      flex-direction: column;
      gap: 14px;
      width: 100%;
      max-width: 520px;
    }
    .char-card {
      position: relative;
      background: rgba(10,8,20,0.8);
      border: 1px solid rgba(100,80,160,0.35);
      border-radius: 8px;
      padding: 10px 12px 12px 12px;
      cursor: pointer;
      transition: transform 0.12s, border-color 0.15s;
      -webkit-tap-highlight-color: transparent;
    }
    .char-card:hover:not(.locked) {
      transform: scale(1.02);
      border-color: rgba(180,140,220,0.7);
    }
    .char-card.selected {
      border-color: rgba(220,190,80,0.9);
      background: rgba(30,20,50,0.9);
      box-shadow: 0 0 16px rgba(220,190,80,0.25);
    }
    .char-card.locked {
      opacity: 0.45;
      cursor: default;
    }

    /* ClassChoice banner — horizontal bar with round portrait on left */
    .char-banner {
      position: relative;
      width: 100%;
      height: 56px;
      background-size: 100% 100%;
      background-repeat: no-repeat;
      border-radius: 4px;
      display: flex;
      align-items: center;
      margin-bottom: 8px;
    }
    .char-hero-ico {
      position: absolute;
      left: -4px;
      top: 50%;
      transform: translateY(-50%);
      width: 64px;
      height: 64px;
      image-rendering: pixelated;
      z-index: 2;
    }
    .char-banner-name {
      position: absolute;
      left: 68px;
      font-size: 16px;
      font-weight: 700;
      color: #e8d8b0;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      letter-spacing: 0.06em;
    }
    .char-selected-frame {
      position: absolute;
      inset: -2px;
      background: url('/ui/SelectedIcon.png') no-repeat center / cover;
      opacity: 0.35;
      border-radius: 4px;
      pointer-events: none;
    }

    .char-passive {
      font-size: 12px;
      color: #8899bb;
      line-height: 1.4;
      padding-left: 4px;
    }

    .char-back {
      margin-top: 28px;
      color: #b8a070;
      font-size: 0.9rem;
      text-decoration: underline;
      cursor: pointer;
    }
  `;
  document.head.appendChild(style);
}

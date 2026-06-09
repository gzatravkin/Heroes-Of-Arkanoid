import { navigateTo } from "../ui/transition";
import { metaApi } from "../net/metaApi";
import type { CampaignNode } from "../net/metaApi";
import { log } from "../log";

// The home screen is ONE journey: a primary "Continue" that resumes the furthest
// playable campaign node, a "Campaign Map" entry into the node graph, and a docked
// bar of secondary destinations. No Play/Dungeons/Editor buttons, no level-chip grid.

const FALLBACK_LEVEL = "hell-1";

interface DockEntry { id: string; label: string; scene: string; icon: string; }

const DOCK: DockEntry[] = [
  { id: "btn-characters",   label: "Heroes",   scene: "characters",   icon: "/ui/InterfaceProfilButtonENG.png" },
  { id: "btn-inventory",    label: "Items",    scene: "inventory",    icon: "/ui/InventoryButton.png" },
  { id: "btn-skills",       label: "Skills",   scene: "skills",       icon: "/ui/InterfaceSkillsButton.png" },
  { id: "btn-achievements", label: "Awards",   scene: "achievements", icon: "/achievements/achievementLvl2Eng.png" },
  { id: "btn-settings",     label: "Settings", scene: "settings",     icon: "/ui/InterfaceNewButton2.png" },
];

/** Furthest *playable* node = the deepest node still unlocked (the campaign frontier). */
function furthestNode(nodes: CampaignNode[]): CampaignNode | null {
  const playable = nodes.filter((n) => n.unlocked);
  if (playable.length) return playable[playable.length - 1];
  return nodes[0] ?? null;
}

export function mountMenu(host: HTMLElement) {
  injectMenuStyles();
  // NOTE: do NOT install a window.__game stub here. Battle pages install the real
  // __game (with getState); a partial stub would make the standard
  // `__game?.getState()` poll throw during the navigation fade. Menu logs are
  // captured via console mirroring (see log.ts) and attached by the test fixture.

  const el = document.createElement("div");
  el.id = "menu";
  el.className = "menu-root";

  const bg = document.createElement("div");
  bg.className = "menu-bg";
  el.appendChild(bg);

  const charArt = document.createElement("div");
  charArt.className = "menu-char-art";
  el.appendChild(charArt);

  const col = document.createElement("div");
  col.className = "menu-col";

  // Screen-reader / test title.
  const h1 = document.createElement("h1");
  h1.textContent = "ARKANOID RPG";
  h1.style.cssText = "position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;";
  col.appendChild(h1);

  const logo = document.createElement("div");
  logo.className = "menu-logo";
  col.appendChild(logo);

  // ── Primary: Continue (resumes the furthest playable node) ──────────────────
  const playBtn = document.createElement("button");
  playBtn.id = "btn-continue";
  playBtn.setAttribute("data-level", FALLBACK_LEVEL); // updated once campaign loads
  playBtn.className = "menu-art-btn menu-btn-continue";
  playBtn.innerHTML = `
    <span class="menu-btn-kicker">Continue</span>
    <span class="menu-btn-node" id="continue-node-label">Hell I</span>`;
  playBtn.addEventListener("click", () => {
    const level = playBtn.getAttribute("data-level") || FALLBACK_LEVEL;
    log("menu", "continue", { level });
    navigateTo(`/?scene=battle&level=${level}&from=campaign`);
  });
  col.appendChild(playBtn);

  // ── Secondary: Campaign Map (the node-graph navigation) ─────────────────────
  const mapBtn = document.createElement("button");
  mapBtn.id = "btn-campaign";
  mapBtn.className = "menu-art-btn menu-btn-map";
  mapBtn.innerHTML = `<span class="menu-btn-label">Campaign Map</span>`;
  mapBtn.addEventListener("click", () => {
    log("menu", "open-map");
    navigateTo("/?scene=campaign");
  });
  col.appendChild(mapBtn);

  // ── Docked secondary destinations (icons along the bottom edge) ─────────────
  const dock = document.createElement("div");
  dock.className = "menu-dock";
  DOCK.forEach((entry) => {
    const btn = document.createElement("button");
    btn.id = entry.id;
    btn.className = "menu-dock-btn";
    btn.setAttribute("aria-label", entry.label);
    btn.innerHTML = `
      <span class="menu-dock-ico" style="background-image:url('${entry.icon}')"></span>
      <span class="menu-dock-label">${entry.label}</span>`;
    btn.addEventListener("click", () => {
      log("menu", "open-scene", { scene: entry.scene });
      navigateTo(`/?scene=${entry.scene}`);
    });
    dock.appendChild(btn);
  });
  col.appendChild(dock);

  el.appendChild(col);
  host.appendChild(el);

  // Resolve the furthest playable node and point Continue at it.
  metaApi.getCampaign()
    .then((camp) => {
      const node = furthestNode(camp.nodes);
      if (!node) return;
      playBtn.setAttribute("data-level", node.id);
      const lbl = document.getElementById("continue-node-label");
      if (lbl) lbl.textContent = node.label;
      log("menu", "furthest-node", { level: node.id, label: node.label });
    })
    .catch((err) => log("menu", "campaign-load-failed", { err: String(err) }));
}

function injectMenuStyles() {
  const id = "menu-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .menu-root {
      position: relative;
      min-height: 100vh;
      width: 100%;
      overflow: hidden;
      display: flex;
      align-items: stretch;
      font-family: sans-serif;
    }

    .menu-bg {
      position: absolute;
      inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, #1a0e06 0%, #0d0808 40%, #050308 100%);
      z-index: 0;
    }

    .menu-char-art {
      position: absolute;
      right: -20px;
      bottom: 0;
      width: min(260px, 60vw);
      height: 70vh;
      background: url('/ui/MainCharacter.png') no-repeat bottom right / contain;
      opacity: 0.16;
      z-index: 1;
      pointer-events: none;
    }

    .menu-col {
      position: relative;
      z-index: 2;
      display: flex;
      flex-direction: column;
      align-items: center;
      width: 100%;
      padding: max(env(safe-area-inset-top, 0px), 28px) 0 env(safe-area-inset-bottom, 16px) 0;
      gap: 0;
    }

    .menu-logo {
      width: min(340px, 88vw);
      height: 80px;
      background: url('/ui/LogoArkanoid.png') no-repeat center / contain;
      margin-bottom: 36px;
    }

    .menu-art-btn {
      position: relative;
      width: min(320px, 88vw);
      border: none;
      background: url('/ui/InterfaceButton.png') no-repeat center / 100% 100%;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      transition: filter 0.15s, transform 0.1s;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .menu-art-btn:hover  { filter: brightness(1.15); }
    .menu-art-btn:active { transform: scale(0.97); filter: brightness(0.9); }

    /* Primary Continue button — largest, two-line (kicker + node name) */
    .menu-btn-continue {
      height: 76px;
      gap: 2px;
      margin-bottom: 14px;
    }
    .menu-btn-kicker {
      color: #ffe9b0;
      font-size: 13px;
      font-weight: 700;
      letter-spacing: 0.18em;
      text-transform: uppercase;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    }
    .menu-btn-node {
      color: #fff6e0;
      font-size: 21px;
      font-weight: 800;
      letter-spacing: 0.04em;
      text-shadow: 0 1px 4px rgba(0,0,0,0.95), 0 0 10px rgba(255,180,60,0.4);
    }

    /* Secondary Campaign Map button */
    .menu-btn-map {
      height: 54px;
    }
    .menu-btn-label {
      color: #f0e0b8;
      font-size: 17px;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9), 0 0 8px rgba(0,0,0,0.6);
      pointer-events: none;
    }

    /* ── Docked secondary destinations ── */
    .menu-dock {
      display: flex;
      justify-content: center;
      gap: 10px;
      width: min(360px, 94vw);
      margin-top: auto;
      padding: 18px 8px calc(env(safe-area-inset-bottom, 0px) + 10px) 8px;
    }
    .menu-dock-btn {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      min-width: 56px;
      min-height: 64px;
      padding: 6px 2px;
      background: url('/ui/Button1.png') no-repeat center / 100% 100%;
      border: none;
      border-radius: 10px;
      cursor: pointer;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter 0.15s, transform 0.1s;
    }
    .menu-dock-btn:hover  { filter: brightness(1.18); }
    .menu-dock-btn:active { transform: scale(0.94); }
    .menu-dock-ico {
      width: 32px;
      height: 32px;
      background-repeat: no-repeat;
      background-position: center;
      background-size: contain;
      image-rendering: pixelated;
    }
    .menu-dock-label {
      color: #d8c598;
      font-size: 10px;
      font-weight: 600;
      letter-spacing: 0.03em;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
  `;
  document.head.appendChild(style);
}

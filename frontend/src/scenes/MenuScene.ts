import { navigateTo } from "../ui/transition";
import { metaApi } from "../net/metaApi";
import { btn1 } from "../ui/nineSlice";
import type { CampaignNode } from "../net/metaApi";
import { log } from "../log";

// The home screen is ONE journey: a primary "Continue" that resumes the furthest
// playable campaign node, a "Campaign Map" entry into the node graph, and a docked
// bar of secondary destinations. No Play/Dungeons/Editor buttons, no level-chip grid.

const FALLBACK_LEVEL = "hell-1";

interface DockEntry { id: string; label: string; scene: string; icon: string; }

const DOCK: DockEntry[] = [
  // Icons must be ICONS — the old Heroes entry was "Profil" word-art (read as
  // "...") and Settings was an empty button pill (read as a dash). docs/13 §S2.
  { id: "btn-characters",   label: "Heroes",   scene: "characters",   icon: "/ui/FireHeroIco.png" },
  { id: "btn-inventory",    label: "Items",    scene: "inventory",    icon: "/ui/InventoryButton.png" },
  { id: "btn-skills",       label: "Skills",   scene: "skills",       icon: "/ui/InterfaceSkillsButton.png" },
  { id: "btn-achievements", label: "Awards",   scene: "achievements", icon: "/achievements/achievementLvl2Eng.png" },
  { id: "btn-settings",     label: "Settings", scene: "settings",     icon: "/ui/SettingsGear.svg" },
];

// Ember particle definitions: left (cqw), delay (s), duration (s), size (px), bottom (cqh).
// 10 particles spread across horizontal range — varied timing to avoid lockstep.
const EMBER_PARTICLES = [
  { left: 10, delay:  0.0, dur:  9, size: 3, bottom: 22 },
  { left: 22, delay:  2.5, dur: 12, size: 4, bottom: 26 },
  { left: 35, delay:  5.0, dur: 10, size: 3, bottom: 20 },
  { left: 48, delay:  1.0, dur: 14, size: 5, bottom: 18 },
  { left: 60, delay:  7.0, dur:  8, size: 3, bottom: 30 },
  { left: 72, delay:  3.5, dur: 11, size: 4, bottom: 24 },
  { left: 83, delay:  9.0, dur: 13, size: 3, bottom: 28 },
  { left: 18, delay:  6.0, dur: 10, size: 4, bottom: 21 },
  { left: 54, delay:  4.0, dur:  9, size: 3, bottom: 25 },
  { left: 90, delay:  8.0, dur: 12, size: 4, bottom: 23 },
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

  // ── Layer 0: deep warm background ──────────────────────────────────────────
  const bg = document.createElement("div");
  bg.className = "menu-bg";
  el.appendChild(bg);

  // ── Layer 1: key-art slot (z-index 1) ──────────────────────────────────────
  // Placeholder behind the column; swap in the hero illustration when art ships.
  const keyart = document.createElement("div");
  keyart.className = "menu-keyart";
  /* future: commissioned hero illustration (docs/13 asset gap #1) */
  el.appendChild(keyart);

  // ── Layer 2a: ember glow — large dim radial behind the CTA block ───────────
  const emberGlow = document.createElement("div");
  emberGlow.className = "menu-ember-glow";
  el.appendChild(emberGlow);

  // ── Layer 2b: ember particles — slowly drifting gold dots ─────────────────
  const particlesWrap = document.createElement("div");
  particlesWrap.className = "menu-particles";
  EMBER_PARTICLES.forEach((p) => {
    const dot = document.createElement("div");
    dot.className = "menu-ember";
    dot.style.cssText =
      `left:${p.left}cqw;bottom:${p.bottom}cqh;` +
      `animation-delay:-${p.delay}s;animation-duration:${p.dur}s;` +
      `width:${p.size}px;height:${p.size}px;`;
    particlesWrap.appendChild(dot);
  });
  el.appendChild(particlesWrap);

  // ── Layer 3: content column ─────────────────────────────────────────────────
  const col = document.createElement("div");
  col.className = "menu-col";

  // Screen-reader / test title.
  const h1 = document.createElement("h1");
  h1.textContent = "ARKANOID RPG";
  h1.style.cssText = "position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;";
  col.appendChild(h1);

  // Logo — anchored near the top (~12% from top via column padding-top).
  const logo = document.createElement("div");
  logo.className = "menu-logo";
  col.appendChild(logo);

  // ── CTA wrapper — vertically centered in remaining space above dock ─────────
  // margin-top: auto; margin-bottom: auto distributes the free space evenly so the
  // block floats in the middle rather than piling up under the logo.
  const ctaWrap = document.createElement("div");
  ctaWrap.className = "menu-cta-wrap";

  // Primary: Continue (resumes the furthest playable node).
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
  ctaWrap.appendChild(playBtn);

  // Secondary: Campaign Map (the node-graph navigation).
  const mapBtn = document.createElement("button");
  mapBtn.id = "btn-campaign";
  mapBtn.className = "menu-art-btn menu-btn-map";
  mapBtn.innerHTML = `<span class="menu-btn-label">Campaign Map</span>`;
  mapBtn.addEventListener("click", () => {
    log("menu", "open-map");
    navigateTo("/?scene=campaign");
  });
  ctaWrap.appendChild(mapBtn);

  col.appendChild(ctaWrap);

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
      min-height: 100cqh;
      width: 100%;
      overflow: hidden;
      display: flex;
      align-items: stretch;
      font-family: var(--font-body);
    }

    /* ── Background ── */
    .menu-bg {
      position: absolute;
      inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      z-index: 0;
    }

    /* ── Key-art slot (z-index 1) — future hero illustration ── */
    .menu-keyart {
      position: absolute;
      inset: 0;
      z-index: 1;
      pointer-events: none;
      /* future: commissioned hero illustration (docs/13 asset gap #1) */
    }

    /* ── Ember glow — large dim radial behind the CTA block (z-index 2) ── */
    .menu-ember-glow {
      position: absolute;
      top: 28cqh;
      left: 50%;
      transform: translateX(-50%);
      width: 92cqw;
      height: 55cqh;
      background: radial-gradient(ellipse at 50% 50%,
        rgba(200, 100, 20, 0.13) 0%,
        rgba(160,  70, 10, 0.06) 45%,
        transparent 72%);
      z-index: 2;
      pointer-events: none;
    }

    /* ── Ember particles container ── */
    .menu-particles {
      position: absolute;
      inset: 0;
      z-index: 2;
      pointer-events: none;
      overflow: hidden;
    }

    /* Individual ember dot — hidden by default; animation applied only when
       motion is acceptable (reduced-motion block below). */
    .menu-ember {
      position: absolute;
      border-radius: 50%;
      background: radial-gradient(circle,
        rgba(255, 190, 60, 0.9)  0%,
        rgba(255, 140, 30, 0.45) 55%,
        transparent              100%);
      filter: blur(1.5px);
      opacity: 0;
    }

    @keyframes ember-rise {
      0%   { transform: translateY(0)       translateX(0);    opacity: 0;    }
      8%   { opacity: 0.30; }
      50%  { transform: translateY(-34cqh)  translateX(7px);  opacity: 0.25; }
      88%  { opacity: 0.10; }
      100% { transform: translateY(-64cqh)  translateX(-6px); opacity: 0;    }
    }

    @media (prefers-reduced-motion: no-preference) {
      .menu-ember {
        animation-name: ember-rise;
        animation-timing-function: linear;
        animation-iteration-count: infinite;
      }
    }

    /* ── Content column ──
       Logo anchored near the top (padding-top ≈ 12cqh).
       CTA wrapper gets margin-top:auto + margin-bottom:auto, which distributes
       the remaining free space evenly above and below it — centering the CTA
       block in the space above the dock. Dock sits flush at the bottom. */
    .menu-col {
      position: relative;
      z-index: 3;
      display: flex;
      flex-direction: column;
      align-items: center;
      width: 100%;
      min-height: 100cqh;
      padding: max(env(safe-area-inset-top, 0px), 12cqh) 0 env(safe-area-inset-bottom, 16px) 0;
    }

    .menu-logo {
      width: min(340px, 88cqw);
      height: 80px;
      background: url('/ui/LogoArkanoid.png') no-repeat center / contain;
      flex-shrink: 0;
    }

    /* CTA wrapper — floats in center of remaining space above the dock */
    .menu-cta-wrap {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 14px;
      margin-top: auto;
      margin-bottom: auto;
      flex-shrink: 0;
    }

    .menu-art-btn {
      position: relative;
      width: min(320px, 88cqw);
      background: none;
      /* 9-slice the InterfaceButton pill (626x162): fixed rounded end-caps + stretched
         middle, so the button doesn't get its ends squished at different widths. */
      border-style: solid;
      border-width: 9px 34px;
      border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .menu-art-btn:hover  { filter: var(--filter-hover); }
    .menu-art-btn:active { transform: scale(0.97); filter: brightness(0.9); }
    .menu-art-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    /* Primary Continue button — largest, two-line (kicker + node name) */
    .menu-btn-continue {
      height: 76px;
      gap: 2px;
    }
    .menu-btn-kicker {
      color: var(--gold-bright);
      font-size: var(--fs-body);
      font-weight: 700;
      letter-spacing: 0.18em;
      text-transform: uppercase;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    }
    .menu-btn-node {
      color: var(--text);
      font-size: var(--fs-xl);
      font-weight: 800;
      letter-spacing: 0.04em;
      text-shadow: 0 1px 4px rgba(0,0,0,0.95), 0 0 10px rgba(255,180,60,0.4);
    }

    /* Secondary Campaign Map button */
    .menu-btn-map {
      height: 54px;
    }
    .menu-btn-label {
      color: var(--text);
      font-size: var(--fs-large);
      font-weight: 700;
      letter-spacing: 0.06em;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9), 0 0 8px rgba(0,0,0,0.6);
      pointer-events: none;
    }

    /* ── Docked secondary destinations ──
       Sits flush at the bottom of the column; ≥18px top-padding gives the dock
       breathing room above the icon row (§6 compliant). */
    .menu-dock {
      display: flex;
      justify-content: center;
      gap: 10px;
      width: min(360px, 94cqw);
      flex-shrink: 0;
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
      ${btn1()}
      border-radius: 10px;
      cursor: pointer;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .menu-dock-btn:hover  { filter: brightness(1.18); }
    .menu-dock-btn:active { transform: scale(0.94); }
    .menu-dock-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .menu-dock-ico {
      width: 32px;
      height: 32px;
      background-repeat: no-repeat;
      background-position: center;
      background-size: contain;
      /* painted art — smooth downscale, never pixelated (docs/13) */
      filter: drop-shadow(0 1px 2px rgba(0,0,0,0.7));
    }
    .menu-dock-label {
      color: var(--text-dim);
      font-size: var(--fs-tiny);
      font-weight: 600;
      letter-spacing: 0.03em;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
  `;
  document.head.appendChild(style);
}

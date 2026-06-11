import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";
import type { DungeonRunState } from "../net/metaApi";
import { css, buffName, buffIcon } from "./battle/overlays";

/** Capitalise first letter; replace dashes with spaces for raw level ids. */
function levelLabel(id: string): string {
  return id.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function mountDungeon(host: HTMLElement) {
  injectDungeonRunStyles();

  const root = document.createElement("div");
  root.id = "dungeon";
  root.className = "dngrun-root";
  host.appendChild(root);

  // Persistent topbar: back chip (top-left, ≥44px) · title · symmetry spacer
  const topbar = document.createElement("div");
  topbar.className = "dngrun-topbar";

  const backChip = document.createElement("a");
  backChip.href = "/?scene=campaign";
  backChip.className = "ui-back";
  backChip.setAttribute("aria-label", "Back to campaign");
  topbar.appendChild(backChip);

  const titleEl = document.createElement("h1");
  titleEl.id = "dngrun-title";
  titleEl.textContent = "Dungeon";
  titleEl.className = "ui-title";
  topbar.appendChild(titleEl);

  const topSpacer = document.createElement("div");
  topSpacer.className = "ui-topbar-spacer";
  topbar.appendChild(topSpacer);

  root.appendChild(topbar);

  async function load() {
    let state: DungeonRunState = {};
    try {
      state = await metaApi.getDungeonState();
    } catch {
      renderError("Failed to load dungeon state.");
      return;
    }

    if (state.cleared) {
      renderCleared();
    } else if (state.active && state.floors) {
      renderActive(state as Required<Pick<DungeonRunState, "floors" | "floorIndex" | "relics" | "ballCores" | "dungeonId">>);
    } else {
      renderInactive();
    }
  }

  function renderError(msg: string) {
    const el = document.createElement("div");
    el.textContent = msg;
    css(el, { color: "#ff6666", marginTop: "40px" });
    root.appendChild(el);
  }

  function renderInactive() {
    const msg = document.createElement("div");
    msg.textContent = "No active run.";
    css(msg, { marginTop: "40px", fontSize: "1.1rem", color: "#8899cc" });
    root.appendChild(msg);
    // Back affordance is the persistent topbar back chip above.
  }

  function renderCleared() {
    titleEl.textContent = "Dungeon Cleared!";
    const msg = document.createElement("div");
    msg.textContent = "Dungeon Cleared!";
    css(msg, { marginTop: "40px", fontSize: "1.5rem", fontWeight: "700", color: "#55ee88",
               textShadow: "0 0 16px rgba(50,220,100,0.5)" });
    root.appendChild(msg);
    // Back affordance is the persistent topbar back chip above.
  }

  function renderActive(state: { floors: string[]; floorIndex: number; relics: string[]; ballCores: string[]; dungeonId: string }) {
    const { floors, floorIndex, relics, ballCores } = state;

    // Update persistent topbar title (back affordance already present)
    titleEl.textContent = "Active Run";

    // Floor progress
    const progressEl = document.createElement("div");
    progressEl.id = "dungeon-floor-progress";
    progressEl.textContent = `Floor ${floorIndex + 1} / ${floors.length}`;
    progressEl.className = "dngrun-progress";
    root.appendChild(progressEl);

    // Current floor name
    const currentFloor = floors[floorIndex];
    const floorNameEl = document.createElement("div");
    floorNameEl.textContent = levelLabel(currentFloor);
    floorNameEl.className = "dngrun-floor-name";
    root.appendChild(floorNameEl);

    // Collected buffs
    const buffsLabel = document.createElement("div");
    buffsLabel.textContent = "Collected Buffs";
    css(buffsLabel, { fontSize: "13px", color: "#8899cc", marginBottom: "8px", alignSelf: "flex-start" });
    root.appendChild(buffsLabel);

    const buffsRow = document.createElement("div");
    buffsRow.id = "dungeon-buffs";
    buffsRow.className = "dngrun-buffs";
    root.appendChild(buffsRow);

    const allBuffs = [...relics, ...ballCores];
    if (allBuffs.length === 0) {
      const emptyEl = document.createElement("span");
      emptyEl.textContent = "None yet";
      css(emptyEl, { color: "#555577", fontSize: "13px" });
      buffsRow.appendChild(emptyEl);
    } else {
      for (const buffId of allBuffs) {
        const chip = document.createElement("div");
        chip.className = "dngrun-buff-chip";
        const icon = document.createElement("img");
        icon.src = buffIcon(buffId);
        css(icon, { width: "20px", height: "20px", imageRendering: "pixelated" });
        chip.appendChild(icon);
        const nameEl = document.createElement("span");
        nameEl.textContent = buffName(buffId);
        chip.appendChild(nameEl);
        buffsRow.appendChild(chip);
      }
    }

    // Enter Floor button
    const enterBtn = document.createElement("button");
    enterBtn.id = "btn-enter-floor";
    enterBtn.className = "dngrun-enter-btn";
    enterBtn.textContent = "Enter Floor";
    enterBtn.addEventListener("click", () => {
      location.href = `/?scene=battle&level=${encodeURIComponent(currentFloor)}&from=dungeon`;
    });
    root.appendChild(enterBtn);
  }

  load().catch(console.error);
}

function injectDungeonRunStyles() {
  const id = "dungeon-run-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .dngrun-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      align-items: center;
      box-sizing: border-box;
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px max(12px, env(safe-area-inset-bottom, 0px)) 12px;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    }
    /* Persistent topbar: back chip (top-left ≥44px) · centered title · spacer */
    .dngrun-topbar {
      display: flex;
      align-items: center;
      gap: 8px;
      padding-bottom: 8px;
      align-self: stretch;
    }
    .dngrun-topbar .ui-title { flex: 1; text-align: center; }
    .dngrun-progress {
      id: dungeon-floor-progress;
      font-size: 13px;
      color: var(--text-dim);
      margin-bottom: 6px;
      letter-spacing: 0.03em;
    }
    .dngrun-floor-name {
      font-size: 15px;
      font-weight: 700;
      color: var(--gold-bright);
      margin-bottom: 16px;
      text-align: center;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .dngrun-buffs {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 16px;
      min-height: 36px;
      max-width: 480px;
      justify-content: center;
    }
    .dngrun-buff-chip {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "8px 10px")}
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 5px;
      padding: 4px 8px;
      font-size: 12px;
      color: var(--text-dim);
      transition: filter var(--dur-normal);
    }
    .dngrun-buff-chip:hover {
      filter: brightness(1.08);
    }
    .dngrun-enter-btn {
      min-height: 36px;
      padding: 2px 14px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      color: var(--gold-bright);
    }
    .dngrun-enter-btn:hover:not(:disabled)  { filter: brightness(1.18); }
    .dngrun-enter-btn:active:not(:disabled) { transform: scale(0.96); }
    .dngrun-enter-btn:disabled {
      filter: saturate(0.25) brightness(0.6);
      cursor: default;
    }
  `;
  document.head.appendChild(style);
}

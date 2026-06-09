import { metaApi } from "../net/metaApi";
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

    const link = document.createElement("a");
    link.href = "/?scene=campaign";
    link.textContent = "← Back to Campaign";
    link.className = "dngrun-link";
    root.appendChild(link);
  }

  function renderCleared() {
    const msg = document.createElement("div");
    msg.textContent = "Dungeon Cleared!";
    css(msg, { marginTop: "40px", fontSize: "1.5rem", fontWeight: "700", color: "#55ee88",
               textShadow: "0 0 16px rgba(50,220,100,0.5)" });
    root.appendChild(msg);

    const link = document.createElement("a");
    link.href = "/?scene=campaign";
    link.textContent = "← Back to Campaign";
    link.className = "dngrun-link";
    root.appendChild(link);
  }

  function renderActive(state: { floors: string[]; floorIndex: number; relics: string[]; ballCores: string[]; dungeonId: string }) {
    const { floors, floorIndex, relics, ballCores } = state;

    // Back link
    const backBtn = document.createElement("a");
    backBtn.href = "/?scene=campaign";
    backBtn.textContent = "← Campaign";
    backBtn.className = "dngrun-back";
    root.appendChild(backBtn);

    // Title
    const h1 = document.createElement("h1");
    h1.textContent = "Active Run";
    h1.className = "dngrun-title";
    root.appendChild(h1);

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
      min-height: 100vh;
      background:
        url('/ui/2DungeonBlur.jpg') no-repeat center top / cover,
        rgba(5,3,12,0.92);
      background-blend-mode: luminosity;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: max(env(safe-area-inset-top,0px), 24px) 16px max(env(safe-area-inset-bottom,0px), 24px);
      box-sizing: border-box;
      font-family: sans-serif;
      color: #e8e8ff;
    }
    .dngrun-back {
      align-self: flex-start;
      color: #b8a070;
      text-decoration: none;
      font-size: 13px;
      margin-bottom: 20px;
    }
    .dngrun-title {
      margin: 0 0 16px 0;
      font-size: 1.7rem;
      letter-spacing: 0.06em;
      color: #ddeeff;
      text-shadow: 0 0 16px rgba(100,150,255,0.4), 0 2px 4px rgba(0,0,0,0.8);
    }
    .dngrun-progress {
      id: dungeon-floor-progress;
      font-size: 1.1rem;
      color: #88aaff;
      margin-bottom: 6px;
      letter-spacing: 0.05em;
    }
    .dngrun-floor-name {
      font-size: 1.25rem;
      font-weight: 700;
      color: #ddeeff;
      margin-bottom: 20px;
      text-align: center;
    }
    .dngrun-buffs {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 28px;
      min-height: 36px;
      max-width: 480px;
      justify-content: center;
    }
    .dngrun-buff-chip {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: 4px 10px;
      background: rgba(20,15,40,0.85);
      border: 1px solid rgba(100,80,180,0.45);
      border-radius: 20px;
      font-size: 12px;
      color: #aabbff;
    }
    .dngrun-enter-btn {
      padding: 0 48px;
      height: 56px;
      min-width: 200px;
      background: url('/ui/InterfaceButton.png') no-repeat center / 100% 100%;
      color: #f0e0b8;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 17px;
      font-family: sans-serif;
      font-weight: 700;
      letter-spacing: 0.06em;
      transition: filter 0.15s, transform 0.1s;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .dngrun-enter-btn:hover  { filter: brightness(1.15); }
    .dngrun-enter-btn:active { transform: scale(0.97); }
    .dngrun-link {
      margin-top: 16px;
      color: #cc88ff;
      text-decoration: none;
      font-size: 1rem;
    }
  `;
  document.head.appendChild(style);
}

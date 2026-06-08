const API = "http://localhost:5080";

function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

interface DungeonRunState {
  dungeonId?: string;
  floors?: string[];
  floorIndex?: number;
  relics?: string[];
  ballCores?: string[];
  pendingChoices?: string[];
  active?: boolean;
  cleared?: boolean;
}

const RELIC_NAMES: Record<string, string> = {
  glass_cannon: "Glass Cannon",
  flint_core: "Flint Core",
  pyroclasm: "Pyroclasm",
  mana_battery: "Mana Battery",
};

const RELIC_ICONS: Record<string, string> = {
  glass_cannon: "/art/ItemHummer.png",
  flint_core: "/art/ItemDrill.png",
  pyroclasm: "/art/ItemTorch.png",
  mana_battery: "/art/ItemGem.png",
};

const BALL_CORE_NAMES: Record<string, string> = {
  heavy: "Heavy Core",
  split: "Split Core",
  ember: "Ember Core",
};

const BALL_CORE_ICONS: Record<string, string> = {
  heavy: "/art/BonusRock.png",
  split: "/art/BonusSplit.png",
  ember: "/art/BonusFire.png",
};

function buffName(id: string): string {
  return RELIC_NAMES[id] ?? BALL_CORE_NAMES[id] ?? id;
}

function buffIcon(id: string): string {
  return RELIC_ICONS[id] ?? BALL_CORE_ICONS[id] ?? "/art/ItemGem.png";
}

/** Capitalise first letter; replace dashes with spaces for raw level ids. */
function levelLabel(id: string): string {
  return id.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}

export function mountDungeon(host: HTMLElement) {
  const root = document.createElement("div");
  root.id = "dungeon";
  css(root, {
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    minHeight: "100vh",
    background: "#0b0b12",
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    padding: "32px 16px",
    boxSizing: "border-box",
  });
  host.appendChild(root);

  async function load() {
    let state: DungeonRunState = {};
    try {
      const res = await fetch(`${API}/dungeon/state`);
      state = await res.json();
    } catch (e) {
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
    link.href = "/?scene=dungeons";
    link.textContent = "Browse Dungeons →";
    css(link, { marginTop: "16px", color: "#cc88ff", textDecoration: "none", fontSize: "1rem" });
    root.appendChild(link);
  }

  function renderCleared() {
    const msg = document.createElement("div");
    msg.textContent = "Dungeon Cleared!";
    css(msg, { marginTop: "40px", fontSize: "1.5rem", fontWeight: "700", color: "#55ee88" });
    root.appendChild(msg);

    const link = document.createElement("a");
    link.href = "/?scene=dungeons";
    link.textContent = "Browse Dungeons →";
    css(link, { marginTop: "16px", color: "#cc88ff", textDecoration: "none", fontSize: "1rem" });
    root.appendChild(link);
  }

  function renderActive(state: { floors: string[]; floorIndex: number; relics: string[]; ballCores: string[]; dungeonId: string }) {
    const { floors, floorIndex, relics, ballCores } = state;

    // Back link
    const backBtn = document.createElement("a");
    backBtn.href = "/?scene=dungeons";
    backBtn.textContent = "← Dungeons";
    css(backBtn, {
      alignSelf: "flex-start",
      color: "#8899cc",
      textDecoration: "none",
      fontSize: "13px",
      marginBottom: "24px",
    });
    root.appendChild(backBtn);

    // Title
    const h1 = document.createElement("h1");
    h1.textContent = "Active Run";
    css(h1, { margin: "0 0 24px 0", fontSize: "1.8rem", letterSpacing: "0.06em", color: "#ddeeff" });
    root.appendChild(h1);

    // Floor progress
    const progressEl = document.createElement("div");
    progressEl.id = "dungeon-floor-progress";
    progressEl.textContent = `Floor ${floorIndex + 1} / ${floors.length}`;
    css(progressEl, {
      fontSize: "1.1rem",
      color: "#88aaff",
      marginBottom: "8px",
      letterSpacing: "0.05em",
    });
    root.appendChild(progressEl);

    // Current floor name
    const currentFloor = floors[floorIndex];
    const floorNameEl = document.createElement("div");
    floorNameEl.textContent = levelLabel(currentFloor);
    css(floorNameEl, {
      fontSize: "1.3rem",
      fontWeight: "700",
      color: "#ddeeff",
      marginBottom: "24px",
    });
    root.appendChild(floorNameEl);

    // Collected buffs row
    const buffsLabel = document.createElement("div");
    buffsLabel.textContent = "Collected Buffs";
    css(buffsLabel, { fontSize: "13px", color: "#8899cc", marginBottom: "6px", alignSelf: "flex-start" });
    root.appendChild(buffsLabel);

    const buffsRow = document.createElement("div");
    buffsRow.id = "dungeon-buffs";
    css(buffsRow, {
      display: "flex",
      gap: "10px",
      flexWrap: "wrap",
      marginBottom: "32px",
      minHeight: "36px",
    });
    root.appendChild(buffsRow);

    const allBuffs = [...relics, ...ballCores];
    if (allBuffs.length === 0) {
      const emptyEl = document.createElement("span");
      emptyEl.textContent = "None yet";
      css(emptyEl, { color: "#555577", fontSize: "13px", alignSelf: "center" });
      buffsRow.appendChild(emptyEl);
    } else {
      for (const buffId of allBuffs) {
        const chip = document.createElement("div");
        css(chip, {
          display: "flex",
          alignItems: "center",
          gap: "6px",
          padding: "4px 10px",
          background: "#1a1a3a",
          border: "1px solid #334466",
          borderRadius: "20px",
          fontSize: "13px",
          color: "#aabbff",
        });
        const icon = document.createElement("img");
        icon.src = buffIcon(buffId);
        css(icon, { width: "18px", height: "18px", imageRendering: "pixelated" });
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
    enterBtn.textContent = "Enter Floor";
    css(enterBtn, {
      padding: "14px 48px",
      background: "#0e2a4a",
      color: "#66ccff",
      border: "2px solid #2255aa",
      borderRadius: "8px",
      cursor: "pointer",
      fontSize: "17px",
      fontFamily: "sans-serif",
      letterSpacing: "0.06em",
      transition: "background 0.15s, border-color 0.15s",
    });
    enterBtn.addEventListener("mouseenter", () => {
      enterBtn.style.background = "#1a3a6a";
      enterBtn.style.borderColor = "#3377cc";
    });
    enterBtn.addEventListener("mouseleave", () => {
      enterBtn.style.background = "#0e2a4a";
      enterBtn.style.borderColor = "#2255aa";
    });
    enterBtn.addEventListener("click", () => {
      location.href = `/?scene=battle&level=${encodeURIComponent(currentFloor)}&from=dungeon`;
    });
    root.appendChild(enterBtn);
  }

  load().catch(console.error);
}

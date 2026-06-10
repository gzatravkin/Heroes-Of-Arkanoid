import { metaApi } from "../net/metaApi";
import { btnInterface } from "../ui/nineSlice";
import type { DungeonDef } from "../net/metaApi";
import { css } from "./battle/overlays";

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

export function mountDungeons(host: HTMLElement) {
  injectDungeonStyles();

  const root = document.createElement("div");
  root.id = "dungeons";
  root.className = "dng-root";
  host.appendChild(root);

  // Back link
  const backBtn = document.createElement("a");
  backBtn.href = "/";
  backBtn.textContent = "← Menu";
  backBtn.className = "dng-back";
  root.appendChild(backBtn);

  // Flavor banner
  const banner = document.createElement("div");
  banner.className = "dng-flavor";
  banner.textContent = "A rift has opened — descend? Death here is permanent.";
  root.appendChild(banner);

  const h1 = document.createElement("h1");
  h1.textContent = "Dungeons";
  h1.className = "dng-title";
  root.appendChild(h1);

  // Dungeon list
  const list = document.createElement("div");
  list.id = "dungeon-list";
  list.className = "dng-list";
  root.appendChild(list);

  async function load() {
    let dungeons: DungeonDef[] = [];
    try {
      const data = await metaApi.getDungeons();
      dungeons = data.dungeons ?? [];
    } catch {
      const err = document.createElement("div");
      err.textContent = "Failed to load dungeons.";
      css(err, { color: "#ff6666" });
      list.appendChild(err);
      return;
    }

    for (const d of dungeons) {
      const card = document.createElement("div");
      card.setAttribute("data-dungeon", d.id);
      card.className = "dng-card";

      // Title bar — MissionName art
      const titleBar = document.createElement("div");
      titleBar.className = "dng-card-titlebar";
      const nameEl = document.createElement("span");
      nameEl.textContent = d.name;
      nameEl.className = "dng-card-name";
      titleBar.appendChild(nameEl);
      card.appendChild(titleBar);

      // Meta row
      const meta = document.createElement("div");
      meta.className = "dng-card-meta";

      const floorCount = document.createElement("span");
      floorCount.textContent = `${d.floors.length} floors`;
      meta.appendChild(floorCount);

      const rewardRow = document.createElement("div");
      rewardRow.className = "dng-reward-row";
      const rewardIcon = document.createElement("img");
      rewardIcon.src = RELIC_ICONS[d.rewardRelic] ?? "/art/ItemGem.png";
      css(rewardIcon, { width: "20px", height: "20px", imageRendering: "pixelated" });
      rewardRow.appendChild(rewardIcon);
      const rewardText = document.createElement("span");
      rewardText.textContent = `${RELIC_NAMES[d.rewardRelic] ?? d.rewardRelic} + ${d.rewardCrystals} crystals`;
      rewardRow.appendChild(rewardText);
      meta.appendChild(rewardRow);
      card.appendChild(meta);

      // Descend button
      const descBtn = document.createElement("button");
      descBtn.textContent = "Descend";
      descBtn.className = "dng-descend-btn";
      descBtn.addEventListener("click", async () => {
        descBtn.disabled = true;
        descBtn.textContent = "Starting…";
        try {
          await metaApi.startDungeon(d.id);
          location.href = "/?scene=dungeon";
        } catch {
          descBtn.disabled = false;
          descBtn.textContent = "Descend";
        }
      });
      card.appendChild(descBtn);

      list.appendChild(card);
    }
  }

  load().catch(console.error);
}

function injectDungeonStyles() {
  const id = "dungeons-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .dng-root {
      min-height: 100cqh;
      background:
        url('/ui/2DungeonBlur.jpg') no-repeat center top / cover,
        rgba(5,3,12,0.9);
      background-blend-mode: luminosity;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: max(env(safe-area-inset-top,0px), 24px) 16px max(env(safe-area-inset-bottom,0px), 24px);
      box-sizing: border-box;
      font-family: sans-serif;
      color: #e8e8ff;
    }
    .dng-back {
      align-self: flex-start;
      color: #b8a070;
      text-decoration: none;
      font-size: 13px;
      margin-bottom: 20px;
      padding: 4px 8px;
    }
    .dng-flavor {
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(20,8,35,0.9);
      border: 1px solid rgba(180,80,200,0.4);
      border-radius: 10px;
      padding: 12px 24px;
      margin-bottom: 24px;
      max-width: 520px;
      text-align: center;
      color: #cc88ff;
      font-size: 0.92rem;
      letter-spacing: 0.04em;
      line-height: 1.5;
      box-shadow: 0 0 20px rgba(100,0,150,0.25);
    }
    .dng-title {
      margin: 0 0 24px 0;
      font-size: 1.8rem;
      letter-spacing: 0.07em;
      color: #ddeeff;
      text-shadow: 0 0 20px rgba(100,150,255,0.4), 0 2px 4px rgba(0,0,0,0.8);
    }
    .dng-list {
      display: flex;
      flex-direction: column;
      gap: 16px;
      width: 100%;
      max-width: 520px;
    }
    .dng-card {
      background: rgba(12,8,26,0.88);
      border: 1px solid rgba(80,50,150,0.5);
      border-radius: 10px;
      padding: 16px 20px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      transition: border-color 0.15s, box-shadow 0.15s;
      box-shadow: 0 2px 12px rgba(0,0,0,0.5);
    }
    .dng-card:hover {
      border-color: rgba(150,100,220,0.7);
      box-shadow: 0 4px 20px rgba(80,0,120,0.35);
    }
    .dng-card-titlebar {
      background: url('/ui/MissionName.png') no-repeat left center / auto 100%;
      min-height: 36px;
      display: flex;
      align-items: center;
      padding-left: 12px;
    }
    .dng-card-name {
      font-size: 1.15rem;
      font-weight: 700;
      color: #e8d0a0;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    }
    .dng-card-meta {
      display: flex;
      gap: 14px;
      align-items: center;
      font-size: 13px;
      color: #8899cc;
    }
    .dng-reward-row {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .dng-descend-btn {
      align-self: flex-start;
      padding: 0 28px;
      height: 48px;
      ${btnInterface()}
      color: #f0e0b8;
      border-radius: 4px;
      cursor: pointer;
      font-size: 15px;
      font-family: sans-serif;
      font-weight: 700;
      letter-spacing: 0.05em;
      transition: filter 0.15s, transform 0.1s;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .dng-descend-btn:hover  { filter: brightness(1.15); }
    .dng-descend-btn:active { transform: scale(0.97); }
    .dng-descend-btn:disabled { filter: grayscale(1) opacity(0.5); cursor: default; }
  `;
  document.head.appendChild(style);
}

import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";
import { navigateTo } from "../ui/transition";
import type { DungeonDef } from "../net/metaApi";
import { css, buffIcon } from "./battle/overlays";
import { getRelicName } from "../net/relicCache";

export function mountDungeons(host: HTMLElement) {
  injectDungeonStyles();

  const root = document.createElement("div");
  root.id = "dungeons";
  root.className = "dng-root";
  host.appendChild(root);

  // Top bar: back chip (top-left, ≥44px) · centered title · symmetry spacer
  const topbar = document.createElement("div");
  topbar.className = "dng-topbar";

  const backBtn = document.createElement("a");
  backBtn.href = "/?scene=menu";
  backBtn.className = "ui-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  topbar.appendChild(backBtn);

  const h1 = document.createElement("h1");
  h1.textContent = "Dungeons";
  h1.className = "ui-title";
  topbar.appendChild(h1);

  const topSpacer = document.createElement("div");
  topSpacer.className = "ui-topbar-spacer";
  topbar.appendChild(topSpacer);

  root.appendChild(topbar);

  // Flavor banner
  const banner = document.createElement("div");
  banner.className = "dng-flavor";
  banner.textContent = "A rift has opened — descend? Death here is permanent.";
  root.appendChild(banner);

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
      css(err, { color: "var(--danger-light)" });
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
      rewardIcon.src = buffIcon(d.rewardRelic);
      css(rewardIcon, { width: "20px", height: "20px", imageRendering: "pixelated" });
      rewardRow.appendChild(rewardIcon);
      const rewardText = document.createElement("span");
      rewardText.textContent = `${getRelicName(d.rewardRelic)} + ${d.rewardCrystals} crystals`;
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
          navigateTo("/?scene=dungeon");
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
    /* Top bar: back chip (top-left, ≥44px) · centered title · symmetry spacer */
    .dng-topbar {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      padding-bottom: var(--sp-2);
      align-self: stretch;
    }
    .dng-topbar .ui-title { flex: 1; text-align: center; }
    .dng-flavor {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
      padding: var(--sp-3) var(--sp-4);
      margin: var(--sp-2) auto var(--sp-4);
      max-width: 520px;
      text-align: center;
      color: var(--text);
      font-size: var(--fs-body);
      letter-spacing: 0.03em;
      line-height: 1.5;
      transition: filter var(--dur-normal);
    }
    .dng-flavor:hover {
      filter: brightness(1.08);
    }
    .dng-list {
      display: flex;
      flex-direction: column;
      gap: var(--sp-3);
      width: 100%;
      max-width: 520px;
      flex: 1;
    }
    .dng-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
      padding: var(--sp-3) var(--sp-3h);
      display: flex;
      flex-direction: column;
      gap: var(--sp-2);
      transition: filter var(--dur-normal);
    }
    .dng-card:hover {
      filter: brightness(1.08);
    }
    .dng-card-titlebar {
      display: flex;
      align-items: center;
      padding: 0;
    }
    .dng-card-name {
      font-size: var(--fs-body);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .dng-card-meta {
      display: flex;
      flex-direction: column;
      gap: var(--sp-1h);
      align-items: flex-start;
      font-size: var(--fs-caption);
      color: var(--text-dim);
    }
    .dng-reward-row {
      display: flex;
      align-items: center;
      gap: var(--sp-1h);
    }
    .dng-descend-btn {
      min-height: 44px;
      padding: 2px 14px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body);
      font-size: var(--fs-caption);
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
      transition: filter var(--dur-normal), transform var(--dur-fast);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      color: var(--gold-bright);
    }
    .dng-descend-btn:hover:not(:disabled)  { filter: brightness(1.18); }
    .dng-descend-btn:active:not(:disabled) { transform: scale(0.96); }
    .dng-descend-btn:disabled {
      filter: saturate(0.25) brightness(0.6);
      cursor: default;
    }
    .dng-descend-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
  `;
  document.head.appendChild(style);
}

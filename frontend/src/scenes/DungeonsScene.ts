import { metaApi } from "../net/metaApi";
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
  const root = document.createElement("div");
  root.id = "dungeons";
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

  // Back link
  const backBtn = document.createElement("a");
  backBtn.href = "/";
  backBtn.textContent = "← Menu";
  css(backBtn, {
    alignSelf: "flex-start",
    color: "#8899cc",
    textDecoration: "none",
    fontSize: "13px",
    marginBottom: "24px",
    padding: "4px 8px",
  });
  root.appendChild(backBtn);

  // Flavor banner
  const banner = document.createElement("div");
  css(banner, {
    background: "#12091e",
    border: "1px solid #442255",
    borderRadius: "10px",
    padding: "14px 28px",
    marginBottom: "32px",
    maxWidth: "560px",
    textAlign: "center",
    color: "#cc88ff",
    fontSize: "0.95rem",
    letterSpacing: "0.04em",
    lineHeight: "1.5",
  });
  banner.textContent = "A rift has opened — descend? Death here is permanent.";
  root.appendChild(banner);

  const h1 = document.createElement("h1");
  h1.textContent = "Dungeons";
  css(h1, { margin: "0 0 28px 0", fontSize: "2rem", letterSpacing: "0.07em", color: "#ddeeff" });
  root.appendChild(h1);

  // Dungeon list
  const list = document.createElement("div");
  list.id = "dungeon-list";
  css(list, {
    display: "flex",
    flexDirection: "column",
    gap: "18px",
    width: "100%",
    maxWidth: "520px",
  });
  root.appendChild(list);

  async function load() {
    let dungeons: DungeonDef[] = [];
    try {
      const data = await metaApi.getDungeons();
      dungeons = data.dungeons ?? [];
    } catch (e) {
      const err = document.createElement("div");
      err.textContent = "Failed to load dungeons.";
      css(err, { color: "#ff6666" });
      list.appendChild(err);
      return;
    }

    for (const d of dungeons) {
      const card = document.createElement("div");
      card.setAttribute("data-dungeon", d.id);
      css(card, {
        background: "#12122a",
        border: "1px solid #2a2a55",
        borderRadius: "10px",
        padding: "20px 24px",
        display: "flex",
        flexDirection: "column",
        gap: "10px",
        transition: "border-color 0.15s",
      });
      card.addEventListener("mouseenter", () => { card.style.borderColor = "#5544aa"; });
      card.addEventListener("mouseleave", () => { card.style.borderColor = "#2a2a55"; });

      const nameEl = document.createElement("div");
      nameEl.textContent = d.name;
      css(nameEl, { fontSize: "1.25rem", fontWeight: "700", color: "#cc99ff" });
      card.appendChild(nameEl);

      const meta = document.createElement("div");
      css(meta, { display: "flex", gap: "18px", alignItems: "center", fontSize: "13px", color: "#8899cc" });

      const floorCount = document.createElement("span");
      floorCount.textContent = `${d.floors.length} floors`;
      meta.appendChild(floorCount);

      // Reward line
      const rewardRow = document.createElement("div");
      css(rewardRow, { display: "flex", alignItems: "center", gap: "6px" });
      const rewardIcon = document.createElement("img");
      rewardIcon.src = RELIC_ICONS[d.rewardRelic] ?? "/art/ItemGem.png";
      css(rewardIcon, { width: "18px", height: "18px", imageRendering: "pixelated" });
      rewardRow.appendChild(rewardIcon);
      const rewardText = document.createElement("span");
      rewardText.textContent = `${RELIC_NAMES[d.rewardRelic] ?? d.rewardRelic} + ${d.rewardCrystals} crystals`;
      rewardRow.appendChild(rewardText);
      meta.appendChild(rewardRow);

      card.appendChild(meta);

      const descBtn = document.createElement("button");
      descBtn.textContent = "Descend";
      css(descBtn, {
        alignSelf: "flex-start",
        marginTop: "4px",
        padding: "10px 28px",
        background: "#1e0a3a",
        color: "#cc88ff",
        border: "2px solid #553377",
        borderRadius: "7px",
        cursor: "pointer",
        fontSize: "15px",
        fontFamily: "sans-serif",
        letterSpacing: "0.05em",
        transition: "background 0.15s, border-color 0.15s",
      });
      descBtn.addEventListener("mouseenter", () => {
        descBtn.style.background = "#2e1a5a";
        descBtn.style.borderColor = "#7755aa";
      });
      descBtn.addEventListener("mouseleave", () => {
        descBtn.style.background = "#1e0a3a";
        descBtn.style.borderColor = "#553377";
      });
      descBtn.addEventListener("click", async () => {
        descBtn.disabled = true;
        descBtn.textContent = "Starting…";
        try {
          await metaApi.startDungeon(d.id);
          location.href = "/?scene=dungeon";
        } catch (e) {
          descBtn.disabled = false;
          descBtn.textContent = "Descend";
          console.error("Failed to start dungeon", e);
        }
      });
      card.appendChild(descBtn);

      list.appendChild(card);
    }
  }

  load().catch(console.error);
}

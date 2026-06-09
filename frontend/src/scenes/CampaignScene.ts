import { metaApi } from "../net/metaApi";
import type { CampaignNode, Profile } from "../net/metaApi";

const SPELL_NAMES: Record<string, string> = {
  ignite: "Ignite",
  fireball: "Fireball",
  firewall: "Firewall",
  turret: "Turret",
};

const SPELL_ICONS: Record<string, string> = {
  ignite: "/art/FireBallIco.png",
  fireball: "/art/FireBallIco.png",
  firewall: "/art/FireWallIco.png",
  turret: "/art/FireTurretIco.png",
};

// Map level id prefix → node art (unlocked / locked / selected variants in /ui/)
function nodeSrc(id: string, state: "unlocked" | "locked" | "completed"): string {
  const prefix = id.startsWith("hell")    ? "LvlHell"
               : id.startsWith("caverns") ? "LvlCave"
               : id.startsWith("village") ? "LvlVillage"
               : id.startsWith("heaven")  ? "LvlHeaven"
               : null;
  if (!prefix) return "/art/Mission_Standart.png";
  if (state === "locked")    return `/ui/${prefix}Closed.png`;
  if (state === "completed") return `/ui/${prefix}Selected.png`;
  return `/ui/${prefix}.png`;
}

function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

export function mountCampaign(host: HTMLElement) {
  injectCampaignStyles();

  const root = document.createElement("div");
  root.id = "campaign";
  root.className = "camp-root";
  host.appendChild(root);

  // ── Profile bar ──────────────────────────────────────────────────────────
  const profileBar = document.createElement("div");
  profileBar.id = "profile-bar";
  profileBar.className = "camp-profile-bar";

  const levelEl = document.createElement("span");
  levelEl.id = "profile-level";
  levelEl.className = "camp-profile-level";

  const expBar = document.createElement("div");
  expBar.className = "camp-exp-wrap";
  const expLabel = document.createElement("span");
  expLabel.id = "profile-exp";
  expLabel.className = "camp-exp-label";
  const expBarOuter = document.createElement("div");
  expBarOuter.className = "camp-exp-outer";
  // background = empty bar art, fill = full bar art via clip
  expBarOuter.style.backgroundImage = "url('/ui/ExpBarEmptyMainMenu.png')";
  const expBarFill = document.createElement("div");
  expBarFill.id = "profile-exp-fill";
  expBarFill.className = "camp-exp-fill";
  expBarFill.style.backgroundImage = "url('/ui/ExpBarFullMainMenu.png')";
  expBarOuter.appendChild(expBarFill);
  expBar.appendChild(expLabel);
  expBar.appendChild(expBarOuter);

  const pointsEl = document.createElement("span");
  pointsEl.id = "profile-points";
  pointsEl.className = "camp-profile-points";

  const crystalsEl = document.createElement("span");
  crystalsEl.id = "profile-crystals";
  crystalsEl.className = "camp-profile-crystals";
  const gemImg = document.createElement("img");
  gemImg.src = "/ui/Gem.png";
  gemImg.alt = "Crystals";
  css(gemImg, { width: "18px", height: "18px", imageRendering: "pixelated" });
  crystalsEl.appendChild(gemImg);
  const crystalsText = document.createElement("span");
  crystalsEl.appendChild(crystalsText);

  profileBar.appendChild(levelEl);
  profileBar.appendChild(expBar);
  profileBar.appendChild(pointsEl);
  profileBar.appendChild(crystalsEl);

  const spacer = document.createElement("div");
  css(spacer, { flex: "1" });
  profileBar.appendChild(spacer);

  // Upgrade button — uses skill-arrows icon
  const btnUpgrade = document.createElement("button");
  btnUpgrade.id = "btn-upgrade";
  btnUpgrade.className = "camp-upgrade-btn";
  btnUpgrade.innerHTML = `<img src="/ui/InterfaceSkillsButton.png" class="camp-upgrade-ico" alt=""> <span>Upgrades</span>`;

  // Back button
  const btnBack = document.createElement("a");
  btnBack.textContent = "← Menu";
  btnBack.href = "/?scene=menu";
  css(btnBack, { color: "#b8a070", textDecoration: "none", fontSize: "13px", padding: "6px 10px" });

  profileBar.appendChild(btnUpgrade);
  profileBar.appendChild(btnBack);
  root.appendChild(profileBar);

  // ── Main content ─────────────────────────────────────────────────────────
  const content = document.createElement("div");
  content.className = "camp-content";
  root.appendChild(content);

  // Campaign map — full-width scrollable row of node buttons
  const mapEl = document.createElement("div");
  mapEl.id = "campaign-map";
  mapEl.className = "camp-map";
  content.appendChild(mapEl);

  // ── Upgrade panel ─────────────────────────────────────────────────────────
  const upgradePanel = document.createElement("div");
  upgradePanel.id = "upgrade-panel";
  upgradePanel.className = "camp-upgrade-panel";
  content.appendChild(upgradePanel);

  const upgTitle = document.createElement("h3");
  upgTitle.textContent = "Spell Upgrades";
  css(upgTitle, { margin: "0 0 12px 0", color: "#e8c870", fontSize: "1.1rem", letterSpacing: "0.05em" });
  upgradePanel.appendChild(upgTitle);

  const pointsRemaining = document.createElement("div");
  pointsRemaining.id = "upgrade-points-remaining";
  css(pointsRemaining, { marginBottom: "16px", color: "#ffcc44", fontSize: "14px" });
  upgradePanel.appendChild(pointsRemaining);

  const spellList = document.createElement("div");
  css(spellList, { display: "flex", flexDirection: "column", gap: "10px" });
  upgradePanel.appendChild(spellList);

  // ── State ────────────────────────────────────────────────────────────────
  let profile: Profile | null = null;
  let upgradePanelOpen = false;

  function renderProfile(p: Profile) {
    profile = p;
    levelEl.textContent = `Lv ${p.level}`;
    const expNeeded = p.level * 100;
    const expPct = Math.min(100, Math.round((p.exp / expNeeded) * 100));
    expLabel.textContent = `EXP ${p.exp}/${expNeeded}`;
    expBarFill.style.width = `${expPct}%`;
    pointsEl.textContent = `Pts: ${p.points}`;
    crystalsText.textContent = `${p.crystals}`;
    if (upgradePanelOpen) renderUpgradePanel(p);
  }

  function renderUpgradePanel(p: Profile) {
    pointsRemaining.textContent = `Skill Points: ${p.points}`;
    spellList.innerHTML = "";
    const spells = ["ignite", "fireball", "firewall", "turret"];
    for (const spellId of spells) {
      const lvl = p.spellLevels[spellId] ?? 1;
      const row = document.createElement("div");
      row.className = "camp-spell-row";

      const icon = document.createElement("img");
      icon.src = SPELL_ICONS[spellId] ?? "/art/FireBallIco.png";
      css(icon, { width: "28px", height: "28px", imageRendering: "pixelated" });
      row.appendChild(icon);

      const nameEl = document.createElement("span");
      nameEl.textContent = SPELL_NAMES[spellId] ?? spellId;
      css(nameEl, { flex: "1", fontWeight: "600", color: "#e8e8ff" });
      row.appendChild(nameEl);

      const levelSpan = document.createElement("span");
      levelSpan.id = `spell-level-${spellId}`;
      levelSpan.textContent = `${lvl}`;
      css(levelSpan, { color: "#88aaff", fontSize: "15px", minWidth: "24px", textAlign: "center" });
      row.appendChild(levelSpan);

      const btnPlus = document.createElement("button");
      btnPlus.id = `btn-upgrade-${spellId}`;
      btnPlus.className = `camp-plus-btn ${p.points > 0 ? "can-afford" : "cannot-afford"}`;
      btnPlus.textContent = "+";
      if (p.points === 0) btnPlus.disabled = true;
      btnPlus.addEventListener("click", async () => {
        const data = await metaApi.upgrade(spellId);
        if (data.ok) renderProfile(data.profile);
      });
      row.appendChild(btnPlus);
      spellList.appendChild(row);
    }
  }

  function renderNodes(ns: CampaignNode[]) {
    mapEl.innerHTML = "";
    ns.forEach((node, i) => {
      if (i > 0) {
        const connector = document.createElement("div");
        connector.className = `camp-connector ${node.unlocked || node.completed ? "active" : ""}`;
        mapEl.appendChild(connector);
      }

      const state = node.completed ? "completed" : node.unlocked ? "unlocked" : "locked";
      const btn = document.createElement("button");
      btn.setAttribute("data-level", node.id);
      btn.setAttribute("data-state", state);
      btn.className = `camp-node camp-node-${state}`;

      // Node art image (the glassy orb icons)
      const nodeImg = document.createElement("img");
      nodeImg.src = nodeSrc(node.id, state);
      nodeImg.alt = node.label;
      nodeImg.className = "camp-node-img";
      btn.appendChild(nodeImg);

      // Label on MissionName banner below
      const labelWrap = document.createElement("div");
      labelWrap.className = "camp-node-label-wrap";
      const labelEl = document.createElement("span");
      labelEl.textContent = node.label;
      labelEl.className = "camp-node-label";
      labelWrap.appendChild(labelEl);
      btn.appendChild(labelWrap);

      if (state !== "locked") {
        btn.addEventListener("click", () => {
          location.href = `/?scene=battle&level=${node.id}&from=campaign`;
        });
      }
      mapEl.appendChild(btn);
    });
  }

  // Toggle upgrade panel
  btnUpgrade.addEventListener("click", () => {
    upgradePanelOpen = !upgradePanelOpen;
    upgradePanel.style.display = upgradePanelOpen ? "block" : "none";
    btnUpgrade.classList.toggle("active", upgradePanelOpen);
    if (upgradePanelOpen && profile) renderUpgradePanel(profile);
  });

  async function loadAll() {
    const [camp, prof] = await Promise.all([
      metaApi.getCampaign(),
      metaApi.getProfile(),
    ]);
    renderProfile(prof);
    renderNodes(camp.nodes);
  }

  loadAll().catch(console.error);
}

function injectCampaignStyles() {
  const id = "campaign-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .camp-root {
      min-height: 100vh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(60,40,10,0.4) 0%, transparent 60%),
        linear-gradient(180deg, #12080a 0%, #070510 50%, #040308 100%);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      font-family: sans-serif;
      color: #e8e8ff;
    }

    /* ── Profile bar ── */
    .camp-profile-bar {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 16px;
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
      border-bottom: 2px solid rgba(180,140,60,0.4);
      flex-shrink: 0;
      flex-wrap: wrap;
      min-height: 52px;
    }
    .camp-profile-level {
      font-weight: 700;
      font-size: 15px;
      color: #ffd700;
      text-shadow: 0 0 8px rgba(255,200,0,0.6);
      white-space: nowrap;
    }
    .camp-exp-wrap {
      display: flex;
      align-items: center;
      gap: 5px;
    }
    .camp-exp-label {
      color: #88aaff;
      font-size: 11px;
      white-space: nowrap;
    }
    .camp-exp-outer {
      position: relative;
      width: 80px;
      height: 14px;
      background-size: 100% 100%;
      background-repeat: no-repeat;
      border-radius: 3px;
      overflow: hidden;
    }
    .camp-exp-fill {
      position: absolute;
      left: 0; top: 0; bottom: 0;
      background-size: auto 100%;
      background-repeat: no-repeat;
      transition: width 0.3s;
    }
    .camp-profile-points {
      color: #ffcc44;
      font-size: 12px;
      white-space: nowrap;
    }
    .camp-profile-crystals {
      display: flex;
      align-items: center;
      gap: 3px;
      font-size: 13px;
      color: #44ddff;
    }
    .camp-upgrade-btn {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: 4px 12px;
      background: url('/ui/Button1.png') no-repeat center / 100% 100%;
      color: #f0e0b8;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 13px;
      font-family: sans-serif;
      font-weight: 600;
      min-height: 36px;
      transition: filter 0.15s;
    }
    .camp-upgrade-btn:hover   { filter: brightness(1.15); }
    .camp-upgrade-btn.active  { filter: brightness(1.2) saturate(1.4); }
    .camp-upgrade-ico {
      width: 22px;
      height: 22px;
    }

    /* ── Main content ── */
    .camp-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow: auto;
      padding: 16px;
    }

    /* ── Campaign map ── */
    .camp-map {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: 0;
      overflow-x: auto;
      padding: 16px 8px 24px 8px;
      min-height: 180px;
      /* Subtle scrollbar */
      scrollbar-width: thin;
      scrollbar-color: rgba(180,140,60,0.4) transparent;
    }
    .camp-connector {
      width: 28px;
      height: 4px;
      background: rgba(80,60,20,0.5);
      border-radius: 2px;
      flex-shrink: 0;
      align-self: center;
      margin-bottom: 28px;
    }
    .camp-connector.active {
      background: linear-gradient(90deg, rgba(180,140,60,0.6), rgba(220,180,80,0.9), rgba(180,140,60,0.6));
    }

    /* Node button */
    .camp-node {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      width: 80px;
      padding: 6px 4px;
      background: transparent;
      border: none;
      cursor: pointer;
      flex-shrink: 0;
      transition: transform 0.15s;
      -webkit-tap-highlight-color: transparent;
    }
    .camp-node:hover { transform: scale(1.08); }
    .camp-node-locked { cursor: not-allowed; opacity: 0.7; }
    .camp-node-locked:hover { transform: none; }

    .camp-node-img {
      width: 64px;
      height: 64px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }
    .camp-node-completed .camp-node-img {
      filter: drop-shadow(0 0 8px rgba(100,220,255,0.8)) drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }

    .camp-node-label-wrap {
      background: url('/ui/MissionName.png') no-repeat center / 100% 100%;
      padding: 2px 8px;
      min-width: 70px;
      text-align: center;
    }
    .camp-node-label {
      font-size: 10px;
      color: #e8d8a0;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      line-height: 1.3;
    }

    /* ── Upgrade panel ── */
    .camp-upgrade-panel {
      display: none;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,20,0.92);
      border: 2px solid rgba(180,140,60,0.5);
      border-radius: 10px;
      padding: 20px;
      margin-top: 12px;
      max-width: 480px;
    }
    .camp-spell-row {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 12px;
      background: rgba(20,20,50,0.85);
      border-radius: 6px;
      border: 1px solid rgba(100,80,180,0.4);
    }
    .camp-plus-btn {
      width: 32px;
      height: 32px;
      background: url('/ui/InterfaceNewButton.png') no-repeat center / contain;
      border: none;
      cursor: pointer;
      font-size: 0;
      transition: filter 0.15s, transform 0.1s;
    }
    .camp-plus-btn.can-afford:hover  { filter: brightness(1.2); transform: scale(1.1); }
    .camp-plus-btn.cannot-afford { filter: grayscale(1) opacity(0.4); cursor: not-allowed; }
  `;
  document.head.appendChild(style);
}

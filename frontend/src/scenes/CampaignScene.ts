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

function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

export function mountCampaign(host: HTMLElement) {
  const root = document.createElement("div");
  root.id = "campaign";
  css(root, {
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    minHeight: "100vh",
    background: "#0b0b12",
    display: "flex",
    flexDirection: "column",
    overflow: "hidden",
  });
  host.appendChild(root);

  // Profile bar
  const profileBar = document.createElement("div");
  profileBar.id = "profile-bar";
  css(profileBar, {
    display: "flex",
    alignItems: "center",
    gap: "18px",
    padding: "10px 24px",
    background: "#10101e",
    borderBottom: "1px solid #2a2a4e",
    fontSize: "14px",
    flexShrink: "0",
  });

  const levelEl = document.createElement("span");
  levelEl.id = "profile-level";
  css(levelEl, { fontWeight: "700", fontSize: "16px", color: "#ffd700" });

  const expBar = document.createElement("div");
  css(expBar, { display: "flex", alignItems: "center", gap: "6px" });
  const expLabel = document.createElement("span");
  expLabel.id = "profile-exp";
  css(expLabel, { color: "#88aaff" });
  const expBarOuter = document.createElement("div");
  css(expBarOuter, {
    width: "120px", height: "8px", background: "#222244",
    borderRadius: "4px", overflow: "hidden", border: "1px solid #334"
  });
  const expBarFill = document.createElement("div");
  expBarFill.id = "profile-exp-fill";
  css(expBarFill, { height: "100%", background: "#4488ff", width: "0%", transition: "width 0.3s" });
  expBarOuter.appendChild(expBarFill);
  expBar.appendChild(expLabel);
  expBar.appendChild(expBarOuter);

  const pointsEl = document.createElement("span");
  pointsEl.id = "profile-points";
  css(pointsEl, { color: "#ffcc44" });

  const crystalsEl = document.createElement("span");
  crystalsEl.id = "profile-crystals";
  css(crystalsEl, { display: "flex", alignItems: "center", gap: "4px" });
  const gemImg = document.createElement("img");
  gemImg.src = "/art/Gem.png";
  css(gemImg, { width: "16px", height: "16px", imageRendering: "pixelated" });
  crystalsEl.appendChild(gemImg);
  const crystalsText = document.createElement("span");
  crystalsEl.appendChild(crystalsText);

  profileBar.appendChild(levelEl);
  profileBar.appendChild(expBar);
  profileBar.appendChild(pointsEl);
  profileBar.appendChild(crystalsEl);

  // Spacer in profile bar
  const spacer = document.createElement("div");
  css(spacer, { flex: "1" });
  profileBar.appendChild(spacer);

  // Upgrade button
  const btnUpgrade = document.createElement("button");
  btnUpgrade.id = "btn-upgrade";
  btnUpgrade.textContent = "⚗ Upgrades";
  css(btnUpgrade, {
    padding: "6px 14px",
    background: "#1e1e3a",
    color: "#cc88ff",
    border: "1px solid #553377",
    borderRadius: "6px",
    cursor: "pointer",
    fontSize: "13px",
  });
  profileBar.appendChild(btnUpgrade);

  // Back button
  const btnBack = document.createElement("a");
  btnBack.textContent = "← Menu";
  btnBack.href = "/?scene=menu";
  css(btnBack, {
    color: "#8899cc",
    textDecoration: "none",
    fontSize: "13px",
    padding: "6px 10px",
  });
  profileBar.appendChild(btnBack);

  root.appendChild(profileBar);

  // Main content area
  const content = document.createElement("div");
  css(content, { flex: "1", display: "flex", flexDirection: "column", overflow: "auto", padding: "24px" });
  root.appendChild(content);

  const title = document.createElement("h2");
  title.textContent = "Campaign";
  css(title, { margin: "0 0 20px 0", fontSize: "1.4rem", letterSpacing: "0.05em", color: "#ddeeff" });
  content.appendChild(title);

  // Campaign map
  const mapEl = document.createElement("div");
  mapEl.id = "campaign-map";
  css(mapEl, {
    display: "flex",
    flexDirection: "row",
    alignItems: "center",
    gap: "0",
    overflowX: "auto",
    padding: "16px 0 24px 0",
    minHeight: "160px",
  });
  content.appendChild(mapEl);

  // Upgrade panel (hidden by default)
  const upgradePanel = document.createElement("div");
  upgradePanel.id = "upgrade-panel";
  css(upgradePanel, {
    display: "none",
    background: "#12122a",
    border: "1px solid #334466",
    borderRadius: "10px",
    padding: "20px",
    marginTop: "16px",
    maxWidth: "480px",
  });
  content.appendChild(upgradePanel);

  const upgTitle = document.createElement("h3");
  upgTitle.textContent = "Spell Upgrades";
  css(upgTitle, { margin: "0 0 12px 0", color: "#cc88ff" });
  upgradePanel.appendChild(upgTitle);

  const pointsRemaining = document.createElement("div");
  pointsRemaining.id = "upgrade-points-remaining";
  css(pointsRemaining, { marginBottom: "16px", color: "#ffcc44", fontSize: "14px" });
  upgradePanel.appendChild(pointsRemaining);

  const spellList = document.createElement("div");
  css(spellList, { display: "flex", flexDirection: "column", gap: "10px" });
  upgradePanel.appendChild(spellList);

  // State
  let profile: Profile | null = null;
  let upgradePanelOpen = false;

  function renderProfile(p: Profile) {
    profile = p;
    levelEl.textContent = `Lv ${p.level}`;
    const expNeeded = p.level * 100;
    const expPct = Math.min(100, Math.round((p.exp / expNeeded) * 100));
    expLabel.textContent = `EXP ${p.exp}/${expNeeded}`;
    expBarFill.style.width = `${expPct}%`;
    pointsEl.textContent = `Points: ${p.points}`;
    crystalsText.textContent = `${p.crystals}`;
    if (upgradePanelOpen) renderUpgradePanel(p);
  }

  function renderUpgradePanel(p: Profile) {
    pointsRemaining.textContent = `Skill Points remaining: ${p.points}`;
    spellList.innerHTML = "";
    const spells = ["ignite", "fireball", "firewall", "turret"];
    for (const spellId of spells) {
      const lvl = p.spellLevels[spellId] ?? 1;
      const row = document.createElement("div");
      css(row, {
        display: "flex", alignItems: "center", gap: "12px",
        padding: "8px 12px", background: "#1a1a30", borderRadius: "6px",
        border: "1px solid #2a2a44",
      });

      const icon = document.createElement("img");
      icon.src = SPELL_ICONS[spellId] ?? "/art/FireBallIco.png";
      css(icon, { width: "28px", height: "28px", imageRendering: "pixelated" });
      row.appendChild(icon);

      const nameEl = document.createElement("span");
      nameEl.textContent = SPELL_NAMES[spellId] ?? spellId;
      css(nameEl, { flex: "1", fontWeight: "600" });
      row.appendChild(nameEl);

      const levelSpan = document.createElement("span");
      levelSpan.id = `spell-level-${spellId}`;
      levelSpan.textContent = `${lvl}`;
      css(levelSpan, { color: "#88aaff", fontSize: "15px", minWidth: "24px", textAlign: "center" });
      row.appendChild(levelSpan);

      const btnPlus = document.createElement("button");
      btnPlus.id = `btn-upgrade-${spellId}`;
      btnPlus.textContent = "+";
      css(btnPlus, {
        width: "28px", height: "28px",
        background: p.points > 0 ? "#2a2a5a" : "#1a1a2a",
        color: p.points > 0 ? "#cc88ff" : "#555",
        border: `1px solid ${p.points > 0 ? "#553377" : "#333"}`,
        borderRadius: "4px",
        cursor: p.points > 0 ? "pointer" : "not-allowed",
        fontSize: "18px",
        lineHeight: "1",
        padding: "0",
      });
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
      // Connector line between nodes
      if (i > 0) {
        const connector = document.createElement("div");
        css(connector, {
          width: "32px", height: "3px",
          background: node.unlocked || node.completed ? "#334488" : "#222233",
          alignSelf: "center",
          flexShrink: "0",
        });
        mapEl.appendChild(connector);
      }

      const state = node.completed ? "completed" : node.unlocked ? "unlocked" : "locked";
      const btn = document.createElement("button");
      btn.setAttribute("data-level", node.id);
      btn.setAttribute("data-state", state);
      css(btn, {
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: "6px",
        width: "110px",
        minHeight: "110px",
        padding: "10px 8px",
        background: state === "completed" ? "#1a3a2a" :
                    state === "unlocked" ? "#1a1a3a" : "#111122",
        color: state === "locked" ? "#444466" : "#e8e8ff",
        border: state === "completed" ? "2px solid #33aa66" :
                state === "unlocked" ? "2px solid #334488" : "2px solid #222233",
        borderRadius: "8px",
        cursor: state === "locked" ? "not-allowed" : "pointer",
        flexShrink: "0",
        position: "relative",
        transition: "background 0.15s, border-color 0.15s",
        opacity: state === "locked" ? "0.6" : "1",
      });
      if (state !== "locked") {
        btn.addEventListener("mouseenter", () => {
          btn.style.background = state === "completed" ? "#1e4430" : "#22224a";
          btn.style.borderColor = state === "completed" ? "#44cc77" : "#4466bb";
        });
        btn.addEventListener("mouseleave", () => {
          btn.style.background = state === "completed" ? "#1a3a2a" : "#1a1a3a";
          btn.style.borderColor = state === "completed" ? "#33aa66" : "#334488";
        });
      }

      // Mission icon
      const missionImg = document.createElement("img");
      missionImg.src = "/art/Mission_Standart.png";
      css(missionImg, {
        width: "40px", height: "40px", imageRendering: "pixelated",
        opacity: state === "locked" ? "0.4" : "1",
      });
      btn.appendChild(missionImg);

      // Label
      const labelEl = document.createElement("span");
      labelEl.textContent = node.label;
      css(labelEl, { fontSize: "11px", textAlign: "center", lineHeight: "1.2" });
      btn.appendChild(labelEl);

      // State indicator
      const indicator = document.createElement("span");
      indicator.textContent = state === "completed" ? "✓" : state === "locked" ? "🔒" : "▶";
      css(indicator, {
        fontSize: state === "locked" ? "12px" : "14px",
        color: state === "completed" ? "#55ee88" :
               state === "unlocked" ? "#aabbff" : "#444466",
      });
      btn.appendChild(indicator);

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
    btnUpgrade.style.background = upgradePanelOpen ? "#2a1a4a" : "#1e1e3a";
    if (upgradePanelOpen && profile) renderUpgradePanel(profile);
  });

  // Load data
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

import { metaApi } from "../net/metaApi";
import type { CampaignNode, Profile } from "../net/metaApi";
import { navigateTo } from "../ui/transition";
import { log } from "../log";

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

  // ── Upgrade panel — fixed overlay so it's always in-viewport ────────────
  const upgradePanel = document.createElement("div");
  upgradePanel.id = "upgrade-panel";
  upgradePanel.className = "camp-upgrade-panel";
  root.appendChild(upgradePanel);

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

    // Serpentine layout: NODES_PER_ROW nodes per row, alternating left→right / right→left
    const NODES_PER_ROW = 3;

    // Convert linear index → (col, row) in serpentine order
    function snakePos(i: number): { col: number; row: number } {
      const row = Math.floor(i / NODES_PER_ROW);
      const posInRow = i % NODES_PER_ROW;
      const col = row % 2 === 0 ? posInRow : (NODES_PER_ROW - 1 - posInRow);
      return { col, row };
    }

    // Node size + spacing (px)
    const NODE_W = 80;     // button width
    const NODE_H = 108;    // button height (img 64 + label ~36 + gap)
    const H_GAP  = 20;     // horizontal gap between nodes
    const V_GAP  = 36;     // vertical gap between rows
    const CONNECTOR_THICKNESS = 6;

    const totalCols = NODES_PER_ROW;
    const totalRows = Math.ceil(ns.length / NODES_PER_ROW);
    const innerW = totalCols * NODE_W + (totalCols - 1) * H_GAP;
    const innerH = totalRows * NODE_H + (totalRows - 1) * V_GAP;

    // Inner wrapper: relative-positioned, holds absolute connectors + nodes
    const inner = document.createElement("div");
    inner.className = "camp-map-inner";
    inner.style.width    = `${innerW}px`;
    inner.style.minHeight = `${innerH + 8}px`;
    mapEl.appendChild(inner);

    function nodeLeft(col: number) { return col * (NODE_W + H_GAP); }
    function nodeTop(row: number)  { return row * (NODE_H + V_GAP); }
    // Centre of node button
    function nodeCX(col: number)   { return nodeLeft(col) + NODE_W / 2; }
    function nodeCY(row: number)   { return nodeTop(row)  + NODE_H / 2; }

    // ── Connectors first (behind nodes) ──────────────────────────────────────
    for (let i = 1; i < ns.length; i++) {
      const node = ns[i];
      const isActive = node.unlocked || node.completed;
      const activeClass = isActive ? "active" : "";

      const prev = snakePos(i - 1);
      const curr = snakePos(i);

      const x1 = nodeCX(prev.col);
      const x2 = nodeCX(curr.col);

      if (prev.row === curr.row) {
        // Same row → horizontal connector between the two node centres
        const hLeft  = Math.min(x1, x2) + NODE_W / 2;
        const hWidth = Math.abs(x2 - x1) - NODE_W;
        if (hWidth > 0) {
          const conn = document.createElement("div");
          conn.className = `camp-connector ${activeClass}`;
          conn.style.left   = `${hLeft}px`;
          conn.style.top    = `${nodeCY(prev.row) - CONNECTOR_THICKNESS / 2}px`;
          conn.style.width  = `${hWidth}px`;
          conn.style.height = `${CONNECTOR_THICKNESS}px`;
          inner.appendChild(conn);
        }
      } else {
        // Row transition — L-path: vertical down from prev, then horizontal to curr column
        const vTop    = nodeTop(prev.row) + NODE_H;
        const vBottom = nodeCY(curr.row);
        const vH = vBottom - vTop;
        if (vH > 0) {
          const vConn = document.createElement("div");
          vConn.className = `camp-connector ${activeClass}`;
          vConn.style.left   = `${x1 - CONNECTOR_THICKNESS / 2}px`;
          vConn.style.top    = `${vTop}px`;
          vConn.style.width  = `${CONNECTOR_THICKNESS}px`;
          vConn.style.height = `${vH}px`;
          inner.appendChild(vConn);
        }
        // Horizontal segment at vertical centre of curr row
        const hY     = nodeCY(curr.row) - CONNECTOR_THICKNESS / 2;
        const hLeft  = Math.min(x1, x2);
        const hWidth = Math.abs(x2 - x1) - CONNECTOR_THICKNESS;
        if (hWidth > 0) {
          const hConn = document.createElement("div");
          hConn.className = `camp-connector ${activeClass}`;
          hConn.style.left   = `${hLeft + (x1 < x2 ? CONNECTOR_THICKNESS : 0)}px`;
          hConn.style.top    = `${hY}px`;
          hConn.style.width  = `${hWidth}px`;
          hConn.style.height = `${CONNECTOR_THICKNESS}px`;
          inner.appendChild(hConn);
        }
      }
    }

    // ── Node buttons ─────────────────────────────────────────────────────────
    let lastUnlockedBtn: HTMLElement | null = null;
    ns.forEach((node, i) => {
      const { col, row } = snakePos(i);
      const state = node.completed ? "completed" : node.unlocked ? "unlocked" : "locked";

      const btn = document.createElement("button");
      btn.setAttribute("data-level", node.id);
      btn.setAttribute("data-state", state);
      btn.className = `camp-node camp-node-${state}`;
      btn.style.position = "absolute";
      btn.style.left  = `${nodeLeft(col)}px`;
      btn.style.top   = `${nodeTop(row)}px`;
      btn.style.width = `${NODE_W}px`;

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
          navigateTo(`/?scene=battle&level=${node.id}&from=campaign`);
        });
        lastUnlockedBtn = btn;
      }
      inner.appendChild(btn);
    });

    // Scroll the most-recently-unlocked node into view
    if (lastUnlockedBtn) {
      requestAnimationFrame(() => {
        (lastUnlockedBtn as HTMLElement).scrollIntoView({ block: "center", behavior: "smooth" });
      });
    }
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

  loadAll()
    .then(() => maybeShowRiftBanner(root))
    .catch(console.error);
}

/**
 * If the URL carries a rift offer (set by the campaign reward flow), slide in a
 * banner offering the dungeon run. Descend → start the run; Skip → stay on the map.
 * The banner is an overlay layered over the (still-present) campaign map.
 */
function maybeShowRiftBanner(root: HTMLElement) {
  const q = new URLSearchParams(location.search);
  const dungeonId = q.get("rift");
  if (!dungeonId) return;
  const floors = q.get("riftFloors") ?? "?";
  const name   = q.get("riftName") ?? "Rift";

  injectRiftStyles();

  const banner = document.createElement("div");
  banner.id = "rift-banner";
  banner.className = "rift-banner";
  banner.innerHTML = `
    <div class="rift-banner-glyph"></div>
    <div class="rift-banner-text">
      <div class="rift-banner-title">A Rift opens</div>
      <div class="rift-banner-sub">${name} · ${floors} floors · permadeath · 1 reward / floor</div>
    </div>
    <div class="rift-banner-actions">
      <button id="btn-rift-descend" class="rift-btn rift-btn-go">Descend</button>
      <button id="btn-rift-skip" class="rift-btn rift-btn-skip">Skip</button>
    </div>`;
  root.appendChild(banner);
  log("rift", "banner-shown", { dungeonId, floors });

  // Slide in on next frame.
  requestAnimationFrame(() => banner.classList.add("rift-banner-in"));

  const close = () => { banner.classList.remove("rift-banner-in"); };

  banner.querySelector("#btn-rift-descend")!.addEventListener("click", async () => {
    log("rift", "descend", { dungeonId });
    try {
      await metaApi.startDungeon(dungeonId);
      navigateTo("/?scene=dungeon");
    } catch (e) {
      log("rift", "descend-failed", { err: String(e) });
    }
  });

  banner.querySelector("#btn-rift-skip")!.addEventListener("click", () => {
    log("rift", "skip", { dungeonId });
    close();
    // Drop the rift params so a refresh doesn't re-offer.
    history.replaceState(null, "", "/?scene=campaign");
    setTimeout(() => banner.remove(), 300);
  });
}

function injectRiftStyles() {
  const id = "rift-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .rift-banner {
      position: fixed;
      left: 50%;
      top: 64px;
      transform: translate(-50%, -160%);
      width: min(360px, 92vw);
      z-index: 200;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px 16px;
      box-sizing: border-box;
      background:
        linear-gradient(180deg, rgba(60,10,70,0.96), rgba(30,5,40,0.97)),
        rgba(20,5,30,0.97);
      border: 2px solid #b048e0;
      border-radius: 12px;
      box-shadow: 0 0 28px rgba(180,70,230,0.55), inset 0 0 30px rgba(120,30,160,0.4);
      color: #f4e6ff;
      font-family: sans-serif;
      transition: transform 0.35s cubic-bezier(0.2, 1.1, 0.4, 1);
    }
    .rift-banner-in { transform: translate(-50%, 0); }
    .rift-banner-glyph {
      width: 26px; height: 26px; flex-shrink: 0;
      border-radius: 50%;
      background: radial-gradient(circle at 38% 35%, #f4d6ff 0%, #c060ff 45%, #5a149a 100%);
      box-shadow: 0 0 14px #c060ff, inset 0 0 6px rgba(255,255,255,0.8);
      animation: rift-pulse 1.4s ease-in-out infinite;
    }
    @keyframes rift-pulse { 0%,100% { opacity: 0.7; transform: scale(1); } 50% { opacity: 1; transform: scale(1.18); } }
    .rift-banner-text { flex: 1; min-width: 0; }
    .rift-banner-title {
      font-size: 15px; font-weight: 800; letter-spacing: 0.04em;
      color: #e9b8ff; text-shadow: 0 0 10px rgba(190,90,240,0.7);
    }
    .rift-banner-sub { font-size: 10px; color: #c9a8e0; margin-top: 2px; line-height: 1.3; }
    .rift-banner-actions { display: flex; flex-direction: column; gap: 6px; }
    .rift-btn {
      min-width: 78px; min-height: 34px;
      border: none; border-radius: 8px; cursor: pointer;
      font-size: 13px; font-weight: 700; font-family: sans-serif;
      touch-action: manipulation; -webkit-tap-highlight-color: transparent;
      transition: filter 0.15s, transform 0.1s;
    }
    .rift-btn:active { transform: scale(0.95); }
    .rift-btn-go {
      background: linear-gradient(180deg, #c860ff, #8a28c0);
      color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.6);
      box-shadow: 0 0 12px rgba(190,90,240,0.6);
    }
    .rift-btn-go:hover { filter: brightness(1.15); }
    .rift-btn-skip {
      background: rgba(40,20,55,0.9); color: #b89ccc;
      border: 1px solid rgba(150,90,190,0.45);
    }
    .rift-btn-skip:hover { filter: brightness(1.2); }
  `;
  document.head.appendChild(style);
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
      overflow-y: auto;
      overflow-x: hidden;
      -webkit-overflow-scrolling: touch;
      /* Subtle scrollbar */
      scrollbar-width: thin;
      scrollbar-color: rgba(180,140,60,0.4) transparent;
    }

    /* ── Campaign map — vertically fills the content area, inner content scrolls via camp-content ── */
    .camp-map {
      /* No flex:1 — natural height from inner content */
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 20px 16px 32px 16px;
      /* No overflow here — parent camp-content scrolls */
    }

    /* Inner relative wrapper that holds abs-positioned connectors + nodes */
    .camp-map-inner {
      position: relative;
      flex-shrink: 0;
    }

    /* Connector shared base (positioned absolutely inside .camp-map-inner) */
    .camp-connector {
      position: absolute;
      border-radius: 3px;
      background: rgba(80,60,20,0.5);
      pointer-events: none;
    }
    .camp-connector.active {
      background: linear-gradient(
        135deg,
        rgba(180,140,60,0.6) 0%,
        rgba(220,180,80,0.95) 50%,
        rgba(180,140,60,0.6) 100%
      );
      box-shadow: 0 0 6px rgba(220,180,60,0.4);
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

    /* ── Upgrade panel — fixed bottom sheet, always in viewport ── */
    .camp-upgrade-panel {
      display: none;
      position: fixed;
      left: 0;
      right: 0;
      bottom: 0;
      z-index: 100;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,20,0.96);
      border-top: 2px solid rgba(180,140,60,0.5);
      border-radius: 12px 12px 0 0;
      padding: 20px 20px 32px 20px;
      max-height: 60vh;
      overflow-y: auto;
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

import { metaApi } from "../net/metaApi";
import type { CampaignNode, Profile } from "../net/metaApi";
import { navigateTo } from "../ui/transition";
import { log } from "../log";
import { RIFT_STYLES, CAMPAIGN_STYLES } from "./campaign/campaignStyles";

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
  // 3-slice frame via CSS border-image, fill as absolute-positioned gradient div
  const expBarFill = document.createElement("div");
  expBarFill.id = "profile-exp-fill";
  expBarFill.className = "camp-exp-fill";
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
  btnBack.className = "ui-link";
  btnBack.textContent = "← Menu";
  btnBack.href = "/?scene=menu";
  css(btnBack, { textDecoration: "none", fontSize: "13px", padding: "12px 14px", minHeight: "44px", display: "flex", alignItems: "center", cursor: "pointer", transition: "filter 0.15s" });

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
    const NODE_W = 104;    // button width — wide enough that labels never wrap mid-phrase
    const NODE_H = 116;    // button height (img 64 + two-line label + gap)
    const H_GAP  = 16;     // horizontal gap between nodes
    const V_GAP  = 36;     // vertical gap between rows
    const CONNECTOR_THICKNESS = 6;
    // Connectors route through the ORB centres, not the button centres — the
    // button includes the label plaque, and centre-of-button lines used to cut
    // straight through the text (docs/13 campaign audit).
    const ORB_TOP_PAD = 6;   // .camp-node padding-top
    const ORB_SIZE    = 64;  // .camp-node-img height
    const ORB_CY      = ORB_TOP_PAD + ORB_SIZE / 2;
    const ORB_BOTTOM  = ORB_TOP_PAD + ORB_SIZE;

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
    // Centre of the node ORB (the label hangs below it)
    function nodeCX(col: number)   { return nodeLeft(col) + NODE_W / 2; }
    function nodeCY(row: number)   { return nodeTop(row)  + ORB_CY; }

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
        // Same row → horizontal connector spanning the gap between the orbs
        const hLeft  = Math.min(x1, x2) + ORB_SIZE / 2;
        const hWidth = Math.abs(x2 - x1) - ORB_SIZE;
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
        // Row transition — L-path: vertical down from prev orb, then horizontal to curr column
        const vTop    = nodeTop(prev.row) + ORB_BOTTOM;
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
      // Labels are "Biome — Subtitle" (e.g. "Hell — The Circuit"). Render as a
      // tiny biome kicker over a single non-wrapping title line; the plaque
      // sizes to the text. Mid-phrase wrapping over 3–4 lines was the worst
      // offender in the docs/13 audit.
      const labelWrap = document.createElement("div");
      labelWrap.className = "camp-node-label-wrap";
      const dashIdx = node.label.indexOf("—");
      const kickerText = dashIdx >= 0 ? node.label.slice(0, dashIdx).trim() : "";
      const titleText  = dashIdx >= 0 ? node.label.slice(dashIdx + 1).trim() : node.label;
      if (kickerText) {
        const kickerEl = document.createElement("span");
        kickerEl.textContent = kickerText;
        kickerEl.className = "camp-node-kicker";
        labelWrap.appendChild(kickerEl);
      }
      const labelEl = document.createElement("span");
      labelEl.textContent = titleText;
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
  style.textContent = RIFT_STYLES;
  document.head.appendChild(style);
}

function injectCampaignStyles() {
  const id = "campaign-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = CAMPAIGN_STYLES;
  document.head.appendChild(style);
}

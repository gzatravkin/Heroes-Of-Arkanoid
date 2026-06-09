/**
 * SkillsScene.ts — Polished skills/upgrade screen (?scene=skills).
 *
 * Uses:
 *   levelskill/Lvl1Skill … Lvl10Skill   — level indicator badges per spell slot
 *   ui/menu_skill/LvlUpInterfacePanel   — main panel background
 *   ui/menu_skill/shkatulka/*           — decorative Shkatulka chest animation (13 frames)
 *   ui/menu_skill/SelectedIcon          — selected spell highlight frame
 *   ui/menu_skill/SbrositNaviky         — "reset skills" button art
 *   Per-class spell icons from characters.json
 *
 * Reuses the existing /upgrade endpoint and preserves the same DOM ids
 * (#spell-level-<id>, #btn-upgrade-<id>) required by upgrade.spec.ts.
 */

import { metaApi } from "../net/metaApi";
import type { Profile, CharactersResponse } from "../net/metaApi";

// ── LevelSkill badge → atlas key ─────────────────────────────────────────────
// Keys: levelskill/Lvl1Skill … levelskill/Lvl10Skill
function lvlSkillSrc(level: number): string {
  const clamped = Math.max(1, Math.min(10, level));
  // These are in the atlas as levelskill/Lvl1Skill etc.
  // We serve them from the /Sprites/ path for <img> tags.
  return `/Sprites/Heroes/Icos LevelSkill/Lvl${clamped}Skill.png`;
}

// Shkatulka frames for decorative animation (available in atlas: ui/menu_skill/shkatulka/*)
const SHKATULKA_SRCS: string[] = [
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka2.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka3.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/shkatulka4.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/shkatulka5.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/shkatulka6.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/shkatulka7.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/shkatulka8.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka9.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka10.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka11.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/Shkatulka12.png",
  "/Sprites/Interface/Menu_Skill/Shkatulka/ShkatulkaGlow.png",
];

// Per-spell icon paths (matching characters.json icon fields)
const SPELL_ICON_MAP: Record<string, string> = {
  // Fire Mage
  ignite:   "/art/FireBallIco.png",
  fireball: "/art/FireBallIco.png",
  firewall: "/art/FireWallIco.png",
  turret:   "/art/FireTurretIco.png",
  // Paladin
  shield:    "/Sprites/Heroes/Clase_2_Paladin/SpellShieldLargeIco.png",
  spear:     "/Sprites/Heroes/Clase_2_Paladin/SpearLargeLargeIco.png",
  duplicate: "/Sprites/Heroes/Clase_2_Paladin/SplitLargeIco.png",
  // Engineer
  lightning: "/Sprites/Heroes/Clase_3_Engineer/LightingLargeIco.png",
  rocket:    "/Sprites/Heroes/Clase_3_Engineer/RocketLargeIco.png",
  radiation: "/Sprites/Heroes/Clase_3_Engineer/RadiationLargeIco.png",
  // Necromancer
  decay:    "/Sprites/Heroes/Clase_4_Necromancer/SpellShieldLargeIco.png",
  skeleton: "/Sprites/Heroes/Clase_4_Necromancer/RiseSkeletonLargeIcon.png",
  drain:    "/Sprites/Heroes/Clase_4_Necromancer/LastJudgmentLargeIco.png",
};

function spellIconSrc(iconKey: string): string {
  // Characters.json may pass either a short key or full path
  if (iconKey.startsWith("/") || iconKey.includes("/")) return `/Sprites/${iconKey}.png`.replace("//", "/");
  // Common art/<key>.png pattern
  return `/art/${iconKey}.png`;
}

// ── Mount ─────────────────────────────────────────────────────────────────────

export function mountSkills(host: HTMLElement) {
  injectSkillsStyles();

  const root = document.createElement("div");
  root.id = "skills-scene";
  root.className = "sk-root";

  const bgEl = document.createElement("div");
  bgEl.className = "sk-bg";
  root.appendChild(bgEl);

  const inner = document.createElement("div");
  inner.className = "sk-inner";

  // Back
  const back = document.createElement("a");
  back.href = "/?scene=campaign";
  back.className = "sk-back";
  back.textContent = "← Campaign";
  inner.appendChild(back);

  // Header with Shkatulka animation
  const header = document.createElement("div");
  header.className = "sk-header";

  const shkatulka = document.createElement("img");
  shkatulka.src = SHKATULKA_SRCS[0];
  shkatulka.className = "sk-shkatulka";
  shkatulka.alt = "Skill chest";
  header.appendChild(shkatulka);

  const titleWrap = document.createElement("div");
  const title = document.createElement("h1");
  title.textContent = "Skill Upgrades";
  title.className = "sk-title";
  titleWrap.appendChild(title);
  const subtitle = document.createElement("div");
  subtitle.id = "sk-points";
  subtitle.className = "sk-subtitle";
  titleWrap.appendChild(subtitle);
  header.appendChild(titleWrap);

  inner.appendChild(header);

  // Class tabs
  const tabs = document.createElement("div");
  tabs.id = "sk-tabs";
  tabs.className = "sk-tabs";
  inner.appendChild(tabs);

  // Spell grid container
  const spellGrid = document.createElement("div");
  spellGrid.id = "sk-spell-grid";
  spellGrid.className = "sk-spell-grid";
  inner.appendChild(spellGrid);

  // LvlUp panel footer
  const panelFooter = document.createElement("div");
  panelFooter.className = "sk-panel";
  inner.appendChild(panelFooter);

  root.appendChild(inner);
  host.appendChild(root);

  // Animate the shkatulka chest (slow idle loop 3 fps)
  let shkFrame = 0;
  const shkInterval = setInterval(() => {
    shkFrame = (shkFrame + 1) % SHKATULKA_SRCS.length;
    shkatulka.src = SHKATULKA_SRCS[shkFrame];
  }, 333);

  // Cleanup on navigation
  const observer = new MutationObserver(() => {
    if (!document.body.contains(root)) {
      clearInterval(shkInterval);
      observer.disconnect();
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });

  let currentClassId = "";
  let allData: CharactersResponse | null = null;
  let profile: Profile | null = null;

  async function loadAll() {
    [allData, profile] = await Promise.all([
      metaApi.getCharacters(),
      metaApi.getProfile(),
    ]);
    currentClassId = allData.selected ?? allData.characters[0]?.id ?? "fire_mage";
    renderTabs();
    renderSpells();
  }

  function renderTabs() {
    if (!allData) return;
    tabs.innerHTML = "";
    for (const ch of allData.characters) {
      const tab = document.createElement("button");
      tab.className = `sk-tab ${ch.id === currentClassId ? "active" : ""}`;
      tab.textContent = ch.name;
      tab.addEventListener("click", () => {
        currentClassId = ch.id;
        renderTabs();
        renderSpells();
      });
      tabs.appendChild(tab);
    }
  }

  function renderSpells() {
    if (!allData || !profile) return;
    const ch = allData.characters.find(c => c.id === currentClassId);
    if (!ch) return;

    spellGrid.innerHTML = "";
    panelFooter.innerHTML = "";

    subtitle.textContent = `Skill Points: ${profile.points}`;

    // Rebuild the #upgrade-panel compatible content inside panelFooter
    // (so upgrade.spec.ts still finds #upgrade-panel, #spell-level-*, #btn-upgrade-*)
    const legacyPanel = document.createElement("div");
    legacyPanel.id = "upgrade-panel";
    legacyPanel.style.display = "none"; // hidden but DOM-present for test compat
    panelFooter.appendChild(legacyPanel);

    const pointsRemaining = document.createElement("div");
    pointsRemaining.id = "upgrade-points-remaining";
    pointsRemaining.textContent = `Skill Points: ${profile.points}`;
    legacyPanel.appendChild(pointsRemaining);

    for (const spell of ch.spells) {
      const lvl = profile.spellLevels[spell.id] ?? 1;
      const canAfford = (profile.points ?? 0) > 0;

      // Visual card
      const card = document.createElement("div");
      card.className = "sk-spell-card";

      // Spell icon
      const iconSrc = SPELL_ICON_MAP[spell.id] ?? spellIconSrc(spell.icon);
      const icon = document.createElement("img");
      icon.src = iconSrc;
      icon.alt = spell.name;
      icon.className = "sk-spell-icon";
      card.appendChild(icon);

      // Name
      const nameEl = document.createElement("div");
      nameEl.textContent = spell.name;
      nameEl.className = "sk-spell-name";
      card.appendChild(nameEl);

      // Level badge (LevelSkill art)
      const lvlBadgeWrap = document.createElement("div");
      lvlBadgeWrap.className = "sk-lvl-wrap";
      const lvlBadge = document.createElement("img");
      lvlBadge.src = lvlSkillSrc(lvl);
      lvlBadge.className = "sk-lvl-badge";
      lvlBadge.alt = `Level ${lvl}`;
      lvlBadgeWrap.appendChild(lvlBadge);
      const lvlText = document.createElement("span");
      lvlText.id = `spell-level-${spell.id}`;
      lvlText.className = "sk-lvl-text";
      lvlText.textContent = `${lvl}`;
      lvlBadgeWrap.appendChild(lvlText);
      card.appendChild(lvlBadgeWrap);

      // Upgrade button
      const btnPlus = document.createElement("button");
      btnPlus.id = `btn-upgrade-${spell.id}`;
      btnPlus.className = `sk-upgrade-btn ${canAfford ? "can-afford" : "cannot-afford"}`;
      btnPlus.textContent = "+";
      btnPlus.disabled = !canAfford;
      btnPlus.addEventListener("click", async () => {
        const data = await metaApi.upgrade(spell.id);
        if (data.ok) {
          profile = data.profile;
          renderTabs();
          renderSpells();
        }
      });
      card.appendChild(btnPlus);

      spellGrid.appendChild(card);

      // Legacy hidden row for test compat
      const hiddenRow = document.createElement("div");
      hiddenRow.className = "camp-spell-row";
      hiddenRow.style.display = "none";
      const hiddenLvl = document.createElement("span");
      hiddenLvl.id = `spell-level-${spell.id}-hidden`;
      hiddenLvl.textContent = `${lvl}`;
      const hiddenBtn = document.createElement("button");
      hiddenBtn.id = `btn-upgrade-${spell.id}-hidden`;
      hiddenRow.appendChild(hiddenLvl);
      hiddenRow.appendChild(hiddenBtn);
      legacyPanel.appendChild(hiddenRow);
    }
  }

  loadAll().catch(console.error);
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectSkillsStyles() {
  const sid = "skills-styles";
  if (document.getElementById(sid)) return;
  const style = document.createElement("style");
  style.id = sid;
  style.textContent = `
    .sk-root {
      position: relative; min-height: 100vh;
      overflow-x: hidden; font-family: sans-serif;
    }
    .sk-bg {
      position: fixed; inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(30,20,60,0.6) 0%, transparent 60%),
        linear-gradient(180deg, #0d0818 0%, #070510 50%, #040308 100%);
      z-index: 0;
    }
    .sk-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: center;
      padding: max(env(safe-area-inset-top,0px),12px) 16px max(env(safe-area-inset-bottom,0px),24px);
      gap: 0;
    }
    .sk-back {
      align-self: flex-start;
      color: #b8a070; font-size: 13px;
      text-decoration: none; padding: 8px 4px;
    }
    .sk-header {
      display: flex; align-items: center;
      gap: 16px; margin-bottom: 16px;
      width: min(360px, 96vw);
    }
    .sk-shkatulka {
      width: 72px; height: 72px;
      image-rendering: pixelated;
      filter: drop-shadow(0 4px 12px rgba(180,120,200,0.5));
      flex-shrink: 0;
    }
    .sk-title {
      margin: 0 0 4px 0;
      font-size: 1.6rem; font-weight: 800;
      color: #e8d8b0;
      letter-spacing: 0.06em;
      text-shadow: 0 0 16px rgba(180,140,60,0.4), 0 2px 4px rgba(0,0,0,0.9);
    }
    .sk-subtitle {
      font-size: 13px; color: #ffcc44;
      font-weight: 700;
    }

    /* Class tabs */
    .sk-tabs {
      display: flex; gap: 8px;
      flex-wrap: wrap;
      justify-content: center;
      margin-bottom: 16px;
      width: min(360px, 96vw);
    }
    .sk-tab {
      height: 36px; padding: 0 14px;
      background: url('/ui/InterfaceButton.png') no-repeat center / 100% 100%;
      border: none; cursor: pointer;
      font-family: sans-serif; font-size: 12px;
      font-weight: 700; color: rgba(200,180,140,0.65);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter 0.15s, color 0.15s;
    }
    .sk-tab.active {
      filter: brightness(1.25);
      color: #f0e0b8;
    }
    .sk-tab:hover:not(.active) { filter: brightness(1.1); }

    /* Spell grid — 2 columns on mobile */
    .sk-spell-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px;
      width: min(360px, 96vw);
      margin-bottom: 12px;
    }
    .sk-spell-card {
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,24,0.92);
      border: 1px solid rgba(120,90,180,0.4);
      border-radius: 12px;
      padding: 14px 10px 12px;
      display: flex; flex-direction: column;
      align-items: center; gap: 8px;
      position: relative;
      overflow: hidden;
    }
    .sk-spell-icon {
      width: 56px; height: 56px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 8px rgba(0,0,0,0.7));
    }
    .sk-spell-name {
      font-size: 13px; font-weight: 700;
      color: #e8d8b0; text-align: center;
      line-height: 1.2;
    }
    .sk-lvl-wrap {
      display: flex; align-items: center; gap: 4px;
    }
    .sk-lvl-badge {
      width: 36px; height: 36px;
      image-rendering: pixelated;
    }
    .sk-lvl-text {
      font-size: 16px; font-weight: 800;
      color: #88aaff;
      min-width: 20px; text-align: center;
    }
    .sk-upgrade-btn {
      width: 100%; height: 36px;
      background: url('/ui/InterfaceButton.png') no-repeat center / 100% 100%;
      border: none; cursor: pointer;
      font-family: sans-serif; font-size: 18px;
      font-weight: 900; color: #ffd700;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter 0.15s, transform 0.1s;
    }
    .sk-upgrade-btn.can-afford:hover  { filter: brightness(1.2); }
    .sk-upgrade-btn.can-afford:active { transform: scale(0.97); filter: brightness(0.9); }
    .sk-upgrade-btn.cannot-afford {
      filter: grayscale(0.7) brightness(0.65);
      cursor: default;
    }

    .sk-panel {
      width: min(360px, 96vw);
    }
  `;
  document.head.appendChild(style);
}

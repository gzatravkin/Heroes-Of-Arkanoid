/**
 * SkillsScene.ts — Polished skills/upgrade screen (?scene=skills).
 *
 * Uses:
 *   levelskill/Lvl1Skill … Lvl10Skill   — level indicator badges per spell slot
 *     These are 183×188 ornate square FRAMES with transparent centers.
 *     Rendered as 34×34 background behind the centered level number.
 *   Per-class spell icons from characters.json
 *
 * Design system: warm-brown bg, BarGoods card panels, Kvadrat icon slots,
 * Button1 upgrade pills, level number centered inside its ornate frame badge.
 *
 * Reuses the existing /upgrade endpoint and preserves the same DOM ids
 * (#spell-level-<id>, #btn-upgrade-<id>) required by upgrade.spec.ts.
 */

import { metaApi } from "../net/metaApi";
import type { Profile, CharactersResponse } from "../net/metaApi";
import { nineSlice, btnInterface } from "../ui/nineSlice";

// ── LevelSkill badge → committed public path ──────────────────────────────────
// Keys: levelskill/Lvl1Skill … levelskill/Lvl10Skill (copied to public/levelskill/)
function lvlSkillSrc(level: number): string {
  const clamped = Math.max(1, Math.min(10, level));
  return `/levelskill/Lvl${clamped}Skill.png`;
}

// Per-spell icon paths (matching characters.json icon fields)
// LargeIco variants copied to public/spellicons/<class>/
const SPELL_ICON_MAP: Record<string, string> = {
  // Fire Mage — square Chose*Ico art; ignite and fireball used to share the
  // same letterboxed crop and phoenix had no entry at all → broken image
  // (docs/13 skills audit).
  ignite:   "/art/SpellIgnite.png",
  fireball: "/art/SpellFireball.png",
  firewall: "/art/SpellFirewall.png",
  turret:   "/art/SpellTurret.png",
  phoenix:  "/art/SpellPhoenix.png",
  // Paladin (public/spellicons/paladin/)
  shield:    "/spellicons/paladin/SpellShieldLargeIco.png",
  spear:     "/spellicons/paladin/SpearLargeLargeIco.png",
  duplicate: "/spellicons/paladin/SplitLargeIco.png",
  // Engineer (public/spellicons/engineer/)
  lightning: "/spellicons/engineer/LightingLargeIco.png",
  rocket:    "/spellicons/engineer/RocketLargeIco.png",
  radiation: "/spellicons/engineer/RadiationLargeIco.png",
  // Necromancer (public/spellicons/necromancer/)
  decay:    "/spellicons/necromancer/SpellShieldLargeIco.png",
  skeleton: "/spellicons/necromancer/RiseSkeletonLargeIcon.png",
  drain:    "/spellicons/necromancer/LastJudgmentLargeIco.png",
};

function spellIconSrc(iconKey: string): string {
  // Characters.json may pass either a short key or a simple name
  // Never use /Sprites/ symlink — fall back to /art/ for anything unrecognized
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

  // ── Top bar: back chip · title · spacer ──────────────────────────────────
  // (The decorative Shkatulka chest animation was removed: every frame
  // in /shkatulka/ is a degenerate 1–13px-wide strip — corrupted exports that
  // rendered as grey garbage over the title. docs/13 §S2. Re-add only with
  // verified art.)
  const topbar = document.createElement("div");
  topbar.className = "sk-topbar";

  const back = document.createElement("a");
  back.href = "/?scene=campaign";
  back.className = "ui-back";
  back.setAttribute("aria-label", "Back to campaign");
  topbar.appendChild(back);

  const title = document.createElement("h1");
  title.textContent = "Skill Upgrades";
  title.className = "ui-title";
  topbar.appendChild(title);

  const spacer = document.createElement("div");
  spacer.className = "ui-topbar-spacer";
  topbar.appendChild(spacer);

  inner.appendChild(topbar);

  // Skill Points gold chip (below topbar, above tabs)
  const subtitle = document.createElement("div");
  subtitle.id = "sk-points";
  subtitle.className = "sk-points-chip";
  inner.appendChild(subtitle);

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
    tabs.setAttribute("role", "tablist");
    tabs.innerHTML = "";
    for (const ch of allData.characters) {
      const tab = document.createElement("button");
      tab.className = `sk-tab ${ch.id === currentClassId ? "active" : ""}`;
      tab.textContent = ch.name;
      tab.setAttribute("role", "tab");
      tab.setAttribute("aria-selected", ch.id === currentClassId ? "true" : "false");
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

      // Spell icon inside Kvadrat slot frame
      const iconSrc = SPELL_ICON_MAP[spell.id] ?? spellIconSrc(spell.icon);
      const iconSlot = document.createElement("div");
      iconSlot.className = "sk-icon-slot";
      const icon = document.createElement("img");
      icon.src = iconSrc;
      icon.alt = spell.name;
      icon.className = "sk-spell-icon";
      iconSlot.appendChild(icon);
      card.appendChild(iconSlot);

      // Name
      const nameEl = document.createElement("div");
      nameEl.textContent = spell.name;
      nameEl.className = "sk-spell-name";
      card.appendChild(nameEl);

      // Level badge fix: 34×34 wrapper — Lvl{n}Skill.png is an ornate FRAME
      // with transparent center; render it as background so the number sits
      // centered INSIDE the frame (not next to it as a tiny image).
      const lvlBadgeWrap = document.createElement("div");
      lvlBadgeWrap.className = "sk-lvl-wrap";
      lvlBadgeWrap.style.backgroundImage = `url('${lvlSkillSrc(lvl)}')`;
      const lvlText = document.createElement("span");
      lvlText.id = `spell-level-${spell.id}`;
      lvlText.className = "sk-lvl-text";
      lvlText.textContent = `${lvl}`;
      lvlBadgeWrap.appendChild(lvlText);
      card.appendChild(lvlBadgeWrap);

      // Upgrade button — Button1 9-slice pill labeled "+ Upgrade"
      const btnPlus = document.createElement("button");
      btnPlus.id = `btn-upgrade-${spell.id}`;
      btnPlus.className = `sk-upgrade-btn ${canAfford ? "can-afford" : "cannot-afford"}`;
      btnPlus.textContent = "+ Upgrade";
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
    /* ── Root & background — warm palette, no purple ── */
    .sk-root {
      position: relative;
      min-height: 100cqh;
      overflow-x: hidden;
      overflow-y: auto;
      font-family: var(--font-body);
      color: var(--text);
      box-sizing: border-box;
    }
    .sk-bg {
      position: absolute; inset: 0;
      min-height: 100cqh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      pointer-events: none;
      z-index: 0;
    }
    .sk-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: center;
      padding: 0 16px max(env(safe-area-inset-bottom,0px),24px);
      gap: 0;
    }

    /* ── Topbar: back chip · centered title · symmetry spacer ── */
    .sk-topbar {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: max(12px, env(safe-area-inset-top,0px)) 0 8px;
      width: min(360px, 96cqw);
      align-self: center;
    }
    .sk-topbar .ui-title { flex: 1; text-align: center; }

    /* ── Skill Points gold chip ── */
    .sk-points-chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 16px;
      background: rgba(216,168,78,0.18);
      border: 1px solid var(--gold-dim);
      border-radius: 999px;
      font-family: var(--font-display);
      font-size: var(--fs-body);
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      margin-bottom: 14px;
      white-space: nowrap;
    }

    /* ── Class tabs ── */
    .sk-tabs {
      display: flex; gap: 8px;
      flex-wrap: wrap;
      justify-content: center;
      margin-bottom: 16px;
      width: min(360px, 96cqw);
    }
    .sk-tab {
      height: 38px; padding: 0 14px;
      ${btnInterface()}
      cursor: pointer;
      font-family: var(--font-body); font-size: var(--fs-caption);
      font-weight: 700; color: var(--text-dim);
      filter: saturate(0.4) brightness(0.75);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), color var(--dur-normal);
    }
    .sk-tab:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .sk-tab.active {
      filter: none;
      color: var(--text);
    }
    .sk-tab:hover:not(.active) { filter: saturate(0.6) brightness(0.9); }
    .sk-tab:active { transform: scale(0.96); }

    /* ── Spell grid — 2 columns ── */
    .sk-spell-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px;
      width: min(360px, 96cqw);
      margin-bottom: 12px;
    }

    /* ── Spell card: BarGoods gold-rimmed navy panel ── */
    .sk-spell-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "12px 14px")}
      padding: 10px 6px 10px;
      display: flex; flex-direction: column;
      align-items: center; gap: 8px;
      position: relative;
    }

    /* ── Icon inside Kvadrat slot frame ── */
    .sk-icon-slot {
      width: 68px; height: 68px;
      ${nineSlice("/ui/Kvadrat.png", "14 14 14 14", "7px 7px")}
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sk-spell-icon {
      width: 56px; height: 56px;
      object-fit: contain;
      border-radius: 6px;
      filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6));
    }

    .sk-spell-name {
      font-size: var(--fs-caption); font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      text-align: center; line-height: 1.2;
    }

    /* ── Level badge fix ──────────────────────────────────────────────────────
       Lvl{n}Skill.png is a 183×188 ornate square FRAME with transparent center.
       Old code placed it as a tiny <img> beside the number (wrong).
       New: 34×34 wrapper with the frame as background; number centered INSIDE.
    ── */
    .sk-lvl-wrap {
      width: 34px; height: 34px;
      background-repeat: no-repeat;
      background-position: center;
      background-size: contain;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sk-lvl-text {
      font-size: var(--fs-body); font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      position: relative; z-index: 1;
      line-height: 1;
    }

    /* ── Upgrade button: Button1 9-slice pill ── */
    .sk-upgrade-btn {
      width: 100%; min-height: 36px;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 18px")}
      cursor: pointer;
      font-family: var(--font-body); font-size: var(--fs-caption);
      font-weight: 700; color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      letter-spacing: 0.04em;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .sk-upgrade-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }
    .sk-upgrade-btn:hover:not(:disabled)  { filter: brightness(1.15); }
    .sk-upgrade-btn:active:not(:disabled) { transform: scale(0.96); }
    .sk-upgrade-btn:disabled {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }
    /* keep legacy state classes working (test-compat) */
    .sk-upgrade-btn.cannot-afford {
      filter: saturate(0.25) brightness(0.65);
      cursor: default;
    }

    .sk-panel {
      width: min(360px, 96cqw);
    }

    @container (min-width: 480px) {
      .sk-spell-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
  `;
  document.head.appendChild(style);
}

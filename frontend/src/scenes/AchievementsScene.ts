/**
 * AchievementsScene.ts — Achievements screen (?scene=achievements).
 *
 * Uses Interface/Achivements art:
 *   AchievmentPanel (background panel)
 *   achievementLvl1–3, achievementLl4–5 (locked badge frames)
 *   achievementLvl1Eng–Lvl3Eng, Ll4Eng, Ll5Eng (unlocked badge frames)
 *   achievementLvl3Oro (gold variant for top tier)
 *
 * Achievement definitions are client-side; server persists the unlocked set.
 * Unlocks are triggered from battle/campaign events and POSTed to /achievement/unlock.
 */

import { metaApi } from "../net/metaApi";

// ── Achievement definitions ───────────────────────────────────────────────────

export interface AchievementDef {
  id: string;
  name: string;
  description: string;
  tier: 1 | 2 | 3 | 4 | 5;  // maps to Lvl1–5 badge art
}

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: "first_win",           name: "First Victory",       description: "Win your first battle.", tier: 1 },
  { id: "equip_item",          name: "Geared Up",           description: "Equip an item.", tier: 1 },
  { id: "clear_biome_hell",    name: "Hell Survivor",        description: "Clear a Hell biome level.", tier: 2 },
  { id: "clear_biome_dungeon", name: "Dungeon Crawler",      description: "Clear a Dungeon biome level.", tier: 2 },
  { id: "clear_biome_village", name: "Village Cleared",      description: "Clear a Village biome level.", tier: 2 },
  { id: "clear_biome_heaven",  name: "Ascended",             description: "Clear a Heaven biome level.", tier: 3 },
  { id: "beat_boss",           name: "Boss Slayer",          description: "Defeat a boss.", tier: 3 },
  { id: "clear_dungeon",       name: "Dungeon Delver",       description: "Complete a dungeon run.", tier: 4 },
  { id: "win_fire_mage",       name: "Pyromancer",           description: "Win a battle as Fire Mage.", tier: 2 },
  { id: "win_paladin",         name: "Holy Knight",          description: "Win a battle as Paladin.", tier: 2 },
  { id: "win_engineer",        name: "Tech Master",          description: "Win a battle as Engineer.", tier: 2 },
  { id: "win_necromancer",     name: "Undying",              description: "Win a battle as Necromancer.", tier: 2 },
  { id: "campaign_complete",   name: "World Saved",          description: "Complete all campaign levels.", tier: 5 },
];

// ── Badge art mapping: tier → locked/unlocked image ──────────────────────────
// Files copied to public/achievements/ (committed, no /Sprites/ symlink dependency).

function badgeSrc(tier: 1 | 2 | 3 | 4 | 5, _unlocked: boolean): string {
  // Always use the English badge art (the non-Eng variants have Russian text baked in).
  // Locked badges are conveyed by dimming the card, not by a different (Russian) sprite.
  if (tier === 1) return "/achievements/achievementLvl1Eng.png";
  if (tier === 2) return "/achievements/achievementLvl2Eng.png";
  if (tier === 3) return "/achievements/achievementLvl3Oro.png";
  if (tier === 4) return "/achievements/achievementLl4Eng.png";
  return "/achievements/achievementLl5Eng.png";
}

// ── Mount ─────────────────────────────────────────────────────────────────────

export function mountAchievements(host: HTMLElement) {
  injectAchievementStyles();

  const root = document.createElement("div");
  root.id = "achievements-scene";
  root.className = "ach-root";

  const bg = document.createElement("div");
  bg.className = "ach-bg";
  root.appendChild(bg);

  const inner = document.createElement("div");
  inner.className = "ach-inner";

  // Back button
  const backBtn = document.createElement("a");
  backBtn.href = "/?scene=menu";
  backBtn.className = "ach-back";
  backBtn.textContent = "← Menu";
  inner.appendChild(backBtn);

  // Title using AchievmentPanel as header decoration
  const header = document.createElement("div");
  header.className = "ach-header";
  const panelDeco = document.createElement("div");
  panelDeco.className = "ach-panel-deco";
  header.appendChild(panelDeco);
  const title = document.createElement("h1");
  title.textContent = "Achievements";
  title.className = "ach-title";
  header.appendChild(title);
  inner.appendChild(header);

  // Progress summary
  const summary = document.createElement("div");
  summary.id = "ach-summary";
  summary.className = "ach-summary";
  inner.appendChild(summary);

  // Achievement grid
  const grid = document.createElement("div");
  grid.id = "ach-grid";
  grid.className = "ach-grid";
  inner.appendChild(grid);

  root.appendChild(inner);
  host.appendChild(root);

  async function render() {
    const profile = await metaApi.getProfile();
    const unlocked = new Set(profile.achievements ?? []);

    const unlockedCount = ACHIEVEMENTS.filter(a => unlocked.has(a.id)).length;
    summary.textContent = `${unlockedCount} / ${ACHIEVEMENTS.length} unlocked`;

    grid.innerHTML = "";
    for (const ach of ACHIEVEMENTS) {
      const isUnlocked = unlocked.has(ach.id);
      const card = document.createElement("div");
      card.setAttribute("data-achievement", ach.id);
      card.className = `ach-card ${isUnlocked ? "unlocked" : "locked"}`;

      const badge = document.createElement("img");
      badge.src = badgeSrc(ach.tier, isUnlocked);
      badge.alt = ach.name;
      badge.className = "ach-badge";
      card.appendChild(badge);

      const nameEl = document.createElement("div");
      nameEl.textContent = ach.name;
      nameEl.className = "ach-name";
      card.appendChild(nameEl);

      const descEl = document.createElement("div");
      descEl.textContent = isUnlocked ? ach.description : "???";
      descEl.className = "ach-desc";
      card.appendChild(descEl);

      grid.appendChild(card);
    }
  }

  render().catch(console.error);
}

// ── Toast helper (exported for use in battle / campaign flow) ─────────────────

export async function unlockAchievement(id: string): Promise<void> {
  try {
    const result = await metaApi.unlockAchievement(id);
    if (result.ok && result.achievements.includes(id)) {
      // Check it was newly added (not already there)
      showAchievementToast(id);
    }
  } catch { /* non-fatal */ }
}

function showAchievementToast(id: string) {
  const ach = ACHIEVEMENTS.find(a => a.id === id);
  if (!ach) return;

  const toast = document.createElement("div");
  toast.className = "ach-toast";
  toast.innerHTML = `
    <img src="${badgeSrc(ach.tier, true)}" class="ach-toast-badge" alt="">
    <div class="ach-toast-text">
      <div class="ach-toast-label">Achievement Unlocked!</div>
      <div class="ach-toast-name">${ach.name}</div>
    </div>
  `;

  injectToastStyles();
  document.body.appendChild(toast);

  // Animate in, hold, animate out
  requestAnimationFrame(() => {
    toast.classList.add("ach-toast-in");
    setTimeout(() => {
      toast.classList.add("ach-toast-out");
      toast.addEventListener("transitionend", () => toast.remove(), { once: true });
    }, 3200);
  });
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectAchievementStyles() {
  const id = "achievement-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .ach-root {
      position: relative;
      min-height: 100vh;
      overflow-x: hidden;
      font-family: sans-serif;
    }
    .ach-bg {
      position: fixed; inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(60,40,15,0.55) 0%, transparent 60%),
        linear-gradient(180deg, #120d04 0%, #090607 50%, #040308 100%);
      z-index: 0;
    }
    .ach-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: center;
      padding: max(env(safe-area-inset-top,0px),16px) 16px max(env(safe-area-inset-bottom,0px),24px);
      gap: 0;
    }
    .ach-back {
      align-self: flex-start;
      color: #b8a070; font-size: 13px;
      text-decoration: none; padding: 8px 4px;
    }
    .ach-header {
      position: relative;
      width: min(360px, 96vw);
      display: flex; flex-direction: column;
      align-items: center; margin-bottom: 8px;
    }
    .ach-panel-deco {
      position: absolute; inset: 0;
      background: url('/achievements/AchievmentPanel.png') no-repeat center / contain;
      opacity: 0.3; pointer-events: none;
    }
    .ach-title {
      position: relative; z-index: 1;
      margin: 12px 0 16px 0;
      font-size: 1.9rem;
      font-weight: 800;
      color: #ffd700;
      letter-spacing: 0.08em;
      text-shadow: 0 0 20px rgba(255,200,0,0.4), 0 2px 5px rgba(0,0,0,0.9);
    }
    .ach-summary {
      margin-bottom: 16px;
      color: #aabbcc;
      font-size: 13px;
      letter-spacing: 0.04em;
    }
    .ach-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px;
      width: min(360px, 96vw);
      padding-bottom: 24px;
    }
    .ach-card {
      background: rgba(10,8,20,0.85);
      border: 1px solid rgba(100,80,160,0.3);
      border-radius: 10px;
      padding: 12px 10px;
      display: flex; flex-direction: column;
      align-items: center; gap: 6px;
      transition: transform 0.12s, border-color 0.15s;
    }
    .ach-card.unlocked {
      border-color: rgba(200,160,50,0.65);
      background: rgba(20,15,5,0.9);
      box-shadow: 0 0 12px rgba(200,160,50,0.15);
    }
    .ach-card.locked {
      opacity: 0.5;
    }
    .ach-badge {
      width: 64px; height: 64px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }
    .ach-card.locked .ach-badge {
      filter: grayscale(0.8) drop-shadow(0 2px 4px rgba(0,0,0,0.6));
    }
    .ach-name {
      font-size: 12px; font-weight: 700;
      color: #e0d0a0;
      text-align: center;
      line-height: 1.3;
    }
    .ach-card.locked .ach-name { color: #7788aa; }
    .ach-desc {
      font-size: 10px; color: #8899bb;
      text-align: center; line-height: 1.4;
    }
  `;
  document.head.appendChild(style);
}

function injectToastStyles() {
  const id = "ach-toast-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .ach-toast {
      position: fixed;
      top: max(env(safe-area-inset-top,0px), 20px);
      left: 50%;
      transform: translateX(-50%) translateY(-80px);
      background: rgba(15,10,30,0.97);
      border: 1px solid rgba(220,180,60,0.7);
      border-radius: 12px;
      padding: 12px 20px;
      display: flex; align-items: center; gap: 12px;
      min-width: min(280px, 85vw);
      max-width: min(340px, 90vw);
      z-index: 9999;
      box-shadow: 0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(220,180,60,0.2);
      transition: transform 0.35s cubic-bezier(0.2,1,0.4,1), opacity 0.35s;
      opacity: 0;
      font-family: sans-serif;
    }
    .ach-toast-in {
      transform: translateX(-50%) translateY(0);
      opacity: 1;
    }
    .ach-toast-out {
      transform: translateX(-50%) translateY(-80px);
      opacity: 0;
    }
    .ach-toast-badge {
      width: 44px; height: 44px;
      image-rendering: pixelated;
      flex-shrink: 0;
    }
    .ach-toast-label {
      font-size: 10px; color: #ffd700;
      letter-spacing: 0.06em; font-weight: 700;
      text-transform: uppercase;
    }
    .ach-toast-name {
      font-size: 14px; color: #f0e0b8;
      font-weight: 700; margin-top: 2px;
    }
  `;
  document.head.appendChild(style);
}

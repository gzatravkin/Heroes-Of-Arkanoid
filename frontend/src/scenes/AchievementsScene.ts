/**
 * AchievementsScene.ts — Achievements screen (?scene=achievements).
 *
 * Badge art in public/achievements/:
 *   achievementLvl1Eng–Lvl3Eng, Ll4Eng, Ll5Eng (badge frames, English)
 *   achievementLvl3Oro (gold variant for top tier)
 *
 * NOTE: /achievements/AchievmentPanel.png is a flat navy rounded rectangle
 * (placeholder export — no painted detail). It is NOT rendered here per
 * Rulebook §4 (docs/plans/2026-06-10-ui-overhaul-execution.md).
 * The NameBlock plaque (BarGoods 9-slice) is used for the title area instead.
 *
 * Achievement definitions are client-side; server persists the unlocked set.
 * Unlocks are triggered from battle/campaign events and POSTed to /achievement/unlock.
 *
 * Styled to match the design system: warm bg gradient, BarGoods card panels,
 * locked/unlocked visual rhythm (unlocked: full-color + gold name;
 * locked: saturate(.45) brightness(.8), text-dim name, ??? desc).
 */

import { metaApi } from "../net/metaApi";
import { nineSlice } from "../ui/nineSlice";

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

// ── Badge art mapping: tier → unlocked image ─────────────────────────────────
// Always the English badge art (non-Eng variants have Russian text baked in).
// Locked/unlocked visual state is conveyed via CSS filter, not a different sprite.

function badgeSrc(tier: 1 | 2 | 3 | 4 | 5, _unlocked: boolean): string {
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

  // Warm background layer
  const bg = document.createElement("div");
  bg.className = "ach-bg";
  root.appendChild(bg);

  const inner = document.createElement("div");
  inner.className = "ach-inner";

  // ── Top bar: back chip · centered title · symmetry spacer ──
  const topbar = document.createElement("div");
  topbar.className = "ach-topbar";

  const backBtn = document.createElement("a");
  backBtn.href = "/?scene=menu";
  backBtn.className = "ach-back";
  backBtn.setAttribute("aria-label", "Back to menu");
  // Arrow rendered by ::before pseudo-element
  topbar.appendChild(backBtn);

  const title = document.createElement("h1");
  title.textContent = "Achievements";
  title.className = "ach-title";
  topbar.appendChild(title);

  const spacer = document.createElement("div");
  spacer.className = "ach-topbar-spacer";
  topbar.appendChild(spacer);

  inner.appendChild(topbar);

  // Progress counter
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

      // Tier data model has only numeric tier (1–5), no tier-name string.
      // Tier chip is intentionally omitted — do NOT parse from filenames (§A3).

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
    /* ── Screen scaffold ── */
    .ach-root {
      position: relative;
      min-height: 100cqh;
      width: 100%;
      color: var(--text);
      font-family: var(--font-body);
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      overflow-x: hidden;
    }
    .ach-bg {
      position: absolute; inset: 0;
      min-height: 100cqh;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
      pointer-events: none;
      z-index: 0;
    }
    .ach-inner {
      position: relative; z-index: 1;
      display: flex; flex-direction: column;
      align-items: stretch;
      padding: 0 0 max(env(safe-area-inset-bottom,0px),24px);
    }

    /* ── Top bar: back chip · centered title · symmetry spacer ── */
    .ach-topbar {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: max(12px, env(safe-area-inset-top, 0px)) 12px 8px 12px;
      width: 100%;
      box-sizing: border-box;
    }

    /* Back chip — Button1 9-slice frame with BackArrow via ::before */
    .ach-back {
      flex: none;
      width: 44px;
      height: 44px;
      padding: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
      cursor: pointer;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter 0.15s, transform 0.1s;
      ${nineSlice("/ui/Button1.png", "24 60 24 60", "8px 14px")}
    }
    .ach-back::before {
      content: "";
      width: 20px;
      height: 20px;
      background: url('/ui/BackArrow.png') no-repeat center / contain;
      filter: drop-shadow(0 1px 2px rgba(0,0,0,0.8));
    }
    .ach-back:hover  { filter: brightness(1.18); }
    .ach-back:active { transform: scale(0.94); }

    /* Centered display title */
    .ach-title {
      flex: 1;
      text-align: center;
      margin: 0;
      font-family: var(--font-display);
      font-size: var(--fs-title);
      font-weight: 700;
      letter-spacing: 0.05em;
      color: var(--gold-bright);
      text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
    }

    /* Symmetry spacer keeps title visually centered */
    .ach-topbar-spacer {
      width: 44px;
      flex: none;
    }

    /* Progress counter */
    .ach-summary {
      text-align: center;
      color: var(--text-dim);
      font-size: 13px;
      letter-spacing: 0.04em;
      margin-bottom: 14px;
      padding: 0 16px;
    }

    /* ── Achievement grid (2-col) ── */
    .ach-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 12px;
      width: min(360px, 96cqw);
      padding-bottom: 24px;
      align-self: center;
    }

    /* ── Achievement card: BarGoods gold-rimmed navy panel ── */
    .ach-card {
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 15px")}
      padding: 10px 8px 10px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      position: relative;
      transition: filter 0.15s, transform 0.12s;
    }

    /* Unlocked: full color + gold glow */
    .ach-card.unlocked {
      filter: drop-shadow(0 0 7px rgba(255,190,80,0.35));
    }
    .ach-card.unlocked:hover {
      filter: drop-shadow(0 0 10px rgba(255,190,80,0.55)) brightness(1.08);
    }
    .ach-card.unlocked:active {
      transform: scale(0.96);
    }

    /* Locked: readable but clearly unowned — NOT blacked out (docs/13, Rulebook §5) */
    .ach-card.locked {
      filter: none;
    }
    .ach-card.locked:hover {
      filter: brightness(1.06);
    }

    /* ── Badge art (medal) ── */
    .ach-badge {
      width: 60px;
      height: 60px;
      object-fit: contain;
      /* painted art — NEVER image-rendering: pixelated (Rulebook §4) */
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
    }
    /* Locked badge: desaturated ~50%, never blacked out */
    .ach-card.locked .ach-badge {
      filter: saturate(0.45) brightness(0.8) drop-shadow(0 2px 4px rgba(0,0,0,0.6));
    }

    /* ── Text ── */
    .ach-name {
      font-size: 12px;
      font-weight: 700;
      color: var(--gold-bright);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
      text-align: center;
      line-height: 1.3;
    }
    .ach-card.locked .ach-name {
      color: var(--text-dim);
      text-shadow: none;
    }
    .ach-desc {
      font-size: 10px;
      color: var(--text-dim);
      text-align: center;
      line-height: 1.4;
    }
    .ach-card.locked .ach-desc {
      color: var(--text-faint);
    }

    /* ── Wider layout on larger containers ── */
    @container (min-width: 480px) {
      .ach-grid {
        grid-template-columns: repeat(3, 1fr);
      }
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
    /* Toast: BarGoods panel, position: fixed intentional (body-appended overlay) */
    .ach-toast {
      position: fixed;
      top: max(env(safe-area-inset-top,0px), 20px);
      left: 50%;
      transform: translateX(-50%) translateY(-80px);
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 20px")}
      padding: 10px 16px;
      display: flex;
      align-items: center;
      gap: 12px;
      min-width: min(280px, 85cqw);
      max-width: min(340px, 90cqw);
      z-index: 9999;
      box-shadow: 0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(220,180,60,0.25);
      transition: transform 0.35s cubic-bezier(0.2,1,0.4,1), opacity 0.35s;
      opacity: 0;
      font-family: var(--font-body);
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
      width: 44px;
      height: 44px;
      object-fit: contain;
      flex-shrink: 0;
    }
    .ach-toast-label {
      font-size: 10px;
      color: var(--gold-bright);
      letter-spacing: 0.06em;
      font-weight: 700;
      text-transform: uppercase;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .ach-toast-name {
      font-size: 14px;
      color: var(--text);
      font-weight: 700;
      margin-top: 2px;
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
  `;
  document.head.appendChild(style);
}

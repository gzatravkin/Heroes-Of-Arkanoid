import { wasmApi as metaApi } from "../net/WasmApi";
import { nineSlice } from "../ui/nineSlice";

// ── Achievement definitions ───────────────────────────────────────────────────

export interface AchievementDef {
  id: string;
  name: string;
  description: string;   // flavour text, shown once unlocked
  criteria: string;      // plain how-to-earn, shown while still locked
  tier: 1 | 2 | 3 | 4 | 5;
}

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: "first_win",           name: "First Victory",       description: "The first brick falls. Hell noticed.", criteria: "Win any level", tier: 1 },
  { id: "equip_item",          name: "Geared Up",           description: "Iron does not grant power; it confesses the need for it.", criteria: "Equip an item from your inventory", tier: 1 },
  { id: "clear_biome_hell",    name: "Hell Survivor",        description: "Three floors of fire, and you came back changed.", criteria: "Win a level in Hell", tier: 2 },
  { id: "clear_biome_dungeon", name: "Dungeon Crawler",      description: "The descent is voluntary. The return is not guaranteed.", criteria: "Win a level in the Caverns", tier: 2 },
  { id: "clear_biome_village", name: "Village Cleared",      description: "The village prayed for a savior; it got you instead.", criteria: "Win a level in the Village", tier: 2 },
  { id: "clear_biome_heaven",  name: "Ascended",             description: "The angels did not welcome you. They stepped aside.", criteria: "Win a level in Heaven", tier: 3 },
  { id: "beat_boss",           name: "Boss Slayer",          description: "Demons have names. You took one.", criteria: "Defeat any boss", tier: 3 },
  { id: "clear_dungeon",       name: "Dungeon Delver",       description: "The stones remember every screaming soul; yours merely survived.", criteria: "Complete a full Dungeon (rift) run", tier: 4 },
  { id: "win_fire_mage",       name: "Pyromancer",           description: "The fire answered your call before you knew its name.", criteria: "Win a level as the Fire Mage", tier: 2 },
  { id: "win_paladin",         name: "Holy Knight",          description: "The blessing was freely given; the mercy was not.", criteria: "Win a level as the Paladin", tier: 2 },
  { id: "win_engineer",        name: "Tech Master",          description: "Where others prayed, you calculated—and the machine did not disappoint.", criteria: "Win a level as the Engineer", tier: 2 },
  { id: "win_necromancer",     name: "Undying",              description: "Death studied you closely, learned nothing, and moved on.", criteria: "Win a level as the Necromancer", tier: 2 },
  { id: "campaign_complete",   name: "World Saved",          description: "The world survives—scarred, grateful, and afraid of what it owes you.", criteria: "Defeat the final boss in Heaven", tier: 5 },
];

export function badgeSrc(tier: 1 | 2 | 3 | 4 | 5, _unlocked: boolean): string {
  if (tier === 1) return "/achievements/achievementLvl1Eng.png";
  if (tier === 2) return "/achievements/achievementLvl2Eng.png";
  if (tier === 3) return "/achievements/achievementLvl3Oro.png";
  if (tier === 4) return "/achievements/achievementLl4Eng.png";
  return "/achievements/achievementLl5Eng.png";
}

// ── Toast helper (exported for use in battle / campaign flow) ─────────────────

export async function unlockAchievement(id: string): Promise<void> {
  try {
    const result = await metaApi.unlockAchievement(id);
    if (result.ok && result.achievements.includes(id)) {
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

  requestAnimationFrame(() => {
    toast.classList.add("ach-toast-in");
    setTimeout(() => {
      toast.classList.add("ach-toast-out");
      toast.addEventListener("transitionend", () => toast.remove(), { once: true });
    }, 3200);
  });
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
      ${nineSlice("/ui/BarGoods.png", "26 30 26 30", "13px 20px")}
      padding: var(--sp-2h) var(--sp-4);
      display: flex; align-items: center; gap: var(--sp-3);
      min-width: min(280px, 85cqw); max-width: min(340px, 90cqw);
      z-index: 9999;
      box-shadow: 0 4px 20px rgba(0,0,0,0.7), 0 0 12px rgba(220,180,60,0.25);
      transition: transform 0.35s cubic-bezier(0.2,1,0.4,1), opacity 0.35s;
      opacity: 0; font-family: var(--font-body);
    }
    .ach-toast-in  { transform: translateX(-50%) translateY(0); opacity: 1; }
    .ach-toast-out { transform: translateX(-50%) translateY(-80px); opacity: 0; }
    .ach-toast-badge { width: 44px; height: 44px; object-fit: contain; flex-shrink: 0; }
    .ach-toast-label {
      font-size: var(--fs-tiny); color: var(--gold-bright); letter-spacing: 0.06em;
      font-weight: 700; text-transform: uppercase; text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .ach-toast-name {
      font-size: var(--fs-subhead); color: var(--text); font-weight: 700;
      margin-top: 2px; text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
  `;
  document.head.appendChild(style);
}

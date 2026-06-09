import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import { buildRewardOverlay, buildDefeatOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";
import { unlockAchievement } from "../AchievementsScene";

export function createCampaignFlow(level: string) {
  let completeCalled = false;
  let overlayShown = false;

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (overlayShown) return true;

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      let reward = null;
      try {
        const data = await metaApi.complete(level, s.treasureBonus ?? 0);
        reward = data.reward;
        // Unlock achievements for this win
        await unlockAchievement("first_win");
        if (level.startsWith("hell"))    await unlockAchievement("clear_biome_hell");
        if (level.startsWith("cavern"))  await unlockAchievement("clear_biome_dungeon");
        if (level.startsWith("village")) await unlockAchievement("clear_biome_village");
        if (level.startsWith("heaven"))  await unlockAchievement("clear_biome_heaven");
        // Per-class win achievements
        try {
          const chars = await metaApi.getCharacters();
          const cls = chars.selected;
          if (cls === "fire_mage")   await unlockAchievement("win_fire_mage");
          if (cls === "paladin")     await unlockAchievement("win_paladin");
          if (cls === "engineer")    await unlockAchievement("win_engineer");
          if (cls === "necromancer") await unlockAchievement("win_necromancer");
        } catch { /* non-fatal */ }
      } catch (e) {
        console.error("Failed to complete level", e);
      }
      const el = buildRewardOverlay(reward, () => { navigateTo("/?scene=campaign"); });
      document.body.appendChild(el);
      return true;
    }

    if (s.phase === "Lost") {
      overlayShown = true;
      const el = buildDefeatOverlay(
        () => { navigateTo(`/?scene=battle&level=${encodeURIComponent(level)}&from=campaign`); },
        () => { navigateTo("/?scene=campaign"); },
      );
      document.body.appendChild(el);
      return true;
    }

    return false;
  }

  return { handlePhase };
}

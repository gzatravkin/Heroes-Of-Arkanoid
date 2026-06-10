import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import type { RiftMode, RiftOffer } from "../../net/metaApi";
import { buildRewardOverlay, buildDefeatOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";
import { unlockAchievement } from "../AchievementsScene";
import { log } from "../../log";

/** Where to return after the reward overlay — to the rift offer if one opened. */
function campaignReturnUrl(rift: RiftOffer | null): string {
  if (rift?.opened) {
    return `/?scene=campaign&rift=${encodeURIComponent(rift.dungeonId)}`
         + `&riftFloors=${rift.floors}&riftName=${encodeURIComponent(rift.name)}`;
  }
  return "/?scene=campaign";
}

export function createCampaignFlow(level: string) {
  let completeCalled = false;
  let overlayShown = false;

  // Tests can force/suppress rifts deterministically via localStorage; players roll.
  const riftMode = ((): RiftMode => {
    const m = (typeof localStorage !== "undefined" && localStorage.getItem("ark_rift_mode")) || "roll";
    return (m === "force" || m === "none") ? m : "roll";
  })();

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (overlayShown) return true;

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      let reward = null;
      let rift: RiftOffer | null = null;
      try {
        const data = await metaApi.complete(level, s.treasureBonus ?? 0, riftMode);
        reward = data.reward;
        rift = data.rift;
        log("rift", rift?.opened ? "offered" : "none", rift ?? { level });
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
      const el = buildRewardOverlay(reward, () => { navigateTo(campaignReturnUrl(rift)); });
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    if (s.phase === "Lost") {
      overlayShown = true;
      const el = buildDefeatOverlay(
        () => { navigateTo(`/?scene=battle&level=${encodeURIComponent(level)}&from=campaign`); },
        () => { navigateTo("/?scene=campaign"); },
      );
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    return false;
  }

  return { handlePhase };
}

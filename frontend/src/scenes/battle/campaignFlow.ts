import type { Snapshot } from "../../net/Connection";
import { wasmApi as metaApi } from "../../net/WasmApi";
import type { RiftMode, RiftOffer, CompleteResult } from "../../net/metaApi";
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
  // Track whether a boss entity ever appeared this level — bossActive flips back
  // to false the instant the boss dies (which is the same tick we reach "Won"),
  // so we must latch it rather than read it at the win.
  let sawBoss = false;

  // Tests can force/suppress rifts deterministically via localStorage; players roll.
  const riftMode = ((): RiftMode => {
    const m = (typeof localStorage !== "undefined" && localStorage.getItem("ark_rift_mode")) || "roll";
    return (m === "force" || m === "none") ? m : "roll";
  })();

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (s.bossActive) sawBoss = true;
    if (overlayShown) return true;

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      let reward = null;
      let rift: RiftOffer | null = null;
      let heroXp: CompleteResult["heroXp"] | undefined;
      try {
        const data = await metaApi.complete(level, s.crystalBonus ?? 0, riftMode, s.bricksDestroyedThisLevel ?? 0);
        reward = data.reward;
        rift = data.rift;
        heroXp = data.heroXp;
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
        // Boss + campaign-finale achievements (were defined but never wired).
        if (sawBoss)                 await unlockAchievement("beat_boss");
        if (level === "heaven-boss") await unlockAchievement("campaign_complete");
      } catch (e) {
        console.error("Failed to complete level", e);
      }
      const el = buildRewardOverlay(reward, () => { navigateTo(campaignReturnUrl(rift)); }, heroXp);
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    if (s.phase === "Lost") {
      overlayShown = true;
      // Hero XP (§5.3): even a lost battle credits the hero for blocks destroyed (no win bonus).
      metaApi.heroXp(s.bricksDestroyedThisLevel ?? 0, false).catch(() => {});
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

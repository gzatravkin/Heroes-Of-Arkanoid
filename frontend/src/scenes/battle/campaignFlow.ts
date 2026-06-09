import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import { buildRewardOverlay, buildDefeatOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";

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

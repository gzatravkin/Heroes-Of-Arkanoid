import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import { buildRewardOverlay, buildDefeatOverlay } from "./overlays";

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
        const data = await metaApi.complete(level);
        reward = data.reward;
      } catch (e) {
        console.error("Failed to complete level", e);
      }
      const el = buildRewardOverlay(reward, () => { location.href = "/?scene=campaign"; });
      document.body.appendChild(el);
      return true;
    }

    if (s.phase === "Lost") {
      overlayShown = true;
      const el = buildDefeatOverlay(
        () => { location.href = `/?scene=battle&level=${encodeURIComponent(level)}&from=campaign`; },
        () => { location.href = "/?scene=campaign"; },
      );
      document.body.appendChild(el);
      return true;
    }

    return false;
  }

  return { handlePhase };
}

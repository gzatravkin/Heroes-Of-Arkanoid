import type { Snapshot } from "../../net/Connection";
import { metaApi } from "../../net/metaApi";
import { buildPickOverlay, buildDungeonClearOverlay, buildDungeonFailOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";

export function createDungeonFlow() {
  let completeCalled = false;
  let overlayShown = false;

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (overlayShown) return true;

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      try {
        const data = await metaApi.floorCleared();
        if (data.isLastFloor) {
          const el = buildDungeonClearOverlay(data, () => { navigateTo("/?scene=dungeons"); });
          document.body.appendChild(el);
        } else {
          const el = buildPickOverlay(
            data.run?.pendingChoices ?? [],
            async (choiceId) => {
              try {
                await metaApi.pick(choiceId);
                navigateTo("/?scene=dungeon");
              } catch (e) {
                console.error("dungeon pick failed", e);
              }
            },
          );
          document.body.appendChild(el);
        }
      } catch (e) {
        console.error("dungeon floor-cleared failed", e);
      }
      return true;
    }

    if (s.phase === "Lost" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      try {
        await metaApi.fail();
      } catch (e) {
        console.error("dungeon fail failed", e);
      }
      const el = buildDungeonFailOverlay(() => { navigateTo("/?scene=dungeons"); });
      document.body.appendChild(el);
      return true;
    }

    return false;
  }

  return { handlePhase };
}

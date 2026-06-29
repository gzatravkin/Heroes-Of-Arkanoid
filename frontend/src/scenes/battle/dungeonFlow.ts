import type { Connection, Snapshot } from "../../net/Connection";
import { wasmApi as metaApi } from "../../net/WasmApi";
import { buildPickOverlay, buildDungeonClearOverlay, buildDungeonFailOverlay, buildShopOverlay, buildRiftRewardCard, buildRiftDraftOverlay } from "./overlays";
import { navigateTo } from "../../ui/transition";
import { unlockAchievement } from "../AchievementsScene";

export function createDungeonFlow(conn?: Connection | null) {
  let completeCalled = false;
  let overlayShown = false;
  let sawBoss = false; // latched — bossActive clears the tick the boss dies (= "Won").
  let draftEl: HTMLElement | null = null; // the active §8 mid-rift draft overlay, if any

  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (s.bossActive) sawBoss = true;
    if (overlayShown) return true;

    // ── Continuous Rift (2026-06-16): the whole rift is ONE battle (floors slide in via the sim), so
    //    there are NO per-floor reloads — a §8 boon draft between floors, then one end reward card. ──
    if (s.isRift) {
      // §8 mid-floor draft: the sim is frozen server-side; show the boon picker once.
      if (s.awaitingDraft) {
        if (!draftEl) {
          draftEl = buildRiftDraftOverlay(s.draftChoices ?? [], (id) => { conn?.riftPick(id); });
          (document.getElementById("app") ?? document.body).appendChild(draftEl);
        }
        return true;
      } else if (draftEl) {
        draftEl.remove();
        draftEl = null;
      }

      if ((s.phase === "Won" || s.phase === "Lost") && !completeCalled) {
        completeCalled = true;
        overlayShown = true;
        const won = s.phase === "Won";
        if (won) unlockAchievement("clear_dungeon").catch(() => {});
        // depth = floors fully cleared (a win clears all; a loss leaves the current floor uncleared).
        const depth = won ? (s.floor ?? 1) : Math.max(0, (s.floor ?? 1) - 1);
        try {
          const data = await metaApi.riftFinish(depth, won, s.bricksDestroyedThisLevel ?? 0);
          const el = buildRiftRewardCard(data, () => navigateTo("/?scene=campaign"));
          (document.getElementById("app") ?? document.body).appendChild(el);
        } catch (e) {
          console.error("rift finish failed", e);
          navigateTo("/?scene=campaign");
        }
        return true;
      }
      return false;
    }

    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      if (sawBoss) unlockAchievement("beat_boss").catch(() => {});
      try {
        // Carry the remaining HP + Gold into the next floor (docs/04 §6.2 permadeath, §5 Gold).
        const data = await metaApi.floorCleared(s.hp, s.gold ?? 0);
        if (data.isLastFloor) {
          unlockAchievement("clear_dungeon").catch(() => {});
          const el = buildDungeonClearOverlay(data, () => { navigateTo("/?scene=campaign"); });
          (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
        } else {
          const el = buildPickOverlay(
            data.run?.pendingChoices ?? [],
            async (choiceId) => {
              // The "shop" pick opens a shop sub-screen (docs/04 §6.2); the floor only advances
              // once the player leaves the shop (which calls pick("shop")).
              if (choiceId === "shop") {
                try {
                  const shopData = await metaApi.getShopItems();
                  el.remove(); // hide the pick overlay so it doesn't bleed through the shop's backdrop
                  const shopEl = buildShopOverlay(
                    shopData,
                    (itemId) => metaApi.buyShopItem(itemId),
                    async () => {
                      try { await metaApi.pick("shop"); } catch (e) { console.error("leave shop failed", e); }
                      navigateTo("/?scene=dungeon");
                    },
                  );
                  (document.getElementById("app") ?? document.body).appendChild(shopEl);
                } catch (e) {
                  console.error("shop failed to open", e);
                }
                return;
              }
              try {
                await metaApi.pick(choiceId);
                navigateTo("/?scene=dungeon");
              } catch (e) {
                console.error("dungeon pick failed", e);
              }
            },
            // Owned relics + cores power the synergy hints (docs/04 §7).
            [...(data.run?.relics ?? []), ...(data.run?.ballCores ?? [])],
          );
          (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
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
      const el = buildDungeonFailOverlay(() => { navigateTo("/?scene=campaign"); });
      (document.getElementById("app") ?? document.body).appendChild(el); // inside the letterbox frame
      return true;
    }

    return false;
  }

  return { handlePhase };
}

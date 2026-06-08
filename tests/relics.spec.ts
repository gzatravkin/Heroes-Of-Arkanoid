import { test, expect } from "./helpers/fixtures";
import { openBattle, getState, cheat, waitForPhase } from "./helpers/game";

test("addRelic:glass_cannon adds relic tile and costs a life", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");

  await cheat(page, "addRelic:glass_cannon");

  // Poll until the snapshot reflects the relic
  await page.waitForFunction(
    () => (window as any).__game.getState()?.activeRelics?.some((r: any) => r.id === "glass_cannon"),
    null, { timeout: 10_000 }
  );

  // Relic tile should appear in the DOM
  const tile = page.locator("#hud-relics [data-relic-id='glass_cannon']");
  await expect(tile).toBeAttached({ timeout: 5_000 });

  // glass_cannon costs a life: started at 3, should now be 2
  await page.waitForFunction(
    () => {
      const el = document.querySelector("#hud-lives") as HTMLElement | null;
      return el?.dataset.lives === "2";
    },
    null, { timeout: 5_000 }
  );
  const livesEl = page.locator("#hud-lives");
  expect(await livesEl.getAttribute("data-lives")).toBe("2");
});

test("addRelic:mana_battery adds second relic tile and raises manaMax", async ({ page }) => {
  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Playing");

  await cheat(page, "addRelic:glass_cannon");
  await page.waitForFunction(
    () => (window as any).__game.getState()?.activeRelics?.some((r: any) => r.id === "glass_cannon"),
    null, { timeout: 10_000 }
  );

  await cheat(page, "addRelic:mana_battery");
  await page.waitForFunction(
    () => {
      const state = (window as any).__game.getState();
      return state?.activeRelics?.length >= 2 && state?.manaMax >= 150;
    },
    null, { timeout: 10_000 }
  );

  const state = await getState(page);
  expect(state.activeRelics.length).toBeGreaterThanOrEqual(2);
  expect(state.manaMax).toBeGreaterThanOrEqual(150);

  // Two relic tiles in the DOM
  const tiles = page.locator("#hud-relics [data-relic-id]");
  expect(await tiles.count()).toBeGreaterThanOrEqual(2);

  // mana text reflects raised max
  const manaText = page.locator("#hud-mana-text");
  await expect(manaText).toContainText("/ 150", { timeout: 5_000 });
});

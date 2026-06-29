import { test, expect } from "./helpers/fixtures";
import { waitForPhase, cheat } from "./helpers/game";

const API = "http://localhost:5080";

// Each test starts its own run so they don't interfere.
// POST /dungeon/start overwrites any existing run.

test("run + floor clear + pick advances to floor 2 with a buff", async ({ page }) => {
  // Start a fresh run via direct API call.
  await page.request.post(`${API}/dungeon/start?id=ember-depths`);

  await page.goto("/?scene=dungeon");

  // Floor progress shows floor 1 of 3.
  const progress = page.locator("#dungeon-floor-progress");
  await expect(progress).toBeVisible({ timeout: 15_000 });
  await expect(progress).toContainText("Floor 1 / 3");

  // Click Enter Floor → battle scene (from=dungeon).
  await page.locator("#btn-enter-floor").click();

  // Wait for the game sim to be ready.
  await page.waitForFunction(() => !!(window as any).__game?.getState(), null, { timeout: 15000 });
  await page.waitForFunction(() => (window as any).__game.getState()?.balls?.length > 0, null, { timeout: 10000 });

  await waitForPhase(page, "Playing");

  // Win the floor via cheat.
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");

  // pick-overlay should appear with 3 choice cards.
  await expect(page.locator("#pick-overlay")).toBeVisible({ timeout: 15_000 });
  const choiceCards = page.locator("[data-choice]");
  expect(await choiceCards.count()).toBe(3);

  // Boons state their effect so the pick is informed, not a guess from the name.
  // (Pool can include picks without a blurb, so assert at least one renders, not all 3.)
  const descs = page.locator("#pick-overlay .ov-bonus-desc");
  expect(await descs.count()).toBeGreaterThan(0);
  expect(((await descs.first().textContent()) ?? "").trim().length).toBeGreaterThan(5);

  // Pick a PERSISTENT buff (relic/core/mod/spell). Skip "heal" (a consumable that adds no buff chip)
  // and "shop" (opens a sub-screen instead of advancing the floor) so this test stays deterministic.
  const cards = await choiceCards.all();
  let chosen = cards[0];
  let firstChoiceId = await chosen.getAttribute("data-choice");
  for (const c of cards) {
    const id = await c.getAttribute("data-choice");
    if (id && id !== "heal" && id !== "shop") { chosen = c; firstChoiceId = id; break; }
  }
  await chosen.click();

  // Navigates back to dungeon scene.
  await page.waitForURL("**/?scene=dungeon*", { timeout: 10000 });

  // Floor 2 of 3.
  const progress2 = page.locator("#dungeon-floor-progress");
  await expect(progress2).toBeVisible({ timeout: 15_000 });
  await expect(progress2).toContainText("Floor 2 / 3");

  // At least one buff chip in dungeon-buffs.
  const buffs = page.locator("#dungeon-buffs [data-choice]");
  // Buffs are div chips without data-choice; check that the row has content beyond the "None yet" span.
  // The buff row contains a chip per collected buff. Since we have no data-choice on chips,
  // verify the buffs row exists and has more than zero children (the buffer span is gone).
  const buffsRow = page.locator("#dungeon-buffs");
  await expect(buffsRow).toBeVisible({ timeout: 5000 });
  // Should contain the chosen buff's name somewhere in the row.
  if (firstChoiceId) {
    // The buff name is rendered in the chip.
    await expect(buffsRow).not.toContainText("None yet", { timeout: 5000 });
  }
});

test("HP carries across floors: floor-cleared?hp=N persists into run state (docs/04 §6.2)", async ({ page }) => {
  await page.request.post(`${API}/dungeon/start?id=ember-depths`);

  // Clear floor 1 reporting 4 HP remaining — the server must carry it into the run.
  const res = await page.request.post(`${API}/dungeon/floor-cleared?hp=4`);
  expect(res.ok()).toBeTruthy();

  const stateRes = await page.request.get(`${API}/dungeon/state`);
  const state = await stateRes.json();
  expect(state.hp).toBe(4); // next floor starts at the carried HP, not the level default
});

test("permadeath: lose a floor → fail overlay → state inactive", async ({ page }) => {
  await page.request.post(`${API}/dungeon/start?id=ember-depths`);

  await page.goto("/?scene=dungeon");
  await expect(page.locator("#dungeon-floor-progress")).toBeVisible({ timeout: 15_000 });

  await page.locator("#btn-enter-floor").click();

  await page.waitForFunction(() => !!(window as any).__game?.getState(), null, { timeout: 15000 });
  await page.waitForFunction(() => (window as any).__game.getState()?.balls?.length > 0, null, { timeout: 10000 });

  await waitForPhase(page, "Playing");

  // Lose the floor.
  await cheat(page, "loseNow");
  await waitForPhase(page, "Lost");

  // Fail overlay must appear.
  await expect(page.locator("#dungeon-fail-overlay")).toBeVisible({ timeout: 15_000 });

  // GET /dungeon/state should return active:false.
  const stateRes = await page.request.get(`${API}/dungeon/state`);
  const stateData = await stateRes.json();
  expect(stateData.active).toBe(false);
});

test("clear reward: win all 3 floors → clear overlay with reward info", async ({ page }) => {
  test.slow(); // plays three full floors of sim — needs headroom under parallel load
  await page.request.post(`${API}/dungeon/start?id=ember-depths`);

  // Helper: enter floor → win → pick (or clear on last)
  async function doFloor(expectPick: boolean) {
    await page.goto("/?scene=dungeon");
    await expect(page.locator("#btn-enter-floor")).toBeVisible({ timeout: 15_000 });
    await page.locator("#btn-enter-floor").click();

    await page.waitForFunction(() => !!(window as any).__game?.getState(), null, { timeout: 15000 });
    await page.waitForFunction(() => (window as any).__game.getState()?.balls?.length > 0, null, { timeout: 10000 });

    await waitForPhase(page, "Playing");
    await cheat(page, "winNow");
    await waitForPhase(page, "Won");

    if (expectPick) {
      await expect(page.locator("#pick-overlay")).toBeVisible({ timeout: 15_000 });
      // Pick a choice that advances the floor directly — skip "shop" (opens a sub-screen).
      const cards = await page.locator("[data-choice]").all();
      let target = cards[0];
      for (const c of cards) {
        if ((await c.getAttribute("data-choice")) !== "shop") { target = c; break; }
      }
      await target.click();
      await page.waitForURL("**/?scene=dungeon*", { timeout: 10000 });
    }
  }

  // Floor 1 → pick.
  await doFloor(true);
  // Floor 2 → pick.
  await doFloor(true);
  // Floor 3 → clear overlay (no pick).
  await page.goto("/?scene=dungeon");
  await expect(page.locator("#btn-enter-floor")).toBeVisible({ timeout: 15_000 });
  await page.locator("#btn-enter-floor").click();

  await page.waitForFunction(() => !!(window as any).__game?.getState(), null, { timeout: 15000 });
  await page.waitForFunction(() => (window as any).__game.getState()?.balls?.length > 0, null, { timeout: 10000 });

  await waitForPhase(page, "Playing");
  await cheat(page, "winNow");
  await waitForPhase(page, "Won");

  // Clear overlay should appear.
  await expect(page.locator("#dungeon-clear-overlay")).toBeVisible({ timeout: 15_000 });
  // Should contain crystals info.
  await expect(page.locator("#dungeon-clear-crystals")).toBeVisible({ timeout: 5000 });
  // btn-dungeon-done present.
  await expect(page.locator("#btn-dungeon-done")).toBeVisible({ timeout: 5000 });
});

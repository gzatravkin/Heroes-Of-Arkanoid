import { test, expect } from "./helpers/fixtures";
import { openBattle, getState } from "./helpers/game";

test("editor: palette loads, paint cells, save, and test-play round-trip", async ({ page }) => {
  // Open editor scene
  await page.goto("/?scene=editor");

  // Palette should be populated with real block swatches
  await expect(page.locator("#editor-palette [data-blocktype]").first()).toBeVisible({ timeout: 8000 });

  // Select a real destructible block type (hell_basic)
  await page.locator('[data-blocktype="hell_basic"]').click();

  // Paint several cells
  await page.locator('[data-col="2"][data-row="1"]').click();
  await page.locator('[data-col="3"][data-row="1"]').click();
  await page.locator('[data-col="4"][data-row="1"]').click();
  await page.locator('[data-col="2"][data-row="2"]').click();
  await page.locator('[data-col="5"][data-row="2"]').click();

  // Set level id
  await page.locator("#editor-id").fill("test-editor-auto");

  // Save
  await page.locator("#btn-editor-save").click();

  // Assert success indicator appears
  await expect(page.locator("#editor-status")).toHaveText(/saved|test-editor-auto/i, { timeout: 5000 });

  // Test-play: load the saved level and confirm blocks are present in the sim
  await openBattle(page, "test-editor-auto");
  const state = await getState(page);
  expect(state.blocks.length).toBeGreaterThan(0);
});

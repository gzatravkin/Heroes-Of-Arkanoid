import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("character select: shows 4 cards, fire_mage selected by default, can pick paladin", async ({ page }) => {
  // Reset to a clean profile so fire_mage is selected.
  await page.request.post(`${API}/reset`);

  await page.goto("/?scene=characters");

  // 4 character cards should be visible.
  const cards = page.locator("[data-character]");
  await expect(cards).toHaveCount(4);

  // fire_mage should be marked selected (has CSS class 'selected').
  const fireMageCard = page.locator("[data-character='fire_mage']");
  await expect(fireMageCard).toBeVisible();
  await expect(fireMageCard).toHaveClass(/selected/);

  // The container should expose data-selected attribute.
  const list = page.locator("#character-list");
  await expect(list).toHaveAttribute("data-selected", "fire_mage");

  // Click the paladin card.
  const paladinCard = page.locator("[data-character='paladin']");
  await expect(paladinCard).toBeVisible();
  await paladinCard.click();

  // Wait for re-render: paladin card should now have 'selected' class.
  await expect(paladinCard).toHaveClass(/selected/, { timeout: 5000 });

  // Verify via API that the backend persisted the selection.
  const apiRes = await page.request.get(`${API}/characters`);
  const data = await apiRes.json();
  expect(data.selected).toBe("paladin");

  // The list container should also reflect the new selection.
  await expect(list).toHaveAttribute("data-selected", "paladin");

  // Reset back to fire_mage so the saved default stays clean.
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

import { test, expect } from "./helpers/fixtures";

const API = "http://localhost:5080";

test("character select: fire_mage starts unlocked; bosses earn the rest (docs/04 §3)", async ({ page }) => {
  // Reset to a clean profile: only the Fire Mage is unlocked.
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

  // Paladin is LOCKED on a fresh save and shows its unlock hint; clicking no-ops.
  const paladinCard = page.locator("[data-character='paladin']");
  await expect(paladinCard).toHaveClass(/locked/);
  await expect(paladinCard).toContainText("Demon Lord");
  await paladinCard.click();
  await expect(list).toHaveAttribute("data-selected", "fire_mage");

  // Defeat the Hell boss through the public completion path → paladin unlocks.
  const completeRes = await page.request.post(`${API}/complete?level=hell-boss`);
  const completion = await completeRes.json();
  expect(completion.reward.characterUnlocked).toBe("paladin");

  // Re-render: paladin is selectable now — click selects it.
  await page.goto("/?scene=characters");
  await expect(paladinCard).not.toHaveClass(/locked/);
  await paladinCard.click();
  await expect(paladinCard).toHaveClass(/selected/, { timeout: 5000 });

  // Verify via API that the backend persisted the selection.
  const apiRes = await page.request.get(`${API}/characters`);
  const data = await apiRes.json();
  expect(data.selected).toBe("paladin");
  expect(data.unlocked).toContain("paladin");

  // Reset back to fire_mage so the saved default stays clean.
  await page.request.post(`${API}/character/select?id=fire_mage`);
});

import { test, expect } from "./helpers/fixtures";
import { openBattle, waitForPhase } from "./helpers/game";

const API = "http://localhost:5080";

// Reset before AND after so the shared default profile is clean for other specs.
test.beforeEach(async ({ page }) => { await page.request.post(`${API}/reset`); });
test.afterEach(async ({ page }) => { await page.request.post(`${API}/reset`); });

// ── Backend contract (docs/04 §3): signature + drafted pool, capped, signature-locked ──

test("GET /spells returns signature, default loadout, and owned pool", async ({ page }) => {
  const data = await (await page.request.get(`${API}/spells`)).json();
  expect(data.character).toBe("fire_mage");
  expect(data.signature).toBe("ignite");
  expect(data.unlockedSlots).toBe(3);
  expect(data.loadout).toEqual(["ignite", "fireball", "firewall"]);

  const byId = Object.fromEntries((data.spells as any[]).map(s => [s.id, s]));
  expect(byId.ignite.signature).toBe(true);
  expect(byId.ignite.equipped).toBe(true);
  // turret is owned (seeded in SpellLevels) but NOT equipped — the slot cap is real.
  expect(byId.turret.owned).toBe(true);
  expect(byId.turret.equipped).toBe(false);
  // a cross-class spell is in the pool but not owned yet.
  expect(byId.spear.owned).toBe(false);
});

test("equip is capped at the unlocked slot count", async ({ page }) => {
  // 3 slots, already full → equipping a 4th owned spell fails.
  let r = await (await page.request.post(`${API}/spell/equip?id=turret`)).json();
  expect(r.ok).toBe(false);

  // Free a slot, then it succeeds and lands in the loadout.
  await page.request.post(`${API}/spell/unequip?id=firewall`);
  r = await (await page.request.post(`${API}/spell/equip?id=turret`)).json();
  expect(r.ok).toBe(true);
  expect(r.loadout).toEqual(["ignite", "fireball", "turret"]);
});

test("signature cannot be unequipped", async ({ page }) => {
  const r = await (await page.request.post(`${API}/spell/unequip?id=ignite`)).json();
  expect(r.ok).toBe(false);
  expect(r.loadout).toContain("ignite");
  expect(r.loadout[0]).toBe("ignite");
});

test("an unowned spell cannot be equipped", async ({ page }) => {
  await page.request.post(`${API}/spell/unequip?id=firewall`); // free a slot
  const r = await (await page.request.post(`${API}/spell/equip?id=phoenix`)).json();
  expect(r.ok).toBe(false);
  expect(r.loadout).not.toContain("phoenix");
});

// ── Loadout UI ──

test("loadout screen shows the hotbar with the signature locked in slot 0", async ({ page }) => {
  await page.goto("/?scene=loadout");
  await expect(page.locator("#loadout-root")).toBeVisible({ timeout: 8000 });

  // Equipped row has 3 filled slots (signature + 2 starting), first is the signature.
  await expect(page.locator("#loadout-equipped-row .equip-slot-filled")).toHaveCount(3, { timeout: 5000 });
  await expect(page.locator("#loadout-equipped-row .equip-slot-sig")).toHaveCount(1);

  // The signature card has no equip button — it shows the locked note instead.
  const sigCard = page.locator('.card[data-spell-id="ignite"]');
  await expect(sigCard.locator(".badge-sig")).toBeVisible();
  await expect(sigCard.locator(".equip-btn")).toHaveCount(0);
  await expect(sigCard.locator(".locked-note")).toContainText("slot 0");
});

test("unequip then equip swaps a drafted spell in the UI", async ({ page }) => {
  await page.goto("/?scene=loadout");
  await expect(page.locator("#loadout-root")).toBeVisible({ timeout: 8000 });

  // Unequip firewall (a drafted starting spell).
  const firewall = page.locator('.card[data-spell-id="firewall"]');
  await expect(firewall.locator(".equip-btn")).toHaveText("Unequip", { timeout: 5000 });
  await firewall.locator(".equip-btn").click();
  await expect(page.locator("#loadout-equipped-row .equip-slot-filled")).toHaveCount(2, { timeout: 5000 });

  // Now a free slot exists → equip the owned-but-unequipped turret.
  const turret = page.locator('.card[data-spell-id="turret"]');
  await expect(turret.locator(".equip-btn")).toHaveText("Equip", { timeout: 5000 });
  await turret.locator(".equip-btn").click();
  await expect(page.locator("#loadout-equipped-row .equip-slot-filled")).toHaveCount(3, { timeout: 5000 });
  await expect(turret.locator(".equip-btn")).toHaveText("Unequip");
});

test("Character screen has a Loadout button linking to the loadout scene", async ({ page }) => {
  await page.goto("/?scene=characters");
  const btn = page.locator("#btn-open-loadout");
  await expect(btn).toBeVisible({ timeout: 6000 });
  await btn.click();
  await expect(page).toHaveURL(/scene=loadout/, { timeout: 5000 });
});

// ── Battle integration: the hotbar is the equipped loadout, not the full kit ──

test("battle hotbar reflects the equipped loadout (not the full kit)", async ({ page }) => {
  // Swap firewall → turret so the loadout is [ignite, fireball, turret].
  await page.request.post(`${API}/spell/unequip?id=firewall`);
  await page.request.post(`${API}/spell/equip?id=turret`);

  await openBattle(page, "hell-1", 1);
  await waitForPhase(page, "Serving");

  // Equipped spells are in the hotbar…
  await expect(page.locator("#hud-spell-ignite")).toBeVisible({ timeout: 6000 });
  await expect(page.locator("#hud-spell-fireball")).toBeVisible();
  await expect(page.locator("#hud-spell-turret")).toBeVisible();
  // …and unequipped/unowned spells are NOT (firewall was swapped out; phoenix never owned).
  await expect(page.locator("#hud-spell-firewall")).toHaveCount(0);
  await expect(page.locator("#hud-spell-phoenix")).toHaveCount(0);
});

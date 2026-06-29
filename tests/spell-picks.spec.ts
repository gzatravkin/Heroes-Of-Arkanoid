import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";

const API = "http://localhost:5080";

test.beforeEach(async ({ page }) => { await page.request.post(`${API}/reset`); });
test.afterEach(async ({ page }) => { await page.request.post(`${API}/reset`); });

// ── Campaign permanent unlock + slot growth (docs/04 §4.1/§5) ──

test("clearing a biome boss unlocks pool spells and grows the hotbar", async ({ page }) => {
  // Baseline: phoenix not owned, 3 slots.
  let spells = await (await page.request.get(`${API}/spells`)).json();
  expect(spells.unlockedSlots).toBe(3);
  expect((spells.spells as any[]).find(s => s.id === "phoenix").owned).toBe(false);

  // Defeat the Hell boss (direct reward grant, like other meta specs use /complete).
  await page.request.post(`${API}/complete?level=hell-boss`);

  spells = await (await page.request.get(`${API}/spells`)).json();
  expect(spells.unlockedSlots).toBe(4);                                   // grew 3 → 4
  expect((spells.spells as any[]).find(s => s.id === "phoenix").owned).toBe(true); // capstone unlocked
});

// ── Dungeon in-run draft (docs/04 §5): a drafted spell joins the run's hotbar ──

test("drafting a spell in a dungeon adds it to that run's loadout", async ({ page }) => {
  // Restart the run until floor 1 offers a spell pick (seed is time-based; retry for determinism).
  let spellChoice: string | undefined;
  for (let i = 0; i < 15 && !spellChoice; i++) {
    await page.request.post(`${API}/dungeon/start?id=ember-depths`);
    const fc = await (await page.request.post(`${API}/dungeon/floor-cleared`)).json();
    spellChoice = (fc.run.pendingChoices as string[]).find(c => c.startsWith("spell:"));
    if (!spellChoice) {
      // advance with a non-spell pick so the next start is a clean fresh run
      await page.request.post(`${API}/dungeon/fail`);
    }
  }
  expect(spellChoice, "a spell pick should be offered within a few runs").toBeTruthy();
  const spellId = spellChoice!.slice("spell:".length);

  // Pick it → it lands in the run's drafted spells.
  await page.request.post(`${API}/dungeon/pick?choice=${encodeURIComponent(spellChoice!)}`);
  const state = await (await page.request.get(`${API}/dungeon/state`)).json();
  expect(state.draftedSpells).toContain(spellId);

  // Enter the next floor → the drafted spell is in the live hotbar (appended to the loadout).
  await page.goto("/?scene=dungeon");
  await expect(page.locator("#btn-enter-floor")).toBeVisible({ timeout: 15_000 });
  await page.locator("#btn-enter-floor").click();
  await page.waitForFunction(() => (window as any).__game?.getState()?.balls?.length > 0, null, { timeout: 15000 });
  await waitForPhase(page, "Serving");

  await expect(page.locator(`#hud-spell-${spellId}`)).toBeVisible({ timeout: 6000 });
});

// ── Boss-clear reward overlay announces the progression payoff ──

test("reward overlay announces unlocked spells + a new slot on a boss clear", async ({ page }) => {
  // Win hell-boss cleanly via the Serving-phase trick (boss can't attack while held in Serving).
  const run = `hell-boss-reward-${Date.now()}`;
  await page.goto(`/?scene=battle&level=hell-boss&seed=1&from=campaign&tutorial=1&run=${run}`);
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await page.waitForFunction(
    () => {
      const s = (window as any).__game.getState();
      return s?.bossActive === true && s?.phase === "Serving";
    },
    null,
    { timeout: 8000 },
  );
  await page.evaluate(() => (window as any).__game.cheat("winNow", 0));

  await expect(page.locator("#reward-overlay")).toBeVisible({ timeout: 8000 });
  await expect(page.locator("#reward-slot")).toContainText("Spell Slot");
  await expect(page.locator("#reward-spells")).toContainText("Phoenix"); // hell-boss grants the FM capstone
});

import { test, expect } from "./helpers/fixtures";
import { waitForPhase } from "./helpers/game";
import * as fs from "fs";
import * as path from "path";

// Hero-XP delivery (design §5.3): XP must accrue from real battles — including a LOSS (blocks only,
// no win bonus) — and a hero level-up must surface as a reward beat on the victory screen.

const OUT = path.resolve(__dirname, "..", "test-results", "hero-xp");

test("hero level-up shows a reward beat on victory", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });
  // Seed Fire Mage to 1 XP short of Lv2 (XpToNext(1)=80), so any win tips it over.
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    const B = "http://localhost:5080";
    await fetch(`${B}/reset`, { method: "POST" });
    await fetch(`${B}/dev/hero?hero=fire_mage&level=1&exp=79`, { method: "POST" });
  });

  await page.goto("/?scene=battle&level=hell-1&seed=2&from=campaign");
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  // Win the battle (the +25 win bonus tips 79 → ≥80 → Lv2).
  await page.waitForFunction(() => { (window as any).__game?.cheat("winNow", 0); return true; });

  // The hero level-up beat must appear on the victory overlay.
  await page.waitForSelector("#reward-hero-levelup", { timeout: 10_000 });
  const txt = await page.textContent("#reward-hero-levelup");
  expect(txt).toContain("Hero Level Up");
  expect(txt).toContain("Lv 2");
  fs.writeFileSync(path.join(OUT, "hero-levelup-beat.png"), await page.screenshot({ fullPage: true }));
});

test("a lost battle still credits hero XP for blocks destroyed", async ({ page }) => {
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    const B = "http://localhost:5080";
    await fetch(`${B}/reset`, { method: "POST" });
  });

  await page.goto("/?scene=battle&level=hell-1&seed=5&from=campaign");
  await page.waitForFunction(() => !!(window as any).__game?.getState());
  await waitForPhase(page, "Playing");
  // Destroy some blocks, then force a loss.
  await page.waitForFunction(() => (window as any).__game.getState()?.bricksDestroyedThisLevel > 0, null, { timeout: 10_000 });
  await page.waitForFunction(() => { (window as any).__game?.cheat("loseNow", 0); return true; });
  await page.waitForSelector("#defeat-overlay", { timeout: 10_000 });

  // The loss POSTed hero XP for the blocks destroyed → the hero's exp is now > 0.
  const exp = await page.evaluate(async () => {
    const p = await fetch("http://localhost:5080/profile").then(r => r.json());
    return p?.heroProgress?.fire_mage?.exp ?? 0;
  });
  expect(exp).toBeGreaterThan(0);
});

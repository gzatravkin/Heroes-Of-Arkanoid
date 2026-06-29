import { test, expect } from "./helpers/fixtures";
import * as fs from "fs";
import * as path from "path";

// Stat-engine progression UI (design §5.3/§5.4/§5.6): the Heroes & Masteries screen must show the
// hero's Level/XP + ★ stars + resolved stat block, and let the player ASCEND (spend Hero Tokens → ★)
// and BUY MASTERIES (spend the shared Skill Points → account stat flats), with every change reflected
// in the resolved stats. We seed a profile through the REAL reward path (wins drip XP/tokens/points).

const OUT = path.resolve(__dirname, "..", "test-results", "masteries");

test("masteries: hero level/ascend/mastery loop reflected in resolved stats", async ({ page }) => {
  fs.mkdirSync(OUT, { recursive: true });

  // Seed a clean profile: 2 boss wins (10 tokens) + 3 normal wins → tokens for ★1, points for masteries,
  // and a big Fire Mage XP drip so the hero levels up. All via the live reward endpoints.
  await page.goto("/?scene=menu");
  await page.evaluate(async () => {
    const B = "http://localhost:5080";
    await fetch(`${B}/reset`, { method: "POST" });
    for (const lvl of ["demo-a-boss", "demo-b-boss", "demo-1", "demo-2", "demo-3"]) {
      await fetch(`${B}/complete?level=${lvl}&blocks=300&rift=none`, { method: "POST" });
    }
  });

  await page.goto("/?scene=masteries");
  await page.waitForSelector("#ms-stats");

  // Hero leveled up from the seeded wins (Lv > 1).
  const lvlText = await page.textContent("#ms-hero-level");
  expect(lvlText).toMatch(/Lv (\d+)/);
  const heroLevel = parseInt(lvlText!.replace(/\D/g, ""), 10);
  expect(heroLevel).toBeGreaterThan(1);

  fs.writeFileSync(path.join(OUT, "1-initial.png"), await page.screenshot({ fullPage: true }));

  // Capture crit-chance before, ascend (★0 → ★1), confirm a star lit + crit rose.
  const critBefore = await page.textContent("#ms-stats .stat:nth-child(3) .val");
  await page.click("#ms-ascend");
  await page.waitForFunction(() => document.querySelectorAll("#ms-stars .star.on").length >= 1);
  fs.writeFileSync(path.join(OUT, "2-ascended.png"), await page.screenshot({ fullPage: true }));
  const starsOn = await page.evaluate(() => document.querySelectorAll("#ms-stars .star.on").length);
  expect(starsOn).toBeGreaterThanOrEqual(1);

  // Buy a Sharpshooter mastery (spends a Skill Point → +crit chance).
  const pointsBefore = await page.textContent("#ms-points");
  await page.click("#ms-buy-sharpshooter");
  await page.waitForFunction(() =>
    (document.querySelector("#ms-lvl-sharpshooter")?.textContent ?? "").startsWith("1"));
  fs.writeFileSync(path.join(OUT, "3-mastery.png"), await page.screenshot({ fullPage: true }));

  const sharpLvl = await page.textContent("#ms-lvl-sharpshooter");
  expect(sharpLvl).toBe("1/5");
  const pointsAfter = await page.textContent("#ms-points");
  expect(pointsAfter).not.toBe(pointsBefore); // shared Skill-Points pool decremented (§5.10)

  const critAfter = await page.textContent("#ms-stats .stat:nth-child(3) .val");
  expect(critAfter).not.toBe(critBefore); // resolved crit chance changed from ascend + mastery
});

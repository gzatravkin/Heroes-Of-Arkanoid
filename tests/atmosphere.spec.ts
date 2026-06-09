import { test } from "./helpers/fixtures";
import { openBattle } from "./helpers/game";
import * as path from "path";

const SHOTS = path.resolve(__dirname, "demo-screenshots");

// Biome atmosphere kits (docs/12): embers / dust / shadows / clouds render behind play.
// Screenshots after a short real-time wait so particles have drifted into view.
const CASES: Array<[string, string]> = [
  ["hell-1",    "atmosphere-hell.png"],
  ["caverns-1", "atmosphere-caverns.png"],
  ["village-1", "atmosphere-village.png"],
  ["heaven-1",  "atmosphere-heaven.png"],
];

for (const [level, shot] of CASES) {
  test(`atmosphere kit renders in ${level}`, async ({ page }) => {
    await openBattle(page, level);
    await page.waitForTimeout(1200); // let particles drift
    await page.screenshot({ path: path.join(SHOTS, shot) });
  });
}

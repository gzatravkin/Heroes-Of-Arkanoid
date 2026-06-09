import { test } from "./helpers/fixtures";
import * as path from "path";

const SHOTS = path.resolve(__dirname, "demo-screenshots");

// Capture the achievements screen to verify English (not Russian) badges (D2).
test("shot: achievements (English badges)", async ({ page }) => {
  await page.goto("/?scene=achievements");
  await page.waitForSelector("#achievements-scene");
  await page.waitForFunction(() => {
    const o = document.querySelector("#scene-transition-overlay") as HTMLElement | null;
    return !o || parseFloat(getComputedStyle(o).opacity) < 0.05;
  }, null, { timeout: 5000 }).catch(() => {});
  await page.screenshot({ path: path.join(SHOTS, "ui-achievements.png") });
});

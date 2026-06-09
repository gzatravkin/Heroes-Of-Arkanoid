import { test } from "./helpers/fixtures";
import * as path from "path";

// Proof screenshots for the 9-slice button conversion (docs/07 item H). Each scene's
// framed buttons must keep their rounded caps at any width — no stretched/distorted art.
const SHOTS = path.resolve(__dirname, "demo-screenshots");

for (const scene of ["settings", "skills", "campaign", "menu"]) {
  test(`shot: 9-slice buttons render in ${scene}`, async ({ page }) => {
    await page.goto(`/?scene=${scene}`);
    // Wait out the "Loading assets…" atlas load, then for a real button to mount.
    await page.waitForFunction(
      () => !document.body.innerText.includes("Loading assets"),
      null, { timeout: 20_000 },
    );
    await page.waitForSelector("button", { timeout: 10_000 });
    await page.waitForTimeout(500); // settle frame
    await page.screenshot({ path: path.join(SHOTS, `nineslice-${scene}.png`) });
  });
}

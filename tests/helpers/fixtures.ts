import { test as base, expect } from "@playwright/test";
import * as fs from "fs";
import * as path from "path";

// Extends `page` to capture browser console + attach client ring buffer and the
// backend session JSONL to the report. Attaches on EVERY test (handy on pass too,
// essential on fail) so an AI can read exactly what happened.
export const test = base.extend({
  page: async ({ page }, use, testInfo) => {
    const console_: string[] = [];
    page.on("console", (m) => console_.push(`[${m.type()}] ${m.text()}`));
    page.on("pageerror", (e) => console_.push(`[pageerror] ${e.message}`));

    await use(page);

    await testInfo.attach("client-console.txt", { body: console_.join("\n"), contentType: "text/plain" });
    try {
      const ring = await page.evaluate(() => (window as any).__game?.getLogs?.() ?? []);
      await testInfo.attach("client-ring.json", { body: JSON.stringify(ring, null, 2), contentType: "application/json" });
      const runId = await page.evaluate(() => (window as any).__game?.runId ?? "");
      if (runId) {
        const file = path.resolve(__dirname, "..", "..", "backend", "Arkanoid.Server", "logs", `${runId}.jsonl`);
        if (fs.existsSync(file)) await testInfo.attach("server-sim.jsonl", { path: file, contentType: "text/plain" });
      }
    } catch { /* page may already be closed */ }
  },
});
export { expect };

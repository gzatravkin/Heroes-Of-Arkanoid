import * as fs from "fs";
import * as path from "path";

// Runs once before the whole Playwright run. Keeps the workspace tidy:
//  - wipes the demo-screenshots folder so it isn't spammed with stale shots,
//  - clears old per-run artifacts,
//  - removes per-worker backend saves so each run starts from a clean profile.
export default async function globalSetup() {
  const tests = __dirname;
  const repo = path.resolve(tests, "..");

  wipeDir(path.join(tests, "demo-screenshots"));
  wipeDir(path.join(tests, "test-results"));

  // Per-worker isolated saves (profile-w0.json, dungeon-w0.json, …) — not the
  // default profile.json (that's the real player's save).
  const saves = path.join(repo, "backend", "Arkanoid.Server", "saves");
  if (fs.existsSync(saves)) {
    for (const f of fs.readdirSync(saves)) {
      if (/^(profile|dungeon)-w\d+\.json(\.tmp)?$/.test(f)) {
        try { fs.unlinkSync(path.join(saves, f)); } catch { /* ignore */ }
      }
    }
  }
}

/** Delete every file in a directory (recreating it empty); leaves the dir present. */
function wipeDir(dir: string) {
  try {
    fs.rmSync(dir, { recursive: true, force: true });
  } catch { /* ignore */ }
  fs.mkdirSync(dir, { recursive: true });
}

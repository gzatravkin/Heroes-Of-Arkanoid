const BASE = "http://localhost:5080";

declare global { interface Window { __ark: ArkCheatApi } }

interface ArkCheatApi {
  unlockAll:       () => Promise<void>;
  getAllSpells:     () => Promise<void>;
  unlockAllLevels: () => Promise<void>;
  addCoins:        (sparks?: number, souls?: number, insight?: number) => Promise<void>;
  reset:           () => Promise<void>;
}

export function installCheats() {
  const S = "font-weight:bold;font-family:monospace";
  const R = "font-weight:normal;font-family:monospace";

  console.log(
    "%c╔════════════════════════════════════════════════╗\n" +
    "║       ARKANOID  DEV  CHEAT  CONSOLE            ║\n" +
    "╚════════════════════════════════════════════════╝%c",
    `${S};color:#ff8c00;font-size:13px`, R
  );

  console.log(
    "%cMETA CHEATS%c (call from any page, then reload the game)\n" +
    "  __ark.unlockAll()              — all levels + all spells + all heroes\n" +
    "  __ark.getAllSpells()            — every spell, max spell slots, all heroes\n" +
    "  __ark.unlockAllLevels()        — every campaign level marked completed\n" +
    "  __ark.addCoins(s, so, i)       — add Sparks / Souls / Insight (default 1000 each)\n" +
    "  __ark.reset()                  — reset profile to fresh new-game state\n" +
    "\n" +
    "%cBATTLE CHEATS%c (active battle session only)\n" +
    "  __game.cheat('winNow')         — instantly win the level\n" +
    "  __game.cheat('loseNow')        — instantly lose the level\n" +
    "  __game.cheat('setMana', 100)   — set mana to value\n" +
    "  __game.cheat('setLives', 10)   — set HP to value\n" +
    "  __game.cheat('setBalls', 5)    — set spare balls count\n" +
    "  __game.cheat('clearAllButN', 1)— destroy all but N blocks\n" +
    "  __game.cheat('setBossHp', 10)  — set boss HP to % (0–100)\n" +
    "  __game.serve()                 — serve the ball",
    `${S};color:#ffd700`, R,
    `${S};color:#ffd700`, R
  );

  window.__ark = {
    unlockAll: () =>
      devPost("/dev/unlock-all").then(() =>
        console.log("%c[Ark] All levels + spells + heroes unlocked. Reload to apply.", "color:#00dd66")),

    getAllSpells: () =>
      devPost("/dev/unlock-all").then(() =>
        console.log("%c[Ark] All spells unlocked + max spell slots. Reload to apply.", "color:#00dd66")),

    unlockAllLevels: () =>
      devPost("/dev/unlock-all").then(() =>
        console.log("%c[Ark] All campaign levels completed. Reload to apply.", "color:#00dd66")),

    addCoins: (sparks = 1000, souls = 1000, insight = 1000) =>
      devPost(`/dev/coins?sparks=${sparks}&souls=${souls}&insight=${insight}`).then(() =>
        console.log(`%c[Ark] +${sparks} Sparks, +${souls} Souls, +${insight} Insight. Reload to see.`, "color:#00dd66")),

    reset: () =>
      devPost("/reset").then(() =>
        console.log("%c[Ark] Profile reset to new-game state. Reload the page.", "color:#00dd66")),
  };
}

async function devPost(path: string): Promise<void> {
  const res = await fetch(`${BASE}${path}`, { method: "POST" });
  if (!res.ok) console.warn(`[Ark] ${path} returned ${res.status} — is the server running?`);
}

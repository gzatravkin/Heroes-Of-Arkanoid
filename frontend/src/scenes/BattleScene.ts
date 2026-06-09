import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";
import { Hud } from "../ui/Hud";
import { metaApi } from "../net/metaApi";
import { createCampaignFlow } from "./battle/campaignFlow";
import { createDungeonFlow } from "./battle/dungeonFlow";
import { maybeShowTutorial } from "./TutorialOverlay";

// Renderer always runs — the pooled draw() is cheap enough for mobile GPUs and
// headless WebGL alike. The HEAVY_FX glow gate in Renderer.ts still skips the
// expensive GlowFilter passes under Playwright, but base rendering is always on.

export function mountBattle(host: HTMLElement, level: string, seed: number, run: string, from = "") {
  const r = new Renderer(host);
  const hud = new Hud(host);
  const conn = new Connection(level, seed, run);

  const flow =
    from === "campaign" ? createCampaignFlow(level) :
    from === "dungeon"  ? createDungeonFlow()        :
    null;

  conn.onSnapshot = (s) => {
    r.draw(s);
    hud.update(s);
    if (flow) flow.handlePhase(s);
  };

  // Fetch selected character's spell kit and load into HUD.
  // Wire conn after (wireConn also handles the fallback path if fetch fails).
  metaApi.getCharacters()
    .then((data) => {
      const selected = data.characters.find(c => c.id === data.selected);
      if (selected && selected.spells?.length > 0) {
        hud.loadSpells(selected.spells);
      }
      hud.wireConn(conn);
    })
    .catch(() => {
      // Network/backend error: fall back to default hotbar.
      hud.wireConn(conn);
    });

  // Fetch equipped items and show small HUD row.
  metaApi.getItems()
    .then((data) => { hud.loadEquippedItems(data.items.filter(it => it.equipped)); })
    .catch(() => { /* non-fatal */ });

  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // Show tutorial on first battle (non-blocking — serves after tutorial or immediately).
  // Skip in Playwright/automated environments (navigator.webdriver=true) to avoid
  // blocking test automation that depends on the ball auto-serving.
  // Can be forced with ?tutorial=1 URL param for dedicated tutorial tests.
  const q2 = new URLSearchParams(location.search);
  const forceTutorial = q2.get("tutorial") === "1";
  const isAutomated = !!(navigator as any).webdriver && !forceTutorial;
  conn.whenReady(() => {
    if (isAutomated) {
      setTimeout(() => conn.serve(), 300);
    } else {
      maybeShowTutorial(host, forceTutorial).then(() => {
        setTimeout(() => conn.serve(), 300);
      });
    }
  });
}

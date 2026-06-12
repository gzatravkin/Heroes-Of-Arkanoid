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
  (window as any).__renderer = r;
  const hud = new Hud(host);
  const conn = new Connection(level, seed, run);
  (window as any).__conn = conn;

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
  // Also switch Renderer to the correct per-class paddle/ball sprites.
  metaApi.getCharacters()
    .then((data) => {
      const selected = data.characters.find(c => c.id === data.selected);
      if (selected) {
        // Switch paddle/ball art to match the selected class.
        r.setClass(selected.id);
        if (selected.spells?.length > 0) {
          hud.loadSpells(selected.spells);
        }
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

  const detachInput = attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  (window as any).__detachInput = detachInput;
  installTestHooks(conn);
  // Show tutorial on first battle (non-blocking — serves after tutorial or immediately).
  // Skip when: Playwright drives the browser (navigator.webdriver=true) OR the player
  // already acknowledged the tutorial (arkanoid_tutorial_seen=1 in localStorage).
  // Tests pre-set that flag via fixtures so the serve fires regardless of webdriver detection.
  // Can be forced with ?tutorial=1 URL param for dedicated tutorial tests.
  const q2 = new URLSearchParams(location.search);
  const forceTutorial = q2.get("tutorial") === "1";
  const tutorialSeen = typeof localStorage !== "undefined" && localStorage.getItem("arkanoid_tutorial_seen") === "1";
  const isAutomated = (!!(navigator as any).webdriver || tutorialSeen) && !forceTutorial;
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

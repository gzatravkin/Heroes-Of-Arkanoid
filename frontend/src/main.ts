import { mount, unmount } from "svelte";
import { loadAtlas } from "./render/assets";
import { initWasm } from "./wasm/bridge";
import { teardownBridge } from "./testBridge";
import MenuScene         from "./scenes/MenuScene.svelte";
import BattleScene       from "./scenes/BattleScene.svelte";
import CampaignScene     from "./scenes/CampaignScene.svelte";
import DungeonScene      from "./scenes/DungeonScene.svelte";
import CharacterScene    from "./scenes/CharacterScene.svelte";
import EditorScene       from "./scenes/EditorScene.svelte";
import SettingsScene     from "./scenes/SettingsScene.svelte";
import AchievementsScene from "./scenes/AchievementsScene.svelte";
import DungeonsScene     from "./scenes/DungeonsScene.svelte";
import MasteriesScene    from "./scenes/MasteriesScene.svelte";
import LoadoutScene      from "./scenes/LoadoutScene.svelte";
import CardsScene        from "./scenes/CardsScene.svelte";
import DailyScene        from "./scenes/DailyScene.svelte";
import LeagueScene       from "./scenes/LeagueScene.svelte";
import ModulesScene      from "./scenes/ModulesScene.svelte";
import SeasonScene       from "./scenes/SeasonScene.svelte";
import LeaderboardScene  from "./scenes/LeaderboardScene.svelte";
import { fadeInOnLoad, setNavigateHandler, navigateTo } from "./ui/transition";
import { injectTheme } from "./ui/theme";
import { preloadRelics } from "./net/relicCache";
import { preloadSpells } from "./net/spellCache";
import { installCheats } from "./cheats";
import { initAuth } from "./net/FirebaseAuth";

injectTheme();
installCheats();

const host = document.getElementById("app")!;

let _svelteScene: object | null = null;

function mountSvelte(component: Parameters<typeof mount>[0]) {
  _svelteScene = mount(component, { target: host });
}

function doMount(search: string) {
  teardownBridge();
  if (_svelteScene) { unmount(_svelteScene); _svelteScene = null; }
  host.innerHTML = "";

  const q = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  const scene = q.get("scene") ?? "menu";

  if      (scene === "battle")       mountSvelte(BattleScene);
  else if (scene === "campaign")     mountSvelte(CampaignScene);
  else if (scene === "dungeons")     mountSvelte(DungeonsScene);
  else if (scene === "dungeon")      mountSvelte(DungeonScene);
  else if (scene === "characters")   mountSvelte(CharacterScene);
  else if (scene === "editor")       mountSvelte(EditorScene);
  else if (scene === "loadout")      mountSvelte(LoadoutScene);
  else if (scene === "cards")        mountSvelte(CardsScene);
  else if (scene === "daily")        mountSvelte(DailyScene);
  else if (scene === "league")       mountSvelte(LeagueScene);
  else if (scene === "modules")      mountSvelte(ModulesScene);
  else if (scene === "season")       mountSvelte(SeasonScene);
  else if (scene === "achievements") mountSvelte(AchievementsScene);
  else if (scene === "settings")     mountSvelte(SettingsScene);
  else if (scene === "masteries")    mountSvelte(MasteriesScene);
  else if (scene === "leaderboard")  mountSvelte(LeaderboardScene);
  else                               mountSvelte(MenuScene);
}

// Loading screen with a deterministic progress bar (atlas 0–70%, WASM 70–100%).
const loadingStyle = document.createElement("style");
loadingStyle.textContent = `
.ark-bar-fill{height:100%;background:linear-gradient(90deg,#d8a84e,#ff9040,#d8a84e);border-radius:3px;width:0%;transition:width 0.25s ease-out}
.ark-pct{color:rgba(201,177,130,0.65);font-size:0.72rem;letter-spacing:0.05em;min-width:3ch;text-align:right}
`;
document.head.appendChild(loadingStyle);

const loading = document.createElement("div");
loading.style.cssText = "display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;gap:18px;font-family:var(--font-body,sans-serif)";
loading.innerHTML = `
  <div style="color:var(--gold,#d8a84e);font-size:1.3rem;letter-spacing:0.08em;text-align:center;text-shadow:0 0 12px rgba(216,168,78,0.5)">Heroes of Arkanoid II</div>
  <div style="display:flex;align-items:center;gap:10px">
    <div style="position:relative;width:160px;height:5px;background:rgba(255,255,255,0.1);border-radius:3px;overflow:hidden"><div class="ark-bar-fill" id="ark-bar"></div></div>
    <span class="ark-pct" id="ark-pct">0%</span>
  </div>
  <div style="color:var(--text-dim,#c9b182);font-size:0.82rem;letter-spacing:0.05em" id="ark-status">Loading assets…</div>
`;
host.appendChild(loading);

const barEl  = loading.querySelector<HTMLElement>("#ark-bar")!;
const pctEl  = loading.querySelector<HTMLElement>("#ark-pct")!;
const statEl = loading.querySelector<HTMLElement>("#ark-status")!;
// Atlas = 0–70%, WASM = 0–30% added on top — tracked separately so parallel loading doesn't regress the bar.
let _atlasPct = 0;
let _wasmPct  = 0;
function updateLoadBar(label?: string) {
  const total = Math.min(100, _atlasPct + _wasmPct);
  barEl.style.width = `${total}%`;
  pctEl.textContent = `${Math.round(total)}%`;
  if (label) statEl.textContent = label;
}

preloadRelics(); // fire-and-forget; populates relicCache before most scenes render
preloadSpells(); // populates spellCache so floor-clear spell picks render with name/icon

function initApp() {
  loading.remove();
  fadeInOnLoad();

  // SPA navigate handler: called by navigateTo() instead of location.href.
  // Strips any leading "/" from the scene-relative URL and prepends the Vite
  // BASE_URL so navigation works both on localhost ("/") and on GitHub Pages
  // ("/Heroes-Of-Arkanoid/") without hardcoding the sub-path.
  setNavigateHandler((url) => {
    const base = import.meta.env.BASE_URL; // "/" in dev, "/Heroes-Of-Arkanoid/" in prod
    const path = url.startsWith("/") ? url.slice(1) : url;
    const full = base + path;
    history.pushState({}, "", full);
    doMount(new URL(full, location.origin).search);
  });

  // Intercept same-origin <a href> clicks so they go through SPA routing.
  document.addEventListener("click", (e) => {
    const a = (e.target as Element).closest("a[href]") as HTMLAnchorElement | null;
    if (!a) return;
    let href: URL;
    try { href = new URL(a.href); } catch { return; }
    if (href.origin !== location.origin) return;
    e.preventDefault();
    navigateTo(href.pathname + href.search);
  });

  // Back/forward browser buttons.
  window.addEventListener("popstate", () => {
    doMount(location.search);
    fadeInOnLoad();
  });

  // Initial scene mount.
  doMount(location.search);
}

// Initialise the SPA whether or not the atlas or WASM loads — meta/social scenes are HTML/Svelte
// and don't need either. Failures must NOT kill the whole app.
Promise.all([
  loadAtlas((pct) => { _atlasPct = pct; updateLoadBar("Loading sprites…"); })
    .catch((err) => console.error("Atlas load failed (continuing; battles may lack sprites):", err)),
  (async () => {
    updateLoadBar("Loading engine…");
    await initWasm().catch((err) => console.error("WASM init failed (continuing; offline sim unavailable):", err));
    _wasmPct = 30; updateLoadBar("Ready");
  })(),
]).finally(initApp);

// Fire-and-forget: sets up anonymous Firebase auth and derives player nickname.
// Runs in parallel with asset loading; social features are unavailable until resolved.
initAuth().catch(() => {});

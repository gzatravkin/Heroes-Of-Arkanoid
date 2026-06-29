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
import { fadeInOnLoad, setNavigateHandler, navigateTo } from "./ui/transition";
import { injectTheme } from "./ui/theme";
import { preloadRelics } from "./net/relicCache";
import { preloadSpells } from "./net/spellCache";
import { installCheats } from "./cheats";

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
  else                               mountSvelte(MenuScene);
}

// Show a minimal loading indicator while fetching the atlas.
const loading = document.createElement("div");
loading.style.cssText = "color:var(--text-dim,#c9b182);font-family:var(--font-body,sans-serif);text-align:center;padding-top:40cqh;font-size:var(--fs-xl,1.2rem)";
loading.textContent = "Loading assets…";
host.appendChild(loading);

preloadRelics(); // fire-and-forget; populates relicCache before most scenes render
preloadSpells(); // populates spellCache so floor-clear spell picks render with name/icon

function initApp() {
  loading.remove();
  fadeInOnLoad();

  // SPA navigate handler: called by navigateTo() instead of location.href.
  setNavigateHandler((url) => {
    const full = url.startsWith("/") ? url : "/" + url;
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
  loadAtlas().catch((err) => console.error("Atlas load failed (continuing; battles may lack sprites):", err)),
  initWasm().catch((err) => console.error("WASM init failed (continuing; offline sim unavailable):", err)),
]).finally(initApp);

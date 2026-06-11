import { loadAtlas } from "./render/assets";
import { mountMenu } from "./scenes/MenuScene";
import { mountBattle } from "./scenes/BattleScene";
import { mountCampaign } from "./scenes/CampaignScene";
import { mountDungeons } from "./scenes/DungeonsScene";
import { mountDungeon } from "./scenes/DungeonScene";
import { mountCharacters } from "./scenes/CharacterScene";
import { mountEditor } from "./scenes/EditorScene";
import { mountInventory } from "./scenes/InventoryScene";
import { mountAchievements } from "./scenes/AchievementsScene";
import { mountSettings } from "./scenes/SettingsScene";
import { mountSkills } from "./scenes/SkillsScene";
import { fadeInOnLoad, setNavigateHandler, navigateTo } from "./ui/transition";
import { injectTheme } from "./ui/theme";

injectTheme();

const host = document.getElementById("app")!;

function teardownBattle() {
  const r = (window as any).__renderer;
  if (r) {
    try { r.app.destroy(false); } catch (_) {}
    delete (window as any).__renderer;
  }
  const c = (window as any).__conn;
  if (c) {
    try { c.close(); } catch (_) {}
    delete (window as any).__conn;
  }
}

function doMount(search: string) {
  teardownBattle();
  host.innerHTML = "";

  const q = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  const scene = q.get("scene") ?? "menu";
  const level = q.get("level") ?? "hell-1";
  const seed  = Number(q.get("seed") ?? "1");
  const run   = q.get("run") ?? `dev-${Date.now()}`;
  const from  = q.get("from") ?? "";

  if      (scene === "battle")       mountBattle(host, level, seed, run, from);
  else if (scene === "campaign")     mountCampaign(host);
  else if (scene === "dungeons")     mountDungeons(host);
  else if (scene === "dungeon")      mountDungeon(host);
  else if (scene === "characters")   mountCharacters(host);
  else if (scene === "editor")       mountEditor(host);
  else if (scene === "inventory")    mountInventory(host);
  else if (scene === "achievements") mountAchievements(host);
  else if (scene === "settings")     mountSettings(host);
  else if (scene === "skills")       mountSkills(host);
  else                               mountMenu(host);
}

// Show a minimal loading indicator while fetching the atlas.
const loading = document.createElement("div");
loading.style.cssText = "color:var(--text-dim,#c9b182);font-family:var(--font-body,sans-serif);text-align:center;padding-top:40cqh;font-size:var(--fs-xl,1.2rem)";
loading.textContent = "Loading assets…";
host.appendChild(loading);

loadAtlas()
  .then(() => {
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
  })
  .catch((err) => {
    loading.textContent = "Failed to load assets: " + String(err);
    console.error("Atlas load failed:", err);
  });

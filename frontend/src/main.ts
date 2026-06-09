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
import { fadeInOnLoad } from "./ui/transition";

const host = document.getElementById("app")!;
const q = new URLSearchParams(location.search);
const scene = q.get("scene") ?? "menu";
const level = q.get("level") ?? "hell-1";
const seed = Number(q.get("seed") ?? "1");
const run = q.get("run") ?? `dev-${Date.now()}`;
const from = q.get("from") ?? "";

// Load the sprite atlas before mounting any scene.
// Shows a minimal loading indicator while fetching.
const loading = document.createElement("div");
loading.style.cssText = "color:#ccc;font-family:sans-serif;text-align:center;padding-top:40vh;font-size:1.2rem";
loading.textContent = "Loading assets…";
host.appendChild(loading);

loadAtlas()
  .then(() => {
    loading.remove();
    // Fade in after atlas loads so every scene entry feels smooth.
    fadeInOnLoad();
    if (scene === "battle") mountBattle(host, level, seed, run, from);
    else if (scene === "campaign") mountCampaign(host);
    else if (scene === "dungeons") mountDungeons(host);
    else if (scene === "dungeon") mountDungeon(host);
    else if (scene === "characters") mountCharacters(host);
    else if (scene === "editor") mountEditor(host);
    else if (scene === "inventory") mountInventory(host);
    else if (scene === "achievements") mountAchievements(host);
    else if (scene === "settings") mountSettings(host);
    else if (scene === "skills") mountSkills(host);
    else mountMenu(host);
  })
  .catch((err) => {
    loading.textContent = "Failed to load assets: " + String(err);
    console.error("Atlas load failed:", err);
  });

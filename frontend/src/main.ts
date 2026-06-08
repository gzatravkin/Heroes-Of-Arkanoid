import { mountMenu } from "./scenes/MenuScene";
import { mountBattle } from "./scenes/BattleScene";
import { mountCampaign } from "./scenes/CampaignScene";
import { mountDungeons } from "./scenes/DungeonsScene";
import { mountDungeon } from "./scenes/DungeonScene";

const host = document.getElementById("app")!;
const q = new URLSearchParams(location.search);
const scene = q.get("scene") ?? "menu";
const level = q.get("level") ?? "hell-1";
const seed = Number(q.get("seed") ?? "1");
const run = q.get("run") ?? `dev-${Date.now()}`;
const from = q.get("from") ?? "";

if (scene === "battle") mountBattle(host, level, seed, run, from);
else if (scene === "campaign") mountCampaign(host);
else if (scene === "dungeons") mountDungeons(host);
else if (scene === "dungeon") mountDungeon(host);
else mountMenu(host);

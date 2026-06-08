import { mountMenu } from "./scenes/MenuScene";
import { mountBattle } from "./scenes/BattleScene";

const host = document.getElementById("app")!;
const q = new URLSearchParams(location.search);
const scene = q.get("scene") ?? "menu";
const level = q.get("level") ?? "hell-1";
const seed = Number(q.get("seed") ?? "1");
const run = q.get("run") ?? `dev-${Date.now()}`;

if (scene === "battle") mountBattle(host, level, seed, run);
else mountMenu(host);

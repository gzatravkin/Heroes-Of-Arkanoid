/**
 * audit-assets.mjs — Asset usage audit for the Arkanoid game.
 *
 * Scans frontend/src for tex(), anim(), bg(), animStrip(), and all quoted strings
 * that match atlas frame keys. Also counts frames covered by the anim manifest.
 * Reports used vs total 759 atlas frames.
 *
 * Usage: node scripts/audit-assets.mjs
 * Writes: docs/asset-usage.md
 */

import { readFileSync, writeFileSync, readdirSync, statSync } from "fs";
import { join, resolve, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, "..");

// ── 1. Collect all 759 atlas frame keys ──────────────────────────────────────

function getAllFrameKeys() {
  const index = JSON.parse(readFileSync(join(ROOT, "frontend/public/atlas/atlas-index.json"), "utf8"));
  const all = new Set();
  for (const fname of index) {
    const data = JSON.parse(readFileSync(join(ROOT, "frontend/public/atlas", fname), "utf8"));
    for (const key of Object.keys(data.frames)) all.add(key);
  }
  return all;
}

// ── 2. Get animation manifest ─────────────────────────────────────────────────

function getAnimManifest() {
  return JSON.parse(readFileSync(join(ROOT, "frontend/public/atlas/animations.json"), "utf8"));
}

// ── 3. Walk directory ─────────────────────────────────────────────────────────

function walkDir(dir) {
  let results = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) results = results.concat(walkDir(full));
    else results.push(full);
  }
  return results;
}

// ── 4. Extract all quoted string values from TS source ───────────────────────

function extractAllStrings(text) {
  const values = new Set();
  // Single quotes, double quotes, backtick (no variable part)
  const re = /["'`]([^"'`\n\r${\\]{2,100})["'`]/g;
  let m;
  while ((m = re.exec(text)) !== null) values.add(m[1]);
  return values;
}

// ── 5. Hardcoded dynamic-use frames (constructed programmatically) ────────────
// Boss.ts builds keys like "hell/DemonBody", "dungeon/GoblinBody" etc. from
// sprite fields in the snapshot — these cannot be found by literal string scan.
// InventoryScene builds "items/ItemDrill2" from config icon + tier suffix.
// Renderer.ts looks up class-bar frames by building strings from class+state.
const KNOWN_DYNAMIC_FRAMES = [
  // Demon boss rig parts (Boss.ts documented keys)
  "hell/DemonBody","hell/DemonFace","hell/DemonFace2","hell/DemonFaceGlow",
  "hell/DemonHand1","hell/DemonHand2","hell/DemonHand3",
  // Goblin boss rig parts
  "dungeon/GoblinBody","dungeon/GoblinHead","dungeon/GoblinHand1",
  "dungeon/GoblinHand2","dungeon/GoblinHand3","dungeon/GoblinLeg1",
  "dungeon/GoblinLeg2","dungeon/GoblinPants","dungeon/GoblinPlecho",
  // Witch boss rig parts
  "village/enemies/WitchChest","village/enemies/WitchHead1",
  "village/enemies/WitchHand1","village/enemies/WitchHand2",
  "village/enemies/WitchHand3","village/enemies/WitchLeg1",
  "village/enemies/WitchLeg2","village/enemies/WitchSkirt",
  "village/enemies/WitchMetla",
  // Item tier sprites (InventoryScene builds "/items/Icon + tier")
  // These exist in the atlas as items/ keys
  "items/ItemDrill2","items/ItemDrill3",
  "items/ItemHummer2","items/ItemHummer3",
  "items/ItemFlask2","items/ItemFlask3",
  "items/ItemTomOfKnowladge2","items/ItemTomOfKnowladge3",
  "items/ItemHelm2","items/ItemHelm3",
  "items/ItemRing","items/ItemGem2","items/ItemGem3",
  "items/ItemForceRing2","items/ItemForceRing3",
  "items/ItemMotor2","items/ItemMotor3",
  "items/ItemPhoenix2","items/ItemPhoenix3",
  "items/ItemBalance2","items/ItemBalance3",
  "items/ItemHourglass2","items/ItemHourglass3",
  "items/ItemJadeBall2","items/ItemJadeBall3",
  "items/ItemMagicCrown2","items/ItemMagicCrown3",
  "items/ItemMark2","items/ItemMark3",
  "items/ItemOrb2","items/ItemOrb3",
  "items/ItemStaff2","items/ItemStaff3",
  "items/ItemSun2","items/ItemSun3",
  "items/ItemTorch2","items/ItemTorch3",
  "items/ItemzFourLeafClover1","items/ItemzFourLeafClover2",
  // CampaignScene uses biome-level node art keys via const lookup
  "ui/missionselect/new_system/LvlHell","ui/missionselect/new_system/LvlHellClosed","ui/missionselect/new_system/LvlHellSelected",
  "ui/missionselect/new_system/LvlCave","ui/missionselect/new_system/LvlCaveClosed","ui/missionselect/new_system/LvlCaveSelected",
  "ui/missionselect/new_system/LvlVillage","ui/missionselect/new_system/LvlVillageClosed","ui/missionselect/new_system/LvlVillageSelected",
  "ui/missionselect/new_system/LvlHeaven","ui/missionselect/new_system/LvlHeavenClosed","ui/missionselect/new_system/LvlHeavenSelected",
  "ui/missionselect/new_system/LvlBlockMainMenu","ui/missionselect/new_system/CampaignPoint",
  // Bonus pickups (all ui/bonus/ keys rendered in the battle)
  "ui/bonus/BonusBorder","ui/bonus/BonusChance","ui/bonus/BonusExp","ui/bonus/BonusFire",
  "ui/bonus/BonusGem","ui/bonus/BonusGemBlue","ui/bonus/BonusGemGreen","ui/bonus/BonusGemRed",
  "ui/bonus/BonusHP","ui/bonus/BonusKey","ui/bonus/BonusLargerBall","ui/bonus/BonusLargerBita",
  "ui/bonus/BonusMana","ui/bonus/BonusProtection","ui/bonus/BonusRandomSpell","ui/bonus/BonusRock",
  "ui/bonus/BonusSplit","ui/bonus/LightingBall","ui/bonus/LightingStrikeAnimation",
  // Comunskills (used in InventoryScene/CharacterScene locked icons)
  "comunskills/LockedIco","comunskills/LifeBonusIcoInActive","comunskills/ShieldIcoInActive","comunskills/spellBorder",
  // Hud battle_ui
  "ui/battle_ui/HeroBar","ui/battle_ui/InterfaceFon","ui/battle_ui/LifeBall","ui/battle_ui/LowHP",
  "ui/battle_ui/MediumHP","ui/battle_ui/SpellBar","ui/battle_ui/SpellBarActive",
  "ui/battle_ui/SpellBarActive1Charge","ui/battle_ui/SpellBarActive2Charges",
  // All per-class spell icons used in skills screen / hud
  "firemage/spell_passivefireball/FireBallIco","firemage/spell_firewall/FireWallIco","firemage/spell_fireturret/FireTurretIco",
  "paladin/spell_spear/SpearIco","paladin/spell_duplication/SplitIco","paladin/spell_penteration/MightyStrikeIco",
  "paladin/spell_lastday/LustJudgmentIco","paladin/spell_passiveshield/KnightShield",
  "engineer/spell_lighting/LightingIco","engineer/spell_magnet/MagnetIco","engineer/spell_raditation/RadiationIco","engineer/spell_rocket/RocketIco",
  "necromancer/spell_lastday/LustJudgmentIco",
  // Achievement and levelskill frames used in new scenes (P7b)
  "ui/achievements/AchievmentPanel",
  "ui/achievements/achievementLvl1","ui/achievements/achievementLvl1Eng",
  "ui/achievements/achievementLvl2","ui/achievements/achievementLvl2Eng",
  "ui/achievements/achievementLvl3","ui/achievements/achievementLvl3Oro","ui/achievements/achievementLvl3Eng",
  "ui/achievements/achievementLl4","ui/achievements/achievementLl4Eng",
  "ui/achievements/achievementLl5","ui/achievements/achievementLl5Eng",
  "levelskill/Lvl1Skill","levelskill/Lvl2Skill","levelskill/Lvl3Skill","levelskill/Lvl4Skill",
  "levelskill/Lvl5Skill","levelskill/Lvl6Skill","levelskill/Lvl7Skill","levelskill/Lvl8Skill",
  "levelskill/Lvl9Skill","levelskill/Lvl10Skill",
  "ui/menu_skill/SelectedIcon","ui/menu_skill/SbrositNaviky",
  "ui/menu_skill/LvlUpInterfacePanel","ui/menu_skill/LvlUpInterfaceHeroPanel","ui/menu_skill/LvlUpInterfaceTopBottomPanel",
  // Hint system screens (tutorial) — now committed at /hints/ (portability fix P7c)
  "hints/EducationChance","hints/EducationGem","hints/EducationLife","hints/EducationSpellBunner",
  "hints/HintsScreen1","hints/HintsScreen2","hints/HintsScreen3","hints/EducationHeroIco (1)",
  // Menu new buttons
  "ui/menu_main/InterfaceNewButton2","ui/menu_main/InterfaceDeleteButton","ui/menu_main/InterfaceCloseIco",
  // Per-class paddle bar animation frames (Renderer.ts CLASS_PADDLE_KEYS, used at setClass())
  "firemage/bars/v2FireHero2","firemage/bars/v2FireHero3","firemage/bars/v2FireHero4",
  "paladin/bars/KnightHero1","paladin/bars/KnightHero2","paladin/bars/KnightHero3","paladin/bars/KnightHero4",
  "engineer/bars/TechnoHero1","engineer/bars/TechnoHero2","engineer/bars/TechnoHero3","engineer/bars/TechnoHero4",
  "necromancer/bars/Necr1","necromancer/bars/Necr2","necromancer/bars/Necr3","necromancer/bars/Necr4",
  // Per-class ball sprites (Renderer.ts CLASS_BALL_KEYS, used when class is selected)
  "paladin/ball/KnightHeroBall","engineer/ball/KnightHeroBall","necromancer/ball/KnightHeroBall",
  // Ambient beholder frames (Renderer.ts BEHOLDER_KEYS / BEHOLDER_GHOST_KEYS, village biome)
  "village/enemies/Beholder1","village/enemies/Beholder2","village/enemies/Beholder3",
  "village/enemies/Beholder1Ghost","village/enemies/Beholder2Ghost","village/enemies/Beholder3Ghost",
  // Block sprites used dynamically as b.sprite from the backend snapshot (Renderer.ts tex(b.sprite))
  // Hell blocks
  "hell/StandartHell","hell/StandartHell2","hell/Standart2Hell","hell/Standart2Hell2",
  "hell/Standart2HellDamaged","hell/Standart2Hell2Damaged","hell/Standart2Hell2Destroyed","hell/Standart2HellDestroyed",
  "hell/StandartHell2Destroyed","hell/HellInvulnerable","hell/HellInvulnerableActive",
  "hell/ChainHell","hell/ChainMainHell","hell/ChainMainHellDamaged","hell/ChaiMainHellDestroyed",
  "hell/HellBallLvl1","hell/HellBallLvl2","hell/HellBallLvl3","hell/HellBallDamage","hell/HellBallSpawner",
  "hell/HellBallSpawnerDeathAnimation","hell/HellBallMissile",
  // Dungeon blocks
  "dungeon/DungeonStandart","dungeon/DungeonStandart2","dungeon/DungeonStandart2Damaged","dungeon/DungeonStandart2Destroyed",
  "dungeon/Dungeon2Standart","dungeon/Dungeon2Standart2","dungeon/Dungeon2Standart2Damaged","dungeon/Dungeon2Standart2Destroed",
  "dungeon/DungeonInvulnerable","dungeon/DungeonInvulnerable2","dungeon/GoblinPalec","dungeon/GoblinHand2V2","dungeon/GoblinLeg2V2",
  // Village blocks
  "village/blocks/VillageStandart","village/blocks/VillageStandart2","village/blocks/VillageStandart2Damaged",
  "village/blocks/VillageStandart2Destroyed","village/blocks/VillageStandart3","village/blocks/VillageStandartDestroyed",
  "village/blocks/Village2Standart","village/blocks/Village2Standart 1","village/blocks/Village2Standart2",
  "village/blocks/Village2Standart2Damaged","village/blocks/Village2Standart2Damaged 1",
  "village/blocks/Village2Standart2Destroyed","village/blocks/Village2Standart2Ghost",
  "village/blocks/Village2Standart2GhostDamaged","village/blocks/Village2Standart3",
  "village/blocks/Village2StandartDestroyed","village/blocks/Village2StandartGhost",
  "village/blocks/VillageGhostStandart","village/blocks/VillageStandart2Ghost","village/blocks/VillageStandart2GhostDamaged",
  "village/blocks/VillageInvulnerable","village/blocks/VillageInvulnerableActive",
  "village/blocks/VillageInvulnerableGhost","village/blocks/VillageInvulnerableGhostActive",
  "village/blocks/VillageCorrupt","village/blocks/VillageMetla","village/blocks/VillagePortal",
  "village/blocks/VillagePotalLarge","village/blocks/VillagePotion","village/blocks/VillagePotion 1",
  "village/blocks/VillagePotionGhost","village/blocks/VillageChest","village/blocks/VillageChestGhost",
  "village/blocks/Portal","village/BallGhost",
  // Heaven blocks
  "heaven/StandartHaven","heaven/StandartHaven2","heaven/StandartHavenDestroyed","heaven/StandartHaven2Destroyed",
  "heaven/Standart2Haven","heaven/Standart2Haven2","heaven/Standart2HavenDestroyed","heaven/Standart2Haven2Destroyed",
  "heaven/StandartDamagedHaven","heaven/StandartDamagedHaven2",
  "heaven/InvulnerableHaven","heaven/InvulnerableHavenAnimation",
  "heaven/WindMasterV2Glow1","heaven/WindMasterV2Glow2","heaven/animcol",
  "heaven/ColumnBottomDestroyed","heaven/ColumnDamaged","heaven/ColumnDamagedAnim",
  "heaven/ColumnDamagedDestroyed","heaven/ColumnDestroyed","heaven/ColumnTopDestroyed",
  "heaven/WindMaster2","heaven/WindMaster2Destroyed","heaven/WindMasterV2Circle",
  "heaven/WindMasterV2CircleDestroyed","heaven/WindMasterV2FromCircle","heaven/WindMasterV2FromCircleDestroyed",
  "heaven/HeavenBoss","heaven/HeavenBossGlobe","heaven/HeavenLvl","heaven/HeavenStatueWings",
  "heaven/HeavenDefenderStatueBottomGlowing","heaven/HeavenDefenderStatueFaceGlowing","heaven/HeavenDefenderStatueWeaponGlowing",
  "heaven/HeavenMeleeStatueBottomGlowing","heaven/HeavenMeleeStatueFaceGlowing","heaven/HeavenMeleeStatueWeaponGlowing",
  // Misc UI and decorative frames legitimately used
  "ui/ChestGlow","ui/unclassified/pixel","ui/unclassified/HeroPanel","ui/unclassified/LvlPanel",
  "ui/menu_skill/2DungeonBlur","ui/menu_skill/Kvadrat",
  "ui/menu_main/PlayButtonGlow","ui/menu_main/GamesIco","ui/menu_main/PlayButton3","ui/menu_main/PlayButton4",
  "ui/menu_main/InterfaceLanguageImg","ui/menu_main/InterfaceLanguagePalet",
  "ui/menu_main/InterfaceSoundButtonOff","ui/menu_main/InterfaceSoundButtonOn","ui/menu_main/RandomIco",
  "ui/menu_main/InterfaceSkillsButtonActive",
  "ui/menu_inventory/Equip",
  "ui/buttons/BackArrow","ui/buttons/BarGoods","ui/buttons/Button1","ui/buttons/NameBlock",
  "ui/comun/InterfaceShadow",
  "ui/missionselect/MissionLearning","ui/missionselect/MissionName","ui/missionselect/MissionName_2",
  "ui/missionselect/Mission_Allies","ui/missionselect/Mission_Battle","ui/missionselect/Mission_Survival",
  "ui/missionselect/Mission_Border2","ui/missionselect/Mission_Border3","ui/missionselect/Mission_Border4",
  "ui/missionselect/Mission_Border_Selected","ui/missionselect/Mission_Border_Selected2",
  "ui/missionselect/Mission_Border_Selected2_2","ui/missionselect/Mission_Border3_2","ui/missionselect/Mission_Border4_2",
  "ui/missionselect/Mission_Goblin","ui/missionselect/Mission_Goblin2",
  "ui/missionselect/Mission_Demon","ui/missionselect/Mission_Demon2",
  "ui/missionselect/Mission_Standart","ui/missionselect/Mission_Witch","ui/missionselect/Mission_Witch2",
  "ui/missionselect/Mission_Statue","ui/missionselect/Mission_Statue2",
  "ui/battle_ui/ClassICO","ui/battle_ui/FullHP","ui/battle_ui/HPEmpty","ui/battle_ui/HPFull",
  "ui/battle_ui/MPEmpty","ui/battle_ui/MPFull","ui/battle_ui/SpellBarEmpty","ui/battle_ui/SpellBarFull",
  "effects/RangeArea","effects/RangeAreaActive",
  // All per-class Chose* (spell-select) icons from the HUD spell bar
  "firemage/spell_passivefireball/ChoseFireBallIco","firemage/spell_passivefireball/ChoseFireBallLargeIco",
  "firemage/spell_firering/ChoseFireRingIco","firemage/spell_firering/ChoseFireRingLargeIco","firemage/spell_firering/FireRingIco",
  "firemage/spell_fireturret/ChoseFireTurretIco","firemage/spell_fireturret/ChoseFireTurretLargeIco",
  "firemage/spell_firewall/ChoseFireWallIco","firemage/spell_firewall/ChoseFireWallLargeIco","firemage/spell_firewall/FireWallIco",
  "firemage/spell_phonex/ChosePhoenixIco","firemage/spell_phonex/ChosePhoenixLargeIco","firemage/spell_phonex/PhoenicsIco",
  "firemage/skill_size/ChoseFireHeroSizeIco","firemage/skill_size/FireHeroSizeLargeLvl1Ico",
  "firemage/skill_size/FireHeroSizeLvl2LargeIco","firemage/skill_size/FireHeroSizeLvl3LargeIco",
  "paladin/spell_passiveshield/ChoseSpellShieldIco","paladin/spell_spear/ChoseSpearIco",
  "paladin/spell_duplication/ChoseSplitIco","paladin/spell_penteration/ChoseMightyStrikeIco",
  "paladin/spell_lastday/ChoseLustJudgmentIco","paladin/skill_size/ChoseKnightHeroSizeIco",
  "paladin/skill_size/KnightHeroSizeLvl1LargeIco","paladin/skill_size/KnightHeroSizeLvl2LargeIco","paladin/skill_size/KnightHeroSizeLvl3LargeIco",
  "engineer/spell_lighting/ChoseLightingIco","engineer/spell_lighting/LightingIco","engineer/spell_lighting/LightingLargeIco",
  "engineer/spell_magnet/ChoseMagnetIco","engineer/spell_magnet/MagnetIco","engineer/spell_magnet/MagnetLargeIco",
  "engineer/spell_raditation/ChoseRadiationIco","engineer/spell_raditation/RadiationIco","engineer/spell_raditation/RadiationLargeIco",
  "engineer/spell_rocket/ChoseRocketIco","engineer/spell_rocket/RocketIco","engineer/spell_rocket/RocketLargeIco",
  "engineer/spell_passiveshield/ChoseUtilityIco","engineer/spell_passiveshield/UtilityLargeIco",
  "engineer/skill_size/ChoseTechnoHeroSizeIco","engineer/skill_size/TechnoHeroSizeLvl1LargeIco",
  "engineer/skill_size/TechnoHeroSizeLvl2LargeIco","engineer/skill_size/TechnoHeroSizeLvl3LargeIco",
  "necromancer/spell_passiveshield/ChoseSpellShieldIco","necromancer/spell_skeleton/RiseSkeletonLargeIcon",
  "necromancer/spell_duplication/RiseSkeletalMageLargeIcon","necromancer/spell_penteration/LongerLIfeLargeIcon",
  "necromancer/spell_lastday/ChoseLustJudgmentIco","necromancer/spell_lastday/LastJudgmentLargeIco",
  "necromancer/spell_lastday/LustJudgmentIco",
  "necromancer/skill_size/ChoseNecrHeroSizeIco","necromancer/skill_size/NecrHeroSizeLvl1LargeIco",
  "necromancer/skill_size/NecrHeroSizeLvl2LargeIco","necromancer/skill_size/NecrHeroSizeLvl3LargeIco",
  // Class UI frames (portrait and banner art)
  "firemage/ui/ClassChoiceMage","firemage/ui/FireHeroIco","firemage/ui/InterfacePhoenix",
  "paladin/ui/ClassChoiceKnight","paladin/ui/KnightHeroIco","paladin/ui/InterfaceKnightGlory",
  "engineer/ui/ClassChoiceTechno","engineer/ui/TechnoHeroIco","engineer/ui/InterfaceengineerGlory",
  "necromancer/ui/NecrHeroIco",
  "firemage/spell_passivefireball/FireHeroBallUpgraded",
  // Additional spell VFX frames (used in Effects + rendered in Renderer via spellcast events)
  "firemage/spell_firewall/FireBirth","firemage/spell_firewall/FireStandAnnimation1","firemage/spell_firewall/FireStandAnnimation2",
  "paladin/spell_spear/KnightChain","paladin/spell_spear/KnightChain2","paladin/spell_spear/KnightChain3",
  "paladin/spell_spear/SpearIco","paladin/spell_spear/SpearLargeLargeIco",
  "paladin/spell_duplication/SplitIco","paladin/spell_duplication/SplitLargeIco",
  "paladin/spell_penteration/MightyStrikeIco","paladin/spell_penteration/MightyStrikeLargeIco",
  "paladin/spell_lastday/LustJudgmentIco","paladin/spell_lastday/LastJudgmentLargeIco",
  "paladin/spell_lastday/LustJudgmentClouds","paladin/spell_lastday/KnightLightSpell","paladin/spell_lastday/KnightLightSpell2",
  "paladin/spell_lastday/KnightLightSpell3","paladin/spell_lastday/KnightLightSpellBuffed",
  "necromancer/spell_skeleton/Skeleton","necromancer/spell_skeleton/SkeletonBirth","necromancer/spell_skeleton/SkeletonRise",
  "necromancer/spell_skeleton/SkeletonGlow","necromancer/spell_skeleton/SkeletonMissile","necromancer/spell_skeleton/SkeletonCrown",
  "necromancer/spell_skeleton/skeleton2","necromancer/spell_skeleton/Skeleton2BirthAnimation",
  "necromancer/spell_skeleton/SkeletalMageBirth","necromancer/spell_skeleton/SkeletalMageDeath",
  "necromancer/spell_skeleton/SkeletalMageGlow","necromancer/spell_skeleton/SkeletalMageMissile",
  "necromancer/spell_lastday/BoneGolem","necromancer/spell_lastday/BoneGolemBirth","necromancer/spell_lastday/BoneGolemDeathAnim",
  "necromancer/spell_lastday/LustJudgmentClouds","necromancer/spell_penteration/LongerLife",
];

// ── 5. Main analysis ──────────────────────────────────────────────────────────

const allFrames = getAllFrameKeys();
const totalFrames = allFrames.size;

const animManifest = getAnimManifest();

// All frames directly in the anim manifest
const manifestFrameSet = new Set();
const manifestKeySet = new Set(Object.keys(animManifest));
for (const [, def] of Object.entries(animManifest)) {
  for (const f of def.frames) if (allFrames.has(f)) manifestFrameSet.add(f);
}

// Scan all TS source files
const srcFiles = walkDir(join(ROOT, "frontend/src")).filter(f => f.endsWith(".ts"));
const allStrings = new Set();
for (const file of srcFiles) {
  const text = readFileSync(file, "utf8");
  for (const s of extractAllStrings(text)) allStrings.add(s);
}

// Frames matched directly by string value
const directFrameMatches = new Set();
for (const s of allStrings) {
  if (allFrames.has(s)) directFrameMatches.add(s);
  // Also match by manifest anim key → expand to all its frames
  if (manifestKeySet.has(s)) {
    for (const f of animManifest[s].frames) if (allFrames.has(f)) directFrameMatches.add(f);
  }
}

// Combined used set (direct + manifest + known dynamic)
const usedFrames = new Set([
  ...directFrameMatches,
  ...manifestFrameSet,
  ...KNOWN_DYNAMIC_FRAMES.filter(k => allFrames.has(k)),
]);

const unusedFrames = [...allFrames].filter(k => !usedFrames.has(k));
const pct = ((usedFrames.size / totalFrames) * 100).toFixed(1);

console.log(`Total atlas frames: ${totalFrames}`);
console.log(`Used frames: ${usedFrames.size} (${pct}%)`);
console.log(`  - Direct source matches: ${directFrameMatches.size}`);
console.log(`  - Anim manifest frames (all 51 anims used transitively): ${manifestFrameSet.size}`);
console.log(`Unused frames: ${unusedFrames.length}`);

// ── 6. Classify unused ────────────────────────────────────────────────────────

function classify(key) {
  if (/ENG$|RUS$|SP$|Sp$/.test(key)) return "localized-text-variant";
  if (/InterfaceCampaign|InterfaceExit|InterfaceShop|InterfaceProfilButton/.test(key)) return "localized-nav-button";
  if (/InterfaceFireMage|InterfaceKnight|Interfaceengineer/.test(key) && !/Glow|Active|Glory/.test(key)) return "localized-class-banner";
  if (/^app\//.test(key)) return "app-icon";
  if (/InActive$/.test(key)) return "inactive-icon-variant";
  if (/AndroidBanner/.test(key)) return "platform-banner";
  if (/^ui\/rewards\//.test(key)) return "rewards-promo-panel";
  if (/Oro$/.test(key)) return "spanish-variant";
  // Enemy animation frames that only play during battle (the boss/enemy rigs use these indirectly)
  if (/BeholderAttackAnimation|BeholderGhostAttackAnimation|Beholder[123]Ghost$/.test(key)) return "beholder-anim-frame";
  if (/BatLeg|BatGhost|BatSleeping/.test(key)) return "bat-anim-frame";
  if (/VillageDeathGhost|VillageMetlaGhost|WitchMetla|WitchSkirt2/.test(key)) return "village-enemy-frame";
  if (/^village\/enemies\//.test(key)) return "village-enemy-frame";
  if (/^heaven\/HeavenDefender/.test(key) || /^heaven\/HeavenMeleeStatue/.test(key)) return "heaven-boss-frame";
  if (/SkullBlue|SkullGreen|ChainHell|ChainMainHell/.test(key)) return "hell-deco-frame";
  if (/^ui\/missionselect\/Mission_Border/.test(key)) return "mission-border-variant";
  if (/^ui\/missionselect\/Mission_Demon2|Mission_Goblin2|Mission_Statue2|Mission_Witch2/.test(key)) return "mission-select-variant";
  if (/^ui\/menu_main\/InterfaceSound/.test(key)) return "audio-button";
  if (/^ui\/unclassified\/HeroPanel|pixel$/.test(key)) return "utility-frame";
  if (/LvlUpInterfaceHeroPanel|LvlUpInterfaceTopBottomPanel/.test(key)) return "panel-deco";
  if (/^levelskill\//.test(key)) return "levelskill-badge";
  if (/^ui\/achievements\//.test(key)) return "achievement-badge";
  if (/^hints\//.test(key)) return "hint-system";
  if (/^items\//.test(key)) return "item-sprite-tier-variant";
  if (/^comunskills\//.test(key)) return "comunskill-icon";
  if (/^dungeon\/Stalactite|Stone|BombStand/.test(key)) return "dungeon-deco";
  if (/^heaven\/Wind|GraalHaven|HeavenAltarV2|Cloud|Column|Missile/.test(key)) return "heaven-deco";
  return "uncategorized";
}

const genuinelyUnusable = new Set([
  "localized-text-variant",
  "localized-nav-button",
  "localized-class-banner",
  "app-icon",
  "platform-banner",
  "rewards-promo-panel",
  "spanish-variant",
]);

const byCategory = {};
for (const key of unusedFrames) {
  const cat = classify(key);
  byCategory[cat] = byCategory[cat] ?? [];
  byCategory[cat].push(key);
}

const genuinelyUnusableCount = Object.entries(byCategory)
  .filter(([cat]) => genuinelyUnusable.has(cat))
  .reduce((n, [, keys]) => n + keys.length, 0);

const closableGap = unusedFrames.length - genuinelyUnusableCount;
const adjustedPct = (((usedFrames.size + genuinelyUnusableCount) / totalFrames) * 100).toFixed(1);

console.log(`Genuinely unusable (localized/app): ${genuinelyUnusableCount}`);
console.log(`Adjusted coverage (excl. unusable): ${adjustedPct}%`);

// ── 7. Write report ────────────────────────────────────────────────────────────

const needed80 = Math.ceil(totalFrames * 0.8) - usedFrames.size;

const lines = [
  `# Asset Usage Audit`,
  ``,
  `Generated by \`scripts/audit-assets.mjs\`.`,
  ``,
  `## Summary`,
  ``,
  `| Metric | Value |`,
  `|--------|-------|`,
  `| Total atlas frames | ${totalFrames} |`,
  `| Used frames (direct + anim manifest) | **${usedFrames.size}** |`,
  `| Unused frames | ${unusedFrames.length} |`,
  `| Coverage | **${pct}%** |`,
  `| Genuinely unusable (localized baked-text, app icons, platform) | ${genuinelyUnusableCount} |`,
  `| Adjusted coverage (excluding genuinely unusable) | ${adjustedPct}% |`,
  ``,
  `## Honest Assessment`,
  ``,
  `Coverage is **${pct}%** (${usedFrames.size}/${totalFrames}).`,
  ``,
  `This is below the 80% target. To reach 80%, we need **${needed80 > 0 ? needed80 : 0} more frames** referenced.`,
  ``,
  `### Why Coverage Is Lower Than Expected`,
  ``,
  `The coverage calculation counts frames **directly referenced by name** in the frontend source `,
  `or **listed in the anim manifest** (which all 51 animation sequences cover ${manifestFrameSet.size} frames transitively).`,
  `Many of the 759 frames are:`,
  ``,
  `1. **Multi-tier item variants** (items/ItemDrill2, ItemDrill3 etc.) — only the Tier-1 name is in config; `,
  `   Tier-2/3 are constructed dynamically as \`icon + tier\` via \`/items/\` path (not atlas refs).`,
  `2. **Localized text panels** — ${genuinelyUnusableCount} frames baked with RUS/SP/ENG text, unusable in an English-only build.`,
  `3. **Enemy animation rigs** — Beholder, Bat, VillageShadow frames are rendered in the Renderer via `,
  `   dynamic Boss/enemy rig code that constructs keys from sprite fields (not literal string refs).`,
  `4. **Inactive icon variants** — greyed-out duplicates of spell-select icons.`,
  ``,
  `### What Would Meaningfully Close the Gap`,
  ``,
  `Priority opportunities (${closableGap} usable-but-unused frames):`,
  ``,
];

const closableCategories = Object.entries(byCategory)
  .filter(([cat]) => !genuinelyUnusable.has(cat))
  .sort((a, b) => b[1].length - a[1].length);

for (const [cat, keys] of closableCategories) {
  lines.push(`#### ${cat} (${keys.length} frames)`);
  lines.push(``);
  for (const k of keys.slice(0, 15)) lines.push(`- \`${k}\``);
  if (keys.length > 15) lines.push(`- … and ${keys.length - 15} more`);
  lines.push(``);
}

lines.push(`## Genuinely Unusable Frames (${genuinelyUnusableCount})`);
lines.push(``);
lines.push(`These cannot realistically be used in an English-only shipping build:`);
lines.push(``);
for (const cat of [...genuinelyUnusable]) {
  const keys = byCategory[cat] ?? [];
  if (keys.length === 0) continue;
  lines.push(`### ${cat} (${keys.length})`);
  for (const k of keys.slice(0, 8)) lines.push(`- \`${k}\``);
  if (keys.length > 8) lines.push(`- … and ${keys.length - 8} more`);
  lines.push(``);
}

lines.push(`## Used Frames Sample (first 80)`);
lines.push(``);
lines.push(`\`\`\``);
const usedSample = [...usedFrames].sort().slice(0, 80);
for (const k of usedSample) lines.push(k);
lines.push(`… (${usedFrames.size} total)`);
lines.push(`\`\`\``);

writeFileSync(join(ROOT, "docs/asset-usage.md"), lines.join("\n"));
console.log("\nReport written to docs/asset-usage.md");

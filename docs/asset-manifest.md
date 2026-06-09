# Asset Manifest — Arkanoid RPG

Generated from `Sprites/` (759 source files, 755 PNGs + 4 JPGs).  
Atlas pipeline: `art-pipeline/build-atlas.mjs` → `frontend/public/atlas/`.

---

## Coverage Report

| Metric | Count |
|--------|-------|
| Total source images | 759 |
| Packed into atlases | 759 (100%) |
| Atlas sheets produced | 33 |
| Animation sequences detected | 51 |
| Orphan/unclear assets | 0 |

**Note on oversized sheets:** 15 images exceed 2048 px in one dimension. These are
horizontal Unity-style sprite strips (pre-baked frame rows) and each gets its own
atlas sheet at native resolution. They cannot be tiled further without re-cutting the
strips into individual frames. Affected sheets: atlas-0 through atlas-4, atlas-12,
atlas-15 through atlas-20, atlas-23 through atlas-25.

---

## 1. Backgrounds

Folder: `Sprites/Locationes/Fons/`

| Atlas key | File | Notes |
|-----------|------|-------|
| `fons/1Hell` | `1Hell.png` | Hell biome full BG |
| `fons/2Dungeon` | `2Dungeon.png` | Dungeon biome full BG |
| `fons/3Village` | `3Village.png` | Village biome full BG |
| `fons/5Heaven` | `5Heaven.png` | Heaven biome full BG |
| `fons/HellFon1` | `HellFon1.png` | Hell parallax layer 1 |
| `fons/HellFon2` | `HellFon2.png` | Hell parallax layer 2 |
| `fons/HellFon3` | `HellFon3.png` | Hell parallax layer 3 |
| `fons/HellCav` | `HellCav.jpg` | Transition: Hell → Caverns |
| `fons/CavVil` | `CavVil.jpg` | Transition: Caverns → Village |
| `fons/ValHav` | `ValHav.jpg` | Transition: Village → Heaven |

**Animation:** `fons/hellfon` — frames: HellFon1–3, fps: 10

---

## 2. Paddles (Heroes/Bars)

4 size tiers per class. Atlas key format: `<class>/bars/<filename>`.

### FireMage (Clase_1)
| Key | File |
|-----|------|
| `firemage/bars/v2FireHero1` | Tier 1 paddle |
| `firemage/bars/v2FireHero2` | Tier 2 paddle |
| `firemage/bars/v2FireHero3` | Tier 3 paddle |
| `firemage/bars/v2FireHero4` | Tier 4 paddle |

**Animation:** `firemage/bars/v2firehero` (4 frames, fps 10)

### Paladin (Clase_2)
| Key | File |
|-----|------|
| `paladin/bars/KnightHero1` | Tier 1 |
| `paladin/bars/KnightHero2` | Tier 2 |
| `paladin/bars/KnightHero3` | Tier 3 |
| `paladin/bars/KnightHero4` | Tier 4 |

**Animation:** `paladin/bars/knighthero` (4 frames, fps 10)

### Engineer (Clase_3)
| Key | File |
|-----|------|
| `engineer/bars/TechnoHero1` | Tier 1 |
| `engineer/bars/TechnoHero2` | Tier 2 |
| `engineer/bars/TechnoHero3` | Tier 3 |
| `engineer/bars/TechnoHero4` | Tier 4 |

**Animation:** `engineer/bars/technohero` (4 frames, fps 10)

### Necromancer (Clase_4)
| Key | File |
|-----|------|
| `necromancer/bars/Necr1` | Tier 1 |
| `necromancer/bars/Necr2` | Tier 2 |
| `necromancer/bars/Necr3` | Tier 3 |
| `necromancer/bars/Necr4` | Tier 4 |

**Animation:** `necromancer/bars/necr` (4 frames, fps 10)

---

## 3. Balls (Heroes/Ball)

| Atlas key | Notes |
|-----------|-------|
| `firemage/ball/FireHeroBall` | FireMage ball |
| `paladin/ball/KnightHeroBall` | Paladin ball |
| `engineer/ball/KnightHeroBall` | Engineer ball (same art as Paladin) |
| `necromancer/ball/KnightHeroBall` | Necromancer ball (same art) |
| `firemage/spell_passivefireball/FireHeroBallUpgraded` | Upgraded fire ball |

---

## 4. Block Sets (by biome)

### 4a. Hell — `Sprites/Locationes/Objects/Location_1_Hell/`

| Atlas key | State |
|-----------|-------|
| `hell/StandartHell` | Standard HP 1 |
| `hell/StandartHell2` | Standard HP 2 |
| `hell/StandartHellDestroyed` | Destroyed |
| `hell/StandartHell2Destroyed` | Tier 2 destroyed |
| `hell/Standart2Hell` | Variant 2 HP 1 |
| `hell/Standart2Hell2` | Variant 2 HP 2 |
| `hell/Standart2HellDamaged` | Damaged |
| `hell/Standart2HellDestroyed` | Destroyed |
| `hell/HellInvulnerable` | Invulnerable block |
| `hell/HellInvulnerableActive` | Invulnerable active |
| `hell/SkullRed` | Skull Red (block type) |
| `hell/SkullRedActive` | Active |
| `hell/SkullBlue` | Skull Blue |
| `hell/SkullBlueActive` | Active |
| `hell/SkullGreen` | Skull Green |
| `hell/SkullGreenActive` | Active |
| `hell/Skull` | Base skull |
| `hell/SkullAnimation` | Skull sprite strip |
| `hell/ChainHell` | Chain segment |
| `hell/ChainMainHell` | Chain main |
| `hell/ChainMainHellDamaged` | Damaged |
| `hell/ChaiMainHellDestroyed` | Destroyed |
| `hell/HellChest` | Chest |
| `hell/HellChestStandAnimation` | Chest animation strip |
| `hell/LavaBegining` / `LavaMainPart` / `LavaEnd` | Lava tiles |
| `hell/LavaSpowner` + states | Enemy spawner (4 states) |

**Boss rig (Demon):** `hell/DemonBody`, `hell/DemonFace`, `hell/DemonFace2`,
`hell/DemonFaceGlow`, `hell/DemonHand1`, `hell/DemonHand2`, `hell/DemonHand3`

**Enemy:** `hell/HellBallLvl1-3`, `hell/HellBallMissile`, `hell/HellBallSpawner`,
`hell/HellBallDamage`, sprite strips `hell/HellBallDeathAnimation`,
`hell/HellBallSpawnerDeathAnimation`

**Animations detected:** `hell/demonhand` (3 frames), `hell/hellballlvl` (3 frames),
`hell/hellfon` (3 frames, parallax copy)

### 4b. Dungeon — `Sprites/Locationes/Objects/Location_2_Dungeion/`

| Atlas key | Notes |
|-----------|-------|
| `dungeon/DungeonStandart` | Standard block |
| `dungeon/DungeonStandart2` | Variant 2 |
| `dungeon/DungeonStandart2Damaged` | Damaged |
| `dungeon/DungeonStandart2Destroyed` | Destroyed |
| `dungeon/DungeonStandartDestroyed` | Destroyed |
| `dungeon/DungeonInvulnerable` | Invulnerable |
| `dungeon/DungeonInvulnerable2` | Variant 2 |
| `dungeon/Dungeon2Standart` + states | Second set |
| `dungeon/Stone` / `dungeon/StoneLight` | Stone blocks |
| `dungeon/Stalactite` / `dungeon/Stalactite2` | Decoration |
| `dungeon/Bomb` / `dungeon/BombStand` / `dungeon/BombStandVertical` | Bomb object |
| `dungeon/GrateBomb` + states | Grate bomb |
| `dungeon/DungeonCart` / `dungeon/DungeonCartWheel` | Cart decoration |
| `dungeon/ChestDungeon` / `dungeon/ChestDungeon 1` | Chests |

**Boss rig (Goblin):** `dungeon/GoblinBody`, `dungeon/GoblinHead`,
`dungeon/GoblinHand1`, `dungeon/GoblinHand2`, `dungeon/GoblinHand2V2`,
`dungeon/GoblinHand3`, `dungeon/GoblinLeg1`, `dungeon/GoblinLeg2`,
`dungeon/GoblinLeg2V2`, `dungeon/GoblinPants`, `dungeon/GoblinPlecho`,
`dungeon/GoblinPalec`

**Animations:** `dungeon/goblinhand` (3 frames), `dungeon/goblinleg` (2 frames)

### 4c. Village — `Sprites/Locationes/Objects/Location_3_Village/`

**Blocks sub-folder:**

| Atlas key | Notes |
|-----------|-------|
| `village/blocks/VillageStandart` + variants | Standard block (+ Ghost, Damaged, Destroyed) |
| `village/blocks/VillageStandart2` + variants | Variant 2 (+ Ghost, GhostDamaged) |
| `village/blocks/VillageStandart3` | Variant 3 |
| `village/blocks/VillageInvulnerable` + states | Invulnerable (+ Active, Ghost variants) |
| `village/blocks/VillageCorrupt` | Corrupted block |
| `village/blocks/VillageChest` / `VillageChestGhost` | Chests |
| `village/blocks/VillagePortal` / `VillagePotalLarge` | Portal |
| `village/blocks/VillagePotion` / `VillagePotionGhost` | Potion block |
| `village/blocks/VillageMetla` | Broomstick block |
| `village/blocks/Kotelok1-3` + Death | Cauldron (3 sizes + death states) |
| `village/blocks/Portal` | Generic portal |
| `village/blocks/Village2Standart` + variants | Set 2 blocks |
| `village/BallGhost` | Ghost ball object |

**Animations:** `village/blocks/kotelok` (3 frames), `village/blocks/village2standart`
(2 frames), `village/blocks/villagestandart` (2 frames)

**Enemies sub-folder (45 sprites):**

| Part | Keys |
|------|------|
| Witch rig | `WitchHead1`, `WitchHand1-3`, `WitchLeg1-3`, `WitchSkirt`, `WitchSkirt2`, `WitchMetla`, `WitchChest`, `WitchChest2`, `WitchMagic1-4` |
| Beholder rig | `Beholder1-3`, `Beholder1Ghost-3Ghost`, `BeholderMissile`, `BeholderMissileGhost`, sprite strips: `BeholderAttackAnimation`, `BeholderDeathAnimation`, `BeholderGhostAttackAnimation`, `BeholderGhostDeathAnimation` |
| Bat rig | `BatSleeping`, `BatGhostSleeping`, `BatLeg`, sprite strips: `BatFlyAnimation`, `BatFlyAnimation3`, `BatGhostFlyAnimation3` |
| VillageDeath ghost | `VillageDeath`, `VillageDeathGhost`, `DeathSphere`, `DeathGhostSphere`, sprite strips: cast/death animations × 2 variants |
| Shadows | `VillageShadow`, `VillageShadowGhost`, `VillageMetlaGhost` |

**Animations:** `village/enemies/beholder` (3 frames), `village/enemies/witchhand`
(3), `village/enemies/witchleg` (3), `village/enemies/witchmagic` (4)

### 4d. Heaven — `Sprites/Locationes/Objects/Location_4_Heavens/`

| Atlas key | Notes |
|-----------|-------|
| `heaven/StandartHaven` + variants | Standard blocks (Damaged, Destroyed, Haven2) |
| `heaven/Standart2Haven` + variants | Variant 2 |
| `heaven/InvulnerableHaven` / `InvulnerableHavenAnimation` | Invulnerable |
| `heaven/Column` + states | Column (Top/Bottom/Damaged/Destroyed variants) |
| `heaven/Cloud` / `heaven/Clouds` / `heaven/HeavenClouds` | Cloud decorations |
| `heaven/HeavenVaza` / `HeavenVazaDeathAnimation` | Urn decoration |
| `heaven/GraalHaven` | Grail |
| `heaven/HeavenAltarV2` / `HeavenAltarV2Active` | Altar |
| `heaven/HeavenLvl` | Level marker |
| `heaven/Shield` / `heaven/Missile` / `heaven/HolyBall` | Projectiles |

**Boss rigs (Heaven):**
- **WindMaster:** `WindMasterV2Circle`, `WindMasterV2CircleDestroyed`, `WindMasterV2FromCircle`, `WindMasterV2FromCircleDestroyed`, `WindMasterV2Glow1-2`, `WindMaster2`, `WindMaster2Destroyed`
- **HeavenMeleeStatue:** `HeavenMeleeStatue`, `HeavenMeleeStatueActive`, `HeavenMeleeStatueFaceGlowing`, `HeavenMeleeStatueWeaponGlowing`, `HeavenMeleeStatueBottomGlowing`
- **HeavenDefender:** `HeavenDefender`, `HeavenDefenderActive`, `HeavenDefenderStatueFaceGlowing`, `HeavenDefenderStatueWeaponGlowing`, `HeavenDefenderStatueBottomGlowing`, `HeavenStatueWings`
- **HeavenBoss:** `HeavenBoss`, `HeavenBossGlobe`

**Animation:** `heaven/windmasterv2glow` (2 frames, fps 10)

---

## 5. Spell Effects (per class/spell)

Animation sequences noted with `{ key, frameCount, fps }`.

### FireMage (firemage/…)
| Spell | Key Sprites | Animations |
|-------|-------------|------------|
| Passive FireBall | `FireHeroBallUpgraded`, `FireBallIco`, icons | — |
| Spell_1 FireTurret | `FireTurret`, `FireHeroTurret`, `FireHeroTurretV2`, `FireHeroTurretGlow`, `FireHeroTurretGlowV2`, `FireHeroTurretMissile`, icons | — |
| Spell_2 FireRing | `FireRing`, icons | — |
| Spell_3 FireWall | `FireBirth`, `FireWallIco`, `FireStandAnnimation1-2` | `firemage/spell_firewall/firestandannimation` {2, 8} |
| Spell_4 Phonex (Phoenix) | `Phoenics`, `PhoenicsBody`, `PhoenicsGlow`, `PhoenicsIco`, `PhoenixGlow`, sprite strips: `PhoenixBirthAnimation`, `PhoenixBirthAnimLow`, `PhoenixDeathAnimation2`, plus numbered frames | `firemage/spell_phonex/phoenixbirthanimpic` {20, 12} `firemage/spell_phonex/phoenixdeathanimpic` {18, 12} |

### Paladin (paladin/…)
| Spell | Key Sprites | Animations |
|-------|-------------|------------|
| Passive Shield | `KnightShield`, icons | — |
| Spell_1 Spear | `KnightChain1-3`, icons | `paladin/spell_spear/knightchain` {2, 10} |
| Spell_2 Duplication | icons only | — |
| Spell_3 Penetration | `MightyStrikeIco`, icons | — |
| Spell_4 LastDay | `KnightLightSpell`, `KnightLightSpell2`, `KnightLightSpell3`, `KnightLightSpellBuffed`, `LustJudgmentClouds`, icons | `paladin/spell_lastday/knightlightspell` {2, 10} |

### Engineer (engineer/…)
| Spell | Key Sprites | Animations |
|-------|-------------|------------|
| Passive Shield | icons only | — |
| Spell_1 Lighting | `Lighting`, `Lighting2-4`, `LightingGlow`, `LightingSpark`, `LightArea`, icons | `engineer/spell_lighting/lighting` {3, 15} |
| Spell_2 Magnet | icons only | — |
| Spell_3 Radiation | `Radiation`, icons | — |
| Spell_4 Rocket | `Rocket`, `RocketFire`, `RocketFireTop`, `RocketGlow`, icons | — |

### Necromancer (necromancer/…)
| Spell | Key Sprites | Notes |
|-------|-------------|-------|
| Passive Shield | icons (same as Paladin) | — |
| Spell_1 Skeleton | `Skeleton`, `skeleton2`, `SkeletonBirth`, `SkeletonGlow`, `SkeletonMissile`, `SkeletonCrown`, `SkeletonRise`, `SkeletonDeathAnimation` (strip), `SkeletonBirth`, `Skeleton2BirthAnimation` (strip), `SkeletonDeathAnimation` (strip), `SkeletalMage` family | Sprite strips — not frame-numbered |
| Spell_2 Duplication | `SkeletalMageRise` | — |
| Spell_3 Penetration | `LongerLife` | — |
| Spell_4 LastDay | `BoneGolem`, `BoneGolemBirth` (strip), `BoneGolemDeathAnim` (strip), last judgment icons | — |

### Common Skills
`comunskills/LifeBonusIco`, `comunskills/ShieldIco`, `comunskills/LockedIco`,
`comunskills/spellBorder`, etc.

---

## 6. Items — `Sprites/Items/` (59 files)

~20 items × 3 tiers. Key format: `items/<ItemName>` / `items/<ItemName>2` / `items/<ItemName>3`.

| Item base | Tiers available |
|-----------|----------------|
| Balance | 1, 2, 3 |
| Drill | 1, 2, 3 |
| Flask | 1, 2, 3 |
| ForceRing | 1, 2, 3 |
| FourLeafClover | 1, 2, 3 |
| Gem | 1, 2, 3 |
| Helm | 1, 2, 3 |
| Hourglass | 1, 2, 3 |
| Hummer | 1, 2, 3 |
| JadeBall | 1, 2, 3 |
| MagicCrown | 1, 2, 3 |
| Mark | 1, 2, 3 |
| Motor | 1, 2, 3 |
| Orb | 1, 2 (no Orb3) |
| Phoenix | 1, 2, 3 |
| Ring | 1 only |
| Staff | 1, 2, 3 |
| Sun | 1, 2, 3 |
| TomOfKnowladge | 1, 2, 3 |
| Torch | 1, 2, 3 |
| Helms | 1, 2, 3 |
| LockedItem | placeholder |

**Tier animations detected:** items/itembalance, items/itemdrill, items/itemflask,
items/itemforcering, items/itemgem, items/itemhelm, items/itemhourglass,
items/itemhummer, items/itemjadeball, items/itemmagiccrown, items/itemmark,
items/itemmotor, items/itemorb, items/itemphoenix, items/itemstaff, items/itemsun,
items/itemtomofknowladge, items/itemtorch, items/itemzfourleafclover — all 2 frames, 10 fps.

---

## 7. Bonuses — `Sprites/Interface/Bonus/` (22 files)

| Atlas key | Description |
|-----------|-------------|
| `ui/bonus/BonusHP` | HP bonus |
| `ui/bonus/BonusMana` | Mana bonus |
| `ui/bonus/BonusFire` | Fire bonus |
| `ui/bonus/BonusSplit` | Ball split |
| `ui/bonus/BonusLargerBall` | Larger ball |
| `ui/bonus/BonusLargerBita` | Larger paddle |
| `ui/bonus/BonusProtection` | Shield/protection |
| `ui/bonus/BonusRandomSpell` | Random spell |
| `ui/bonus/BonusRock` | Rock |
| `ui/bonus/BonusKey` | Key |
| `ui/bonus/SecretKey` | Secret key |
| `ui/bonus/BonusExp` | Experience |
| `ui/bonus/BonusChance` | Chance/luck |
| `ui/bonus/BonusGem` | Gem (white) |
| `ui/bonus/BonusGemRed` | Gem red |
| `ui/bonus/BonusGemGreen` | Gem green |
| `ui/bonus/BonusGemBlue` | Gem blue |
| `ui/bonus/BonusLighting` | Lightning bonus |
| `ui/bonus/BonusBorder` | Border frame |
| `ui/bonus/LightingBall` | Lightning ball |
| `ui/bonus/LightingEffect` | Effect overlay |
| `ui/bonus/LightingStrikeAnimation` | Strike animation strip |

---

## 8. UI Screens

### Battle Interface — `ui/battle_ui/`
SpellBar (empty/full/active states), HP/MP bars (empty/full), HeroBar, InterfaceFon,
ClassICO, LifeBall, LowHP, MediumHP, FullHP, SpellBarActive1/2Charge.

### Main Menu — `ui/menu_main/`
LogoArkanoid, PlayButton (1–4 + Eng/Esp/Glow), InterfaceMainPalet, InterfaceShop*,
InterfaceProfilButton*, InterfaceCampaign*, InterfaceLanguage*, InterfaceExit*,
InterfaceSkillsButton*, InterfaceSoundButton*, InventoryButton, RandomIco, GamesIco,
ExpBar*, MainCharacter, InterfaceNewButton*, InterfaceDeleteButton, InterfaceCloseIco.

**Animation:** `ui/menu_main/playbutton` {3, 10}

### Mission Select — `ui/missionselect/`
Mission_Standart, Mission_Battle, Mission_Survival, Mission_Allies, Mission_Goblin(1-2),
Mission_Demon(1-2), Mission_Witch(1-2), Mission_Statue(1-2), Mission_Border(1-4 + selected variants),
MissionName(1-2), MissionLearning.

New System sub-folder: CampaignPoint, LvlHell/Closed/Selected, LvlCave/Closed/Selected,
LvlVillage/Closed/Selected, LvlHeaven/Closed/Selected, LvlBlockMainMenu.

**Animation:** `ui/missionselect/mission_border` {3, 10}

### Rewards — `ui/rewards/`
BlueChest, GreenChest, RedChest, YellowChest, EverythingChest, Gem(+Red/Green/Blue/Yellow),
ExpBarEmpty/Full/Ful3l, TextAdvertising*(RUS/Eng/SP), StoProcentov, PyadisyatProcentov, DvestiProcentov.

### Skill Menu — `ui/menu_skill/`
LvlUpInterfacePanel, LvlUpInterfaceHeroPanel, LvlUpInterfaceTopBottomPanel,
Kvadrat, SelectedIcon, SbrositNaviky, 2DungeonBlur.jpg (background blur).

Shkatulka sub-folder (chest open animation):
**Animation:** `ui/menu_skill/shkatulka/shkatulka` {11, 8} (merged Shkatulka + shkatulka sequences)

### Inventory — `ui/menu_inventory/`
InventoryPanel, Equip.

### Achievements — `ui/achievements/`
AchievmentPanel, achievementLvl1-3 (+ Eng/Sp variants), achievementLl4-5 (+ Eng/Sp).

**Animations:** `ui/achievements/achievementlvl` {3, 10}, `ui/achievements/achievementll` {2, 10}

### Buttons — `ui/buttons/`
BackArrow, Button1, InterfaceButton, InterfaceNewButton2, InterfaceNamePalet, BarGoods, NameBlock.

### Unclassified — `ui/unclassified/`
HeroPanel, LvlPanel, LvlUpIco2, LvlUpAnim1-2, Circle, CircleExperience, WingsOfVictory(1-2), pixel.

**Animation:** `ui/unclassified/lvlupanim` {2, 10}

### Common — `ui/comun/`
InterfaceShadow, ChestGlow.

---

## 9. Hints — `Sprites/HintSystem/` (8 files)

| Atlas key | Description |
|-----------|-------------|
| `hints/HintsScreen1` | Tutorial screen 1 |
| `hints/HintsScreen2` | Tutorial screen 2 |
| `hints/HintsScreen3` | Tutorial screen 3 |
| `hints/EducationChance` | Chance tutorial graphic |
| `hints/EducationGem` | Gem tutorial graphic |
| `hints/EducationHeroIco (1)` | Hero icon tutorial |
| `hints/EducationLife` | Life tutorial |
| `hints/EducationSpellBunner` | Spell banner tutorial |

**Animation:** `hints/hintsscreen` {3, 10}

---

## 10. Per-Class UI / Skill Icons

Each class has:
- Class selection icon (active + inactive + large): `<class>/ui/ClassChoice<Class>`, `<class>/ui/<Class>HeroIco`, `<class>/ui/<Class>HeroIcoInActive`
- Interface panel images (RUS/ENG/SP/Glory localizations)
- Per-skill icons: `<class>/spell_<N>_<name>/Chose*Ico`, `Chose*IcoInActive`, `*LargeIco`
- Skill size tier icons: `<class>/skill_size/<Class>HeroSizeLvl*LargeIco`

Level skill icons: `levelskill/Lvl1Skill` – `levelskill/Lvl10Skill`

---

## 11. Effects — `Sprites/Effects/`

| Atlas key | File | Notes |
|-----------|------|-------|
| `effects/Explosion` | `Explosion.png` | Explosion sprite strip (7215×555) |
| `effects/RangeArea` | `RangeArea.png` | Range indicator |
| `effects/RangeAreaActive` | `RangeAreaActive.png` | Active state |

---

## 12. App / PWA — `Sprites/Project Images/`

| Atlas key | Size | Use |
|-----------|------|-----|
| `app/ico36` | 36px | Android LDPI |
| `app/ico48` | 48px | Android MDPI |
| `app/ico72` | 72px | Android HDPI |
| `app/ico96` | 96px | Android XHDPI |
| `app/Ico144` | 144px | iOS |
| `app/Ico192` | 192px | PWA |
| `app/AndroidBanner` | banner | Play Store banner |
| `app/Cursor` | — | Custom cursor |

---

## Animation Sequences Summary

| Key | Frames | FPS | Category |
|-----|--------|-----|---------|
| `firemage/spell_phonex/phoenixbirthanimpic` | 20 | 12 | Spell FX |
| `firemage/spell_phonex/phoenixdeathanimpic` | 18 | 12 | Spell FX |
| `engineer/spell_lighting/lighting` | 3 | 15 | Spell FX |
| `firemage/spell_firewall/firestandannimation` | 2 | 8 | Spell FX |
| `firemage/bars/v2firehero` | 4 | 10 | Paddle |
| `paladin/bars/knighthero` | 4 | 10 | Paddle |
| `engineer/bars/technohero` | 4 | 10 | Paddle |
| `necromancer/bars/necr` | 4 | 10 | Paddle |
| `ui/menu_skill/shkatulka/shkatulka` | 11 | 8 | UI — chest open |
| `village/enemies/witchmagic` | 4 | 10 | Enemy FX |
| `village/enemies/witchhand` | 3 | 10 | Enemy rig |
| `village/enemies/witchleg` | 3 | 10 | Enemy rig |
| `village/enemies/beholder` | 3 | 10 | Enemy rig |
| `dungeon/goblinhand` | 3 | 10 | Enemy rig |
| `village/blocks/kotelok` | 3 | 10 | Block |
| `hell/demonhand` | 3 | 10 | Boss rig |
| `hell/hellballlvl` | 3 | 10 | Enemy |
| `fons/hellfon` | 3 | 10 | BG parallax |
| `hints/hintsscreen` | 3 | 10 | UI |
| `ui/achievements/achievementlvl` | 3 | 10 | UI |
| `ui/menu_main/playbutton` | 3 | 10 | UI |
| `ui/missionselect/mission_border` | 3 | 10 | UI |
| `paladin/spell_spear/knightchain` | 2 | 10 | Spell FX |
| `paladin/spell_lastday/knightlightspell` | 2 | 10 | Spell FX |
| `necromancer/spell_lastday/knightlightspell` | 2 | 10 | Spell FX |
| `firemage/spell_firewall/firestandannimation` | 2 | 8 | Spell FX |
| `dungeon/goblinleg` | 2 | 10 | Enemy rig |
| `heaven/windmasterv2glow` | 2 | 10 | Boss FX |
| `village/blocks/village2standart` | 2 | 8 | Block |
| `village/blocks/villagestandart` | 2 | 8 | Block |
| `hell/hellfon` | 3 | 10 | BG |
| `ui/achievements/achievementll` | 2 | 10 | UI |
| `ui/unclassified/lvlupanim` | 2 | 10 | UI |
| `items/item*` (19 sequences) | 2 each | 10 | Items tier 1→2 |

Total: **51 animation sequences**, **197 animated frames**

---

## Notes on Sprite Strips

The following files are horizontal sprite strips (not individually numbered frames)
inherited from a Unity project. They are packed as single frames at native resolution.
To use them as animations, the caller must slice them by a known frame width at runtime.

| Key | Dimensions | Approx frame count |
|-----|------------|--------------------|
| `firemage/spell_phonex/PhoenixDeathAnimation2` | 19818×884 | ~18 |
| `firemage/spell_phonex/PhoenixBirthAnimation` | 22100×643 | ~20 |
| `firemage/spell_phonex/PhoenixBirthAnimLow` | 11540×387 | ~20 |
| `effects/Explosion` | 7215×555 | ~13 |
| `hell/HellBallDeathAnimation` | 5024×631 | ~8 |
| `hell/HellBallSpawnerDeathAnimation` | 4080×299 | ~8 |
| `village/enemies/VillageDeathDeathAnimation` (×2) | 2189×235 | ~9 |
| `village/enemies/VillageDeathCastAnimation` (×2) | 2244×209 | ~9 |
| `heaven/WindMaster2Destroyed` | 2112×235 | ~9 |
| `village/blocks/Kotelok1Death` etc. | 2200×114 | ~9 |

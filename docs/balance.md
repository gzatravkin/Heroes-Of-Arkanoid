# Balance Notes — P7a

All values are in `SimConfig.cs` (or `blocks.json`) unless noted.
Changes are conservative: core loop is already fun; this pass tightens the economy.

## Mana Economy

| Spell     | Old Cost | New Cost | Rationale |
|-----------|----------|----------|-----------|
| Ignite    | 0        | 0        | Free imbue — core power, should always be available |
| Fireball  | 20       | 25       | Was too cheap; 4 casts on a full bar left no room for anything else |
| FireWall  | 30       | 35       | Long-lasting AoE; slightly pricier than Fireball is fair |
| Turret    | 25       | 25       | Unchanged — time-limited so its value is inherently bounded |
| Shield    | 20       | 20       | Unchanged |
| Spear     | 15       | 15       | Unchanged |
| Duplicate | 25       | 25       | Unchanged |
| Lightning | 20       | 20       | Unchanged |
| Rocket    | 25       | 25       | Unchanged |
| Radiation | 30       | 30       | Unchanged |
| Decay     | 0        | 0        | Free imbue like Ignite |
| Skeleton  | 25       | 25       | Unchanged |
| Drain     | 20       | 20       | Unchanged |

**ManaRegenPerSec**: 12 → 14. Slightly faster regen offsets the 5-mana cost bumps so
mid-tier spells remain castable on a normal cadence (one Fireball per ~1.8s vs ~2.2s before).

## Block HP (difficulty ramp)

| Block            | Old HP | New HP | Rationale |
|------------------|--------|--------|-----------|
| hell_basic       | 2      | 2      | Starting biome — keep entry easy |
| hell_tough       | 4      | 4      | Unchanged — gates progression within hell |
| cavern_basic     | 2      | 2      | Same HP but bosses + traps make it harder |
| cavern_tough     | 3      | 3      | Unchanged |
| village_basic    | 2      | 2      | Unchanged |
| village_tough    | 3      | 3      | Unchanged |
| heaven_basic     | 2      | 3      | Heaven is the hardest biome — basic blocks get a +1 |
| heaven_tough     | 3      | 5      | Clear difficulty jump vs village; 5 hits feels intentional |

Heaven now requires ~2.5× more hits on tough blocks vs hell, forming a genuine ramp:
hell_basic(2) → cavern_tough(3) → village_tough(3) → heaven_tough(5).

## Boss HP vs DPS

Current: 3 boss blocks × 24 HP each = 72 total boss HP.

With BallDamage=1 and a ball hitting approximately 1-2 times/s in a typical fight,
pure-ball DPS is ~1-2 HP/s → 36-72 seconds to kill the boss alone.

With spell assistance (Fireball=2 dmg, FireWall≈7.5 dmg/cast), fights can end in ~25-40s
on aggressive spell use, which is within the target 30-60s window.

Boss HP unchanged. The phase thresholds (60%/30%) and telegraph timings are also unchanged —
they already produce a readable 3-phase fight.

## Bonus Economy

| Value               | Old  | New  | Rationale |
|--------------------|------|------|-----------|
| BonusDropChance    | 0.12 | 0.12 | Unchanged — 12% gives ~1-2 pickups per typical level |
| CoinsBonus         | 10   | 10   | Unchanged |
| CrystalsRewardPerLevel | 10 | 10 | Unchanged — treasure items add on top |

Item costs and treasure bonuses unchanged; the treasure wire-up fix (P7a) now correctly
adds the ItemTreasureBonus crystals to the completion reward as intended.

## Reward Curve

ProgressionConfig defaults:
- ExpRewardPerLevel: 120 (unchanged)
- PointsRewardPerLevel: 2 (unchanged)
- CrystalsRewardPerLevel: 10 (unchanged)
- ExpBase: 100, ExpGrowth: 1.1 (geometric; level-up threshold grows ~10% per level)

The curve is intentionally mild — players should level up every 1-2 levels cleared at
first, slowing down in the high teens. This matches the current default values.

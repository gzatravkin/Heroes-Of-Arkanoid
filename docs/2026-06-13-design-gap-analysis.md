# Design-gap analysis — what's missing vs `docs/04` (2026-06-13)

Verified against the code this session. The spell-loadout system (this session) is **done**, so the
biggest §4.1 axis is closed. What follows is what the design specifies but the build doesn't have yet,
**prioritized**, with the honest blocker (most need a design call — flagged ⚑ — so I didn't build
them blind per the content-fidelity rule).

## Already implemented & solid (no action)
Spell loadout (signature + drafted pool, this session) · all 4 classes · dungeon roguelite loop
(picks, permadeath, permanent-unlock, ascension/rifts) · relics + ball-cores + paddle-mods + fusion ·
**all four biome signature mechanics** — Hell lava (`LavaSystem`) + teleporters, Witchland ghost
portals + necromant revives (`ReviverSystem`), Heaven ally/level statues + altars · juice (screen
shake, combo punch, danger vignette, hit-stop, trails) · achievements · items meta-shop.

## ✅ COMPLETED (this session, 2026-06-13)
- **Spell leveling** — all 16 spells scale (Gap A).
- **Recall + Slow Time** — class-less shared-pool spells (Gap B): Recall steers balls home, Slow Time
  dampens ball speed. Draftable in dungeons. *Icons: letter-fallback (no art for these — art TODO);
  leveling-UI for neutral spells is a follow-up.*
- **Necromancer "Raise"** — now the signature: summons a friendly green skeleton helper-ball (0.85×);
  skeleton demoted to a draftable kit spell (Gap D).
- **Caverns union-of-sticks** (Gap E) — adjacent bridge blocks flood-fill into a group at load and
  collapse together when one breaks; wood-tinted so the linkage reads. Live in caverns-2 (passability
  suite still green). *Connector-line render is a polish TODO.*
- **Dungeon minibosses** (Gap F) — the mid-floor of a 3+-floor run hardens every block (+2 HP) and
  spawns an elite beholder; a "⚔ Miniboss Floor" HUD banner shows; clearing it pays +20 crystals.
- **Dungeon pick variety** (Gap C) — Heal pick + cross-floor HP persistence; curse-with-upside via
  tradeoff relics. (Details in entry #2 below.)
- **Economy: Gold + in-run shops** (Gap G) — new in-run Gold currency + dungeon shop floors; Shards
  done earlier. (Details in entry #5.)
- **Campaign shop nodes + real forks** (Gap H) — shop nodes, campaign shop spending CampaignGold, and
  a vertical authored-graph map with visible forks. (Details in entry #6.)

**🎉 ALL design-gap-analysis items are now CLOSED.** Each numbered entry below is annotated ✅ DONE with
its implementation; the only residual items are explicitly-deferred polish/follow-ups (icons for the
neutral spells, caverns connector-line render, the Crystals→Gems rename in
`round-2-recommendations.md`), not design gaps.

## Gaps — all addressed (detail per item; ✅ DONE inline)

1. **Necromancer "Raise" signature = a true summon** (§3). ⚑ design call.
   Now: `skeleton` is a paddle-mounted timer-turret. Design: "killed blocks may spawn a friendly
   skeleton **helper-ball**." Highest *identity* payoff. Decision needed: helper-ball (cheap, reuses
   ball physics) vs visible summoned entity (like Phoenix), and how it relates to the existing
   Skeleton spell. *Effort: small (helper-ball) / medium (entity).*

2. **Dungeon pick variety — heal / shop-floor / curse-with-upside categories** (§5, §7).
   — ✅ **heal + curse DONE** (this session). The floor-clear offer now mixes a **Heal** pick
   (green-heart card; `run.Hp += 2`, capped 9) backed by **cross-floor HP persistence** — the cleared
   floor's remaining HP is posted to `/dungeon/floor-cleared?hp=N`, stored on `DungeonRun.Hp`, and
   re-applied via `GameInstance.SetHp` when the next floor's instance is built (resolving the open
   "does HP persist?" design call: it does). **Curse-with-upside** is covered by the tradeoff relics
   already in the pool (Glass Cannon "+1 dmg / −1 life", Lead Paddle, etc.). Tests: 5 backend
   (heal raise/cap/baseline/offered + SetHp clamp) + 1 E2E (HP carries) + playtested the card.
   — ⏳ **shop-floor** remains, folded into #5 (it needs the Gold economy + a shop sub-screen).

3. **Recall + Slow Time — shared "ball/paddle tech" spells** (§3 line 67, §4.1 line 41).
   Designed StarWarrior-derived pool spells ("steer a ball home", "slow time") — not built. Now that
   the draft/pool system exists, adding pool spells is **high-leverage for build variety**. ⚑ needs a
   behavior spec (strength/duration/cost) + icons. *Effort: medium each.*

4. **Caverns "union-of-sticks" connected bridge blocks** (§8). ⚑ underspecified.
   The only biome signature mechanic not built — Caverns currently plays as generic blocks. Needs a
   precise spec (do linked blocks share HP? break together? form moving bridges?). *Effort: medium-high
   (block-link model + render).*

5. **Economy reconciliation: Gold + Shards** (§5, §10). — ✅ **DONE** (this session).
   Reconciled the three-currency model with the existing code:
   — **Shards** (meta, drip-on-death → unlocks): shipped earlier this session.
   — **Gold** (in-run spending): new `GameInstance.Gold`, fed by the **coins/treasure pickup**
   (pivoted from Crystals → Gold), exposed in the snapshot + a **HUD gold counter**, and **persisted
   across dungeon floors** (`DungeonRun.Gold`, posted via `/dungeon/floor-cleared?gold=N`, restored by
   `GameInitializer.SetGold` — the same pattern as HP).
   — **Dungeon shop floor**: a **"Visit Shop"** floor-clear pick opens a shop (`buildShopOverlay`)
   selling 3 boons (relic/core/mod/spell/heal) for Gold; `GET /dungeon/shop/items` +
   `POST /dungeon/shop/buy` (server re-derives the deterministic inventory to validate the buy);
   `DungeonService.GenerateShopItems` + `TryBuy`. Leaving the shop advances the floor.
   — **Crystals**: deliberately LEFT as the meta-progression drip (per-kill + level-clear reward +
   items shop scrip) to avoid breaking that economy. This is a **naming drift** from docs/04 (which
   calls the dungeon scrip "Crystals"); the codebase plays that role with `run.Gold`. Logged for a
   post-launch rename in `docs/round-2-recommendations.md`.
   — **Campaign seam**: `Profile.CampaignGold` field added for Gap H (campaign rest/shop nodes); the
   earn+spend wiring is part of #6.
   Tests: 4 backend Gold + 11 backend shop + 1 E2E HP/Gold carry; shop flow playtested
   (pick card → shop overlay → buy → leave advances). *Was: large; reconcile the model first.*

6. **Campaign rest / shop nodes + richer forks** (§6.1). — ✅ **DONE** (this session).
   — **Shop nodes**: `CampaignNode.Type` ("battle"|"shop"); 6 shop nodes in `config/campaign.json` —
   3 mandatory biome-gate shops (hell/caverns/village, the next biome's first level requires them) +
   3 fork-detour shops (caverns-3, village-5, heaven-5). Clicking a shop node opens a campaign shop
   (`buildCampaignShopOverlay`) selling **spell level-ups / relic unlocks / skill points** for the
   persistent `Profile.CampaignGold` — `CampaignShopService.GenerateCampaignShopItems`/`TryCampaignBuy`,
   `GET /campaign/shop/items` + `POST /campaign/shop/buy` (visiting a shop marks it complete so gated
   nodes unlock). CampaignGold is earned at level clear (boss > regular) and shown in the reward overlay
   + profile bar.
   — **Real, visible forks**: `CampaignScene` now renders the AUTHORED graph (per-`requires` edges),
   transposed to a **vertical** path (progression ↓, fork lane →) so it fits the portrait frame and
   the forks/Heaven-reconvergence are visible (was a fixed index-snake that ignored the graph). The
   dead-end side branches are now meaningful (detour shops); Heaven has a true reconvergence
   (heaven-7 requires BOTH the shop fork and the battle fork).
   — Reconciled an in-scope shop-stability bug found while building: both shops now rank inventory by a
   STABLE per-item key (not a reshuffle), so multiple purchases in one visit stay valid.
   Tests: 16 backend (CampaignShopTests) + 4 E2E (campaign-shop.spec) + map/shop playtested. *Was:
   medium-large; depended on #5.*

7. **Dungeon minibosses mid-run** (§6.2). Rifts/bosses exist; "minibosses mid" may be absent.
   *Effort: low-medium (reuse biome enemies as a floor type).*

## Spell-fidelity audit (docs/01 §61-63) — checked this session
- **Duplicate → N smaller balls** — ✅ fixed (sim radius + visible render).
- **Engineer Magnet** — doc: "pulls blocks toward the ball"; impl steers the ball toward blocks. The
  auto-aim version is arguably better feel; **leave** unless you want literal fidelity.
- **Paladin Last Day** — doc: "each top-wall hit drops a nuke line." Impl is a timed aura that emits
  golden **judgement smite columns** (own visual + SFX) — real identity, not a generic AoE. Close
  enough; the exact "top-wall-hit trigger" is a tuning call, not a bug.

## Spell leveling — ✅ DONE (all 16 spells scale)
All 16 non-fire/fire spells now scale with level: Projectile dmg, TimedAura duration, Imbue hits,
Lightning/Radiation dmg, Phoenix duration, **Shield lifetime (+0.5/lvl), Duplicate copies (+1/lvl),
Overload blast radius (+1/lvl)**. Tests cover a representative of each scaling type.

## Content-systems audit (this session) — no dead picks/purchases
Checked every catalog entry against its code, the same lens that caught the spell-leveling bug:
- **Relics** — all 18 referenced/implemented (empty-`effect` ones are ID-checked at hook sites). ✅
- **Items** — all 8 effects (ball_damage, crit_tough, max_mana, mana_regen, start_life, treasure,
  kill_mana, paddle_width) applied in `ItemEffects`. ✅
- **Bonuses/power-ups** — all 8 (extra_ball, mana_surge, wide_paddle, slow_ball, heal, coins,
  fireshot, shield) handled in `BonusSystem`. ✅
- **Spells** — behavior present for all 20; **leveling** was the gap (now 13/16, 3 logged above).

## External best-practice check (web)
Brick-breaker/Arkanoid feel fundamentals — **angle control** (centre vs edge deflect), **progressive
ball-speed escalation**, and **audio+visual juice on every hit** — are all already implemented here
(paddle deflect bands + perfect-deflect reward, `PacingSystem` +5%/20-brick speed-up, a full SFX
recipe set + screen shake/combo/vignette/trails). Research **confirmed** the game meets these; it did
not surface a missing fundamental. Sources: PlayMore Arkanoid guide, Glaive (Steam).

## Recommendation
If you want the **most build-variety per hour**: greenlight #3 (Recall/Slow-Time as pool spells) and
#2's *curse-with-upside* picks — both ride the systems built this session. If you want the most
**character identity**: #1 (Necromancer Raise). The economy/shop arc (#5/#6) is the biggest lift and
should be scoped on its own. Tell me which and I'll spec + build it.

# Session log — game feel, balance & bug fixes (2026-06-12)

Autonomous iteration pass on **feel, look, balance, and correctness**. Every gameplay/visual change was verified with a live Playwright screenshot reviewed against intent (per `CLAUDE.md` — "it rendered / it passed" is not the bar).

## Game feel / juice
- **Combo escalation** — score popups now scale and shift colour with the streak (gold ×2 → orange ×3 → big red **"×4 !"**) with a spawn pop, plus an escalating micro screen-shake and a tiny hit-stop at max combo. (`Renderer`, `ScreenShake.pulse`)
- **Per-hit chip spark** — every ball/projectile contact that damages but doesn't destroy a block now throws a small spark, so chipping a tough block feels tactile. (`Effects.hitSpark`, `Renderer`)
- **Burning blocks are visible** — ignited/firewall blocks get a looping flame overlay + warm tint, so the "fire propagates over time" mechanic actually reads on screen. (`BlockLayer`, snapshot `burning`)
- **Low-HP danger vignette** — a red edge pulse at critical HP (1) and a steady tint at 2, gated on the FX/reduced-motion setting. Verified across biomes (bold on cool biomes, a subtle pulse on Hell). (`DangerVignette`)
- **HP bar danger levels** — the HP bar swaps sprite green → amber → red as it drains.
- **Spell-ready flash** — a spell slot pops + glows the moment mana rises past its cost, so the player notices newly-castable spells. (`Hud` `$effect` + `.spell-ready`)
- **Perfect-deflect reward** — a bright gold burst on a perfect (centre-band) paddle deflect, making the existing +mana skill-reward *felt*. (`SimEventKind.PerfectDeflect` → `Effects.spawnPerfectDeflect`)
- **Ball contrast ring** — a subtle dark ring behind the ball so it stays *paramount* on bright biomes (heaven); invisible on dark ones (hell). Applies the "keep the ball paramount" level-design principle from research. (`BallLayer`)
- **Perfect-deflect chime** — a bright rising reward tone layered over the deflect sound, so the skill reward is *felt* in audio too (juice research: feedback should be audio + visual + mechanical). (`Sfx.perfectDeflect`)
- **Reward count-up** — the level-complete EXP/points/crystals now tick up from 0 (easeOutCubic) for a satisfying reveal — polish on the now-fixed reward moment. (`overlays.countUp`)

## Research applied
Searched mobile-game HUD best practices (thumb zones, ≥48px targets, safe areas — the HUD already conforms: 52px slots, bottom hotbar), and breakout/arkanoid level-design principles ([Game Developer](https://www.gamedeveloper.com/design/breaking-down-breakout-system-and-level-design-for-breakout-style-games)). The hell campaign tracks the principles well (20–36% density = good void space, progressive block-type intro, `MinVerticalRatio` anti-stall, ball trail). The one applied fix was ball contrast (above); hell-8's dense "wall" before the boss is a deliberate, valid archetype, so I left the hand-authored levels alone.

## Balance
- **Fire Wall tamed** — it now reaches the blocks (was expiring at ~270px on real boards; the unit test only passed because its level was 4 rows tall), but lights a **short, non-spreading** burn so a full-width sweep no longer compounds into a guaranteed board nuke.

## Real bugs fixed (not just feel)
- **"Continue" pointed at the level you just beat.** `MenuScene.furthestNode` returned the last *unlocked* node in array order, including completed ones — so after clearing Hell I, Continue still launched Hell I. Now it resumes the furthest *unbeaten* node.
- **Power-ups did nothing when spawned by id.** `BonusSystem.SpawnWithType` matched only on `effect` and stored the raw arg as `Type`, so a legacy/catalog id (`powerup_wide`) was caught but never applied. Now it resolves either id and stores the canonical effect — also hardens any block `ForcedDropEffect` using a legacy id.
- **Enemy telegraphs/shields/lava went stale.** The block snapshot cache only refreshed on HP changes, so time-based flags (`charging`, `shielded`) and newly-crept lava were stale whenever the ball wasn't chipping a block. `EmitterSystem`/`ShieldSystem` now mark the snapshot dirty while active.
- **`BlockList`** — the block collection now auto-invalidates the spatial index + snapshot version on every add/remove/replace, so callers can't forget (the root cause of the lava-staleness bug, and a latent floor-swap bug). `g.Blocks.Add(...)` just works.

## Biggest bug — reward overlay always showed "+0"
Found while reviewing the level-complete dopamine moment (research-driven). The server's WS loop auto-granted the win reward on `Won` (same tick, before the snapshot is even sent), so the client's `/complete` always saw the level already completed → returned `firstClear:false, +0`. **Every player saw "+0 EXP / +0 Skill Points / +0 Crystals" on every level win** despite actually receiving them — and the blanket server grant also wrongly marked campaign nodes complete for *dungeon* floors. Removed the server-side grant (`GameSession.GrantWinReward`); the client's context-aware flows are the single, correct grant path (campaign → `/complete`, dungeon → `/dungeon/floor-cleared`). The overlay now shows **+120 EXP / +4 Skill Points / +10 Crystals / "Level Up!" / "First Clear!"**.

## Test suite restored (pre-existing drift from earlier refactors)
- `__renderer` → `__bridge.renderer` (F6 migration) — `combo`, `paddle-anim`.
- `getState().lives` → `.hp` (T9 rename) — `hud-bars`, `hud-visual`, `shell-shots`, `boss`.
- F-SV class renames (`inv-*`/`ach-*`/`sk-*` → scoped names) — `inventory`, `p7b-new-screens`.
- `hud-visual` goldens regenerated for the new HP bar; `spell-turret` rewritten for fire-on-deflect.

## Round 2 — onboarding clarity & a dead-achievement bug
Research-driven ([Game Designing — RPG class archetypes](https://gamedesigning.org/gaming/rpg-classes/), [Game Developer — Four Axes of RPG Design](https://www.gamedeveloper.com/design/four-axes-of-rpg-design)): the key applicable principle was *don't force longstanding choices before the player understands the game*. The game already mitigates this (3 of 4 classes lock at the start), so the gap was **comprehension**, not timing.

- **Character select shows each class's spell kit.** Cards listed only the passive; a new player couldn't see what spells a class actually plays with before committing. Each card now shows a `SPELLS` row (e.g. *Ignite · Fireball · Fire Wall · Turret · Phoenix*). Locked classes show the kit **and** the unlock hint, which doubles as a "here's what you'll earn" teaser. (`CharacterScene.svelte`) *(Deliberately did **not** add a separate "playstyle" tag — the passive line already conveys playstyle in prose; a tag would be redundant clutter.)*
- **Achievements tell you how to earn them.** Locked cards showed `???` for every achievement, so the screen was a mystery box instead of a goal list (the actual retention function of achievements). Added a plain, accurate `criteria` field per achievement and show it (🔒-prefixed, legible) while locked; the poetic flavour `description` still appears once unlocked. (`AchievementsScene.ts` / `.svelte`)

### Real bug — two achievements were permanently unobtainable
`beat_boss` ("Boss Slayer", tier 3) and `campaign_complete` ("World Saved", tier **5**, the capstone) were defined in the catalog but **never unlocked by any code path** — 2 of 13 achievements (15%) could never be earned, including the game's final reward. Wired both into the win flows:
- `beat_boss` — latch `sawBoss` whenever a snapshot reports `bossActive` (it clears the tick the boss dies, i.e. the same tick as "Won", so it must be latched), then unlock on win. Added to **both** `campaignFlow` (the 4 biome bosses) and `dungeonFlow` (rift bosses).
- `campaign_complete` — unlock when `heaven-boss` is won. Confirmed via the campaign graph (`config/campaign.json`) that the order is hell → caverns → village → heaven and `heaven-boss` is the terminal finale (requires `heaven-7`, unlocks nothing).

Verified end-to-end on the live build: winning `hell-boss` unlocks `beat_boss` (+ `first_win`, `clear_biome_hell`, `win_fire_mage`); winning `heaven-boss` additionally unlocks `campaign_complete`. New guard spec `achievement-unlocks.spec.ts` encodes the trigger→identity contract. (Boss levels insta-attack on entering *Playing* and defeat an automated bot instantly, so the test uses `tutorial=1` to hold the game in *Serving* — boss present, latching `sawBoss`, but unable to attack — then `winNow` for a deterministic, defeat-free win.)

### Real bug — desktop keyboard play was broken for 3 of 4 classes
The hotbar keybinds `Q/E/W/R` called `conn.castIgnite()/castFireball()/castFireWall()/castTurret()`, which the backend maps to **Fire-Mage spell ids** (`SpellSystem.Cast(this, GetSpellDef("ignite"))`) regardless of the selected class. So a Paladin/Engineer/Necromancer pressing Q tried to cast a spell they don't have → nothing happened. (The tap hotbar was fine — it already used the class-agnostic `castSlot`.) Routed the keys through `castSlot(0..4)` like the tap path: identical for the Fire Mage (slot 0 = ignite, …), correct for every class now. Verified live (Paladin Q → Shield barriers; Fire Mage E → Fireball) and guarded in `class-kits.spec.ts` (a keyboard-`Q` test that asserts the Paladin's Shield casts). (`PaddleInput.ts`)

### Dungeon pick-a-boon: informed choice, not a guess
The roguelite's core decision moment (pick 1 of 3 after each cleared floor) showed only an **icon + name** — you couldn't tell what a boon *did* before committing. Each card now shows a one-line **effect**: relic blurbs come straight from the catalog (`RelicDef.description`, which `relicCache` was silently discarding — now cached + exposed via `getRelicDesc`), and ball-core/paddle-mod blurbs are transcribed from the **actual implementation** (`BallSystem`/`Modifiers`/`GameInstance.Commands`), not the design doc, so they match what the code does (e.g. *Ghost Core: "Ball phases through a block each serve"*, *Glass Cannon: "+1 ball damage, but −1 life"*). (`overlays.ts`, `relicCache.ts`) Guard added to `dungeon.spec.ts`. *(This is the boon pool; the Skills-screen spell descriptions remain the separate deferred content task.)*

### Campaign map: the playable node now pops
Wayfinding gap: **completed** nodes glowed blue and **locked** nodes were dimmed, but the **unlocked** node — the one you can actually play — had *no* special treatment, so the eye was drawn to finished levels instead of the next objective (on a fresh save, hell-1 was lost among 34 near-identical orbs). The unlocked node(s) now carry a **gold "play-here" pulse** (steady gold glow under reduced-motion). Pure CSS on `.camp-node-unlocked` — the map's auto-scroll/progression/layout is untouched (it's a complex hand-authored arrangement). Verified the pulse lands exactly on the frontier node (`data-state="unlocked"` = hell-1 on a fresh save). (`CampaignScene.svelte`)

### Inventory: no more silent equip dead-click
`ItemShop.Equip` "fails silently when the 3-slot limit is reached" — so a player with 3 items equipped who tapped **Equip** on a 4th got *nothing* (a dead click, no explanation). The card's Equip button now reads **"Slots Full"** and is disabled (with a `title` hint) whenever 3 are equipped and this item isn't; it flips back to a live **Equip** the instant you unequip one. (`InventoryScene.svelte`) Guard added to `inventory.spec.ts` (disabled + label + re-enable-on-unequip). *(The rest of the inventory screen was reviewed and is already strong — items carry clear, quantified per-tier descriptions, costs, tier badges, and locked indicators.)*

### Skills screen shows mana cost
The upgrade cards showed icon + name + level + Upgrade, but nothing about the spell. Surfaced the **mana cost** per card (e.g. *Ignite: Free, Fireball: 25 mana, Phoenix: 30 mana*) — data-driven and 100% accurate, so it helps prioritise upgrades with zero content-accuracy risk. (`SkillsScene.svelte`) Full per-spell *descriptions* are a deliberate **deferral**: there's no description field in `config/characters.json` or the `/characters` payload, so doing it right means authoring ~20 descriptions verified against the design docs (a content task, not a quick win) — rushing inaccurate flavour would violate the content-fidelity bar. Logged as a follow-up.

### Level-up celebration
The reward rows count up, but **"Level Up!"** — the single biggest progression beat — was static text. It now pops in (scale-overshoot) *after* the count-ups finish (700ms delay, so the dopamine is sequenced: rewards tally → **Level Up!** punches in) and then glows to hold the eye. Reduced-motion users get the plain text (animation disabled via media query). (`overlays.ts` `.ov-levelup`)

### FX / reduced-motion audit (no change needed)
Verified the prior session's juice respects accessibility: `shakeEnabled` gates on **both** the FX toggle and `prefers-reduced-motion`, and correctly covers the combo shake, player/boss-hit shake, and renders the low-HP danger vignette *steady* (not throbbing) under reduced motion. The HUD mana bar was also confirmed to track state exactly (100% mana → full fill width). Both audits passed — recorded here so the checks aren't re-run blindly.

### Defeat screen is now a teaching moment
The defeat overlay was bare (title + Retry + Map). A death is a natural moment to learn, so it now shows one random **TIP** — drawn from a pool of *universal* (class-agnostic) gameplay hints so the advice is always accurate regardless of which hero died (perfect-deflect mana, spell-ready glow, low-HP vignette, angling bounces, paddle positioning, catching power-ups). (`overlays.ts` `buildDefeatOverlay`) The Lost flow was previously **untested** — added a guard in `campaign.spec.ts` that loses a level and asserts the overlay + tip + Retry/Map. The **dungeon permadeath** overlay ("Run Over") gets the same tip treatment (a death is a death), reusing the identical pool — verified live + via the existing `dungeon` permadeath spec.

### SFX volume slider (accessibility)
Research ([Game Accessibility Guidelines — separate volume controls](https://gameaccessibilityguidelines.com/provide-separate-volume-controls-or-mutes-for-effects-speech-and-background-music/), [Audiokinetic — blind accessibility](https://www.audiokinetic.com/en/blog/blind-accessibility-in-interactive-entertainment/)): granular, independent volume is an accessibility essential (hearing loss is frequency-dependent; sliders should default to 100%). Settings only had on/off toggles and `Sfx.ts` a hard-coded `0.22` master gain. Added an **SFX Volume** slider (0–100%, default 100% = current loudness) under the Audio toggle: persists to `localStorage.arkanoid_sfx_volume`, live-updates the Web Audio master gain via `setSfxVolume()`, plays a sample tone on release so you hear the level, and disables/dims when Audio is off. (`SettingsScene.svelte`, `Sfx.ts`) Guard test added to `p7b-new-screens.spec.ts` (persist + disable-with-audio). Music has no real tracks yet (experimental), so no music slider — that would be controls for nothing.

## Verification
- Backend: **313/313** C# tests (incl. 2 new perfect-deflect tests). *(Round 2 was frontend-only — no C# touched.)*
- Round 2 Playwright: full suite green (clean, 0 flaky), grown 186 → 192 with six new guard tests this round: `achievement-unlocks` (2 — boss + finale), `p7b-new-screens` SFX-volume (1), `campaign` defeat-tip (1, also first coverage of the Lost flow), `inventory` slots-full (1), `class-kits` keyboard-Q (1) — plus added assertions to the existing `dungeon` pick test (boon descriptions). All affected specs also re-run green individually after each change. `vite build` clean throughout. Frontend-only — no C# touched (313/313 still valid).
- Frontend: `vite build` clean.
- Playwright: full suite green (workers=1), plus new guard specs (`firemage-playtest`, `hud-feel`, `render-smoke`, `multifloor`) and 2 C# perfect-deflect tests.

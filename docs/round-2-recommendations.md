# Round 2 — prioritized recommendations

After the Fire Mage spell rebuild + the feel/balance/bug pass (`2026-06-12-feel-and-fixes-session.md`), here's what I'd tackle next, in priority order. Each notes *why* and the *risk*, so you can direct it.

## 1. Necromancer "Skeleton" → a real summoned minion *(the clearest remaining flattened spell)*
- **Now:** a paddle-mounted bolt shooter on a timer (the same generic shape the old Fire Mage Turret had). Functional and skeleton-themed, but not a *summon*.
- **Design:** Necromancer's identity is **summons** (`docs/04` §65: "Raise — killed blocks may spawn a friendly skeleton helper-ball").
- **Options (pick one — this is a design call, which is why I didn't build it blind):**
  - **(a) Helper-ball** — the spell spawns a friendly extra ball that bounces and breaks blocks (literal reading of §65). Cheapest to build; reuses ball physics.
  - **(b) Summoned skeleton entity** — a visible minion (like the Phoenix entity I built) that stands on the field and shoots at blocks. Most "summoner" feeling; needs an entity + render layer.
- **Effort:** (a) small, (b) medium (entity + `SkeletonLayer` + snapshot, same pattern as Phoenix).

## 2. Minor spell-fidelity touches *(small, grounded in `docs/01` §61-63)*
- **Paladin Duplicate** clones same-size balls; design says "**N smaller** balls." A 0.85× clone radius adds variety. *Risk: touches ball radius (collision) — low but non-zero.*
- **Engineer Magnet** steers the *ball toward blocks*; original "pulls *blocks* toward the ball." The current version is arguably better (auto-aim) — **leave unless you want literal fidelity.**

## 3. Balance you may want to revisit *(needs your difficulty intent)*
- **Fire Wall** is now a strong full-board sweep AoE. It clears the *tutorial* level in one cast (weak blocks); it only softens tougher boards. If that feels too strong even on easy levels, I can make it a *placed/limited-reach* wall instead of a full sweep.
- **Combo ceiling is 4** (reached at 9 kills, resets on every paddle touch). Raising it / making it decay-over-time would reward keeping the ball up — but it feeds the crystal economy, so it needs a paired economy check.

## 4. Spell descriptions on the Skills/upgrade screen *(content task)*
- The Skills cards now show name + **mana cost** + level, but not *what each spell does* or *what a level grants*. There's no description field in `config/characters.json` or the `/characters` payload, so doing it right means authoring ~20 short descriptions (and ideally per-level effect deltas) **verified against `docs/01`/`docs/04`** — a content task, not a quick UI tweak. Deferred rather than rushed with inaccurate flavour (content-fidelity bar). *Effort: medium (config + serialization + frontend), plus careful copy.*

## 5. Dungeon-clear overlay: crystal display is ambiguous *(needs a backend field)*
- The "Dungeon Cleared!" overlay shows `{profile.crystals} Crystals` under a **"Permanent Reward"** heading — but that's the player's *total balance*, not what the run granted, so it reads as "this run gave you 150 crystals." `FloorClearedResult` exposes only `profile`, no per-run *gained* amount. Either (a) expose `crystalsGained` from the floor-cleared endpoint and show "+N Crystals" (with a count-up, matching the campaign reward), or (b) relabel to "Total: N". I left it rather than guess the reward model. *Effort: small (b) / small-medium (a, needs backend).*

## 6. Test-infra hardening *(quality-of-life)*
- The meta/demo specs share the **default** profile, so they can only run `--workers=1` cleanly (see the `test-and-server-gotchas` memory). Giving those specs a per-worker `pid` would let the whole suite run in parallel.
- **`menu.spec.ts:61` flake — FIXED at the root (2026-06-13).** The whole SPA was gated behind
  `loadAtlas().then(initApp)`; when the Pixi atlas load *rejected* (headless WebGL without compressed-texture
  support — the `Atlas load failed: getExtension` error), `initApp` never ran and **every screen was dead**,
  so any dock click timed out. `main.ts` now runs `loadAtlas().catch(...).finally(initApp)` — the menu and
  all meta/social scenes (HTML/Svelte, no atlas needed) mount regardless; only the battle renderer degrades
  if the atlas truly fails. menu.spec now passes repeatedly. This also resolved the sibling atlas-race flakes
  (spell-firewall/touch-controls/spell-picks all needed the app to mount).

## 7. Currency naming drift — `Crystals` vs docs/04 "Crystals" *(post-launch rename)*
- The Gold economy (Gap G, 2026-06-13) introduced `GameInstance.Gold` / `DungeonRun.Gold` as the in-run
  spending currency and made the dungeon shop spend it. To avoid breaking the existing crystal economy
  (per-kill drip → `Profile.Crystals` → items meta-shop), `Crystals` was **left as-is** as the
  meta-progression scrip.
- **Drift:** docs/04 §5 calls the *dungeon-only shop scrip* "Crystals"; in code that role is played by
  `run.Gold`, and `Crystals` is the items-shop meta scrip. They are NOT the same thing the doc means.
- **Recommended rename (mechanical, low-risk, do it when touching the meta-shop):**
  `Profile.Crystals` → `Profile.Gems` (the items-shop scrip), keep `run.Gold` as the dungeon shop scrip.
  Then docs/04 and the code agree: Gold (in-run), Gems (items meta-shop), Shards (unlocks).

## What's already solid (no action needed)
- Fire Mage kit (rebuilt + playtested), the other classes' spells (mostly faithful), all 4 biomes' look, the boss presentation, the HUD, and the core-loop juice. The full Playwright + backend suites are green.

## Deferred: Engineer ★5 "extra balls deal full damage" (stat engine §5.5)
Blocked on the §5.9 balance pass. The perk removes a damage penalty on EXTRA balls (Duplicate
clones / Raise & Necro helper-balls / Multiball serves) — but no such penalty exists in the sim
today (all balls deal full damage via `Modifiers.BallDamage`). Implementing the perk faithfully
means FIRST introducing an extra-ball damage reduction, which is a coordinated balance change to
Duplicate/Raise/Multiball that §5.9 explicitly defers to a playtest pass. Activating the perk now
would be a no-op stub, so `StatResolver.PerksFor` does NOT emit `eng_s5_extraball_fulldmg`. When the
balance pass lands: add a `Ball.Extra` flag (set on multiball serves, Duplicate clones, summoned
helpers), reduce their `BallDamage` (e.g. ×0.5, min 1) unless `HasPerk(EngExtraFullDmg)`, and
re-enable the perk in `PerksFor`. The other 7 behavioral perks (§5.5) are implemented.

## Reconciled: Paladin ★3 "first ball-drain saved" stacks with the base wall-save (§5.5)
The Paladin's BASE passive (`WinLoseSystem` `_wallSaveAvailable`, characters.json "Once per level, a
lost ball is saved") already saves the first drain at ★0. §5.5 lists "first ball-drain each level is
saved" as the ★3 perk. DECISION: the ★3 perk (`pal_s3_save_drain`, `_perkSaveAvailable`) grants a
SECOND once-per-level save that stacks with the base — so a ★3+ Paladin gets two free saves per
level. This is the additive "stronger at higher ★" reading and avoids removing the class's signature
identity. If a future pass prefers the base passive to BE the ★3 unlock instead, gate the base
wall-save behind ★3 and drop the separate perk flag.

## Tuning watch: Conflagration mana refund on large lit clusters (§3/§5.9)
Conflagration costs 25 mana but each block it kills refunds ~4 mana via `Modifiers.KillManaGain`
(ManaPerKill × kills). On a heavily-ignited board (e.g. a fire build that lit 24 blocks) the detonation
is net mana-POSITIVE and the chain clears the rest "for free" — potentially a spammable self-funding
board-wipe. Gated today by: hard-fizzle without setup, the mana/tempo cost of igniting first, and that a
whole board is rarely lit at once in real play. Revisit in the §5.9 balance pass — e.g. cap kill-mana
refund during a detonation, or scale Conflagration cost with the number of blocks detonated.

## Tuning notes: Reckoning (§3 Paladin) — balance levers (logged from subagent review)
- **Threshold lever saturates by Lvl 4.** `threshold = max(1, 3 − (lvl−1))` hits the floor of 1 at Lvl 4 (3→2→1 over L1–L3), so the §6 "fires sooner per level" half has only ~2 steps of runway before only damage scales. This is forced by §5.10's single-digit HP. Consider a finer charge unit (e.g. charge by fractional HP or by hits-taken) if more level runway is wanted.
- **Feast-or-famine / content-dependent.** With HP ~6 and threshold 3, one smite costs ~half the bar on big-hit levels (a near-death panic payoff), while lava/emitter chip levels (1 HP each) charge it steadily — and at Lvl 4+ (threshold 1) every 1-HP lava chip triggers a full 5-col smite, a potential board-clear engine for standing in lava. Tune base threshold / smite columns / damage against the one-life HP pool (§7) in the §5.9 balance pass.

## Tuning notes: Tesla Grid (§3 Engineer) — balance + visual (logged from subagent review)
- **Recharge economy.** Armed once for 30 mana, then curtains are free (no per-curtain mana), recharged by bouncing both side walls. Added a 0.5s `CurtainCooldown` to stop multiball/bouncy-ball spam (multiball can charge both walls via different balls in the same instant). Revisit in the §5.9 pass: a per-curtain mana cost, consuming the arm per fire, or scaling cooldown — vs. narrow/vertical levels where it may rarely fire.
- **Visual.** The curtain renders via the Lightning event but reads as orange embers under the hell-biome tint, blurring with Fire FX. Give it a distinct electric blue/white arc so the "lightning" identity is unmistakable. (Art polish, deferred.)

## Tuning note: Lich's Gaze (§3 Necro) — curse snowball (logged from subagent review)
The curse bonus sits INSIDE the crit multiply (BallSystem: `dmg = round(BallDamage(incl. curseBonus) * critMult)`), and the curse is PERMANENT for the level (board-painting). On the Necromancer (high crit-damage) at high curse levels stacked with Cruelty/Brutality, a crit on a cursed block snowballs multiplicatively. On-brand for an attrition class, but watch cost(25)/coverage vs. the tough-block pass in §5.9 — consider a curse duration, or capping curse stacking. §6 table lists only "+Duration/lvl" for this archetype but the impl also scales curse bonus +1/lvl (two-axis payoff; update the doc).

## Decision: Rot & Collapse gravity vs Hell descend overrun (§3/§7)
GravitySystem.CollapseColumn drops survivors toward the floor; on Hell DESCEND levels the bottom row is
the overrun/loss line, so an unguarded collapse could yank a NeedToKill block onto it and self-inflict a
loss. DECISION: on descend levels (DescendInterval>0) the collapse floor is clamped to Rows-2 (never the
overrun row) — Rot stays a help, not a trap. Revisit in the §5.9/§7 balance pass if descend levels want
the risk back. UI polish deferred: Rot & Collapse still uses the inherited shield icon (wrong glyph) and
the name truncates "Rot & Colla…" in the hotbar.

## §3 Bonewalker (Skeleton rework) — deferrals (subagent PASS, non-blocking)
Bonewalker is a genuine rooftop-walking melee minion (subagent-verified PASS: code + log + screenshots).
Deferred:
- BALANCE: melee is a flat 3/swing and scales ONLY via +duration/lvl (§6 timed-aura), so it will not dent
  the planned tough/tall blocks (§5.9, ~20–60 HP) even at Lvl 10 — it chips once and walks on. Revisit in
  the §5.9 tough-block pass (small per-level chip bump, or let it linger/repeat on a block it can't one-shot).
- It whiffs over gaps (TopBlockUnder is null in empty columns) → weakest on a sparse end-of-level board.
  Intentional front-loaded identity; note for tuning.
- CLEANUP (cosmetic): legacy "skeleton" naming persists by design for id-stability (spell id `skeleton`,
  `SimEventKind.SkeletonShot` reused as the melee FX marker, `SkeletonActive`, the `RiseSkeletonLargeIcon`
  sprite, a stale `skeleton_bullet` mention in Projectile.cs's comment). Future cleanup pass.
- DONE this pass: added a walk-duration life bar above the bonewalker (was the subagent's only flagged UX gap).

## §3 Bone Golem (fix) — deferrals (subagent PASS, non-blocking)
Bone Golem is a genuine climbing-bodyguard minion that bulldozes its column and soaks hazards (subagent PASS).
Deferred:
- IDENTITY/FX: the bulldoze and soak reuse `SimEventKind.SkeletonShot` / `BarrierHit` as event markers.
  These currently render NOTHING on the frontend (no FX/SFX is bound to them), so there is no misleading
  cue today — but they're the wrong glyphs semantically. Add bespoke `GolemCrush` / `GolemSoak` events +
  FX when the effects pass happens.
- DESIGN DECISION: `BlockAtHead` skips Indestructible, so an unbreakable block does NOT stop the golem's
  climb (it phases past). Defensible (a "bone golem" plows on), but a bulldozer arguably should be halted.
  Decide in the §7/§5.9 pass.
- BALANCE: the "bodyguard" window is brief + single-lane (it climbs away from the paddle and exits off the
  top in a few seconds, soaking ~2–3 shots on the way). Matches the doc's "*climbing* bodyguard" wording;
  fair, arguably slightly underwhelming as a dedicated tank. Revisit if it wants a longer guard window.

## §1 Cards — Batch A (Headhunter, Underdog, Opening Gambit, Cleanup Crew) — subagent PASS
First 4 of the 20 §1 cards. Built the card runtime (GameInstance `_activeCards`/`HasCard`/`CardLevel`/`SetCards`,
CardSystem hooked into BallSystem damage + BlockDamage kill branch; CardEffects registers equipped trigger
cards). Replaced the 12 filler stat-cards in cards.json (the rest of the 20 come in later batches). Deferrals:
- Opening Gambit's `_cardOpeningGambitUsed` once/level flag resets only via a fresh GameInstance (per level/
  floor). Multi-floor Caverns collapse (WinLoseSystem) advances floors in the SAME instance without resetting
  it, so Opening Gambit fires only on floor 1 of a multi-floor level. Arguably design-correct ("first kill each
  LEVEL", and the multi-floor level is one level) — but if floors should count as separate levels, reset the
  flag (and Combo.BricksDestroyed) on floor advance. Decide in the §7 pass.
- UX: only Opening Gambit has an on-screen cue (its Explosion event). The damage-bonus cards (Headhunter/
  Underdog/Cleanup Crew) surface only as bigger numbers / faster clears. Consider a subtle cue (gated-row
  highlight or floating +N) in the effects pass — applies to all number-only §1 cards.
- E2E robustness: card-batch-a's neighbour-AoE-chip assertion is guarded and was SKIPPED in the evidence run
  (the AoE killed the neighbour). The once/level + AoE proof rests on the unit test + sim log (solid). Tighten
  the E2E later (assert neighbour HP drop OR neighbour death) if cards get an E2E hardening pass.

## §1 Cards — Batch B (Bank Shot, Executioner's Edge, Overkill, Erosion) — subagent PASS
Cards 5/8 of 20. Damage/hit cluster. Added Ball.BankCharge, Block.ErosionHits; hooks at wall-bounce, crit
branch, and post-hit in BallSystem. Live logs for Erosion/Overkill/Executioner; Bank Shot unit-tested (its
side-wall trigger is bypassed by the deterministic ballToBlock drive). Added hardening tests for the Erosion
behavior-exclusion guard + Overkill horizontal axis. Deferrals (non-blocking):
- Executioner's window widens to 70% at L10 (0.20+0.05×level), diluting the "execute" identity at max level.
  Consider capping the window (~40%) and scaling the multiplier instead, in the balance pass.
- Bank Shot counts SIDE walls only (not the top wall) — matches "carom" intent; confirm with design in §7 pass.
- Erosion uses the generic BlockDestroyed pop; a bespoke "broke the unbreakable" VFX would sell the mythic
  moment (effects pass).
- Bank Shot has no live-play log (only unit tests). Acceptable (subagent agreed) but a cheap angled-carom
  demo would fully satisfy the run-it bar later.

## §1 Cards — Batch C (Dead Center, Metronome, Phase Window) — subagent PASS
Cards 9-11 of 20. Deflect/combo cluster. Hooks: CardSystem.OnPaddleHit (in SpellSystem.OnPaddleHit) +
CardSystem.OnBallTick (in BallSystem.UpdateBallStep). Live logs: metronome stacks=1..7, ball dmg bursts,
ballcore phase-through. Deferrals (non-blocking):
- Metronome `_metronomeStacks` is GAME-level, not per-ball: under multiball any ball's perfect increments and
  any non-perfect zeroes the shared streak, and the bonus applies to all balls. Defensible but make an explicit
  design call (Dead Center is correctly per-ball). 
- Phase Window tops PhasesLeft to 2 while combo high → up to ~2 residual pierces after the combo breaks (grace,
  not instant-0). And it shares PhasesLeft with the Ghost ball-core (no hard conflict; synergy blurs ghost's
  "free phases this serve"). Note for the §7/balance pass.
- UX: Metronome has no HUD stack counter — the player can't see the streak or that a sloppy catch reset it.
  Add a stack pip in the HUD/effects pass.
- Dead Center + Metronome both key off the same action (perfect deflect) → always collected together. Distinct
  levers (burst vs ramp) so acceptable synergy, not redundancy.

## §1 Cards — Batch D (Avalanche, Keystone, Domino) — subagent PASS
Cards 12-14 of 20. On-kill cluster (CardSystem.OnBlockDestroyed). Avalanche = combo≥8 → crush block BELOW
(downward), Keystone = load-bearing kill → CollapseColumn (distinct from Avalanche per cardinal rule), Domino
= 3 deaths/1s → next death 3×3 AoE (re-entrancy guarded). All three live-logged. FIXED this pass: Domino now
clears its death window on detonation (was re-arming every other kill → snowball). Deferrals (non-blocking):
- Avalanche raises a generic Explosion event, not a bespoke "falling rubble" cue (effects-pass polish).
- Keystone is situational under top-down clearing (rarely a live stack above the kill); fine for a
  position-gated rare, routine in bottom-up/side play.

## §1 Cards — Batch E (Martyr's Brand, Ricochet, Sleight of Hand) — subagent PASS — §1 COMPLETE (20/20)
Cards 15-17... (final 3 of 20). Hooks: CardSystem.OnHpLost (CombatSystem + LavaSystem), OnWallBounce
(Ricochet bolt), OnBonusCaught (BonusSystem; loop converted foreach→for to allow mid-iteration dup spawn).
All three live-logged (martyrs_brand vengeance buff, ricochet ×35 bolts, sleight ×20 dups).
FIXED this pass (subagent caught a real break): Sleight of Hand self-chained — the duplicate spawned dead-
centre → re-caught → re-duplicated infinitely (20 extra_ball from 1 spawn). Added Bonus.NoDuplicate; the
duplicate is tagged so it can't re-duplicate (one-hop). Locked with SleightOfHand_DuplicateDoesNotReDuplicate.
Deferrals (non-blocking):
- Martyr's Brand refreshes on each hit → on lava biomes the steady chip keeps it near-permanent (but you pay
  HP continuously, so arguably fair). Watch in balance pass.
- Ricochet fired 35× in ~5s of bouncing → many Lightning FX events; consider a small per-fire cooldown or
  lighter VFX if it reads as flicker (effects pass).
- UX: Martyr's Brand has no on-screen cue (damage number only); a brief vengeance tint would help (HUD pass).

## §1 Cards — Batch F (Hot Hand, Redline, Channeling) — subagent PASS — §1 NOW COMPLETE (20/20)
The deferred ball-state/regen trio. Hot Hand grows the ball at combo milestones (persists across paddle
touches, resets on serve; radiusScale exposed to renderer). Redline ramps speed (+40% cap) + damage with
airborne time, resets on paddle touch. Channeling: regen ×0 aloft / ×2 cradled-low (a 3-cell band over the
paddle — accepted as a fair "caught" proxy since there's no literal catch mechanic). All three live-logged
(hot_hand ball-grows r=9.2→15.2, ball dmg +5..+7 Redline ramp, mana froze aloft then climbed low). Deferrals:
- Channeling can be near-dead/negative in MULTIBALL builds (any aloft ball → ×0 pause). Balance pass: maybe a
  proportional multiplier or count low-vs-aloft balls.
- Redline re-normalizes ball speed every tick → it OWNS ball speed, overriding Slow Time / Tidal swift /
  speed pickups while equipped. Verify intended synergy interactions.
- UX: Redline (speed trail/tint) + Channeling (a "regen paused/doubled" indicator) have no on-screen cue;
  Hot Hand's bigger ball reads great. Effects/HUD pass.

## §2 Modules — Batch 1 (Tidal Core, Hollow Ball, Gyro Paddle, Drumhead Paddle) — subagent PASS
First 4 of 12. Built the module runtime (ModuleSystem + GameInstance _activeModules/HasModule/ModuleLevel/
SetModules; ModuleEffects registers effect="module" modules, skips RunModifier; no sub-stats). Hooks: BallSystem
damage (+floor ≥1) + OnBallTick, SpellSystem.OnPaddleHit, GameInstance.Serve OnServe, Tick paddle-velocity.
Added /dev/hero?modules= for E2E. Live evidence: Hollow big ball (radiusScale 1.8), Drumhead carved-column
screenshot + shockwave log, Tidal swift/heavy toggle log, Gyro paddleVel→vx whip log. FIXED this pass: added
Hollow's "erratic" heading-wobble (design said erratic; was only size+damage) + a ≥1 damage-floor test.
Deferrals (non-blocking, balance pass):
- Hollow's −1 flat penalty evaporates at high Power; its bigger radius also EASES the paddle catch (less risk).
  Consider a % damage cut. (Erraticness now adds a real downside.)
- Tidal swift mode hard-sets ball speed each tick → overrides slow_ball/other speed effects; both modes are
  pure upside (no downside beyond exposure); no on-screen heavy/swift mode cue.
- Gyro whip strength is input-speed dependent (keyboard vs mouse feel) — playtest.

## §2 Modules — Batch 2 (Gravity Well, Toll Roads, Brittle Glass, Spin-Loaded) — subagent PASS (8/12)
Field/economy/ball cluster. Gravity Well steers to block centroid (real-play x=51→205 pull); Toll Roads gates
kill gold to crit/perfect (0 vs ×2, 13+13 logs); Brittle Glass +5..13 dmg + shatters on indestructible (5
shatter logs); Spin-Loaded edge hit → curving spin (decays). Added Ball.Spin, _tollPerfectWindow, KillCrystals
gate at BlockDamage:110, ModuleSystem.OnBlockHit at BallSystem. Deferrals (balance pass, non-blocking):
- Gravity Well's "drain risk" is weak while the mass is high (acts as anti-drain aim-assist ~80% of a level);
  true downside only late. Consider strengthening the downward pull or edge penalty.
- Brittle Glass downside is level-contingent (no indestructibles = pure upside) + multiball-diluted; shatter is
  on-touch (no catch-after grace). Fine per design wording.
- Toll Roads floor can feel harsh for low-crit builds (intended skill gate).
- Spin-Loaded is the lowest-impact (control flavor, no dmg/economy) — fits rare.

## §2 Modules — Batch 3 (Pressure Cooker, Riposte Paddle) — subagent PASS (10/12)
Field-descend + paddle-parry. Pressure Cooker: ModuleSystem.Update descends the whole field every 6s
(overrun=instant loss, same rule as Hell descend), kills push it back every max(2,5-level); real-play y 16→48
+ 7 descend logs + overrun log. Riposte: moving paddle (|paddleVelX|≥4) parries enemy hazards into upward
damage bolts (7 parries logged); hooked in CombatSystem hazard→paddle. spawnEnemyBolt (no-golem) now drops
near the paddle for controllable parry timing. FIXED: Pressure Cooker description (push-back is per-few-kills,
not each); added unequipped + riposte-bolt-damages-block tests. Deferrals (non-blocking):
- Doc §2 says Pressure Cooker is "HP-pressure" but it's space/overrun instant-loss (faithful to "clear too
  slow → lose"; matches Hell descend). Reconcile doc wording if desired.
- Non-NeedToKill blocks can descend past the bottom row (cosmetic; matches Hell descend).
- Riposte grants defense+offense on enemy levels, worthless on enemy-free levels (fair: situational epic).

## §2 Modules — Batch 4 (Twin Soul Core, Fission Core) — subagent PASS — §2 COMPLETE (12/12)
The two complex cores. Twin Soul: AfterServe spawns a 2nd twin fanned ±35°, both −1 weaker; tether (rendered
cyan line, TwinTetherDto) slices blocks between them every 0.25s; lose one → tether dies. Fission: splits a
fission ball every max(2,4-(lvl-1)) kills into smaller fragments (×0.72, capped 4), fuses ALL fragments on a
paddle catch into a bigger ball (≤1.6×). Live evidence: tether-line screenshot + "tether sliced 2 block(s)";
fission "split→2/3/4 fragments" + "fused 4 → r=11.8". Added Ball.Twin/Fission, TwinTether snapshot+render.
Deferrals (balance pass, non-blocking):
- Fission has no per-hit damage penalty on fragments (only positional fragility) → arguably the stronger of
  the two legendaries (4 balls = ~4× contact + mana). Consider a fragment damage cut.
- Fuse-on-any-deflect pulls fragments from ANYWHERE into the caught ball (near-free regroup; fragments vanish
  from across the board). Consider proximity-gating the fuse.
- Twin tether is potent continuous AoE when both twins straddle a packed row (fair: double-gated by 2-alive
  + positioning, with a real lose-one penalty).

## §7 Rifts + §8 Modifier pool — subagent PASS — §7 COMPLETE (design fully implemented)
Reworked rifts into the §7 10-level biome gauntlet: ONE HP+ball pool carried across all levels (no reset),
~10% trigger (RiftChance 0.10), 1-of-3 §8 modifier picks between levels (applied rest-of-run), depth-scaled
rewards (jackpot at 10, bail/death still pays by depth), NO permanent relic/core draft. Built RiftModifierService
(the 10 §8 modifiers + Offer 1-of-3 + Pick on-effects + ApplyToGame per-level stat effects + DepthCrystals/Tokens).
DungeonRun gained IsRift/RiftModifiers/RiftMaxHp/SpareBalls/RewardMult/ExtraEmitters. Verified end-to-end via the
live API: rift-hell 10 floors → §8 offer [snowball,field_medic,prospector] → picked twin_serve → hell-8 battle
served 2 balls + HP pool carried (hp=3). 16 RiftModifier tests + rewritten Rift/Ascension tests; 625 backend green.
FIXED post-review (subagent caught): (1) Cursed Bounty's emitter downside is now WIRED — Block.ForcedEmitter +
EmitterSystem honours it; ApplyToGame forces N blocks to fire (no longer strictly-better than Prospector);
(2) RiftLevels config now passed to Roll (was dead); (3) RiftMaxHp seeds from the hero's TRUE max via a maxHp
param on /dungeon/pick (Field Medic/Ironclad heal to real max). Deferrals (non-blocking):
- Tier/ascension no longer scales the depth reward (harder for same payout) — add a tier multiplier in the
  balance pass if ascension should pay more.
- "Escalating difficulty" is via attrition (one HP pool draining) + a shuffle-cycle of biome levels, not a
  monotonic per-floor ramp — defensible; revisit if a true ramp is wanted.
- Frontend: /dungeon/pick should pass ?maxHp= (the client knows it); backend defaults gracefully if omitted.
  The rift modifier-pick overlay reuses the existing dungeon-pick UI (renders run.pendingChoices = §8 ids).

## Economy rework (2026-06-14) — deferred follow-ups

The 3-currency / random-roll / global-spell / linear-map rework (`docs/2026-06-14-economy-rework-plan.md`) is
functionally complete + tested. Deferred (functional rework done; these are cleanup/polish):

- **Legacy dead code (cosmetic).** The `Currency` enum still carries the 6 pre-rework members
  (Crystals/Shards/Points/CampaignGold/CardDust/ModuleCores) and Profile keeps their fields for the one-time
  `MigrateCurrencies` fold; `Upgrades.TryUpgradeSpell` + the `/upgrade` route, and `Upgrades.TryAscendHero` +
  `/hero/ascend`, are inert. Remove all once nothing references them (and migration has shipped a release).
- **Rewards.SpellUnlocks leak.** Boss first-clears still hand out a few free pool spells + the 4th slot
  (the old milestone path). That bypasses the roll/Souls economy — remove the free-spell grant; keep slot
  growth on Souls only. (Touches LoadoutProgressionTests.)
- **Secondary acquisition scenes not yet reworked to rolls:** `CardsScene` (Card-Dust leveling),
  `ModulesScene` (craft/reroll/cores), `MasteriesScene` (was Points; `/mastery` now spends Insight so the
  buttons work but the UI still reads Points), `SeasonScene` (no shop UI yet though `/season/shop` exists).
  The new `RollScene` ("The Forge") + the 3-coin campaign bar cover the core acquire loop; fold the rest in.
- **Coin-source tuning.** First-clear faucet = +20 Insight / +12 Sparks; boss +40 Souls; rolls 30/40/50/80;
  slot unlock 40×slots Souls; mastery 25×(lvl+1) Insight, respec 60 Souls. All first-pass — needs a balance
  pass against the ~1350 Insight to max all masteries + the campaign's ~31 first-clears.
- **Hero pool UX:** the Forge shows "All collected" for the hero pool when it's simply *empty* (no boss
  cleared yet) — distinguish "none rollable yet" from "all owned".
